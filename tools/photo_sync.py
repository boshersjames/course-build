"""Sync course photos, scans and drone video between a course folder and an S3-compatible bucket (Cloudflare R2, Backblaze B2, S3).
Git keeps the index and small derived files; the bucket keeps the originals; this moves them.

    python tools/photo_sync.py push [--what unsorted|scans|drone|all] [--dry-run]     # local course_photos -> bucket (new/changed only)
    python tools/photo_sync.py pull [--what ...] [--hole 10] [--imgs IMG_4651,IMG_4652] [--dry-run]   # bucket -> local, on demand
    python tools/photo_sync.py ls [--what ...]                                        # what the bucket holds for this course
    python tools/photo_sync.py check                                                  # credentials, endpoint, bucket reachable, counts

Bucket location: course.json paths.photo_store = "r2://<bucket>/<prefix>" (or "s3://..."). Credentials come from the environment,
never from a file in the repo: R2_ACCOUNT_ID + R2_ACCESS_KEY_ID + R2_SECRET_ACCESS_KEY for Cloudflare R2 (endpoint
https://<account>.r2.cloudflarestorage.com), or AWS_ACCESS_KEY_ID + AWS_SECRET_ACCESS_KEY (+ S3_ENDPOINT_URL for B2 or others).
Layout in the bucket mirrors the course folder: <prefix>/course_photos/unsorted/<date>/IMG.HEIC, .../scans/x.las, .../drone/x.mp4.
Only originals go up (HEIC/JPG/LAS/LAZ/MP4/MOV); display JPEGs, sheets and index.csv stay in git. Skips files whose size matches.
"""
import argparse, csv, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from course_io import course

ORIG = (".heic", ".heif", ".jpg", ".jpeg", ".png", ".las", ".laz", ".mp4", ".mov", ".dng")
WHAT = {"unsorted": "unsorted", "scans": "scans", "drone": "drone"}


def client_and_bucket(C):
    import boto3
    store = C.get("paths", {}).get("photo_store")
    if not store or "://" not in store: sys.exit("course.json paths.photo_store must be r2://<bucket>/<prefix> or s3://<bucket>/<prefix>")
    scheme, rest = store.split("://", 1); bucket, _, prefix = rest.partition("/")
    if scheme == "r2":
        acct = os.environ.get("R2_ACCOUNT_ID"); key = os.environ.get("R2_ACCESS_KEY_ID"); sec = os.environ.get("R2_SECRET_ACCESS_KEY")
        if not (acct and key and sec): sys.exit("set R2_ACCOUNT_ID, R2_ACCESS_KEY_ID, R2_SECRET_ACCESS_KEY in the environment (never in the repo)")
        s3 = boto3.client("s3", endpoint_url="https://%s.r2.cloudflarestorage.com" % acct, aws_access_key_id=key, aws_secret_access_key=sec, region_name="auto")
    else:
        kw = {}
        if os.environ.get("S3_ENDPOINT_URL"): kw["endpoint_url"] = os.environ["S3_ENDPOINT_URL"]
        s3 = boto3.client("s3", **kw)
    return s3, bucket, prefix.strip("/")


def local_root(C):
    return C.path("photos_root") if "photos_root" in C.get("paths", {}) else os.path.join(C.dir, "course_photos")


def remote_listing(s3, bucket, prefix):
    out = {}; tok = None
    while True:
        kw = {"Bucket": bucket, "Prefix": prefix + "/"} if prefix else {"Bucket": bucket}
        if tok: kw["ContinuationToken"] = tok
        r = s3.list_objects_v2(**kw)
        for o in r.get("Contents", []): out[o["Key"]] = o["Size"]
        if not r.get("IsTruncated"): break
        tok = r.get("NextContinuationToken")
    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("cmd", choices=["push", "pull", "ls", "check"]); ap.add_argument("--what", default="all"); ap.add_argument("--hole", type=int); ap.add_argument("--imgs"); ap.add_argument("--dry-run", action="store_true")
    a = ap.parse_args(); C = course(); s3, bucket, prefix = client_and_bucket(C); root = local_root(C)
    subs = list(WHAT.values()) if a.what == "all" else [WHAT[a.what]]
    rprefix = (prefix + "/" if prefix else "") + "course_photos"
    if a.cmd == "check":
        try:
            s3.head_bucket(Bucket=bucket); lst = remote_listing(s3, bucket, rprefix)
            print("ok: bucket %s reachable; %d objects, %.1f GB under %s" % (bucket, len(lst), sum(lst.values()) / 1e9, rprefix)); return
        except Exception as e: sys.exit("bucket check failed: %s" % e)
    lst = remote_listing(s3, bucket, rprefix)
    if a.cmd == "ls":
        by = {}
        for k, sz in lst.items(): d = "/".join(k.split("/")[:-1]); by.setdefault(d, [0, 0]); by[d][0] += 1; by[d][1] += sz
        for d, (n, sz) in sorted(by.items()): print("%6d  %7.1f MB  %s" % (n, sz / 1e6, d))
        print("%d objects, %.2f GB" % (len(lst), sum(lst.values()) / 1e9)); return
    if a.cmd == "push":
        n = 0; nb = 0
        for sub in subs:
            d = os.path.join(root, sub)
            if not os.path.isdir(d): continue
            for dp, _, fs in os.walk(d):
                for f in fs:
                    if not f.lower().endswith(ORIG): continue
                    p = os.path.join(dp, f); rel = os.path.relpath(p, root).replace("\\", "/"); key = rprefix + "/" + rel; sz = os.path.getsize(p)
                    if sz < 1000: continue   # LFS pointer or empty, not an original
                    if lst.get(key) == sz: continue
                    print(("DRY " if a.dry_run else "") + "put %s (%.1f MB)" % (key, sz / 1e6))
                    if not a.dry_run: s3.upload_file(p, bucket, key)
                    n += 1; nb += sz
        print("%d files, %.2f GB %s" % (n, nb / 1e9, "would upload" if a.dry_run else "uploaded")); return
    if a.cmd == "pull":
        want = None
        if a.imgs: want = set(a.imgs.split(","))
        elif a.hole is not None:
            ip = os.path.join(root, "index.csv"); want = {r["img"] for r in csv.DictReader(open(ip, encoding="utf-8-sig")) if r.get("hole") == str(a.hole)} if os.path.exists(ip) else set()
            if not want: sys.exit("no index rows for hole %d; run photo_index first or pass --imgs" % a.hole)
        n = 0; nb = 0
        for key, sz in lst.items():
            rel = key[len(rprefix) + 1:]; sub = rel.split("/")[0]
            if sub not in subs: continue
            stem = os.path.splitext(os.path.basename(rel))[0]
            if want is not None and stem not in want: continue
            p = os.path.join(root, rel)
            if os.path.exists(p) and os.path.getsize(p) == sz: continue
            print(("DRY " if a.dry_run else "") + "get %s (%.1f MB)" % (rel, sz / 1e6))
            if not a.dry_run: os.makedirs(os.path.dirname(p), exist_ok=True); s3.download_file(bucket, key, p)
            n += 1; nb += sz
        print("%d files, %.2f GB %s" % (n, nb / 1e9, "would download" if a.dry_run else "downloaded"))


if __name__ == "__main__":
    main()

"""Set the GKD course ratings / slopes to the card BoJa sent on 10/31 (men's): Professional = Black 73.8/136, Championship = grey
(Green slot) 71.4/130, Tournament = Blue 69.3/124, Players = White 66.9/118, Forward = Gold (Yellow slot) 64.7/112. Text edit of
those five fields only; BOM kept. Back up the GKD first.
"""
import json, re, sys
from pathlib import Path

GKD = Path(__file__).resolve().parent.parent / "greenkeeper" / "TierraRejada.GKD"
NEW = {"BlackSR": "73.8/136", "GreenSR": "71.4/130", "BlueSR": "69.3/124", "WhiteSR": "66.9/118", "YellowSR": "64.7/112"}

data = GKD.read_bytes(); bom = data.startswith(b"\xef\xbb\xbf"); t = data.decode("utf-8-sig")
for k, v in NEW.items():
    t, n = re.subn(r'("%s"\s*:\s*")[^"]*(")' % k, lambda m: m.group(1) + v + m.group(2), t)
    if n != 1: sys.exit("field %s found %d times" % (k, n))
g = json.loads(t)
if "--go" in sys.argv: GKD.write_bytes((b"\xef\xbb\xbf" if bom else b"") + t.encode("utf-8"))
print({k: g[k] for k in NEW}, "written" if "--go" in sys.argv else "(dry run)")

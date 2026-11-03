"""Course rating / slope from the SCGA course handicap chart PDFs (one per 18-hole pairing), printed per tee.
    python tools/scga_ratings.py <pdf> [<pdf> ...]
"""
import re, sys
from pypdf import PdfReader

for path in sys.argv[1:]:
    t = " ".join(p.extract_text() for p in PdfReader(path).pages)
    name = re.search(r"Moorpark Country Club - [A-Za-z /]+", t)
    print(name.group(0).strip() if name else path)
    for m in re.finditer(r"(Men's|Women's) - ([A-Za-z/]+)\s+Course Rating\S*: ([0-9.]+)\s+Slope Rating\S*: ([0-9]+)", t):
        print("   %-7s %-10s %s/%s" % m.groups())

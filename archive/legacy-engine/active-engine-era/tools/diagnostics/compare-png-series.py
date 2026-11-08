"""Compare repeated saves against the decoded fixed reference image."""
import hashlib
import json
import sys
from pathlib import Path
from PIL import Image

with Image.open(sys.argv[1]) as source:
    expected = source.convert("RGBA").tobytes()
reports = []
for directory in sys.argv[3:]:
    files = sorted(Path(directory).glob("*.png"))
    hashes = set()
    failures = []
    for path in files:
        hashes.add(hashlib.sha256(path.read_bytes()).hexdigest())
        try:
            with Image.open(path) as im:
                actual = im.convert("RGBA").tobytes()
            if actual != expected:
                failures.append({"path": str(path), "reason": "decoded pixels differ"})
        except Exception as exc:
            failures.append({"path": str(path), "reason": str(exc)})
    reports.append({"directory": directory, "count": len(files), "distinct_file_hashes": len(hashes),
                    "failure_count": len(failures), "failures": failures})
Path(sys.argv[2]).write_text(json.dumps(reports, indent=2), encoding="utf-8")
print(json.dumps([{k: v for k, v in r.items() if k != "failures"} for r in reports]))

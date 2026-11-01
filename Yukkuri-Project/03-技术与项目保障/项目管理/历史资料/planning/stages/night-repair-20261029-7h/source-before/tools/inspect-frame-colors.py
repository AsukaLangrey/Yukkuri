"""Read-only triage of the saturated stripes seen in one actual PNG capture.

This is a search heuristic, not a rendering test or visual acceptance criterion.
Images are never modified. A flagged image still needs visual inspection.
"""
import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image

parser = argparse.ArgumentParser()
parser.add_argument("directories", nargs="+")
parser.add_argument("--output", required=True)
args = parser.parse_args()
records = []
errors = []
for folder in args.directories:
    for path in sorted(Path(folder).glob("*.png")):
        try:
            with Image.open(path) as im:
                rgb = np.asarray(im.convert("RGB"))
                extreme = (rgb.max(axis=2) >= 250) & (rgb.min(axis=2) <= 5)
                count = int(extreme.sum())
                records.append({"path": str(path), "width": im.width, "height": im.height,
                                "extreme_pixels": count, "fraction": count / extreme.size,
                                "max_row_fraction": float(extreme.mean(axis=1).max())})
        except Exception as exc:
            errors.append({"path": str(path), "error": str(exc)})
records.sort(key=lambda r: r["extreme_pixels"], reverse=True)
report = {"scope": "Retrospective color heuristic only; not proof of corruption or correctness.",
          "images": len(records), "records": records, "errors": errors}
Path(args.output).write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
print(json.dumps({"images": len(records), "top": records[:12], "errors": errors}, ensure_ascii=False))

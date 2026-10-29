"""Independent fixed-byte zlib diagnostic; no game or GPU is involved."""
import hashlib
import json
import struct
import sys
import time
import zlib
from pathlib import Path

data = Path(sys.argv[1]).read_bytes()
out = Path(sys.argv[2])
out.mkdir(parents=True, exist_ok=True)
pos = 8
parts = []
while pos + 12 <= len(data):
    size = struct.unpack_from(">I", data, pos)[0]
    if data[pos + 4:pos + 8] == b"IDAT":
        parts.append(data[pos + 8:pos + 8 + size])
    pos += 12 + size
raw = zlib.decompress(b"".join(parts))
start = time.monotonic()
reference = None
failures = []
hashes = set()
for i in range(256):
    packed = zlib.compress(raw, 6)
    digest = hashlib.sha256(packed).hexdigest()
    hashes.add(digest)
    if reference is None:
        reference = packed
    try:
        restored = zlib.decompress(packed)
        same = restored == raw
        error = None
    except zlib.error as exc:
        same = False
        error = str(exc)
    if not same or packed != reference:
        failures.append({"iteration": i, "decoded_equal": same, "deterministic": packed == reference, "error": error})
        (out / f"unexpected-{i}.zlib").write_bytes(packed)
report = {"runtime": sys.version, "zlib_runtime": zlib.ZLIB_RUNTIME_VERSION, "iterations": 256,
          "source_bytes": len(raw), "source_sha256": hashlib.sha256(raw).hexdigest(),
          "distinct_compressed_hashes": len(hashes), "failures": failures, "seconds": time.monotonic() - start}
(out / "result.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps({"failures": len(failures), "distinct": len(hashes), "seconds": report["seconds"]}))

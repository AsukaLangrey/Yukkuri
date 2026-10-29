"""Inspect PNG chunk checksums and zlib stream without changing source files."""
import hashlib
import json
import struct
import sys
import zlib
from pathlib import Path

for name in sys.argv[1:]:
    data = Path(name).read_bytes()
    pos = 8
    idat = bytearray()
    bad = []
    chunks = 0
    while pos + 12 <= len(data):
        size = struct.unpack_from(">I", data, pos)[0]
        tag = data[pos + 4:pos + 8]
        end = pos + 8 + size
        if end + 4 > len(data):
            bad.append({"offset": pos, "reason": "truncated"})
            break
        expected = struct.unpack_from(">I", data, end)[0]
        actual = zlib.crc32(data[pos + 4:end])
        if expected != actual:
            bad.append({"offset": pos, "type": tag.decode("ascii", "replace"), "expected": expected, "actual": actual})
        if tag == b"IDAT":
            idat.extend(data[pos + 8:end])
        chunks += 1
        pos = end + 4
    try:
        raw = zlib.decompress(idat)
        stream = {"decoded_bytes": len(raw)}
    except zlib.error as exc:
        stream = {"error": str(exc)}
    print(json.dumps({"path": name, "sha256": hashlib.sha256(data).hexdigest(), "chunks": chunks,
                      "bad_crc_or_truncated": bad, "zlib": stream}, ensure_ascii=False))

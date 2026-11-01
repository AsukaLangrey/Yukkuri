"""Read unencrypted standalone Godot PCK v2-v4 directories and hash payloads.

Layout follows the pinned Godot core/io/file_access_pack.cpp; never extracts files.
"""
import hashlib
import json
import struct
import sys
from pathlib import Path

def inspect(name):
    data = Path(name).read_bytes()
    if data[:4] != b"GDPC":
        raise ValueError("Expected standalone Godot PCK")
    version, major, minor, patch, flags = struct.unpack_from("<5I", data, 4)
    if version not in (2, 3, 4) or flags & 5:
        raise ValueError("Unsupported version, encrypted directory, or sparse bundle")
    base = struct.unpack_from("<Q", data, 24)[0]
    pos = struct.unpack_from("<Q", data, 32)[0] if version >= 3 else 96
    count = struct.unpack_from("<I", data, pos)[0]
    pos += 4
    if count > 100000:
        raise ValueError("Unreasonable entry count")
    entries = {}
    for _ in range(count):
        size = struct.unpack_from("<I", data, pos)[0]
        pos += 4
        if size > 65536 or pos + size + 36 > len(data):
            raise ValueError("Invalid directory entry")
        path = data[pos:pos + size].rstrip(b"\0").decode("utf-8")
        pos += size
        offset, length = struct.unpack_from("<QQ", data, pos)
        md5 = data[pos + 16:pos + 32].hex()
        file_flags = struct.unpack_from("<I", data, pos + 32)[0]
        pos += 36
        if file_flags or base + offset + length > len(data):
            raise ValueError("Unsupported entry flags or out-of-bounds payload")
        payload = data[base + offset:base + offset + length]
        entries[path] = {"size": length, "sha256": hashlib.sha256(payload).hexdigest(),
                         "directory_md5_matches": hashlib.md5(payload).hexdigest() == md5}
    return {"path": name, "version": version, "engine": [major, minor, patch], "entries": entries}

a, b = inspect(sys.argv[1]), inspect(sys.argv[2])
changed = [p for p in sorted(set(a["entries"]) | set(b["entries"])) if a["entries"].get(p) != b["entries"].get(p)]
report = {"first": a, "second": b, "changed": changed}
Path(sys.argv[3]).write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps({"first_entries": len(a["entries"]), "second_entries": len(b["entries"]), "changed": changed,
                  "first_md5_mismatches": sum(not v["directory_md5_matches"] for v in a["entries"].values()),
                  "second_md5_mismatches": sum(not v["directory_md5_matches"] for v in b["entries"].values())}))

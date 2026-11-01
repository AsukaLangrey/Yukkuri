"""Check saved PNG structure and zlib integrity, without claiming visual quality."""
import argparse
import hashlib
import json
import struct
import zlib
from datetime import datetime, timezone
from pathlib import Path


def inspect(path):
    data = path.read_bytes()
    result = {"file": path.name, "sha256": hashlib.sha256(data).hexdigest(), "errors": []}
    errors = result["errors"]
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        errors.append("Invalid PNG signature")
        return result
    offset, chunks, compressed, ended = 8, [], bytearray(), False
    while offset < len(data):
        if offset + 12 > len(data):
            errors.append("Truncated chunk header")
            break
        size = struct.unpack_from(">I", data, offset)[0]
        tag = data[offset + 4:offset + 8]
        end = offset + 8 + size
        if end + 4 > len(data):
            errors.append("Truncated chunk payload")
            break
        payload = data[offset + 8:end]
        if zlib.crc32(data[offset + 4:end]) != struct.unpack_from(">I", data, end)[0]:
            errors.append("Chunk CRC mismatch: " + tag.decode("ascii", "replace"))
        chunks.append(tag)
        if tag == b"IHDR":
            if size != 13:
                errors.append("Invalid IHDR length")
            else:
                width, height, depth, color, comp, filt, interlace = struct.unpack(">IIBBBBB", payload)
                result.update(width=width, height=height, bit_depth=depth, color_type=color)
                if not width or not height or comp != 0 or filt != 0 or interlace not in (0, 1):
                    errors.append("Invalid IHDR values")
        if tag == b"IDAT":
            compressed.extend(payload)
        offset = end + 4
        if tag == b"IEND":
            ended = True
            if size or offset != len(data):
                errors.append("Invalid IEND or trailing bytes")
            break
    if not chunks or chunks[0] != b"IHDR" or chunks.count(b"IHDR") != 1:
        errors.append("Missing, duplicate or misplaced IHDR")
    if not ended:
        errors.append("Missing IEND")
    if not compressed:
        errors.append("Missing image stream")
    else:
        try:
            decoder = zlib.decompressobj()
            raw = decoder.decompress(compressed)
            raw += decoder.flush()
            if not decoder.eof or decoder.unused_data or decoder.unconsumed_tail:
                errors.append("Incomplete image stream or trailing compressed data")
            result["decoded_bytes"] = len(raw)
            if result.get("width") and interlace == 0:
                channels = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}.get(color)
                if channels:
                    stride = (width * channels * depth + 7) // 8 + 1
                    if len(raw) != stride * height:
                        errors.append("Decoded scanline length does not match dimensions")
                    elif any(raw[row * stride] > 4 for row in range(height)):
                        errors.append("Invalid scanline filter")
        except zlib.error as exc:
            errors.append("Image stream: " + str(exc))
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("directory", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--require-valid", action="store_true")
    args = parser.parse_args()
    if not args.directory.is_dir():
        parser.error("Evidence directory does not exist")
    records = []
    for path in sorted(args.directory.glob("*.png")):
        try:
            records.append(inspect(path))
        except (OSError, ValueError, struct.error) as exc:
            records.append({"file": path.name, "errors": [str(exc)]})
    bad = sum(bool(record["errors"]) for record in records)
    report = {"checked_at": datetime.now(timezone.utc).isoformat(), "directory": str(args.directory.resolve()),
              "scope": "PNG file integrity only. This does not verify visual correctness, runtime stability, or user acceptance.",
              "files": len(records), "valid_files": len(records) - bad, "invalid_files": bad,
              "integrity_status": "no_images" if not records else "invalid_images" if bad else "valid_images",
              "all_present_files_valid": bool(records) and bad == 0, "records": records}
    args.output.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps({key: report[key] for key in ("files", "valid_files", "invalid_files")}, ensure_ascii=False))
    return 2 if args.require_valid and (bad or not records) else 0


if __name__ == "__main__":
    raise SystemExit(main())

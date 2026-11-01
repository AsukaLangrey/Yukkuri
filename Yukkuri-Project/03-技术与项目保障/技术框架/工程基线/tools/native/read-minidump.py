"""Read captured address ranges from a local Windows minidump; never edits it."""
import argparse
import json
import mmap
import struct
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("dump", type=Path)
parser.add_argument("address", nargs="?", type=lambda value: int(value, 0))
parser.add_argument("--length", type=int, default=256)
args = parser.parse_args()
dump_file = args.dump.open("rb")
data = mmap.mmap(dump_file.fileno(), 0, access=mmap.ACCESS_READ)
signature, version, count, directory, checksum, timestamp, flags = struct.unpack_from("<IIIIIIQ", data)
if signature != 0x504D444D:
    raise ValueError("Not a Windows minidump")
ranges = []
modules = []
context = {}
for index in range(count):
    kind, size, rva = struct.unpack_from("<III", data, directory + 12 * index)
    if kind == 5:
        entries, = struct.unpack_from("<I", data, rva)
        for entry in range(entries):
            start, length, position = struct.unpack_from("<QII", data, rva + 4 + 16 * entry)
            ranges.append((start, length, position))
    elif kind == 9:
        entries, position = struct.unpack_from("<QQ", data, rva)
        for entry in range(entries):
            start, length = struct.unpack_from("<QQ", data, rva + 16 + 16 * entry)
            ranges.append((start, length, position))
            position += length
    elif kind == 4:
        entries, = struct.unpack_from("<I", data, rva)
        for entry in range(entries):
            base, module_size, _, _, name_rva = struct.unpack_from("<QIIII", data, rva + 4 + 108 * entry)
            name_length, = struct.unpack_from("<I", data, name_rva)
            name = data[name_rva + 4:name_rva + 4 + name_length].decode("utf-16-le")
            modules.append({"base": base, "size": module_size, "name": name})
    elif kind == 6:
        context_size, context_rva = struct.unpack_from("<II", data, rva + 160)
        # This tool is for the project's x64 Windows dumps only.
        context_flags, = struct.unpack_from("<I", data, context_rva + 48)
        if context_size < 256 or not context_flags & 0x100000:
            raise ValueError("Expected AMD64 CONTEXT")
        for register, offset in {"rax": 120, "rcx": 128, "rdx": 136, "rbx": 144, "rsp": 152,
                                 "rbp": 160, "rsi": 168, "rdi": 176, "r8": 184, "r9": 192,
                                 "r10": 200, "r11": 208, "r12": 216, "r13": 224, "r14": 232,
                                 "r15": 240, "rip": 248}.items():
            context[register], = struct.unpack_from("<Q", data, context_rva + offset)
if any(position + length > len(data) for _, length, position in ranges):
    raise ValueError("Dump memory stream is truncated; do not treat it as a complete capture")
if args.address is None:
    rip = context.get("rip", 0)
    module = next((m for m in modules if m["base"] <= rip < m["base"] + m["size"]), None)
    print(json.dumps({"context": {k: hex(v) for k, v in context.items()},
                      "fault_module": module,
                      "module_offset": hex(rip - module["base"]) if module else None,
                      "memory_ranges": len(ranges), "file_bytes": len(data),
                      "modules": modules}, indent=2))
    raise SystemExit(0)
for address in range(args.address, args.address + args.length, 16):
    found = next((r for r in ranges if r[0] <= address < r[0] + r[1]), None)
    if found is None:
        print(f"{address:016x}: not captured")
        continue
    start, length, position = found
    chunk = data[position + address - start:position + min(address - start + 16, length)]
    words = " ".join(f"{struct.unpack_from('<Q', chunk, offset)[0]:016x}" for offset in range(0, len(chunk) - 7, 8))
    print(f"{address:016x}: {chunk.hex(' '):47} | {words}")

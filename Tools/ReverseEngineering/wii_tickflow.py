"""Read-only DOL/TickFlow inspector for Rhythm Heaven Fever (Wii).

The instruction layout and common opcodes are documented by conhlee/rhf:
https://github.com/conhlee/rhf/blob/master/src/GameUtil/TickFlowDecl.hpp
https://github.com/conhlee/rhf/blob/master/src/GameUtil/TickFlow.cpp
Wii differs from the Nintendo 3DS Megamix format. Never apply 3DS IDs here.
This module only decodes data from a caller-supplied, already extracted DOL.
"""
from __future__ import annotations

import argparse
import json
import struct
from dataclasses import asdict, dataclass
from pathlib import Path


OP_NAMES = {
    0x00: "async_call", 0x01: "call", 0x02: "return", 0x03: "stop",
    0x04: "category", 0x05: "set_condvar", 0x06: "incr_condvar",
    0x07: "push_condvar", 0x08: "pop_condvar", 0x09: "rest",
    0x0A: "get_set_rest", 0x0B: "rest_frames", 0x0C: "unrest",
    0x0D: "reset_tick_pass", 0x0E: "label", 0x0F: "jump",
    0x10: "if", 0x11: "else", 0x12: "endif", 0x13: "switch",
    0x14: "case", 0x15: "break", 0x16: "default", 0x17: "endswitch",
    0x19: "check_bgm_type", 0x1A: "tempo", 0x1B: "tempo_seq",
    0x1C: "tempo_wave", 0x1D: "speed", 0x21: "spawn_cellanim",
    0x22: "play_sfx_vol", 0x23: "play_sfx", 0x2B: "prepare_seq",
    0x2C: "start_seq", 0x2D: "start_prepared_seq", 0x2E: "stop_seq",
    0x31: "prepare_strm", 0x32: "start_strm", 0x33: "start_prepared_strm",
    0x34: "stop_strm", 0x37: "prepare_wave", 0x38: "start_wave",
    0x39: "start_prepared_wave", 0x3A: "stop_wave", 0x3C: "get_wave_prepared",
    0x3F: "get_group_loading", 0x42: "set_input_allow", 0x49: "set_skippable",
    0x4A: "set_skip_handler", 0x50: "random", 0x100: "set_scene",
    0x101: "set_scene_ver", 0x102: "scene_assets", 0x104: "get_scene_running",
    0x105: "set_prologue", 0x106: "set_epilogue", 0x107: "set_epilogue_msg",
    0x108: "set_grading_caption", 0x10A: "fade", 0x10E: "ui_pause",
    0x118: "checkpoint_submit_criteria", 0x119: "checkpoint_set_criteria",
    0x11A: "checkpoint_enable_scoring", 0x11D: "checkpoint_grading",
    0x121: "tutorial", 0x124: "mesg_pane_set_text", 0x12C: "load_snd_group",
    0x12D: "clear_snd_heap", 0x12E: "reset_snd_group_heap",
    0x12F: "snd_loading", 0x132: "cellanim_render_eff",
}


@dataclass(frozen=True)
class Section:
    index: int
    kind: str
    offset: int
    address: int
    size: int


@dataclass(frozen=True)
class Instruction:
    address: int
    offset: int
    word: int
    opcode: int
    argc: int
    arg0: int
    args: tuple[int, ...]

    @property
    def next_address(self):
        return self.address + 4 * (self.argc + 1)

    @property
    def name(self):
        return OP_NAMES.get(self.opcode, f"op_{self.opcode:03X}")

    def json(self):
        result = asdict(self)
        result["name"] = self.name
        return result


class Dol:
    def __init__(self, path: str | Path):
        self.path = Path(path)
        self.data = self.path.read_bytes()
        if len(self.data) < 0x100:
            raise ValueError("DOL header is truncated")
        self.sections = []
        for i in range(18):
            off, addr, size = (self.u32(i * 4 + base) for base in (0, 0x48, 0x90))
            if size:
                if off < 0x100 or off + size > len(self.data):
                    raise ValueError(f"Invalid DOL section {i}")
                self.sections.append(Section(i, "text" if i < 7 else "data", off, addr, size))

    def u32(self, offset):
        return struct.unpack_from(">I", self.data, offset)[0]

    def offset(self, address):
        for s in self.sections:
            if s.address <= address < s.address + s.size:
                return s.offset + address - s.address
        raise ValueError(f"Address 0x{address:08X} is not mapped in this DOL")

    def address(self, offset):
        for s in self.sections:
            if s.offset <= offset < s.offset + s.size:
                return s.address + offset - s.offset
        raise ValueError(f"Offset 0x{offset:X} is not mapped in this DOL")

    def valid_address(self, address):
        try:
            self.offset(address)
            return True
        except ValueError:
            return False

    def read(self, address):
        off = self.offset(address)
        word = self.u32(off)
        argc = (word >> 10) & 0xF
        args = tuple(self.u32(off + 4 * (i + 1)) for i in range(argc))
        return Instruction(address, off, word, word & 0x3FF, argc, word >> 14, args)

    def cstring(self, address):
        if not self.valid_address(address):
            return None
        off = self.offset(address)
        end = self.data.find(b"\0", off, off + 150)
        if end < 0 or end == off:
            return None
        value = self.data[off:end]
        if not all(32 <= b < 127 for b in value):
            return None
        return value.decode("ascii")

    def scene_references(self, scene):
        """Find exact SET_SCENE and SET_SCENE_VER instruction encodings."""
        found = []
        for s in self.sections:
            if s.kind != "data":
                continue
            for off in range(s.offset, s.offset + s.size - 12, 4):
                word = self.u32(off)
                if word in (0x900, 0x901) and self.u32(off + 4) == scene:
                    instruction = self.read(self.address(off))
                    found.append(instruction)
        return found

    def routine(self, address, max_instructions=5000):
        """Decode sequentially until RETURN/STOP, without inventing branch outcomes."""
        result = []
        for _ in range(max_instructions):
            ins = self.read(address)
            # 28 useful low bits; higher values here almost always signal non-code.
            if ins.word >= 0x10000000 or ins.opcode > 0x2FF:
                raise ValueError(f"Unlikely TickFlow word 0x{ins.word:08X} at 0x{address:08X}")
            result.append(ins)
            if ins.opcode in (2, 3):
                return result
            address = ins.next_address
        raise ValueError("Routine does not terminate within instruction cap")

    def graph(self, roots, max_routines=1000):
        todo = list(roots)
        found = {}
        while todo:
            addr = todo.pop(0)
            if addr in found:
                continue
            if len(found) >= max_routines:
                raise ValueError("Call graph routine cap reached")
            try:
                code = self.routine(addr)
                found[addr] = {"instructions": [i.json() for i in code]}
                for i in code:
                    target = None
                    if i.opcode in (0, 1, 0x4A) and i.args:
                        target = i.args[0]
                    if target is not None and self.valid_address(target):
                        todo.append(target)
            except (ValueError, struct.error) as exc:
                found[addr] = {"error": str(exc)}
        return found


def format_instruction(dol, ins):
    args = []
    for value in ins.args:
        label = f"0x{value:08X}" if value >= 0x80000000 else str(value)
        string = dol.cstring(value)
        if string:
            label += f" ({string!r})"
        args.append(label)
    return f"{ins.address:08X}  {ins.word:08X}  {ins.name}<{ins.arg0}> " + ", ".join(args)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("dol", type=Path)
    parser.add_argument("--scene", type=lambda v: int(v, 0), help="Find scene (Flipper-Flop/Goma=11; Seal=22 is Packing Pests)")
    parser.add_argument("--address", action="append", default=[], type=lambda v: int(v, 0))
    parser.add_argument("--graph", type=Path, help="Write reachable routine graph as JSON")
    args = parser.parse_args()
    dol = Dol(args.dol)
    print("DOL sections:")
    for s in dol.sections:
        print(f"  {s.kind}{s.index}: file=0x{s.offset:X} address=0x{s.address:08X} size=0x{s.size:X}")
    roots = list(args.address)
    if args.scene is not None:
        print(f"Scene {args.scene} references:")
        for ins in dol.scene_references(args.scene):
            print(format_instruction(dol, ins))
            if ins.opcode == 0x100 and len(ins.args) == 2:
                roots.append(ins.args[1])
    if args.graph:
        graph = dol.graph(roots)
        args.graph.parent.mkdir(parents=True, exist_ok=True)
        args.graph.write_text(json.dumps(graph, indent=2), encoding="utf-8")
        print(f"Saved {len(graph)} routines to {args.graph}")
    else:
        for root in roots:
            print(f"Routine 0x{root:08X}:")
            for ins in dol.routine(root):
                print(format_instruction(dol, ins))


if __name__ == "__main__":
    main()

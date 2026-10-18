"""Extract Wii SOMJ01 Flipper-Flop input timing from the user's main.dol.

No music detection, hand-authored beat grid, or Megamix bytecode is used.
Timing is the sum of original Wii TickFlow rests, async offsets and input delay.
The common VM implementation follows publicly documented Wii TickFlow semantics:
https://github.com/conhlee/rhf/blob/master/src/GameUtil/TickFlow.cpp
The game-specific opcode meanings are corroborated by the supplied DOL and
original goma/captain animation labels. Raw commands are retained for audit.

This targets the confirmed Japanese revision and checks its entry signature;
it intentionally refuses other DOLs rather than guessing regional addresses.
"""
from __future__ import annotations

import argparse
from collections import Counter
import csv
import hashlib
import json
from pathlib import Path
import struct

from wii_tickflow import Dol, format_instruction


MAIN = 0x8022E3EC
MUSIC_START = 0x8022E48C
MAIN_AFTER_START = MUSIC_START + 4
MAIN_INPUT_OFF = 0x8022F224
TICKS_PER_BEAT = 48

GOMA_NAMES = {
    0x200: "input", 0x201: "seal_beat", 0x202: "raise_flipper",
    0x203: "npc_flip", 0x204: "npc_roll", 0x205: "captain_animation",
    0x206: "get_roll_success_stage", 0x207: "set_roll_success_stage_1", 0x208: "reset_roll_success_count",
    0x20B: "random_praise",
}

ANIMATION_NAMES = [
    "goma_Interval", "goma_A_Press", "goma_A_ReadyForL", "goma_A_ReadyForR",
    "goma_A_TurnToL", "goma_A_TurnToR", "goma_A_TurnedToL", "goma_A_TurnedToR",
    "goma_A_RollFromL", "goma_A_RollFromR", "goma_A_RolledFromL", "goma_A_RolledFromR",
    "goma_B_Press", "goma_B_ReadyForL", "goma_B_ReadyForR", "goma_B_TurnToL",
    "goma_B_TurnToR", "goma_B_TurnedToL", "goma_B_TurnedToR", "goma_B_RollFromL",
    "goma_B_RollFromR", "goma_B_RolledFromL", "goma_B_RolledFromR", "goma_face_normal",
    "goma_face_sad", "goma_face_surprise", "goma_face_angry", "goma_snow",
    "goma_A_TurnedToL_old", "goma_A_TurnedToR_old",
]


def annotate_animations(events):
    """Apply actual Goma NPC state transitions decoded from the supplied DOL.

    Scene direction flag at +0x251; A/B flag at +0x253. Initial values are 1/0
    at 8004E64C/8004E654. Raise-flipper op202 explicitly resets direction.
    Normal flip case 8004D670 toggles direction at 8004D720. Roll case
    8004D728 toggles direction at 8004D844 and A/B at 8004D858.
    """
    left, phase_b = True, False
    inputs = {event["inputTick"]: event for event in events if event["opcode"] == 0x200}
    for event in events:
        op, av = event["opcode"], event["args"]
        index = None
        if op == 0x201:
            index = 12 if phase_b else 1
        elif op == 0x202:
            left = bool(av[0])
            index = (2 if left else 3) + (11 if phase_b else 0)
        elif op in (0x203, 0x204):
            before_left, before_b = left, phase_b
            if op == 0x203:
                index = (4 if left else 5) + (11 if phase_b else 0)
                action = "left" if left else "right"
            else:
                index = (9 if left else 8) + (0 if phase_b else 11)
                action = "roll"
                event["bodyRootDeltaX"] = -72 if left else 72
                phase_b = not phase_b
            left = not left
            note = inputs[event["tick"]]
            note.update({"action": action, "animationIndex": index,
                         "animationName": ANIMATION_NAMES[index],
                         "directionFlagBefore": int(before_left), "phaseBBefore": before_b,
                         "directionFlagAfter": int(left), "phaseBAfter": phase_b,
                         "npcSourceAddress": event["sourceAddress"]})
        if index is not None:
            event["animationIndex"] = index
            event["animationName"] = ANIMATION_NAMES[index]


def s32(value):
    return value if value < 0x80000000 else value - 0x100000000


class GomaTrace:
    """Bounded interpreter; records every branch choice and untouched raw word.

    Game-result-only conditions affect praise/success-stage/Perfect effects. Two
    opposite settings are traced and the resulting input sequence must agree.
    This proves these choices do not alter required input timings/types.
    """
    def __init__(self, dol, alternative=False):
        self.dol = dol
        self.alternative = alternative
        self.events = []
        self.choices = []
        self.steps = 0
        self.stage_resets = []

    def find_branch(self, pc, targets, opening, closing, wanted=None):
        depth = 0
        for _ in range(10000):
            ins = self.dol.read(pc)
            if ins.opcode == closing:
                if depth == 0:
                    return ins.next_address
                depth -= 1
            elif ins.opcode == opening:
                depth += 1
            if depth == 0 and ins.opcode in targets:
                if wanted is None or ins.opcode != 0x14 or ins.arg0 == wanted:
                    return ins.next_address
            pc = ins.next_address
        raise ValueError("Unbalanced TickFlow conditional")

    def run(self, start, ticks=0, callers=(), root=False):
        pc, base = start, start
        stack, cond_stack = [], []
        cond = 0
        while True:
            self.steps += 1
            if self.steps > 100000 or ticks > 13000 or len(callers) > 30:
                raise ValueError("Trace exceeded bounded original level execution")
            if root and pc == MAIN_INPUT_OFF:
                return ticks
            ins = self.dol.read(pc)
            pc = ins.next_address
            op, a, av = ins.opcode, ins.arg0, ins.args
            if op == 0:
                self.run(av[0], ticks + av[1], callers + (ins.address,))
            elif op == 1:
                stack.append((pc, base))
                pc = base = av[0]
            elif op == 2:
                if not stack:
                    return ticks
                pc, base = stack.pop()
            elif op == 3:
                return ticks
            elif op == 5:
                cond = s32(av[0])
            elif op == 6:
                cond += s32(av[0])
            elif op == 7:
                cond_stack.append(cond)
            elif op == 8:
                cond = cond_stack.pop()
            elif op == 9:
                ticks += a
            elif op == 0x0C:
                ticks -= a
            elif op == 0x0B:
                raise ValueError(f"Frame-based rest in music path at 0x{ins.address:X}")
            elif op == 0x0F:
                target = base
                for _ in range(10000):
                    label = self.dol.read(target)
                    if label.opcode == 0x0E and label.arg0 == a:
                        pc = label.next_address
                        break
                    target = label.next_address
                else:
                    raise ValueError("Jump label not found")
            elif op == 0x10:
                value = s32(av[0])
                passed = (cond == value, cond != value, cond < value, cond <= value,
                          cond > value, cond >= value)[a]
                self.choices.append({"address": f"0x{ins.address:08X}", "condvar": cond,
                                     "passed": passed, "tick": ticks})
                if not passed:
                    pc = self.find_branch(pc, (0x11,), 0x10, 0x12)
            elif op == 0x11:
                pc = self.find_branch(pc, (), 0x10, 0x12)
            elif op == 0x13:
                pc = self.find_branch(pc, (0x14, 0x16), 0x13, 0x17, cond)
                self.choices.append({"address": f"0x{ins.address:08X}", "switch": cond,
                                     "tick": ticks})
            elif op == 0x15:
                pc = self.find_branch(pc, (), 0x13, 0x17)
            elif op == 0x131 and a == 1:
                cond = int(self.alternative)  # Perfect-campaign-only effects
            elif op == 0x121 and a == 7:
                cond = int(self.alternative)  # Fail versus success praise
            elif op == 0x206:
                # DOL 8004D9AC -> 800902F8 returns min(cap=2, stage).
                # 0x207 sets stage=1; successful rolls advance it, failures reset.
                # Both stage 1 and stage 2 select captain beat_just_00/face_just_00;
                # stage 0 selects beat_miss_00/face_miss_00. Canonical is success.
                if self.alternative:
                    cond = 0
                else:
                    reset_tick = max((t for t in self.stage_resets if t <= ticks), default=0)
                    previous_rolls = sum(e["opcode"] == 0x200 and e.get("inputType") == "roll"
                                         and reset_tick <= e["inputTick"] <= ticks
                                         for e in self.events)
                    cond = min(2, 1 + previous_rolls // 4)
            elif op == 0x207:
                self.stage_resets.append(ticks)
            elif op == 0x20B:
                cond = av[0] - 1 if self.alternative else 0
            elif op == 0x0A or (op not in GOMA_NAMES and op < 0x19 and
                               op not in (4, 0x0D, 0x0E, 0x12, 0x14, 0x16, 0x17)):
                raise ValueError(f"Unsupported timing/control opcode {op:X} at 0x{ins.address:X}")

            if op not in (0, 1, 2, 3, 9, 0x0E):
                event = {
                    "tick": ticks,
                    "beat": ticks / TICKS_PER_BEAT,
                    "sourceAddress": f"0x{ins.address:08X}",
                    "sourceFileOffset": ins.offset,
                    "word": f"0x{ins.word:08X}",
                    "opcode": op,
                    "name": GOMA_NAMES.get(op, ins.name),
                    "arg0": a,
                    "args": list(av),
                    "callers": [f"0x{address:08X}" for address in callers],
                }
                if op == 0x200:
                    assert len(av) == 2 and av[0] == 48 and av[1] in (0, 1)
                    event["inputTick"] = ticks + av[0]
                    event["inputBeat"] = event["inputTick"] / TICKS_PER_BEAT
                    event["inputType"] = "flip" if av[1] == 0 else "roll"
                self.events.append(event)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("dol", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    dol = Dol(args.dol)
    assert dol.read(MAIN).word == 0x404
    assert dol.read(MAIN + 8).word == (45 << 14) | 0x37
    assert dol.read(MUSIC_START).word == 0x39
    # Original WaveInfo table, sound ID 45. Read and verify instead of importing
    # the project's previously chosen tempo as evidence.
    sid, sample_rate, tempo_address = struct.unpack_from(">HHI", dol.data, dol.offset(0x802EF640))
    assert sid == 45 and sample_rate == 32000 and tempo_address == 0x802EE86C
    wave_beats, wave_samples, wave_flags = struct.unpack_from(">IIH", dol.data, dol.offset(tempo_address))
    assert wave_flags == 0x8000
    traces = [GomaTrace(dol, alternative=mode) for mode in (False, True)]
    for trace in traces:
        trace.run(MAIN_AFTER_START, root=True)
    events = sorted(traces[0].events, key=lambda row: (row["tick"], row["sourceAddress"]))
    sequences = [[(e["inputTick"], e["inputType"]) for e in trace.events if e["opcode"] == 0x200]
                 for trace in traces]
    assert sorted(sequences[0]) == sorted(sequences[1]), "Branches changed input rhythm"
    inputs = sorted((e for e in events if e["opcode"] == 0x200), key=lambda e: e["inputTick"])
    assert len({e["inputTick"] for e in inputs}) == len(inputs), "Overlapping original inputs"
    npc = sorted((e for e in events if e["opcode"] in (0x203, 0x204)), key=lambda e: e["tick"])
    assert [(e["inputTick"], e["inputType"]) for e in inputs] == [
        (e["tick"], "flip" if e["opcode"] == 0x203 else "roll") for e in npc
    ], "Input targets differ from original NPC action times"
    annotate_animations(events)
    args.output.mkdir(parents=True, exist_ok=True)
    payload = {
        "source": str(args.dol.resolve()), "sha256": hashlib.sha256(dol.data).hexdigest(),
        "discId": "SOMJ01", "sceneId": 11, "internalName": "goma", "sceneVersion": 0,
        "entryAddress": f"0x{MAIN:08X}", "musicStartAddress": f"0x{MUSIC_START:08X}",
        "musicWaveSoundId": 45, "ticksPerBeat": TICKS_PER_BEAT,
        "originalWaveTempo": {"waveInfoAddress": "0x802EF640", "tempoAddress": "0x802EE86C",
                              "sampleRate": sample_rate, "beatCount": wave_beats,
                              "sampleCount": wave_samples, "flags": wave_flags,
                              "bpm": wave_beats * sample_rate * 60.0 / wave_samples},
        "timeOrigin": "Original start_prepared_wave command; no external audio offset applied",
        "inputCount": len(inputs), "typeCounts": dict(Counter(e["inputType"] for e in inputs)),
        "branchModel": "Canonical successful play: roll-success stage starts at 1 and reaches cap 2 after four successful rolls, no tutorial miss, normal (non-Perfect-campaign) mode, deterministic first random praise variant. Alternative trace uses failure stage 0 and other branches; required input rhythm is identical.",
        "animationModel": {"nativeFramesPerSecond": 60, "playbackSpeed": 1,
                           "normalFlipTogglesDirection": True,
                           "rollTogglesDirectionAndABPhase": True,
                           "readySetsDirection": True,
                           "initialDirectionAtFirstInput": "right",
                           "introBobsAreJudgedInputs": False,
                           "turnEDandrollEDVariants": "Used by original failure handling; not normal success transitions"},
        "verification": {"oppositeBranchSettingsProduceSameInputs": True,
                         "inputTimesAndTypesMatchNpcFlipRollCommands": True,
                         "uniqueInputTicks": True},
        "inputs": inputs, "events": events,
        "branches": traces[0].choices, "alternativeBranches": traces[1].choices,
    }
    (args.output / "goma_original_timing.json").write_text(json.dumps(payload, indent=2), encoding="utf-8")
    with (args.output / "goma_original_inputs.csv").open("w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.writer(handle)
        writer.writerow(("tick", "beat", "type", "action", "animationIndex", "animationName",
                         "sourceAddress", "sourceFileOffset", "callers"))
        for event in inputs:
            writer.writerow((event["inputTick"], event["inputBeat"], event["inputType"], event["action"],
                             event["animationIndex"], event["animationName"],
                             event["sourceAddress"], event["sourceFileOffset"], " > ".join(event["callers"])))
    print(json.dumps({k: payload[k] for k in ("entryAddress", "inputCount", "typeCounts", "verification")}))
    print("First:", [(e["inputBeat"], e["inputType"]) for e in inputs[:20]])
    print("Last:", [(e["inputBeat"], e["inputType"]) for e in inputs[-15:]])


if __name__ == "__main__":
    main()

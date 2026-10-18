#!/usr/bin/env python3
"""Rebuild or verify the full seal level's two existing Koreography tracks.

Usage from any directory:
    python Tools/generate_seal_chart.py
    python Tools/generate_seal_chart.py --check

Only the Python standard library is required. Existing Unity asset GUIDs and
track IDs are preserved. The JSON source uses zero-based musical beats.
"""

import argparse
import collections
import json
from pathlib import Path
import re
import struct


ROOT = Path(__file__).resolve().parents[1]
TRACKS = ROOT / "Assets/Resources/Art/Level_cellanim/Kore"
SOURCE = Path(__file__).with_name("SealChart.json")


def sample_at(beat, source):
    return round(beat * source["sampleRate"] * 60 / source["bpm"])


def build_chart(source):
    assert source['schemaVersion'] == 2, 'Use the explicit original-ROM chart schema'
    notes = [(event['beat'], event['action']) for event in source['notes']]
    # Two captain commands may target separate animation layers at one beat.
    # Merge only identical trigger/time pairs, keeping distinct layer commands.
    people = sorted(set((event['beat'], event['action']) for event in source['captainEvents']))
    intro = sorted((event['beat'], event['action']) for event in
                   source['introDemonstrations'] + source['preparationEvents'])
    return notes, people, intro


def serialize_track(existing, events, source):
    header = existing.split("  mEventList:", 1)[0]
    lines = [header, "  mEventList:\n"]
    for beat, _ in events:
        sample = sample_at(beat, source)
        lines.append("  - mStartSample: {0}\n    mEndSample: {0}\n".format(sample))
    lines.append("  _SerializedPayloadTypes:\n  - SonicBloom.Koreo.TextPayload\n")
    for kind in ("Asset", "Color", "Curve", "Float", "Gradient", "Int", "Spectrum"):
        lines.append("  _{0}Payloads: []\n  _{0}PayloadIdxs: \n".format(kind))
    lines.append("  _TextPayloads:\n")
    for _, text in events:
        lines.append("  - mTextVal: " + text + "\n")
    indices = b"".join(struct.pack("<i", index) for index in range(len(events)))
    lines.append("  _TextPayloadIdxs: " + indices.hex() + "\n")
    return "".join(lines)


def validate(source, notes, people, intro):
    assert source["sampleRate"] == 44100 and source["bpm"] == 133
    assert len(source["extraHalfBeats"]) == 18
    assert all(beat % 1 == .5 for beat in source["extraHalfBeats"])
    assert len(notes) == 230, 'Only the 230 original input commands are judged'
    samples = [sample_at(beat, source) for beat, _ in notes]
    assert all(a < b for a, b in zip(samples, samples[1:])), "Duplicate or unsorted input"
    assert min(b - a for a, b in zip(samples, samples[1:])) >= 9947
    assert source["clipSamples"] - samples[-1] >= source["sampleRate"] * 1.8
    assert all(text in ('roll', 'left', 'right') for _, text in notes)
    assert collections.Counter(action for _, action in notes) == {'left': 84, 'right': 84, 'roll': 62}
    assert notes[0] == (15, 'right') and notes[-1] == (226, 'roll')
    for index, (beat, action) in enumerate(notes):
        original = source['notes'][index]
        assert beat == original['originalTick'] / 48 + source['provenance']['audioOffsetBeats']
        if action != 'roll':
            assert action == ('right' if index % 2 == 0 else 'left')
        assert original['sourceAddress'].startswith('0x80') and original['sourceCallers']
    assert intro == [(beat, 'diantou') for beat in (7, 9, 11, 12, 13)] + [(14, 'readyRight')]
    assert not (set(beat for beat, _ in intro) & set(beat for beat, _ in notes))
    assert len(set(people)) == len(people), "Duplicate captain cue"
    assert source['provenance']['discId'] == 'SOMJ01'


def verify_serialization(text, events, source):
    starts = [int(value) for value in re.findall(r"mStartSample: (\d+)", text)]
    ends = [int(value) for value in re.findall(r"mEndSample: (\d+)", text)]
    payloads = re.findall(r"mTextVal: (\w+)", text)
    indices_hex = re.search(r"_TextPayloadIdxs: ([0-9a-f]+)", text).group(1)
    indices = [value[0] for value in struct.iter_unpack("<i", bytes.fromhex(indices_hex))]
    assert starts == ends == [sample_at(beat, source) for beat, _ in events]
    assert payloads == [action for _, action in events]
    assert indices == list(range(len(events)))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Verify without writing assets")
    args = parser.parse_args()
    source = json.loads(SOURCE.read_text(encoding="utf-8"))
    notes, people, intro = build_chart(source)
    validate(source, notes, people, intro)
    for filename, events in (("haibaoEvent.asset", notes), ("PeopleEvent.asset", people), ('haibaoIntroEvent.asset', intro)):
        # Original captain gestures are retained for provenance checks outside the player.
        path = (ROOT / "Assets/Editor/OriginalData" / filename) if filename == "PeopleEvent.asset" else TRACKS / filename
        current = path.read_text(encoding="utf-8")
        expected = serialize_track(current, events, source)
        verify_serialization(expected, events, source)
        if args.check:
            if current != expected:
                raise SystemExit("Stale chart: " + str(path) + "; run this script without --check")
            verify_serialization(current, events, source)
        else:
            path.write_text(expected, encoding="utf-8")
        print("{}: {} events {}".format(filename, len(events), "verified" if args.check else "written"))
    last_time = sample_at(notes[-1][0], source) / source["sampleRate"]
    tail = source["clipSamples"] / source["sampleRate"] - last_time
    print("Actions: {}".format(dict(collections.Counter(text for _, text in notes))))
    print("Last input: {:.6f}s (beat {}); tail: {:.3f}s".format(last_time, notes[-1][0], tail))


if __name__ == "__main__":
    main()

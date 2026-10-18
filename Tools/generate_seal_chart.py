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
    phrase_nods = set()
    for phrase in source["nodPhrases"]:
        phrase_nods.update(range(phrase["firstBeat"], phrase["firstBeat"] + phrase["count"]))
    main_beats = sorted(set(range(source["marchFirstBeat"], source["marchLastBeat"] + 1))
                        | set(source["extraHalfBeats"]))
    notes = [(beat, "diantou") for beat in source["introNodBeats"]]
    direction = "left"
    for beat in main_beats:
        action = "diantou" if beat in phrase_nods else direction
        notes.append((beat, action))
        if action != "diantou":
            direction = "right" if direction == "left" else "left"
    notes.sort()

    # Both NPC seals use haibaoEventID, so their actions and the player's
    # expected input share the exact same timestamps and payloads.
    people = [(0, "characterMove")]
    people.extend((beat, "characterDiantou") for beat, _ in notes)
    speak_beats = {11}
    # Original first extra response is beat 25.5. Its three-syllable spoken
    # cue spans 22.5/23/23.5; animate the captain at the first syllable.
    speak_beats.update(beat - 3 for beat in source["extraHalfBeats"])
    speak_beats.update(phrase["cueBeat"] for phrase in source["nodPhrases"])
    people.extend((beat, "characterSpeak01") for beat in sorted(speak_beats))
    people.sort(key=lambda item: (item[0], item[1]))
    return notes, people


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


def validate(source, notes, people):
    assert source["sampleRate"] == 44100 and source["bpm"] == 133
    assert len(source["extraHalfBeats"]) == 18
    assert all(beat % 1 == .5 for beat in source["extraHalfBeats"])
    assert len(notes) == 235, "Five opening nods plus 230 main inputs are expected"
    samples = [sample_at(beat, source) for beat, _ in notes]
    assert all(a < b for a, b in zip(samples, samples[1:])), "Duplicate or unsorted input"
    assert min(b - a for a, b in zip(samples, samples[1:])) >= 9947
    assert source["clipSamples"] - samples[-1] >= source["sampleRate"] * 1.8
    assert all(text in ("diantou", "left", "right") for _, text in notes)
    # Protect the hand-authored opening's action sequence. Quantization fixes
    # the 25.5-beat marker's original 686-sample timing error, not the pattern.
    opening_beats = [7, 9, 11, 12, 13] + list(range(15, 26)) + [25.5] + list(range(26, 31))
    opening_actions = ["diantou"] * 5 + ["left", "right"] * 8 + ["left"]
    assert notes[:22] == list(zip(opening_beats, opening_actions))
    assert len(set(people)) == len(people), "Duplicate captain cue"
    assert [beat for beat, text in people if text == "characterDiantou"] == [beat for beat, _ in notes]
    for phrase in source["nodPhrases"]:
        assert phrase["cueBeat"] < phrase["firstBeat"]
        assert not (set(source["extraHalfBeats"]) & set(range(phrase["firstBeat"], phrase["firstBeat"] + phrase["count"])))


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
    notes, people = build_chart(source)
    validate(source, notes, people)
    for filename, events in (("haibaoEvent.asset", notes), ("PeopleEvent.asset", people)):
        path = TRACKS / filename
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

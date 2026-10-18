#!/usr/bin/env python3
"""Reproduce the spoken-cue fingerprint analysis used for SealChart.json.

Optional analysis dependencies: ffmpeg on PATH, numpy, scipy.
This script reads audio and chart data; it does not write Unity assets.
"""

import json
from pathlib import Path
import subprocess

import numpy as np
from scipy.signal import butter, correlate, sosfilt


ROOT = Path(__file__).resolve().parents[1]
SOURCE = Path(__file__).with_name("SealChart.json")


def main():
    chart = json.loads(SOURCE.read_text(encoding="utf-8"))
    audio = ROOT / "Assets/Resources/Art/Level_cellanim/Music/haibao.mp3"
    rate = 11025
    decoded = subprocess.check_output([
        "ffmpeg", "-v", "error", "-i", str(audio), "-vn", "-f", "f32le",
        "-ac", "1", "-ar", str(rate), "pipe:1"
    ])
    waveform = np.frombuffer(decoded, dtype=np.float32)
    print("Decoded audio: {} samples at {} Hz; {:.6f}s".format(
        len(waveform), rate, len(waveform) / rate))
    filtered = sosfilt(butter(3, [300, 2500], fs=rate, btype="bandpass", output="sos"), waveform)
    samples_per_beat = rate * 60 / chart["bpm"]

    # The original hand-authored response at beat 25.5 anchors the template.
    # A quarter-second excerpt of its preceding spoken cue at beat 23 is
    # matched to every later integer beat, allowing +/-20 ms alignment.
    start = round(23 * samples_per_beat - .05 * rate)
    template = filtered[start:start + round(.25 * rate)].copy()
    template /= np.linalg.norm(template)
    matches = []
    for beat in range(chart["marchFirstBeat"], chart["marchLastBeat"] + 1):
        start = round(beat * samples_per_beat - .07 * rate)
        segment = filtered[start:start + round(.29 * rate)]
        energy = np.sqrt(np.convolve(segment * segment, np.ones(len(template)), "valid"))
        score = float((correlate(segment, template, "valid") / (energy + 1e-8)).max())
        if score > .65:
            response = beat + 2.5
            matches.append(response)
            print("Cue beat {:3d} -> extra response {:5.1f}; correlation {:.3f}".format(
                beat, response, score))
    assert matches == chart["extraHalfBeats"], "Audio cue matches differ from authored half beats"
    assert abs(len(waveform) / rate - chart["clipSamples"] / chart["sampleRate"]) < .01
    print("All {} three-syllable cue groups agree with the chart.".format(len(matches)))


if __name__ == "__main__":
    main()

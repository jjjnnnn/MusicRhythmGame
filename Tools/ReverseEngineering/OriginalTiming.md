# Wii Flipper-Flop original timing extraction

This directory contains read-only inspectors written for the user's Japanese Wii
image. `goma` is the original animal-seal minigame. The internal name `seal`
means sealing boxes (Packing Pests), and is **not** Flipper-Flop.

## Reproduce

```powershell
python Tools/ReverseEngineering/wii_tickflow.py Logs/SealExtraction/SOMJ01/sys/main.dol --scene 11 --graph Logs/SealExtraction/SOMJ01/derived/goma_call_graph.json
python Tools/ReverseEngineering/extract_goma_timing.py Logs/SealExtraction/SOMJ01/sys/main.dol Logs/SealExtraction/SOMJ01/derived
```

The input DOL is extracted locally from the user's `SOMJ01` WBFS. No ROM is
downloaded. Original binary data, third-party research sources, and disassembly
are temporary local evidence; they should not be committed as project source.

## Original data and checks

- Scene ID: 11 (`eScene_Goma`), version 0.
- Script entry: `0x8022E3EC`; music start command: `0x8022E48C`.
- Background wave sound ID: 45 (`WSD_GOMA`).
- The script's tempo instruction at `0x8022E3F8` is `tempo_wave<45>`, not an
  immediate BPM literal. The supplied DOL's WaveInfo at `0x802EF640` says sound
  45, sample rate 32000, tempo-table pointer `0x802EE86C`. That table specifies
  230 beats, 3,320,281 samples, last-stop flag `0x8000`. The original formula
  `beats * rate * 60 / samples` yields **133.000791198 BPM**, which confirms
  the project's 133 BPM grid independently (less than one millisecond drift
  over this track). The original waveform's tempo span is 103.75878125 seconds.
- 48 TickFlow ticks per beat; instruction words and arguments are big endian.
- Format: opcode = word & 0x3FF; argc = (word >> 10) & 15;
  embedded argument = word >> 14. This is the Wii format, not Megamix's format.
- The full original level has 230 judged inputs: 168 ordinary flips and 62 rolls.
- Five opening bobs are animation commands only; they are not original inputs.
- Every extracted input time/type is checked against an independently scheduled
  original NPC flip/roll command. Source addresses and call sites are retained.
- Opposite successful/failure/Perfect branches produce identical input timing.
  Captain praise is a runtime-dependent outcome; the canonical event list models
  successful play and a deterministic first choice for random praise.
- Export times are relative to the original background wave start. The project's
  previously imported MP3 has an additional five-beat introduction; audio
  alignment must be checked separately before using this offset in Unity.

## Animation state, confirmed in the supplied Japanese DOL

`Goma::CMyFlow::_1C` starts at `0x8004D2F4`. Its opcode dispatch table is at
`0x802F1A10`. Scene initialization stores direction flags at offsets `0x250`
(player) and `0x251` (NPC), and A/B phase flags at `0x252` / `0x253`.

- `0x200 delay, type`: schedule input. Type 0 uses A input (4/0x20 internally);
  type 1 uses A+B input (6/0x22 internally). See `0x8004D344..0x8004D4B8`.
- `0x201`: bob/press animation. Index 1 for phase A, index 12 for phase B.
- `0x202 direction, freeze`: ready-flipper animation; sets both direction flags.
  Direction 1 is ready L (2/13), direction 0 ready R (3/14).
- `0x203`: normal NPC flip. It chooses 4/5 for phase A or 15/16 for phase B,
  then toggles direction at `0x8004D720`. Direction 1 selects TurnToL;
  direction 0 selects TurnToR.
- `0x204 last`: normal NPC roll. If direction 1, selects index 20 from phase A,
  or 9 from phase B; if direction 0, selects index 19 from phase A, or 8 from
  phase B. Then toggles direction at `0x8004D844` and A/B phase at
  `0x8004D858`. There is no direction reset after rolling.
- The music-beat-9 ready command sets direction 0. The first actual input at
  music beat 10 is therefore `goma_A_TurnToR` (5).
- Successful player handling (`0x8004EA00..0x8004EF98`) uses the same choices.
  `TurnedTo` and `RolledFrom` variants occur in failure handling, not as an
  automatic transition after successful actions.

Original rolls update the actor's root X by ±72 and scroll the scene base;
the BRCAD root offsets are authored to work with those movements. A fixed-stage
Unity renderer must compensate root motion explicitly rather than directly
adding the BRCAD displacement and causing a teleport. NPC position changes are
at `0x8004D798..0x8004D7F8`; player changes at `0x8004ED98..0x8004EDD4`.

Animation uses native 60 fps at speed 1.0 in this scene. The base scene resets
the CellAnimManager's tempo-update flag to false (JP `0x800081E8..0x800081F4`),
and Goma does not enable it. Do not stretch every animation to one musical beat.
`CCellAnim::setAnim` freezes the final frame when complete unless another
animation is explicitly scheduled.

## Captain outcome branches

Opcode `0x206` is a successful-roll-stage query, not a locale selector. Its DOL
handler (`0x8004D9AC`) calls `0x800902F8`, which returns min(stage, cap=2).
`0x207` sets stage 1; `0x208` resets the successful-roll count. Successful rolls
increment that count and advance the stage every four; misses clear the stage.
Stage 0 selects captain miss animations, stages 1/2 select just animations.

## Public research sources

- [Wii TickFlow declaration](https://github.com/conhlee/rhf/blob/master/src/GameUtil/TickFlowDecl.hpp)
- [Wii TickFlow interpreter](https://github.com/conhlee/rhf/blob/master/src/GameUtil/TickFlow.cpp)
- [Wii scene IDs](https://github.com/conhlee/rhf/blob/master/src/Game/SceneManager.hpp)
- [CellAnim playback](https://github.com/conhlee/rhf/blob/master/src/GameUtil/CellAnim.cpp)
- [WaveInfo and WaveTempo structures](https://github.com/conhlee/rhf/blob/master/src/GameUtil/Sound.hpp)
- [Original sample-to-tempo formula](https://github.com/conhlee/rhf/blob/master/src/GameUtil/Sound.cpp)
- [Goma animation labels](https://github.com/conhlee/rhf/blob/master/build_default/SOME01/include/cellanim/goma/ver0/rcad_goma_labels.h)

These public sources document the US decompilation. The Japanese addresses and
actual event sequence above are obtained from the user's Japanese DOL and are
not transplanted from US/3DS offsets.

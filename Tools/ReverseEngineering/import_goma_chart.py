"""Convert traced SOMJ01 goma TickFlow events into the Unity chart's explicit source data.

Usage: python Tools/ReverseEngineering/import_goma_chart.py <goma_original_timing.json>
The game scripts use 48 ticks/beat. The existing Perfect Version MP3 starts five
beats earlier than the original prepared-wave start, as verified by all five
intro presses and the 18 triple-response half beats.
"""
import argparse
import collections
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def convert(path):
    raw = json.loads(Path(path).read_text(encoding='utf-8'))
    assert (raw['discId'], raw['sceneId'], raw['sceneVersion'], raw['internalName']) == ('SOMJ01', 11, 0, 'goma')
    assert raw['ticksPerBeat'] == 48 and raw['inputCount'] == 230
    assert raw['verification']['oppositeBranchSettingsProduceSameInputs']
    assert raw['verification']['inputTimesAndTypesMatchNpcFlipRollCommands']
    notes = []
    for index, event in enumerate(raw['inputs']):
        # Goma opcode 202 resets direction=false. On the first flip the game
        # selects A_TurnToR, then flips this state after EVERY flip or roll.
        target = 'right' if index % 2 == 0 else 'left'
        notes.append(dict(beat=event['inputBeat'] + 5,
                          action=event['action'],
                          directionAfter=target, originalType=event['inputType'],
                          animationIndex=event['animationIndex'], animationName=event['animationName'],
                          directionFlagBefore=event['directionFlagBefore'], phaseBBefore=event['phaseBBefore'],
                          directionFlagAfter=event['directionFlagAfter'], phaseBAfter=event['phaseBAfter'],
                          npcSourceAddress=event['npcSourceAddress'],
                          originalTick=event['inputTick'], sourceAddress=event['sourceAddress'],
                          sourceCallers=event['callers']))
    intro, preparations, captain = [], [], []
    for event in raw['events']:
        if event['name'] == 'seal_beat':
            intro.append(dict(beat=event['beat'] + 5, action='diantou', sourceAddress=event['sourceAddress']))
        elif event['name'] == 'raise_flipper':
            preparations.append(dict(beat=event['beat'] + 5,
                                     action='readyLeft' if event['args'][0] else 'readyRight',
                                     sourceAddress=event['sourceAddress'], arguments=event['args']))
        elif event['name'] == 'captain_animation':
            kind = event['arg0']
            action = 'characterDiantou' if kind == 1 else 'characterMove' if kind == 2 else 'characterSpeak01'
            captain.append(dict(beat=event['beat'] + 5, action=action, commandKind=kind,
                                arguments=event['args'], sourceAddress=event['sourceAddress']))
    counts = dict(collections.Counter(note['originalType'] for note in notes))
    assert counts == {'flip': 168, 'roll': 62}
    assert [event['beat'] for event in intro] == [7, 9, 11, 12, 13]
    halves = [note['beat'] for note in notes if note['beat'] % 1]
    chart = dict(schemaVersion=2, sampleRate=44100, bpm=133, clipSamples=4687488,
                 marchFirstBeat=15, marchLastBeat=226, extraHalfBeats=halves,
                 expectedCounts=dict(inputs=230, flip=168, roll=62, introDemonstrations=5),
                 provenance=dict(discId=raw['discId'], dolSha256=raw['sha256'], internalName='goma/ver0',
                                 sceneId=11, entryAddress=raw['entryAddress'],
                                 musicStartAddress=raw['musicStartAddress'], musicWaveSoundId=raw['musicWaveSoundId'],
                                 originalWaveTempo=raw.get('originalWaveTempo'),
                                 ticksPerBeat=48, audioOffsetBeats=5,
                                 inputBasis='Original opcode 0x200 delay/type, verified against NPC 0x203/0x204 events and opposite branches.',
                                 directionBasis='Opcode 0x202 resets false; first flip A_TurnToR; both flip and roll toggle direction, roll also toggles A/B.',
                                 captainVisualMapping='Original event times and arguments retained; mapped to the project captain animator move/nod/speak triggers.',
                                 introBasis='Five opcode 0x201 demonstrations have no opcode 0x200 input; excluded from judgement.'),
                 notes=notes, introDemonstrations=intro, preparationEvents=preparations, captainEvents=captain)
    target = ROOT / 'Tools/SealChart.json'
    target.write_text(json.dumps(chart, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Imported:', len(notes), 'inputs,', counts, '; intro:', len(intro), '; captain events:', len(captain))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('trace')
    convert(parser.parse_args().trace)

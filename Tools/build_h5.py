"""Build the mobile rhythm game as one offline HTML file, with no network dependencies."""
import argparse
import base64
from io import BytesIO
import json
from pathlib import Path
import shutil
import subprocess
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
WEB = ROOT / 'Web'
ART = ROOT / 'Assets/Resources/Characters/ArcticBand'
LOG = ROOT / 'Logs/SealValidation/Mobile'

def data_uri(data, mime):
    return 'data:' + mime + ';base64,' + base64.b64encode(data).decode('ascii')

def artwork(path, quality=92):
    folder = LOG / 'h5-assets'
    folder.mkdir(parents=True, exist_ok=True)
    cache = folder / ('%s-%s-%s.webp' % (path.stem, quality, path.stat().st_mtime_ns))
    if cache.exists():
        content = cache.read_bytes()
        return data_uri(content, 'image/webp'), len(content)
    with Image.open(path) as image:
        output = BytesIO()
        image.save(output, format='WEBP', quality=quality, method=6, exact=True)
        content = output.getvalue()
        cache.write_bytes(content)
        return data_uri(content, 'image/webp'), len(content)

def build_data():
    source = json.loads((ROOT / 'Tools/SealChart.json').read_text(encoding='utf-8'))
    rate, bpm = source['sampleRate'], source['bpm']
    rows = [(x['beat'], 'nod') for x in source['introDemonstrations']]
    rows += [(x['beat'], x['action']) for x in source['notes']]
    rows.sort()
    phase, next_left = False, False
    notes = []
    for beat, action in rows:
        input_key = 0 if action == 'nod' else (1 if next_left else 2) if action == 'roll' else (1 if action == 'left' else 2)
        notes.append(dict(sample=round(beat * 60 / bpm * rate), beat=beat, action=action, input=input_key,
                          phaseBefore=phase, nextLeftBefore=next_left))
        if action != 'nod': next_left = not next_left
        if action == 'roll': phase = not phase
    assert len(notes) == 235 and all(x['input'] == 0 for x in notes[:5])
    assert sum(x['action'] == 'roll' for x in notes) == 62
    animation = json.loads((ROOT / 'Assets/Resources/Art/Level_cellanim/OriginalGoma/goma.json').read_text(encoding='utf-8'))
    durations = [x['totalDuration'] for x in animation['animations']]
    assets, sizes = {}, {}
    for key, path in [('background', ART / 'background.png'), ('seals', ART / 'seals.png'),
                      ('captain', ART / 'captain.png'), ('ui', ROOT / 'Assets/Resources/UI/SealPaper/seal-ui-atlas.png'),
                      ('beatBar', ROOT / 'Assets/Resources/UI/SealPaper/rhythm-bar.png')]:
        assets[key], sizes[key] = artwork(path)
    LOG.mkdir(parents=True, exist_ok=True)
    music = LOG / 'h5-music.mp3'
    original = ROOT / 'Assets/Resources/Art/Level_cellanim/Music/haibao.mp3'
    ffmpeg = shutil.which('ffmpeg')
    if not ffmpeg: raise RuntimeError('FFmpeg is required to package the offline music.')
    if not music.exists() or music.stat().st_mtime < original.stat().st_mtime:
        subprocess.run([ffmpeg, '-hide_banner', '-loglevel', 'error', '-y', '-i', str(original),
                        '-codec:a', 'libmp3lame', '-b:a', '128k', '-ar', str(rate), '-ac', '2',
                        '-map_metadata', '-1', str(music)], check=True)
    pcm = subprocess.check_output([ffmpeg, '-hide_banner', '-loglevel', 'error', '-i', str(music), '-f', 's16le', '-acodec', 'pcm_s16le', '-'])
    assert len(pcm) // 4 == source['clipSamples'], 'MP3 sample count changed'
    assets['music'] = data_uri(music.read_bytes(), 'audio/mpeg')
    sizes['music'] = music.stat().st_size
    # Canvas source rectangles are top-left based, including the packed top padding.
    seal_rows = {
        'partner': [[73,73,341,330], [511,142,348,266], [942,78,325,331], [1376,77,328,332]],
        'player': [[73,496,341,330], [510,563,349,268], [942,502,325,332], [1377,501,328,333]]}
    captain_rows = [[120,49,311,422], [558,42,397,432], [1071,44,434,425],
                    [157,562,279,397], [586,520,377,445], [1083,524,377,439]]
    ui_rows = dict(key=[82,108,204,205], pressedKey=[383,106,200,207], paper=[645,70,265,255],
                   track=[969,106,203,212], left=[89,412,190,178], right=[389,412,188,178],
                   down=[673,404,192,194], pause=[1000,412,150,180], play=[105,688,163,184],
                   restart=[387,672,196,208], home=[664,686,216,196], settings=[985,676,195,202],
                   tick=[145,955,78,222], seal=[371,959,222,221], ring=[664,958,224,220], sparkle=[1006,983,154,164])
    for rect in ui_rows.values(): rect[1] += 2
    return dict(sampleRate=rate, bpm=bpm, clipSamples=source['clipSamples'], notes=notes,
                introSamples=[n['sample'] for n in notes[:5]], motionDurations=durations,
                preparationSamples=[round(x['beat'] * 60 / bpm * rate) for x in source['preparationEvents']],
                assets=assets, spriteRects=dict(sealFrames=seal_rows, captainFrames=captain_rows, ui=ui_rows,
                                               beatBar=[544,65,168,1130])), sizes

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=ROOT / 'Builds/H5/SealRhythm.html')
    args = parser.parse_args()
    data, sizes = build_data()
    html = (WEB / 'index.template.html').read_text(encoding='utf-8')
    replacements = {'STYLE': (WEB / 'game.css').read_text(encoding='utf-8'),
                    'DATA': json.dumps(data, ensure_ascii=False, separators=(',', ':')).replace('<', '\\u003c'),
                    'ENGINE': (WEB / 'game-engine.js').read_text(encoding='utf-8'),
                    'UI': (WEB / 'game-ui.js').read_text(encoding='utf-8')}
    for key, text in replacements.items():
        assert '{{' + key + '}}' in html, 'Template placeholder missing: ' + key
        html = html.replace('{{' + key + '}}', text)
    assert '{{ENGINE}}' not in html and '{{UI}}' not in html
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(html, encoding='utf-8')
    report = dict(file=str(args.output), bytes=args.output.stat().st_size, assets=sizes,
                  totalNotes=len(data['notes']), practiceNotes=sum(n['sample'] < data['sampleRate'] * 32 * 60 / data['bpm'] - data['sampleRate'] * .15 for n in data['notes']),
                  samples=data['clipSamples'], offline=True)
    (LOG / 'h5-build.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(report, ensure_ascii=False, indent=2))

if __name__ == '__main__':
    main()

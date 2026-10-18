"""Record deleted tracked Assets against HEAD, including their original byte sizes."""
from pathlib import Path
import json
import subprocess

root = Path(__file__).resolve().parents[1]
git = ['git', '-c', 'safe.directory=' + root.as_posix()]
paths = subprocess.check_output(git + ['diff', '--name-only', '--diff-filter=D', '-z', '--', 'Assets'], cwd=str(root)).decode('utf-8').split('\0')
paths = [path for path in paths if path]
objects = ''.join('HEAD:' + path + '\n' for path in paths).encode('utf-8')
sizes = subprocess.check_output(git + ['cat-file', '--batch-check=%(objectsize)'], input=objects, cwd=str(root)).decode().splitlines()
files = [{'path': path, 'bytes': int(size)} for path, size in zip(paths, sizes)]
assets = [file for file in files if not file['path'].endswith('.meta')]
report = {'deletedAssets': len(assets), 'deletedSourceBytes': sum(file['bytes'] for file in assets),
          'deletedMetadataFiles': len(files) - len(assets), 'files': files}
destination = root / 'Logs/SealValidation/Optimization/deleted-files.json'
destination.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({key: value for key, value in report.items() if key != 'files'}, ensure_ascii=False))

"""Refresh the project asset ledger without downloading or changing assets."""
from pathlib import Path
from datetime import date
import hashlib, json
root=Path(__file__).resolve().parent.parent
assets=root/'src/Yukkuri.Game/Assets'
ledger=root/'assets-source/asset-manifest.json'
previous={a['path']:a for a in json.loads(ledger.read_text(encoding='utf-8-sig'))['assets']} if ledger.exists() else {}
entries=[]
for p in sorted(assets.rglob('*')):
    if not p.is_file() or p.suffix in ('.import','.json'): continue
    rel=p.relative_to(root).as_posix()
    item=dict(path=rel,sha256=hashlib.sha256(p.read_bytes()).hexdigest(),bytes=p.stat().st_size,recorded=previous.get(rel,{}).get('recorded',date.today().isoformat()),status='prototype')
    if p.suffix=='.ttf':
        item.update(author='Noto / Adobe / Google contributors',source='https://github.com/google/fonts/tree/main/ofl/notosanssc',download='https://raw.githubusercontent.com/google/fonts/main/ofl/notosanssc/NotoSansSC%5Bwght%5D.ttf',license='SIL Open Font License 1.1',licenseFile='third-party/NotoSansSC-OFL.txt',commercialUse='Bundling with software permitted under OFL; retain license; do not sell font by itself.',modifications='None to font binary; runtime variation settings only')
    elif p.suffix=='.png':
        item.update(author='Generated for this project using built-in OpenAI ImageGen',source='assets-source/sanctuary-morning-prompt.txt',license='Generated asset; OpenAI service terms apply, not a CC0 download',referenceImages=[],modifications='None',use='Title menu concept art; not actual game screenshot')
    elif p.suffix=='.wav':
        item.update(author='Project-generated procedural composition',source='tools/make-audio.py',license='Project-owned generated material; no external samples or imported melody',modifications='None; Godot import compression only',use='Region music or interaction sound')
        if p.stem.startswith('coo-'):
            item.update(source='tools/make-m01-audio.ps1',status='milestone-prototype',license='Project-generated original nonverbal synthesis; no external samples',use='M01 reaction cue, not a licensed character voice or speech synthesis')
    elif p.suffix=='.svg':
        item.update(author='Project-native vector artwork',source='tools/make-icons.py',license='Project-generated artwork; no external assets',modifications='None',use='UI button icon')
    elif p.suffix=='.glb':
        item.update(author='Project-generated geometry',source='tools/make-probe.py',license='Project-generated; no external model',use='Earlier toolchain probe retained, not current actors')
    entries.append(item)
for filename in ('SceneArt.cs','CreatureActor.cs','Main.MilestoneArt.cs'):
    path=root/'src/Yukkuri.Game'/filename
    entries.append(dict(path=path.relative_to(root).as_posix(),sha256=hashlib.sha256(path.read_bytes()).hexdigest(),author='Project-generated procedural geometry',source='Same file',license='Project-generated geometry; no external mesh or texture',status='Prototype interpretation, not final approved character art',use='Character, expression, tree, facility and environment mesh construction'))
(root/'assets-source/asset-manifest.json').write_text(json.dumps(dict(schemaVersion=1,assets=entries),ensure_ascii=False,indent=2),encoding='utf-8')
print(f'Catalogued {len(entries)} assets / asset generators.')

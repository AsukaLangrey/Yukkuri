"""Original synthesized prototype cues. No samples, source recordings or external melodies."""
import math, wave, struct, random, json
from pathlib import Path
from array import array

root = Path(__file__).resolve().parent.parent
out = root / 'src/Yukkuri.Game/Assets/Audio'
out.mkdir(parents=True, exist_ok=True)
rate = 22050
reports = []
def write(name, samples):
    peak = max(abs(v) for v in samples) or 1
    gain = min(0.7 / peak, 1.0)
    pcm = array('h', (int(max(-1,min(1,v*gain))*32767) for v in samples))
    with wave.open(str(out/name),'wb') as w:
        w.setparams((1,2,rate,len(pcm),'NONE','not compressed'))
        w.writeframes(pcm.tobytes())
    reports.append(dict(file=name,seconds=len(pcm)/rate,peak=peak*gain,rms=math.sqrt(sum(v*v for v in samples)/len(samples))*gain,source='original deterministic synthesis'))
def hz(midi): return 440*2**((midi-69)/12)
def tone(buf, start, duration, note, amplitude, bell=False):
    start=int(start*rate); n=int(duration*rate); f=hz(note)
    for i in range(n):
        t=i/rate
        env=(1-math.exp(-t*40))*math.exp(-t*(2.3 if bell else .6))*min(1,(n-i)/(rate*.12))
        value=math.sin(2*math.pi*f*t)+.2*math.sin(2*math.pi*f*2*t)
        buf[(start+i)%len(buf)]+=amplitude*env*value
for name, chords, notes, seed in [
    ('sanctuary-loop.wav',[(48,52,55),(45,48,52),(41,45,48),(43,47,50)], [72,74,76,79,81],83),
    ('wild-edge-loop.wav',[(45,48,52),(41,45,48),(43,46,50),(40,43,47)], [57,60,62,64,67],271)]:
    buf=array('f',[0])*(rate*24);rng=random.Random(seed)
    for bar in range(8):
        for note in chords[bar%4]: tone(buf,bar*3,3.8,note,.045)
        for beat in (0,1.5,2.25):
            tone(buf,bar*3+beat,1.4,rng.choice(notes),.075 if seed==83 else .035,True)
    # Wrapped note tails make the file a periodic loop, with no hard truncation.
    write(name,buf)
for name,notes in [('feed.wav',[76,79,84]),('build.wav',[48,55]),('select.wav',[81])]:
    buf=array('f',[0])*(rate//2)
    for i,note in enumerate(notes):tone(buf,i*.07,.28,note,.12,True)
    write(name,buf)
(out/'audio-analysis.json').write_text(json.dumps(reports,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(reports,ensure_ascii=False))

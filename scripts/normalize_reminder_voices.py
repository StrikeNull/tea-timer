"""Match cached built-in voice loudness offline; no runtime dependency in the EXE.

Requires numpy, scipy, soundfile and pyloudnorm in the audio processing environment.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
import pyloudnorm as pyln
import soundfile as sf
from scipy.signal import resample_poly

ROOT = Path(__file__).resolve().parents[1]
VOICES = ('tea-ready.wav', 'voice-cheerful.wav', 'voice-soft.wav',
          'voice-playful.wav', 'voice-today.wav', 'voice-brewing.wav')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--target', type=float, default=-20.0)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    reports = []
    for name in VOICES:
        path = ROOT / 'assets' / name
        raw_hash = hashlib.sha256(path.read_bytes()).hexdigest()
        data, rate = sf.read(path, dtype='float64')
        assert rate == 24000 and data.ndim == 1 and np.isfinite(data).all()
        meter = pyln.Meter(rate, filter_class='DeMan')
        before = float(meter.integrated_loudness(data))
        gain = 1.0
        if not args.check and abs(before - args.target) > .02:
            # One constant gain per phrase preserves its natural expression.
            gain = 10 ** ((args.target - before) / 20)
            true_peak = float(np.max(np.abs(resample_poly(data, 4, 1))))
            gain = min(gain, 10 ** (-1.0 / 20) / max(true_peak, 1e-12))
            sf.write(path, data * gain, rate, subtype='PCM_16')
            data, _ = sf.read(path, dtype='float64')
        after = float(meter.integrated_loudness(data))
        peak_db = float(20 * np.log10(max(np.max(np.abs(resample_poly(data, 4, 1))), 1e-12)))
        assert abs(after - args.target) < .1, f'{name}: unable to reach target without clipping'
        assert peak_db <= -.98 and np.max(np.abs(data)) < 1
        reports.append(dict(file=name, beforeLufs=before, afterLufs=after,
                            gainDb=20 * float(np.log10(gain)), truePeakDb=peak_db,
                            originalSha256=raw_hash,
                            sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    assert max(r['afterLufs'] for r in reports) - min(r['afterLufs'] for r in reports) < .1
    if not args.check:
        (ROOT / 'assets' / 'audio-levels.json').write_text(json.dumps(dict(
            source='Offline pyloudnorm BS.1770 loudness matching', targetLufs=args.target,
            truePeakLimitDb=-1, voices=reports), indent=2) + '\n', encoding='utf-8')
        for filename in ('voice-generation.json', 'voice-pack-generation.json', 'interaction-voice-generation.json'):
            path = ROOT / 'assets' / filename
            content = json.loads(path.read_text(encoding='utf-8'))
            records = content if isinstance(content, list) else [content]
            for record in records:
                match = next(r for r in reports if r['file'] == record['file'])
                record.setdefault('synthesizedSha256', match['originalSha256'])
                record['sha256'] = match['sha256']
                record['postprocessing'] = dict(loudnessLufs=match['afterLufs'], truePeakDb=match['truePeakDb'])
            path.write_text(json.dumps(content, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(reports))


if __name__ == '__main__':
    main()

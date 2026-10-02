"""Generate a cached reminder WAV with TK's existing local Qwen TTS GPU env."""
import argparse
import json
import os
from pathlib import Path
import time

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument("--model", type=Path, default=Path(r"G:\codex\TK\models\qwen3-tts-1.7b-customvoice"))
parser.add_argument("--speaker", default="Serena")
parser.add_argument("--text", default="茶泡好了，记得出汤哦。")
args = parser.parse_args()
os.environ["HF_HUB_OFFLINE"] = "1"
os.environ["HF_HUB_DISABLE_TELEMETRY"] = "1"
os.environ["TOKENIZERS_PARALLELISM"] = "false"
os.environ["MIOPEN_FIND_MODE"] = "FAST"

import numpy as np
import soundfile as sf
import torch
from qwen_tts import Qwen3TTSModel

torch.set_num_threads(4)
torch.set_num_interop_threads(1)
torch.manual_seed(42)
if not torch.cuda.is_available():
    raise SystemExit("TK's Qwen TTS GPU environment is required.")
model = Qwen3TTSModel.from_pretrained(str(args.model), device_map="cuda:0", dtype=torch.float16,
    attn_implementation="sdpa", local_files_only=True)
started = time.perf_counter()
torch.cuda.reset_peak_memory_stats()
instruction = "用清甜温柔、活泼的少女语气提醒，语速正常，自然亲切。"
with torch.inference_mode():
    waves, rate = model.generate_custom_voice(text=args.text, language="Chinese", speaker=args.speaker,
        instruct=instruction, max_new_tokens=128)
torch.cuda.synchronize()
wave = waves[0]
if not len(wave) or not np.isfinite(wave).all():
    raise SystemExit("TTS returned invalid audio.")
output = ROOT / "assets" / "tea-ready.wav"
sf.write(output, wave, rate, subtype="PCM_16")
report = {"source": "Local Qwen3-TTS-12Hz-1.7B-CustomVoice", "speaker": args.speaker, "text": args.text,
    "instruction": instruction, "sample_rate": rate, "duration_seconds": len(wave) / rate,
    "generation_seconds": time.perf_counter() - started, "device": torch.cuda.get_device_name(0),
    "peak_allocated_gib": torch.cuda.max_memory_allocated() / 1024**3, "file": output.name}
(ROOT / "verification-media").mkdir(exist_ok=True)
(ROOT / "verification-media" / "tts-generation.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(report, ensure_ascii=False))

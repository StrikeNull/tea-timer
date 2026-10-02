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
group = parser.add_mutually_exclusive_group()
group.add_argument("--pack", action="store_true", help="Generate three additional reminder voices with one model load.")
group.add_argument("--interactions", action="store_true", help="Generate the greeting and brewing voices with one model load.")
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
presets = [
    ("voice-cheerful.wav", "Vivian", "主人，茶泡好啦，请享用。", "用甜美活泼的动漫女仆少女语气，带着笑意，清晰自然，语速正常。"),
    ("voice-soft.wav", "Serena", "茶香正好，记得及时出汤哦。", "用温柔舒缓的少女语气轻声提醒，自然亲切，语速正常，不要拖长尾音。"),
    ("voice-playful.wav", "Vivian", "叮咚，茶好啦，快来喝茶吧。", "用俏皮可爱的少女语气，明亮轻快，有一点开心的小雀跃，语速正常。"),
] if args.pack else [("tea-ready.wav", args.speaker, args.text, "用清甜温柔、活泼的少女语气提醒，语速正常，自然亲切。")]
if args.interactions:
    presets = [
        ("voice-today.wav", "Vivian", "今天喝什么茶呢？", "用甜美活泼的动漫女仆少女语气，带着笑意自然地询问，清晰亲切，语速正常。"),
        ("voice-brewing.wav", "Serena", "开始泡茶啦，稍等一下哦。", "用清甜温柔的少女语气，轻快自然地宣布开始泡茶，语速正常，不要拖长尾音。"),
    ]
reports = []
for filename, speaker, text, instruction in presets:
    started = time.perf_counter()
    torch.cuda.reset_peak_memory_stats()
    with torch.inference_mode():
        waves, rate = model.generate_custom_voice(text=text, language="Chinese", speaker=speaker,
            instruct=instruction, max_new_tokens=128)
    torch.cuda.synchronize()
    wave = waves[0]
    if not len(wave) or not np.isfinite(wave).all():
        raise SystemExit("TTS returned invalid audio.")
    output = ROOT / "assets" / filename
    sf.write(output, wave, rate, subtype="PCM_16")
    report = {"source": "Local Qwen3-TTS-12Hz-1.7B-CustomVoice", "speaker": speaker, "text": text,
        "instruction": instruction, "sample_rate": rate, "duration_seconds": len(wave) / rate,
        "generation_seconds": time.perf_counter() - started, "device": torch.cuda.get_device_name(0),
        "peak_allocated_gib": torch.cuda.max_memory_allocated() / 1024**3, "file": output.name}
    reports.append(report)
    print(json.dumps(report, ensure_ascii=False), flush=True)
(ROOT / "verification-media").mkdir(exist_ok=True)
name = "tts-interactions-generation.json" if args.interactions else "tts-pack-generation.json" if args.pack else "tts-generation.json"
(ROOT / "verification-media" / name).write_text(json.dumps(reports if args.pack or args.interactions else reports[0], ensure_ascii=False, indent=2), encoding="utf-8")

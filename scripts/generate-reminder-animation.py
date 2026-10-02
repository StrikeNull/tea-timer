"""Generate a short reminder with the user's local ComfyUI and existing models."""
import argparse
import json
from pathlib import Path
from urllib import request

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument("role", choices=["maid", "gpt", "dragon"])
parser.add_argument("--server", default="http://127.0.0.1:8189")
parser.add_argument("--input-dir", type=Path, default=Path(r"G:\Comfy-Desktop\ComfyUI-Shared\input"))
parser.add_argument("--turbo", action="store_true", help="Use the existing 8-step turbo LoRA; otherwise use the saved workflow's 20-step base model.")
args = parser.parse_args()
atlas_names = {"maid": "tea-maid.png", "gpt": "gpt-maid.png", "dragon": "gpt-dragon.png"}
descriptions = {
    "maid": "blue-haired chibi whale maid wearing a navy blue and white frilled maid dress, whale headband and small whale tail",
    "gpt": "silver-haired chibi tea maid with round glasses, mint green and white frilled dress and hair ribbon",
    "dragon": "white-haired lavender-eyed chibi dragon girl with white horns, lavender wings and tail, white dress and knot ornaments",
}
atlas = Image.open(ROOT / "assets" / atlas_names[args.role]).convert("RGBA")
cell = atlas.width // 3
sprite = atlas.crop((0, 0, cell, atlas.height))
sprite.thumbnail((340, 340), Image.Resampling.LANCZOS)
canvas = Image.new("RGB", (384, 384), (0, 255, 0))
canvas.paste(sprite, ((384 - sprite.width) // 2, (384 - sprite.height) // 2), sprite)
canvas.save(args.input_dir / ("tea_ready_" + args.role + "_first.png"))

prompt = ("A fixed camera, full body 2D anime sticker animation of the exact " + descriptions[args.role] +
    " from the first reference frame. Keep her face, clothes, proportions and accessories unchanged. "
    "She gives a gentle happy smile, blinks once, gives a small friendly nod, and slightly raises her teacup with both hands "
    "to remind the viewer that tea is ready. Her hair and skirt move subtly. Keep the cup stable and hand motion small. Smooth natural character motion, "
    "no camera motion or zoom, no text, no additional characters, no scene change. Entire character remains inside frame. "
    "Background stays a perfectly flat pure bright chroma green RGB 0,255,0, without gradients, shadows or props. "
    "The character retains her original colors without green reflections. Silent animation. Finish in a calm steady pose.")

def node(kind, **inputs):
    return {"class_type": kind, "inputs": inputs}

graph = {
    "1": node("UNETLoader", unet_name="minimax_h3_fl2va_pruned_int8_convrot.safetensors", weight_dtype="default"),
    "2": node("CLIPLoader", clip_name="qwen3vl_32b_minimax_h3_nvfp4_awq.safetensors", type="minimax", device="default"),
    "3": node("VAELoader", vae_name="minimax_h3_video_vae_fp16.safetensors"),
    "4": node("LoadImage", image="tea_ready_" + args.role + "_first.png"),
    "6": node("MiniMaxH3ImageToVideo", clip=["2", 0], vae=["3", 0], prompt=prompt, width=384, height=384, length=56, first_frame=["4", 0]),
    "8": node("MiniMaxLowVRAMAttention", model=["7" if args.turbo else "1", 0], head_chunks=4),
    "9": node("MiniMaxChunkFeedForward", model=["8", 0], chunks=4, seq_threshold=4096),
    "10": node("BasicGuider", model=["9", 0], conditioning=["6", 0]),
    "11": node("RandomNoise", noise_seed=2026100201),
    "12": node("KSamplerSelect", sampler_name="res_multistep"),
    "13": node("BasicScheduler", model=["9", 0], scheduler="simple", steps=8 if args.turbo else 20, denoise=1.0),
    "14": node("SamplerCustomAdvanced", noise=["11", 0], guider=["10", 0], sampler=["12", 0], sigmas=["13", 0], latent_image=["6", 1]),
    "15": node("VAEDecode", samples=["14", 0], vae=["3", 0]),
    "16": node("SaveImage", images=["15", 0], filename_prefix="tea_timer/ready_" + args.role),
}
if args.turbo:
    graph["7"] = node("LoraLoaderModelOnly", model=["1", 0], lora_name="minimax_h3_fl2v_turbo_8step_v1.0_comfyui_bf16.safetensors", strength_model=1.0)
workflows = ROOT / "workflows"
workflows.mkdir(exist_ok=True)
(workflows / ("ready-" + args.role + ".api.json")).write_text(json.dumps(graph, ensure_ascii=False, indent=2), encoding="utf-8")
data = json.dumps({"prompt": graph, "client_id": "tea-timer-media"}).encode("utf-8")
req = request.Request(args.server + "/prompt", data=data, headers={"Content-Type": "application/json"})
with request.urlopen(req, timeout=30) as response:
    result = json.load(response)
report = ROOT / "verification-media"
report.mkdir(exist_ok=True)
(report / ("job-" + args.role + ".json")).write_text(json.dumps(result, indent=2), encoding="utf-8")
print(json.dumps(result))

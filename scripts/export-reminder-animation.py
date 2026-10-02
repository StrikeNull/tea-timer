"""Export completed local ComfyUI PNG frames to a transparent reminder GIF."""
import argparse
import json
from pathlib import Path
from urllib import request

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument("role", choices=["maid", "gpt", "dragon"])
parser.add_argument("--variant", choices=["ready", "extra", "brewing"], default="ready")
parser.add_argument("--server", default="http://127.0.0.1:8189")
parser.add_argument("--output-dir", type=Path, default=Path(r"G:\Comfy-Desktop\ComfyUI-Shared\output"))
args = parser.parse_args()
key = args.role if args.variant == "ready" else args.variant + "-" + args.role
job = json.loads((ROOT / "verification-media" / ("job-" + key + ".json")).read_text())
with request.urlopen(args.server + "/history/" + job["prompt_id"], timeout=20) as response:
    history = json.load(response)
if job["prompt_id"] not in history:
    raise SystemExit("Generation is still running.")
result = history[job["prompt_id"]]
if result["status"]["status_str"] != "success":
    messages = [m[1].get("exception_message", "") for m in result["status"]["messages"] if m[0] == "execution_error"]
    raise SystemExit("ComfyUI generation failed: " + " ".join(messages))
sources = result["outputs"]["16"]["images"]
(ROOT / "verification-media" / ("frames-" + key + ".json")).write_text(json.dumps({"prompt_id": job["prompt_id"], "images": sources}, indent=2), encoding="utf-8")
indices = np.linspace(0, len(sources) - 1, 28).round().astype(int)
frames, masks = [], []
for index in indices:
    item = sources[index]
    image = Image.open(args.output_dir / item["subfolder"] / item["filename"]).convert("RGB")
    pixels = np.array(image).astype(np.int16)
    green = (pixels[:, :, 1] > np.maximum(pixels[:, :, 0], pixels[:, :, 2]) + 80) & (pixels[:, :, 1] > 160)
    boundary = Image.new('L', (image.width + 2, image.height + 2), 255)
    boundary.paste(Image.fromarray((green * 255).astype(np.uint8)), (1, 1))
    ImageDraw.floodfill(boundary, (0, 0), 128)
    background = np.array(boundary)[1:-1, 1:-1] == 128
    # Enclosed gaps between hair, wings and the body also contain green screen.
    strong_green = (pixels[:, :, 1] > 160) & (pixels[:, :, 1] > np.maximum(pixels[:, :, 0], pixels[:, :, 2]) + 90)
    background |= strong_green
    if args.variant == 'brewing':
        # Atlas cells can contain a tiny tip from an adjacent pose. Keep the
        # main character's extent, including detached steam within that extent.
        foreground = ~background
        ys, xs = np.nonzero(foreground)
        seed = np.argmin((xs - image.width / 2) ** 2 + (ys - image.height / 2) ** 2)
        connected = Image.fromarray((foreground * 255).astype(np.uint8)).copy()
        ImageDraw.floodfill(connected, (int(xs[seed]), int(ys[seed])), 128)
        main = Image.fromarray(((np.array(connected) == 128) * 255).astype(np.uint8))
        left, top, right, bottom = main.getbbox()
        extent = np.zeros_like(foreground)
        extent[max(0, top - 3):bottom + 3, max(0, left - 3):right + 3] = True
        background |= ~extent
    if background.mean() < .25:
        raise SystemExit("Chroma background was not preserved; inspect generated frames before exporting.")
    edge = np.array(Image.fromarray((background * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(3))) > 0
    spill = edge & ~background & (pixels[:, :, 1] > np.maximum(pixels[:, :, 0], pixels[:, :, 2]) + 15)
    pixels[:, :, 1][spill] = np.maximum(pixels[:, :, 0], pixels[:, :, 2])[spill]
    pixels[background] = [0, 255, 0]
    frames.append(Image.fromarray(pixels.astype(np.uint8)))
    masks.append(background)

contact = Image.new("RGB", (384 * 7, 384), "white")
for col, frame in enumerate(frames[::4]):
    rgba = frame.convert("RGBA")
    rgba.putalpha(Image.fromarray((~masks[col * 4] * 255).astype(np.uint8)))
    contact.paste(rgba, (col * 384, 0), rgba)
contact.save(ROOT / "verification-media" / ("contact-" + key + ".png"))
quantized = contact.quantize(colors=255, method=Image.Quantize.MEDIANCUT)
palette = Image.new("P", (1, 1))
palette.putpalette([0, 255, 0] + quantized.getpalette()[:765])
gif_frames = []
for frame, mask in zip(frames, masks):
    indexed = frame.quantize(palette=palette, dither=Image.Dither.NONE)
    indices = np.array(indexed)
    indices[mask] = 0
    indexed = Image.fromarray(indices, "P")
    indexed.putpalette(palette.getpalette())
    indexed.info["transparency"] = 0
    gif_frames.append(indexed)
duration = round(len(sources) / 24 * 100) * 10
delays = [round((i + 1) * duration / 28 / 10) * 10 - round(i * duration / 28 / 10) * 10 for i in range(28)]
output = ROOT / "assets" / (args.variant + "-" + args.role + ".gif")
gif_frames[0].save(output, save_all=True, append_images=gif_frames[1:], duration=delays,
    loop=0, transparency=0, disposal=2, optimize=False)
with Image.open(output) as check:
    assert check.n_frames >= 20
    actual_frame_count = check.n_frames
    actual_duration = sum(check.seek(i) or check.info["duration"] for i in range(check.n_frames))
    assert 2000 <= actual_duration <= 3000
report = {"role": args.role, "variant": args.variant, "prompt_id": job["prompt_id"], "source_frames": len(sources),
    "gif_frames": actual_frame_count, "duration_ms": actual_duration, "bytes": output.stat().st_size,
    "width": frames[0].width, "height": frames[0].height,
    "source": "Local ComfyUI MiniMax H3", "file": output.name}
(ROOT / "verification-media" / ("export-" + key + ".json")).write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report))

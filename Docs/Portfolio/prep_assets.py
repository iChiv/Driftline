# Prepares cropped gameplay images and syntax-highlighted code screenshots
# for the Driftline portfolio deck. Run from anywhere; paths are absolute.
import os
from PIL import Image

ROOT = r"E:/Projects/ABIG/Josef/Project-3/Driftline"
SHOTS = os.path.join(ROOT, "Assets/Screenshots")
OUT = os.path.join(ROOT, "Docs/Portfolio/assets")
os.makedirs(OUT, exist_ok=True)

def shot(name):
    return os.path.join(SHOTS, name)

CROPS = [
    # (source png, output name, box as fraction: left, top, right, bottom)
    ("cur_midgame.png",     "crop_board_en.png", (0.28, 0.04, 0.78, 0.96)),
    ("cur_menu.png",        "menu_en.png",       (0.0, 0.0, 1.0, 1.0)),
    ("cur_howto.png",       "guide_en.png",      (0.20, 0.08, 0.72, 0.72)),
    ("cur_lan.png",         "lan_full.png",      (0.0, 0.0, 1.0, 1.0)),
    (os.path.join(ROOT, "Docs/Review/2026-10-14-implemented-board.png"), "early_board.png", (0.32, 0.05, 0.83, 0.95)),
    (os.path.join(SHOTS, "cur_midgame.png"), "today_board.png", (0.276, 0.04, 0.794, 0.96)),
    ("cur_game_start.png",  "crop_roll.png",     (0.0, 0.0, 0.22, 0.20)),
    ("cur_select.png",      "crop_select.png",   (0.31, 0.11, 0.47, 0.38)),
    ("cur_select.png",      "crop_slide.png",    (0.29, 0.08, 0.53, 0.44)),
    ("cur_estop2.png",      "crop_estop.png",    (0.28, 0.10, 0.50, 0.42)),
    ("cur_ram_preview.png", "crop_resolve.png",  (0.42, 0.26, 0.68, 0.64)),
    ("cur_midgame.png",     "crop_hangar.png",   (0.24, 0.05, 0.85, 0.80)),
    ("cur_midgame.png",     "crop_portal.png",   (0.33, 0.12, 0.70, 0.52)),
    ("cur_lan.png",         "lan_board.png",     (0.40, 0.10, 0.97, 0.88)),
    ("cur_local_setup.png", "local_setup.png",   (0.0, 0.0, 1.0, 1.0)),
    ("theme_fortress.png",  "theme_fortress.png",(0.29, 0.04, 0.96, 1.0)),
    ("theme_dungeon.png",   "theme_dungeon.png", (0.29, 0.04, 0.96, 1.0)),
    ("theme_hangar.png",    "theme_hangar.png",  (0.29, 0.04, 0.96, 1.0)),
    ("theme_forest.png",    "theme_forest.png",  (0.29, 0.04, 0.96, 1.0)),
    ("theme_harbor.png",    "theme_harbor.png",  (0.29, 0.04, 0.96, 1.0)),
]

for src, name, box in CROPS:
    im = Image.open(src if os.path.isabs(src) else shot(src))
    w, h = im.size
    l, t, r, b = box
    im.crop((int(l*w), int(t*h), int(r*w), int(b*h))).save(os.path.join(OUT, name))
    print("crop ok:", name, im.size)

# ---- code screenshots ----
from pygments import highlight
from pygments.lexers import CSharpLexer
from pygments.formatters import ImageFormatter

EXCERPTS = [
    ("Assets/Scripts/Draftline/Core/MatchRules.cs", 444, 475, "code_rules.png"),
    ("Assets/Scripts/Draftline/Core/ComputerPlanner.cs", 52, 70, "code_ai.png"),
    ("Assets/Scripts/Draftline/Unity/DraftlineLanRoom.cs", 334, 353, "code_lan.png"),
]

for rel, start, end, name in EXCERPTS:
    path = os.path.join(ROOT, rel.replace("/", os.sep))
    lines = open(path, encoding="utf-8").read().splitlines()
    code = "\n".join(lines[start-1:end])
    fmt = ImageFormatter(
        style="monokai",
        font_name="Consolas",
        font_size=30,
        line_numbers=True,
        line_number_start=start,
        line_number_bg=None,
        image_pad=24,
        line_pad=8,
    )
    data = highlight(code, CSharpLexer(), fmt)
    with open(os.path.join(OUT, name), "wb") as f:
        f.write(data)
    print("code ok:", name)

# record native pixel sizes for aspect-true placement in the deck
import json
dims = {}
for f in os.listdir(OUT):
    if f.endswith(".png"):
        with Image.open(os.path.join(OUT, f)) as im:
            dims["assets/" + f] = {"w": im.size[0], "h": im.size[1]}
with open(os.path.join(OUT, "dims.json"), "w") as f:
    json.dump(dims, f)
print("dims ok:", len(dims))

print("done")

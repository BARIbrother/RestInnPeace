from PIL import Image, ImageDraw
from pathlib import Path

OUT = Path(__file__).resolve().parents[1] / "Assets" / "Art" / "Rooms" / "Placeholders"
OUT.mkdir(parents=True, exist_ok=True)

BG = (18, 14, 12, 255)
WALL = (44, 36, 32, 255)
WALL_EDGE = (28, 22, 18, 255)
TRIM = (110, 84, 52, 255)
TRIM_DARK = (70, 52, 34, 255)
WOOD = (62, 44, 30, 255)
WOOD_LIGHT = (86, 62, 42, 255)
FLOOR = (36, 28, 22, 255)
FLOOR_LINE = (52, 40, 30, 255)
SHEET = (72, 48, 58, 255)
SHEET_LIGHT = (96, 64, 76, 255)
ACCENT = (140, 28, 28, 255)
GOLD = (168, 132, 72, 255)
LOCK = (180, 150, 70, 255)
CLUE = (200, 170, 90, 255)
SMOKE = (120, 100, 80, 120)
GUEST = (55, 70, 85, 255)
GUEST_OUT = (90, 120, 140, 255)
FRAME = (220, 180, 60, 230)
TRANS = (0, 0, 0, 0)


def save(name, img):
    path = OUT / f"{name}.png"
    img.save(path)
    print(path.name, img.size)


def new(w, h, color=TRANS):
    return Image.new("RGBA", (w, h), color)


def rect(d, box, fill, outline=None, width=2):
    d.rectangle(box, fill=fill, outline=outline, width=width)


W, H = 300, 165
back = new(W, H, WALL)
d = ImageDraw.Draw(back)
rect(d, (0, 0, W - 1, H - 1), None, WALL_EDGE, 4)
for x in range(12, W - 12, 18):
    d.line([(x, 10), (x, H - 40)], fill=(52, 42, 38, 255), width=2)
rect(d, (0, 0, W - 1, 14), TRIM_DARK, TRIM, 2)
rect(d, (0, H - 38, W - 1, H - 28), WOOD, TRIM_DARK, 2)
save("room_back", back)

# 조사 전 닫힌 방. 외벽 + 창문(투명) + 닫힌 문. 창문 너머로 실루엣이 비친다.
closed = new(W, H, (34, 28, 24, 255))
d = ImageDraw.Draw(closed)
rect(d, (0, 0, W - 1, H - 1), None, WALL_EDGE, 4)
for x in range(8, W - 8, 22):
    d.line([(x, 8), (x, H - 8)], fill=(28, 22, 18, 255), width=3)
rect(d, (0, 0, W - 1, 16), TRIM_DARK, TRIM, 2)
rect(d, (0, H - 18, W - 1, H - 1), WOOD, TRIM_DARK, 2)
wx0, wy0, wx1, wy1 = 36, 36, 168, 128
rect(d, (wx0, wy0, wx1, wy1), TRIM_DARK, GOLD, 3)
rect(d, (wx0 + 8, wy0 + 8, wx1 - 8, wy1 - 8), TRANS, None, 0)
mx = (wx0 + wx1) // 2
my = (wy0 + wy1) // 2
rect(d, (mx - 3, wy0 + 6, mx + 3, wy1 - 6), TRIM, None, 0)
rect(d, (wx0 + 6, my - 3, wx1 - 6, my + 3), TRIM, None, 0)
door_x0, door_y0, door_x1, door_y1 = 198, 28, 278, H - 18
rect(d, (door_x0, door_y0, door_x1, door_y1), WOOD, TRIM, 3)
rect(d, (door_x0 + 8, door_y0 + 10, door_x1 - 8, door_y0 + 58), WOOD_LIGHT, TRIM_DARK, 2)
rect(d, (door_x0 + 8, door_y0 + 70, door_x1 - 8, door_y1 - 10), WOOD_LIGHT, TRIM_DARK, 2)
d.ellipse((door_x1 - 22, (door_y0 + door_y1) // 2 - 5, door_x1 - 12, (door_y0 + door_y1) // 2 + 5), fill=GOLD)
save("room_closed", closed)

floor = new(W, 36, FLOOR)
d = ImageDraw.Draw(floor)
for x in range(0, W, 40):
    rect(d, (x + 2, 2, x + 38, 33), WOOD, FLOOR_LINE, 1)
    d.line([(x + 20, 4), (x + 20, 31)], fill=FLOOR_LINE, width=1)
save("room_floor", floor)

side = new(28, H, TRANS)
d = ImageDraw.Draw(side)
for i, a in enumerate([90, 60, 35]):
    d.rectangle((i * 6, 0, i * 6 + 8, H), fill=(0, 0, 0, a))
save("room_side_shadow", side)

bed = new(120, 56, TRANS)
d = ImageDraw.Draw(bed)
rect(d, (4, 20, 115, 52), WOOD, TRIM_DARK, 2)
rect(d, (10, 10, 108, 36), SHEET, SHEET_LIGHT, 2)
rect(d, (10, 8, 38, 28), SHEET_LIGHT, SHEET, 2)
d.line([(12, 40), (106, 40)], fill=TRIM, width=2)
save("bed", bed)

stand = new(36, 40, TRANS)
d = ImageDraw.Draw(stand)
rect(d, (2, 10, 33, 38), WOOD, TRIM_DARK, 2)
rect(d, (6, 4, 29, 14), WOOD_LIGHT, TRIM, 1)
d.ellipse((14, 0, 22, 8), fill=GOLD)
save("nightstand", stand)

door_c = new(52, 110, TRANS)
d = ImageDraw.Draw(door_c)
rect(d, (2, 2, 49, 107), WOOD, TRIM, 3)
rect(d, (8, 10, 43, 50), WOOD_LIGHT, TRIM_DARK, 2)
rect(d, (8, 58, 43, 98), WOOD_LIGHT, TRIM_DARK, 2)
d.ellipse((36, 52, 44, 60), fill=GOLD)
save("door_closed", door_c)

door_o = new(52, 110, TRANS)
d = ImageDraw.Draw(door_o)
rect(d, (2, 2, 49, 107), None, TRIM, 3)
rect(d, (8, 8, 44, 102), (10, 8, 8, 255), WALL_EDGE, 1)
rect(d, (40, 6, 50, 104), WOOD, TRIM_DARK, 2)
save("door_open", door_o)

lock = new(32, 40, TRANS)
d = ImageDraw.Draw(lock)
d.arc((6, 2, 26, 24), 0, 180, fill=LOCK, width=4)
rect(d, (4, 16, 27, 36), LOCK, GOLD, 2)
rect(d, (13, 22, 18, 30), (40, 30, 10, 255))
save("lock", lock)

fur = new(48, 24, TRANS)
d = ImageDraw.Draw(fur)
for pts in [
    ((4, 16), (12, 6), (20, 18), (28, 5), (36, 17), (44, 8)),
    ((6, 20), (18, 10), (30, 20), (42, 12)),
]:
    d.line(list(pts), fill=CLUE, width=2)
save("clue_fur", fur)

smell = new(40, 48, TRANS)
d = ImageDraw.Draw(smell)
for i, a in enumerate([160, 110, 70]):
    x = 12 + i * 4
    d.arc((x, 4 + i * 6, x + 14, 28 + i * 6), 200, 340, fill=(*SMOKE[:3], a), width=3)
save("clue_smell", smell)

foot = new(40, 28, TRANS)
d = ImageDraw.Draw(foot)
d.ellipse((2, 8, 16, 24), outline=CLUE, width=2)
d.ellipse((20, 4, 34, 20), outline=CLUE, width=2)
d.ellipse((8, 4, 12, 8), fill=CLUE)
d.ellipse((26, 2, 30, 6), fill=CLUE)
save("clue_footprint", foot)

scratch = new(64, 40, TRANS)
d = ImageDraw.Draw(scratch)
for dx in range(0, 18, 6):
    d.line([(8 + dx, 6), (4 + dx, 34)], fill=ACCENT, width=2)
    d.line([(28 + dx, 8), (36 + dx, 34)], fill=ACCENT, width=2)
save("clue_scratch", scratch)

frame = new(W, H, TRANS)
d = ImageDraw.Draw(frame)
rect(d, (1, 1, W - 2, H - 2), None, FRAME, 4)
rect(d, (7, 7, W - 8, H - 8), None, (FRAME[0], FRAME[1], FRAME[2], 100), 2)
for box in [
    (0, 0, 18, 18),
    (W - 19, 0, W - 1, 18),
    (0, H - 19, 18, H - 1),
    (W - 19, H - 19, W - 1, H - 1),
]:
    rect(d, box, None, GOLD, 3)
save("selection_frame", frame)

guest = new(48, 88, TRANS)
d = ImageDraw.Draw(guest)
d.ellipse((12, 2, 36, 28), fill=GUEST, outline=GUEST_OUT, width=2)
rect(d, (10, 28, 38, 70), GUEST, GUEST_OUT, 2)
rect(d, (8, 70, 22, 86), GUEST, GUEST_OUT, 2)
rect(d, (26, 70, 40, 86), GUEST, GUEST_OUT, 2)
save("guest_silhouette", guest)

owner = new(80, 48, TRANS)
d = ImageDraw.Draw(owner)
rect(d, (2, 10, 77, 46), WOOD, TRIM, 2)
rect(d, (10, 2, 70, 18), TRIM_DARK, GOLD, 2)
d.rectangle((20, 22, 60, 28), fill=GOLD)
d.rectangle((24, 32, 56, 38), fill=ACCENT)
save("owner_desk", owner)

corridor = new(W, 48, (30, 24, 20, 255))
d = ImageDraw.Draw(corridor)
for x in range(0, W, 24):
    d.line([(x, 0), (x, 47)], fill=(22, 18, 14, 255), width=1)
rect(d, (0, 0, W - 1, 47), None, TRIM_DARK, 2)
save("corridor", corridor)

print("done", len(list(OUT.glob("*.png"))))

# OnGUI 버튼용 9-slice 텍스처. Resources/UI 에 두고 런타임에 Resources.Load 로 읽는다.
UI_OUT = Path(__file__).resolve().parents[1] / "Assets" / "Resources" / "UI"
UI_OUT.mkdir(parents=True, exist_ok=True)


def button(name, fill, edge, inner, accent=None):
    size = 48
    img = new(size, size, TRANS)
    d = ImageDraw.Draw(img)
    rect(d, (0, 0, size - 1, size - 1), fill, edge, 3)
    rect(d, (4, 4, size - 5, size - 5), None, inner, 1)
    # 모서리 못
    for cx, cy in [(7, 7), (size - 8, 7), (7, size - 8), (size - 8, size - 8)]:
        d.ellipse((cx - 2, cy - 2, cx + 2, cy + 2), fill=accent or GOLD)
    path = UI_OUT / f"{name}.png"
    img.save(path)
    print(path.name, img.size)


button("button_normal", (40, 30, 26, 255), (110, 84, 52, 255), (70, 52, 34, 255))
button("button_hover", (60, 44, 36, 255), (168, 132, 72, 255), (110, 84, 52, 255))
button("button_active", (24, 18, 16, 255), (140, 28, 28, 255), (90, 20, 20, 255), (200, 60, 60, 255))

# 패널·팝업 배경 (버튼과 같은 톤)
panel = new(64, 64, (22, 17, 15, 235))
d = ImageDraw.Draw(panel)
rect(d, (0, 0, 63, 63), None, (70, 52, 34, 255), 3)
rect(d, (5, 5, 58, 58), None, (44, 36, 32, 255), 1)
panel.save(UI_OUT / "panel.png")
print("panel.png", panel.size)

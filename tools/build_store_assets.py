from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageOps


ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "store-assets" / "v1.7.2"
SOURCE = OUT / "source"
SIZE = (1366, 768)

FONT_JA_REGULAR = Path(r"C:\Windows\Fonts\YuGothR.ttc")
FONT_JA_BOLD = Path(r"C:\Windows\Fonts\YuGothB.ttc")
FONT_EN_REGULAR = Path(r"C:\Windows\Fonts\segoeui.ttf")
FONT_EN_BOLD = Path(r"C:\Windows\Fonts\segoeuib.ttf")


SLIDES = {
    "ja": [
        {
            "file": "01-capture.png",
            "title": "必要な場所だけ、\n一瞬で切り取る。",
            "subtitle": "ドラッグで選び、\n画面の最前面に固定。",
            "chip": "Ctrl + Shift + 5",
            "source": ROOT / "screenshot" / "screenshot_007.png",
        },
        {
            "file": "02-pin-and-control.png",
            "title": "資料を前面に固定",
            "subtitle": "拡大・透過・コピーを\n右クリックから操作。",
            "chip": "いつでも見える",
            "source": ROOT / "screenshot" / "screenshot_005.png",
        },
        {
            "file": "03-shortcuts.png",
            "title": "すぐ使える\nショートカット",
            "subtitle": "キャプチャ、OCR、コピーを\nキーボードから。",
            "chip": "Kiritori v1.7.2",
            "source": SOURCE / "info-ja.png",
        },
        {
            "file": "04-live-preview.png",
            "title": "選んだ範囲を\nリアルタイム表示",
            "subtitle": "画面の変化を、そのまま\n別ウィンドウで追跡。",
            "chip": "Ctrl + Shift + 6",
            "source": SOURCE / "live-preview.png",
        },
    ],
    "en": [
        {
            "file": "01-capture.png",
            "title": "Capture what\nmatters. Instantly.",
            "subtitle": "Select any region and keep it\nalways on top.",
            "chip": "Ctrl + Shift + 5",
            "source": ROOT / "screenshot" / "screenshot_005.png",
        },
        {
            "file": "02-pin-and-control.png",
            "title": "Keep references\nin sight",
            "subtitle": "Zoom, adjust opacity, copy,\nor save from the menu.",
            "chip": "Always on top",
            "source": ROOT / "screenshot" / "screenshot_001.png",
        },
        {
            "file": "03-shortcuts.png",
            "title": "Ready when\nyou are",
            "subtitle": "Capture, OCR, and copy with\nfamiliar shortcuts.",
            "chip": "Kiritori v1.7.2",
            "source": SOURCE / "info-en.png",
        },
        {
            "file": "04-live-preview.png",
            "title": "Watch any region\nin real time",
            "subtitle": "Follow on-screen changes in a\nseparate always-on-top window.",
            "chip": "Ctrl + Shift + 6",
            "source": SOURCE / "live-preview.png",
        },
    ],
}


def font(path: Path, size: int) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(str(path), size=size)


def rounded_mask(size: tuple[int, int], radius: int) -> Image.Image:
    mask = Image.new("L", size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, size[0] - 1, size[1] - 1), radius, fill=255)
    return mask


def draw_card(canvas: Image.Image, source_path: Path) -> None:
    x, y, width, height = 500, 68, 796, 624
    shadow = Image.new("RGBA", SIZE, (0, 0, 0, 0))
    shadow_draw = ImageDraw.Draw(shadow)
    shadow_draw.rounded_rectangle((x + 5, y + 14, x + width + 5, y + height + 14), 24, fill=(15, 35, 55, 58))
    shadow = shadow.filter(ImageFilter.GaussianBlur(18))
    canvas.alpha_composite(shadow)

    card = Image.new("RGBA", (width, height), (255, 255, 255, 255))
    draw = ImageDraw.Draw(card)
    draw.rounded_rectangle((0, 0, width - 1, height - 1), 24, outline=(199, 216, 230, 255), width=2)

    with Image.open(source_path) as raw:
        screenshot = raw.convert("RGB")
    fitted = ImageOps.contain(screenshot, (width - 38, height - 38), Image.Resampling.LANCZOS)
    sx = (width - fitted.width) // 2
    sy = (height - fitted.height) // 2
    card.paste(fitted, (sx, sy))

    mask = rounded_mask((width, height), 24)
    canvas.paste(card, (x, y), mask)


def draw_slide(locale: str, spec: dict) -> Image.Image:
    with Image.open(SOURCE / "background.png") as raw_background:
        background = ImageOps.fit(raw_background.convert("RGB"), SIZE, Image.Resampling.LANCZOS)
    canvas = background.convert("RGBA")
    draw = ImageDraw.Draw(canvas)

    is_ja = locale == "ja"
    regular_path = FONT_JA_REGULAR if is_ja else FONT_EN_REGULAR
    bold_path = FONT_JA_BOLD if is_ja else FONT_EN_BOLD

    brand_font = font(bold_path, 20)
    title_font = font(bold_path, 48 if is_ja else 50)
    subtitle_font = font(regular_path, 24 if is_ja else 25)
    chip_font = font(bold_path, 20)
    note_font = font(regular_path, 15)

    navy = (14, 37, 61, 255)
    muted = (57, 79, 99, 255)
    blue = (2, 132, 199, 255)

    draw.rounded_rectangle((68, 60, 322, 101), 20, fill=(255, 255, 255, 225), outline=(186, 214, 234, 255), width=1)
    draw.ellipse((84, 72, 101, 89), fill=blue)
    draw.text((114, 69), "KIRITORI · WINDOWS", font=brand_font, fill=navy)

    draw.rectangle((69, 132, 131, 140), fill=blue)
    draw.multiline_text((68, 166), spec["title"], font=title_font, fill=navy, spacing=9)

    title_box = draw.multiline_textbbox((68, 166), spec["title"], font=title_font, spacing=9)
    subtitle_y = min(max(title_box[3] + 34, 318), 366)
    draw.multiline_text((68, subtitle_y), spec["subtitle"], font=subtitle_font, fill=muted, spacing=7)

    chip_y = min(subtitle_y + 104, 468)
    chip_box = draw.textbbox((0, 0), spec["chip"], font=chip_font)
    chip_width = chip_box[2] - chip_box[0] + 44
    draw.rounded_rectangle((68, chip_y, 68 + chip_width, chip_y + 48), 24, fill=(226, 244, 255, 240))
    draw.text((90, chip_y + 9), spec["chip"], font=chip_font, fill=(3, 105, 161, 255))

    note = "Kiritori v1.7.2 の実画面" if is_ja else "Actual Kiritori v1.7.2 interface"
    draw.text((68, 696), note, font=note_font, fill=(79, 101, 120, 255))

    draw_card(canvas, spec["source"])
    return canvas.convert("RGB")


def build_contact_sheet(locale: str, paths: list[Path]) -> None:
    sheet = Image.new("RGB", (1160, 700), (238, 245, 250))
    draw = ImageDraw.Draw(sheet)
    heading_path = FONT_JA_BOLD if locale == "ja" else FONT_EN_BOLD
    draw.text((40, 24), f"Kiritori v1.7.2 · {locale.upper()}", font=font(heading_path, 30), fill=(14, 37, 61))

    positions = [(40, 82), (600, 82), (40, 390), (600, 390)]
    for path, position in zip(paths, positions):
        with Image.open(path) as image:
            thumb = image.convert("RGB").resize((520, 292), Image.Resampling.LANCZOS)
        sheet.paste(thumb, position)
    sheet.save(OUT / f"contact-sheet-{locale}.png", optimize=True)


def main() -> None:
    generated: dict[str, list[Path]] = {"ja": [], "en": []}
    for locale, slides in SLIDES.items():
        locale_dir = OUT / locale
        locale_dir.mkdir(parents=True, exist_ok=True)
        stale_gpu_slide = locale_dir / "04-live-preview-gpu.png"
        if stale_gpu_slide.exists():
            stale_gpu_slide.unlink()
        for spec in slides:
            destination = locale_dir / spec["file"]
            draw_slide(locale, spec).save(destination, optimize=True)
            generated[locale].append(destination)
        build_contact_sheet(locale, generated[locale])

    for locale, paths in generated.items():
        for path in paths:
            with Image.open(path) as image:
                assert image.size == SIZE
                assert image.mode == "RGB"
            print(f"{locale}: {path.relative_to(ROOT)} ({path.stat().st_size} bytes)")


if __name__ == "__main__":
    main()

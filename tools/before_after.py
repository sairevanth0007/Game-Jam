"""Builds before/after comparison images for the visual overhaul.

Usage: python tools/before_after.py <after_png_dir>
Reads docs/images/before_after/before/<key>.jpg (key = L1..L10, Title), finds the matching
after PNG (L{n}_*.png or Title.png) in <after_png_dir>, writes after/<key>.jpg and a stacked
compare_<key>.jpg (before on top, after below) into docs/images/before_after/.
"""
import glob
import os
import sys

from PIL import Image, ImageDraw

ROOT = os.path.join(os.path.dirname(__file__), '..', 'docs', 'images', 'before_after')


def label(im, text):
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, 150, 44], fill=(0, 0, 0))
    d.text((14, 12), text, fill=(255, 255, 255))
    return im


def main(after_dir):
    os.makedirs(os.path.join(ROOT, 'after'), exist_ok=True)
    for before in sorted(glob.glob(os.path.join(ROOT, 'before', '*.jpg'))):
        key = os.path.splitext(os.path.basename(before))[0]
        pattern = 'Title.png' if key == 'Title' else key + '_*.png'
        hits = sorted(glob.glob(os.path.join(after_dir, pattern)))
        if not hits:
            print('no after shot for', key)
            continue
        a = Image.open(hits[0]).convert('RGB')
        b = Image.open(before).convert('RGB').resize(a.size)
        a.save(os.path.join(ROOT, 'after', key + '.jpg'), quality=88)
        w, h = 960, int(960 * a.size[1] / a.size[0])
        out = Image.new('RGB', (w, h * 2 + 8), (20, 20, 20))
        out.paste(label(b.resize((w, h)), 'BEFORE (RC4)'), (0, 0))
        out.paste(label(a.resize((w, h)), 'AFTER'), (0, h + 8))
        out.save(os.path.join(ROOT, 'compare_' + key + '.jpg'), quality=85)
        print('wrote', key)


if __name__ == '__main__':
    main(sys.argv[1])

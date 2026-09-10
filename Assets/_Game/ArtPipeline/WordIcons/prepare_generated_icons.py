"""Prepare locally generated icons; never calls an image API.

Usage: python prepare_generated_icons.py SOURCE_PNG sprite_key
Requires Pillow. Saves a new 512px PNG; refuses to overwrite an existing file.
"""
import argparse
import re
from pathlib import Path
from PIL import Image

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('source', type=Path)
parser.add_argument('key')
args = parser.parse_args()
if not re.fullmatch(r'[a-z0-9_]+', args.key):
    parser.error('Invalid sprite key')
project = Path(__file__).resolve().parents[4]
destination = project / 'Assets/_Game/Resources/Icons/Generated'
destination.mkdir(parents=True, exist_ok=True)
target = destination / (args.key + '.png')
if target.exists():
    parser.error(f'Already exists: {target}')
with Image.open(args.source) as source:
    if source.mode != 'RGBA' or source.getchannel('A').getextrema()[0] != 0:
        parser.error('Source lacks genuine transparency; do not import a white/checkerboard image.')
    source = source.convert('RGBA')
    source.thumbnail((512, 512), Image.Resampling.LANCZOS)
    output = Image.new('RGBA', (512, 512))
    output.alpha_composite(source, ((512-source.width)//2, (512-source.height)//2))
    output.save(target)
print(target)

"""Make a 64px review sheet and validate the requested generated files."""
from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[4]
keys = ['colors__red','colors__blue','colors__green','colors__yellow',
        'shapes__triangle','shapes__rectangle','fruits__banana','flowers__rose',
        'foods__pizza','planets__earth','reptiles__lizard','space__moon','americas__south_america']
sheet = Image.new('RGB', (800, 480), '#56c6d5')
draw = ImageDraw.Draw(sheet)
for i, key in enumerate(keys):
    path = root / 'Assets/_Game/Resources/Icons/Generated' / (key+'.png')
    if not path.exists():
        print('MISSING', key)
        continue
    with Image.open(path) as im:
        assert im.mode == 'RGBA' and im.size == (512,512), key
        assert all(im.getpixel(p)[3] == 0 for p in [(0,0),(511,0),(0,511),(511,511)]), key
        x, y = (i%4)*200, (i//4)*120
        sheet.paste(im.resize((64,64), Image.Resampling.LANCZOS), (x+68,y+8),
                    im.resize((64,64), Image.Resampling.LANCZOS))
        draw.text((x+6,y+82), key, fill='black')
        print('OK',key)
output = root / 'output/icon-review.png'
output.parent.mkdir(exist_ok=True)
sheet.save(output)
print(output)

import os
from PIL import Image

# Setup directory paths
p = r'E:\S&box Addons\xgui-3_test\Libraries\XGUI-3\Assets\XGUI\Resources\aqua'
out = os.path.join(p, 'push_button_default_pulse.webp')

# 1. Load source images and convert to RGBA
src_filenames = [
    'push_button_default.png', 
    'push_button_default_1.png', 
    'push_button_default_2.png'
]
src = [Image.open(os.path.join(p, n)).convert('RGBA') for n in src_filenames]

# 2. Configure animation properties
path = [0, 1, 2, 1, 0]
intervals = 19
frames = []

# 3. Generate blended frames for smooth animation
for i in range(len(path) - 1):
    for j in range(intervals):
        if j == 0:
            frames.append(src[path[i]])
        else:
            blended = Image.blend(src[path[i]], src[path[i+1]], j / intervals)
            frames.append(blended)

# Append the very last frame in the sequence
frames.append(src[path[-1]])

# 4. Save as animated WEBP
frames[0].save(
    out,
    format='WEBP',
    save_all=True,
    append_images=frames[1:],
    duration=17,
    loop=0,
    lossless=True,
    method=6
)

# 5. Output generation statistics
print('frames:', len(frames), 'fps:', round(1000 / 17, 2), 'cycle_ms:', len(frames) * 17, 'size:', os.path.getsize(out))

# 6. Verification block (The second command in the screenshot)
im = Image.open(out)
print('format=', im.format, 'size=', im.size, 'frames=', im.n_frames, 'duration=', im.info.get('duration'), 'loop=', im.info.get('loop'))

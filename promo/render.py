# -*- coding: utf-8 -*-
"""渲染各分镜片段并拼接成宣传视频。"""
import os, subprocess, sys

BASE = r"F:\easyer Launcher 1\promo"
FF = r"C:\Users\Administrator\.workbuddy\binaries\node\workspace\ffmpegtmp\node_modules\@ffmpeg-installer\win32-x64\ffmpeg.exe"
FR = os.path.join(BASE, "frames")
SEG = os.path.join(BASE, "segs")
OUTP = os.path.join(BASE, "EasyerLauncher-宣传片.mp4")
os.makedirs(SEG, exist_ok=True)

# (帧文件, 时长s, zoom方向: 1放大/0静止, 转场样式)
SCENES = [
    ("f1_title.png",   4.2, 1),
    ("f2_home.png",    4.6, 1),
    ("f3_download.png",4.6, 1),
    ("f4_loaders.png", 4.6, 1),
    ("f5_packs.png",   4.8, 1),
    ("f6_mods.png",    4.6, 1),
    ("f7_account.png", 4.6, 1),
    ("f8_end.png",     5.4, 0),
]

def run(args):
    r = subprocess.run(args, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if r.returncode != 0:
        print(r.stderr[-1500:])
        sys.exit(1)

segs = []
for i, (name, dur, zoom) in enumerate(SCENES):
    out = os.path.join(SEG, f"s{i}.mp4")
    segs.append(out)
    src = os.path.join(FR, name)
    fade_out_st = dur - 0.45
    vf = "scale=3840:-2"
    if zoom:
        vf += ",zoompan=z='min(1+0.0011*in,1.085)':d=1:x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':s=1920x1080:fps=30"
    else:
        vf += ",scale=1920:1080"
    vf += f",fade=t=in:st=0:d=0.45,fade=t=out:st={fade_out_st}:d=0.45,format=yuv420p"
    run([FF, "-y", "-loop", "1", "-t", str(dur), "-i", src,
         "-vf", vf, "-c:v", "libx264", "-preset", "medium", "-crf", "19",
         "-r", "30", "-movflags", "+faststart", out])
    print(f"seg {i}: {name} -> {dur}s")

lst = os.path.join(SEG, "list.txt")
with open(lst, "w", encoding="utf-8") as f:
    for s in segs:
        f.write("file '" + s.replace("\\", "/") + "'\n")

run([FF, "-y", "-f", "concat", "-safe", "0", "-i", lst,
     "-c", "copy", "-movflags", "+faststart", OUTP])
size = os.path.getsize(OUTP) / 1048576
print(f"DONE: {OUTP}  {size:.1f} MB")

# -*- coding: utf-8 -*-
"""生成宣传视频的各分镜帧（1920x1080）。"""
import os
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageOps

BASE = r"F:\easyer Launcher 1\promo"
SHOTS = os.path.join(BASE, "shots")
OUT = os.path.join(BASE, "frames")
os.makedirs(OUT, exist_ok=True)

W, H = 1920, 1080
BG_TOP = (22, 24, 33)
BG_BOT = (30, 34, 48)
ACCENT = (96, 205, 255)      # Win11 亮蓝
ACCENT_DIM = (0, 120, 212)
FG = (238, 241, 246)
FG_DIM = (150, 158, 172)

F_BOLD = r"C:\Windows\Fonts\msyhbd.ttc"
F_REG = r"C:\Windows\Fonts\msyh.ttc"

def font(path, size):
    return ImageFont.truetype(path, size)

def background():
    """深色渐变 + 角落蓝色辉光。"""
    img = Image.new("RGB", (W, H))
    px = img.load()
    for y in range(H):
        t = y / (H - 1)
        r = int(BG_TOP[0] + (BG_BOT[0] - BG_TOP[0]) * t)
        g = int(BG_TOP[1] + (BG_BOT[1] - BG_TOP[1]) * t)
        b = int(BG_TOP[2] + (BG_BOT[2] - BG_TOP[2]) * t)
        for x in range(W):
            px[x, y] = (r, g, b)
    # 辉光
    glow = Image.new("RGB", (W, H), (0, 0, 0))
    gd = ImageDraw.Draw(glow)
    gd.ellipse((-300, 620, 520, 1240), fill=(20, 60, 100))
    gd.ellipse((1500, -260, 2300, 340), fill=(16, 50, 88))
    glow = glow.filter(ImageFilter.GaussianBlur(160))
    img = Image.blend(img, Image.composite(glow, img, Image.new("L", (W, H), 110)), 0.55)
    # 细网格点
    d = ImageDraw.Draw(img)
    for gy in range(90, H, 90):
        for gx in range(90, W, 90):
            d.point((gx, gy), fill=(46, 52, 68))
    return img

def rounded_shot(path, target_w):
    """截图 → 圆角 + 阴影。返回 RGBA。"""
    im = Image.open(path).convert("RGB")
    ratio = target_w / im.width
    im = im.resize((target_w, int(im.height * ratio)), Image.LANCZOS)
    rad = 14
    mask = Image.new("L", im.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, im.width - 1, im.height - 1), rad, fill=255)
    shot = Image.new("RGBA", im.size)
    shot.paste(im, (0, 0), mask)
    # 阴影
    pad = 46
    shadow = Image.new("RGBA", (im.width + pad * 2, im.height + pad * 2), (0, 0, 0, 0))
    sm = Image.new("L", shadow.size, 0)
    ImageDraw.Draw(sm).rounded_rectangle((pad, pad + 10, pad + im.width, pad + im.height + 10), rad, fill=150)
    sm = sm.filter(ImageFilter.GaussianBlur(22))
    black = Image.new("RGBA", shadow.size, (0, 0, 0, 255))
    shadow.paste(black, (0, 0), sm)
    shadow.paste(shot, (pad, pad), shot)
    return shadow

def paste_shot(canvas, path, cx, cy, target_w=1180):
    shot = rounded_shot(path, target_w)
    canvas.paste(shot, (int(cx - shot.width / 2), int(cy - shot.height / 2)), shot)
    return shot

def text_ls(d, xy, s, f, fill, anchor=None):
    d.text(xy, s, font=f, fill=fill, anchor=anchor)

def accent_bar(d, x, y, h=4, w=64):
    d.rounded_rectangle((x, y, x + w, y + h), 2, fill=ACCENT)

def scene_shot(shot_file, num, total, title, lines, badge=None, out="scene.png", target_w=1180):
    """截图分镜：左文右图。"""
    img = background()
    d = ImageDraw.Draw(img)
    # 左侧文案
    lx = 96
    cy = 300
    if badge:
        bf = font(F_BOLD, 26)
        bw = d.textlength(badge, font=bf)
        d.rounded_rectangle((lx, cy - 8, lx + bw + 36, cy + 44), 22, outline=ACCENT, width=2)
        text_ls(d, (lx + 18, cy + 2), badge, bf, ACCENT)
        cy += 88
    accent_bar(d, lx, cy + 8)
    cy += 40
    tf = font(F_BOLD, 60)
    for i, seg in enumerate(title):
        text_ls(d, (lx, cy), seg, tf, FG)
        cy += 84
    cy += 14
    lf = font(F_REG, 30)
    for line in lines:
        text_ls(d, (lx, cy), line, lf, FG_DIM)
        cy += 52
    # 截图
    paste_shot(img, os.path.join(SHOTS, shot_file), 640 + 1180 / 2 + 96, H / 2, target_w)
    # 页码
    text_ls(d, (W - 96, H - 66), f"{num:02d} / {total:02d}", font(F_REG, 22), (110, 118, 134), anchor="rm")
    img.save(os.path.join(OUT, out))
    print("scene:", out)

def scene_title(out, big, sub, foot=None, chip=None):
    """片头/片尾。"""
    img = background()
    d = ImageDraw.Draw(img)
    cx = W / 2
    y = 360
    if chip:
        cf = font(F_BOLD, 30)
        cw = d.textlength(chip, font=cf)
        d.rounded_rectangle((cx - cw / 2 - 26, y, cx + cw / 2 + 26, y + 58), 29, fill=(30, 60, 92))
        text_ls(d, (cx, y + 12), chip, cf, ACCENT, anchor="ma")
        y += 110
    bf = font(F_BOLD, 108)
    text_ls(d, (cx, y), big, bf, FG, anchor="ma")
    y += 160
    sf = font(F_REG, 40)
    text_ls(d, (cx, y), sub, sf, FG_DIM, anchor="ma")
    if foot:
        ff = font(F_REG, 26)
        text_ls(d, (cx, H - 120), foot, ff, (120, 128, 144), anchor="ma")
    # 顶部小图标条（装饰）
    accent_bar(d, int(cx - 40), 260, h=5, w=80)
    img.save(os.path.join(OUT, out))
    print("scene:", out)

T = 8
scene_title("f1_title.png", "Easyer Launcher 1", "简单点，启动个游戏而已", foot="C# + VB.NET  ·  Windows 10/11  ·  免安装", chip="开源 Minecraft 启动器")
scene_shot("home.png", 1, T, ["一键启动", "版本一目了然"], ["已装版本卡片化展示，双击即玩", "整合包实例带专属徽标"], badge="主页", out="f2_home.png")
scene_shot("download.png", 2, T, ["全版本目录", "920 个版本"], ["正式版 / 快照 / Beta / Alpha", "BMCLAPI 国内镜像加速"], badge="版本下载", out="f3_download.png")
scene_shot("loaders.png", 3, T, ["四大加载器", "一键装好"], ["Fabric · Quilt · Forge · NeoForge", "镜像优先，官方兜底"], badge="加载器", out="f4_loaders.png")
scene_shot("packs.png", 4, T, ["整合包", "点一下就能玩"], ["Modrinth 热门包图形化搜索", "自动装原版 + 加载器 + 模组", "独立实例，互不干扰"], badge="整合包", out="f5_packs.png")
scene_shot("mods.png", 5, T, ["模组 / 资源", "一站式搜索"], ["MOD · 资源包 · 光影 · 数据包", "国内镜像直连，秒开秒下"], badge="内容市场", out="f6_mods.png")
scene_shot("account.png", 6, T, ["三种登录", "想怎么玩都行"], ["离线登录 · LittleSkin 外置", "Microsoft 正版（已过 Mojang 审核）"], badge="账号", out="f7_account.png")
scene_title("f8_end.png", "现在就试试", "github.com/Minecraftboyhei/easyer-Launcher-1", foot="单文件 68MB  ·  双击即用  ·  完全免费开源", chip="完全免费 · 开源")
print("ALL FRAMES DONE")

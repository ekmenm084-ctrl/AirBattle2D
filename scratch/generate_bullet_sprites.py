import os
import math
from PIL import Image, ImageDraw

output_dir = "My project/Assets/Resources"
os.makedirs(output_dir, exist_ok=True)

def create_bullet(name, width, height, draw_fn):
    img = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    draw_fn(draw, width, height)
    path = os.path.join(output_dir, f"{name}.png")
    img.save(path, "PNG")
    print(f"Generated {path} ({width}x{height})")
    
    # Generate meta file if not exists
    meta_path = path + ".meta"
    # Use template meta
    with open("My project/Assets/Resources/bullet_tracer.png.meta", "r") as f:
        meta_content = f.read()
    # Replace guid with a new deterministic or unique one
    import uuid
    new_guid = uuid.uuid5(uuid.NAMESPACE_DNS, name).hex
    import re
    meta_content = re.sub(r'guid: [a-f0-9]{32}', f'guid: {new_guid}', meta_content)
    with open(meta_path, "w") as f:
        f.write(meta_content)
    print(f"Generated meta: {meta_path} (guid: {new_guid})")

# 1. F-16: M61A1 Vulcan (Golden-Amber 20mm Tracer)
def draw_f16(draw, w, h):
    cx = w / 2.0
    # Outer glow
    for y in range(h):
        prog = y / float(h) # 0 = top tip, 1 = tail
        t_w = (1.0 - math.pow(prog - 0.45, 2) * 1.5) if prog > 0.15 else (prog / 0.15)
        t_w = max(0.05, t_w) * (w * 0.38)
        
        # Color gradient: Tip is intense white-yellow, body is golden-amber, tail is fiery orange
        for x in range(int(cx - t_w * 1.6), int(cx + t_w * 1.6) + 1):
            dx = abs(x - cx) / (t_w * 1.6 + 0.001)
            if dx <= 1.0:
                core = 1.0 - dx
                alpha = int(255 * math.pow(core, 1.2) * (1.0 - prog * 0.35))
                
                if prog < 0.2:
                    r, g, b = 255, int(250 - prog*100), int(220 - prog*500)
                elif prog < 0.7:
                    r, g, b = 255, int(190 - (prog-0.2)*120), int(30 + (prog-0.2)*20)
                else:
                    r, g, b = 255, int(130 - (prog-0.7)*150), 10
                
                # White-hot inner core
                if dx < 0.25 and prog < 0.5:
                    r, g, b = 255, 255, 240
                
                r = min(255, max(0, r))
                g = min(255, max(0, g))
                b = min(255, max(0, b))
                alpha = min(255, max(0, alpha))
                draw.point((x, y), fill=(r, g, b, alpha))

# 2. A-10: GAU-8 Avenger (30mm Heavy Depleted Uranium / Incandescent Red-Copper)
def draw_a10(draw, w, h):
    cx = w / 2.0
    for y in range(h):
        prog = y / float(h)
        # Wider, chunky projectile with heavy blunt penetrator cap
        t_w = (1.0 - math.pow(prog - 0.5, 2) * 1.2) if prog > 0.10 else (prog / 0.10)
        t_w = max(0.1, t_w) * (w * 0.42)
        
        for x in range(int(cx - t_w * 1.7), int(cx + t_w * 1.7) + 1):
            dx = abs(x - cx) / (t_w * 1.7 + 0.001)
            if dx <= 1.0:
                core = 1.0 - dx
                alpha = int(255 * math.pow(core, 1.3) * (1.0 - prog * 0.25))
                
                # Deep crimson, hot volcanic orange & charcoal edge
                if prog < 0.25:
                    r, g, b = 255, int(180 - prog*300), int(100 - prog*350)
                elif prog < 0.75:
                    r, g, b = 255, int(60 + math.sin(prog*10)*20), 15
                else:
                    # Heavy molten smoke & trailing embers
                    r, g, b = int(220 - (prog-0.75)*250), 25, 10
                
                # Super hot incendiary tungsten center
                if dx < 0.35 and prog < 0.6:
                    r, g, b = 255, 230, 180
                
                r = min(255, max(0, r))
                g = min(255, max(0, g))
                b = min(255, max(0, b))
                alpha = min(255, max(0, alpha))
                draw.point((x, y), fill=(r, g, b, alpha))

# 3. F-22: M61A2 Supersonic Kinetic Dart (Electric Cyan & Ice-White Mach Shockwave)
def draw_f22(draw, w, h):
    cx = w / 2.0
    for y in range(h):
        prog = y / float(h)
        # Razor thin supersonic needle
        t_w = (1.0 - math.pow(prog - 0.4, 2) * 1.8) if prog > 0.08 else (prog / 0.08)
        t_w = max(0.04, t_w) * (w * 0.32)
        
        # Mach diamond oscillations
        mach_ring = 1.0 + 0.28 * math.sin(prog * 35.0) * math.exp(-prog * 2.0)
        t_w *= mach_ring
        
        for x in range(int(cx - t_w * 1.6), int(cx + t_w * 1.6) + 1):
            dx = abs(x - cx) / (t_w * 1.6 + 0.001)
            if dx <= 1.0:
                core = 1.0 - dx
                alpha = int(255 * math.pow(core, 1.4) * (1.0 - prog * 0.3))
                
                # Ice-white to electric cyan and deep cobalt
                if dx < 0.28 and prog < 0.65:
                    r, g, b = 240, 255, 255
                elif prog < 0.3:
                    r, g, b = int(100 + prog*200), 245, 255
                elif prog < 0.7:
                    r, g, b = 0, int(220 - (prog-0.3)*120), 255
                else:
                    r, g, b = int(30), int(100 - (prog-0.7)*180), 255
                
                r = min(255, max(0, r))
                g = min(255, max(0, g))
                b = min(255, max(0, b))
                alpha = min(255, max(0, alpha))
                draw.point((x, y), fill=(r, g, b, alpha))

# 4. Su-57: GSh-30-1 Russian High-Explosive Plazma-Kor (Ruby Crimson & Violet Plasma)
def draw_su57(draw, w, h):
    cx = w / 2.0
    for y in range(h):
        prog = y / float(h)
        # Menacing angular heavy wedge
        t_w = (1.0 - math.pow(prog - 0.45, 2) * 1.4) if prog > 0.12 else (prog / 0.12)
        t_w = max(0.06, t_w) * (w * 0.36)
        
        for x in range(int(cx - t_w * 1.6), int(cx + t_w * 1.6) + 1):
            dx = abs(x - cx) / (t_w * 1.6 + 0.001)
            if dx <= 1.0:
                core = 1.0 - dx
                alpha = int(255 * math.pow(core, 1.3) * (1.0 - prog * 0.3))
                
                # Violent ruby crimson to magenta-violet
                if dx < 0.25 and prog < 0.5:
                    r, g, b = 255, 230, 255
                elif prog < 0.35:
                    r, g, b = 255, 40, int(90 + prog*300)
                elif prog < 0.7:
                    r, g, b = int(240 - (prog-0.35)*150), 20, 250
                else:
                    r, g, b = int(180 - (prog-0.7)*200), 10, int(220 - (prog-0.7)*180)
                
                r = min(255, max(0, r))
                g = min(255, max(0, g))
                b = min(255, max(0, b))
                alpha = min(255, max(0, alpha))
                draw.point((x, y), fill=(r, g, b, alpha))

# 5. F-35: GAU-22/A 25mm Cyber Emerald & Gold High-Tech
def draw_f35(draw, w, h):
    cx = w / 2.0
    for y in range(h):
        prog = y / float(h)
        # Crisp faceted 25mm dart
        t_w = (1.0 - math.pow(prog - 0.42, 2) * 1.6) if prog > 0.10 else (prog / 0.10)
        t_w = max(0.05, t_w) * (w * 0.34)
        
        for x in range(int(cx - t_w * 1.6), int(cx + t_w * 1.6) + 1):
            dx = abs(x - cx) / (t_w * 1.6 + 0.001)
            if dx <= 1.0:
                core = 1.0 - dx
                alpha = int(255 * math.pow(core, 1.35) * (1.0 - prog * 0.3))
                
                # Cyber emerald green with bright gold inner laser conduits
                if dx < 0.22 and prog < 0.6:
                    r, g, b = 240, 255, 210
                elif dx < 0.5 and prog < 0.4:
                    r, g, b = 220, 255, 80 # Gold-green ionization
                elif prog < 0.65:
                    r, g, b = 0, 240, 140
                else:
                    r, g, b = 0, int(180 - (prog-0.65)*200), int(120 - (prog-0.65)*180)
                
                r = min(255, max(0, r))
                g = min(255, max(0, g))
                b = min(255, max(0, b))
                alpha = min(255, max(0, alpha))
                draw.point((x, y), fill=(r, g, b, alpha))

# 6. TAI KAAN: Sovereign Turkish 5.+ Gen Turquoise & Imperial Gold
def draw_kaan(draw, w, h):
    cx = w / 2.0
    for y in range(h):
        prog = y / float(h)
        # Majestic diamond-head aerodynamic dart
        t_w = (1.0 - math.pow(prog - 0.38, 2) * 1.7) if prog > 0.08 else (prog / 0.08)
        t_w = max(0.05, t_w) * (w * 0.36)
        
        for x in range(int(cx - t_w * 1.6), int(cx + t_w * 1.6) + 1):
            dx = abs(x - cx) / (t_w * 1.6 + 0.001)
            if dx <= 1.0:
                core = 1.0 - dx
                alpha = int(255 * math.pow(core, 1.4) * (1.0 - prog * 0.25))
                
                # Brilliant Turquoise / Cyan core wrapped in Imperial Gold corona!
                if dx < 0.25 and prog < 0.6:
                    r, g, b = 245, 255, 255 # Diamond white tip
                elif dx < 0.6:
                    # Turkish Turquoise / Cyan
                    r, g, b = 0, int(230 - prog*60), 255
                else:
                    # Imperial Gold radiant casing and wake
                    r, g, b = 255, int(215 - prog*50), int(20 + prog*40)
                
                r = min(255, max(0, r))
                g = min(255, max(0, g))
                b = min(255, max(0, b))
                alpha = min(255, max(0, alpha))
                draw.point((x, y), fill=(r, g, b, alpha))

# 7. Enemy Standard: Crimson/Orange Combat Tracer
def draw_enemy(draw, w, h):
    cx = w / 2.0
    for y in range(h):
        prog = y / float(h)
        t_w = (1.0 - math.pow(prog - 0.45, 2) * 1.5) if prog > 0.12 else (prog / 0.12)
        t_w = max(0.06, t_w) * (w * 0.35)
        for x in range(int(cx - t_w * 1.6), int(cx + t_w * 1.6) + 1):
            dx = abs(x - cx) / (t_w * 1.6 + 0.001)
            if dx <= 1.0:
                core = 1.0 - dx
                alpha = int(255 * math.pow(core, 1.3) * (1.0 - prog * 0.3))
                if dx < 0.25 and prog < 0.5:
                    r, g, b = 255, 240, 200
                elif prog < 0.5:
                    r, g, b = 255, int(80 - prog*80), 20
                else:
                    r, g, b = int(230 - (prog-0.5)*180), 20, 10
                draw.point((x, y), fill=(r, g, b, alpha))

# 8. Boss Heavy: Volcanic Plasma Sphere/Core
def draw_boss(draw, w, h):
    cx = w / 2.0
    for y in range(h):
        prog = y / float(h)
        t_w = (1.0 - math.pow(prog - 0.5, 2) * 1.2) if prog > 0.15 else (prog / 0.15)
        t_w = max(0.1, t_w) * (w * 0.45)
        for x in range(int(cx - t_w * 1.6), int(cx + t_w * 1.6) + 1):
            dx = abs(x - cx) / (t_w * 1.6 + 0.001)
            if dx <= 1.0:
                core = 1.0 - dx
                alpha = int(255 * math.pow(core, 1.35) * (1.0 - prog * 0.25))
                if dx < 0.35 and prog < 0.6:
                    r, g, b = 255, 255, 180
                elif prog < 0.5:
                    r, g, b = 255, 40, int(60 + prog*200)
                else:
                    r, g, b = int(200 - (prog-0.5)*150), 10, int(150 - (prog-0.5)*150)
                draw.point((x, y), fill=(r, g, b, alpha))

create_bullet("bullet_f16", 64, 200, draw_f16)
create_bullet("bullet_a10", 88, 220, draw_a10)
create_bullet("bullet_f22", 52, 220, draw_f22)
create_bullet("bullet_su57", 72, 210, draw_su57)
create_bullet("bullet_f35", 64, 210, draw_f35)
create_bullet("bullet_kaan", 68, 230, draw_kaan)
create_bullet("bullet_enemy", 60, 190, draw_enemy)
create_bullet("bullet_boss", 90, 210, draw_boss)
print("All bullet sprites generated successfully!")

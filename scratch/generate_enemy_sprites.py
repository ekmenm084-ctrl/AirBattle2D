import os
import math
from PIL import Image, ImageDraw

output_dirs = [
    "My project/Assets/Sprites",
    "My project/Assets/Resources"
]
for d in output_dirs:
    os.makedirs(d, exist_ok=True)

size = 512
center = (size - 1) * 0.5

def save_enemy_sprite(name, img, ppu=520):
    for out_dir in output_dirs:
        path = os.path.join(out_dir, f"{name}.png")
        img.save(path, "PNG")
        print(f"Saved {path} ({img.width}x{img.height})")
        
        meta_path = path + ".meta"
        with open("My project/Assets/Sprites/plane_f16.png.meta", "r") as f:
            meta = f.read()
        import uuid, re
        new_guid = uuid.uuid5(uuid.NAMESPACE_DNS, f"enemy_{name}_{out_dir}").hex
        meta = re.sub(r'guid: [a-f0-9]{32}', f'guid: {new_guid}', meta)
        meta = re.sub(r'spritePixelsToUnits: \d+', f'spritePixelsToUnits: {ppu}', meta)
        with open(meta_path, "w") as f:
            f.write(meta)
        print(f"Saved meta: {meta_path} (PPU: {ppu})")

# 1. MiG-29 Fulcrum (Red & Dark Slate Agile Fighter)
def draw_mig29():
    img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    
    # Palette
    c_base = (145, 50, 50, 255)       # Aggressive dark crimson
    c_shading = (100, 30, 30, 255)    # Shadow
    c_light = (185, 75, 75, 255)      # Highlights
    c_trim = (240, 60, 40, 255)       # Red Star trim
    c_grey = (70, 75, 85, 255)        # Titanium panels
    c_cockpit = (25, 30, 40, 255)     # Glass
    c_cockpit_glint = (255, 190, 70, 255) # Gold reflection
    c_nozzle = (45, 45, 50, 255)
    c_burner = (255, 90, 20, 255)
    c_missile = (235, 235, 235, 255)
    c_missile_tip = (255, 50, 50, 255)

    # 1. Dual Afterburners (Bottom y: 440..495)
    for off in [-38, 38]:
        draw.ellipse([center + off - 14, 430, center + off + 14, 490], fill=c_burner)
        draw.ellipse([center + off - 8, 430, center + off + 8, 465], fill=(255, 255, 200, 255))
        draw.rectangle([center + off - 15, 420, center + off + 15, 445], fill=c_nozzle)

    # 2. Horizontal Tail Fins (y: 380..460)
    draw.polygon([
        (center - 18, 400), (center - 110, 455), (center - 95, 475), (center - 18, 450)
    ], fill=c_shading, outline=c_trim)
    draw.polygon([
        (center + 18, 400), (center + 110, 455), (center + 95, 475), (center + 18, 450)
    ], fill=c_base, outline=c_trim)

    # 3. Main Swept Wings (y: 200..380)
    # Left Wing
    draw.polygon([
        (center - 28, 240), (center - 200, 360), (center - 180, 395), (center - 32, 385)
    ], fill=c_shading, outline=c_trim)
    # Right Wing
    draw.polygon([
        (center + 28, 240), (center + 200, 360), (center + 180, 395), (center + 32, 385)
    ], fill=c_base, outline=c_trim)

    # Wing panel highlights
    draw.line([(center - 40, 260), (center - 180, 365)], fill=c_light, width=2)
    draw.line([(center + 40, 260), (center + 180, 365)], fill=c_light, width=2)

    # Wingtip Missiles (R-73)
    for off in [-195, 195]:
        draw.rectangle([center + off - 4, 320, center + off + 4, 380], fill=c_missile)
        draw.polygon([(center + off - 4, 320), (center + off + 4, 320), (center + off, 305)], fill=c_missile_tip)

    # 4. Twin Vertical Stabilizers (Vertical tails seen from top)
    draw.polygon([(center - 48, 360), (center - 55, 440), (center - 42, 440)], fill=c_grey, outline=c_trim)
    draw.polygon([(center + 48, 360), (center + 55, 440), (center + 42, 440)], fill=c_grey, outline=c_trim)

    # 5. Main Fuselage Body (y: 60..430)
    draw.polygon([
        (center, 50),             # Nose tip
        (center - 20, 150),
        (center - 36, 260),
        (center - 38, 420),
        (center + 38, 420),
        (center + 36, 260),
        (center + 20, 150)
    ], fill=c_base, outline=c_light)

    # LERX (Leading Edge Root Extensions)
    draw.polygon([(center - 20, 150), (center - 36, 260), (center - 15, 260)], fill=c_shading)
    draw.polygon([(center + 20, 150), (center + 36, 260), (center + 15, 260)], fill=c_light)

    # 6. Cockpit Canopy (y: 130..220)
    draw.ellipse([center - 14, 130, center + 14, 215], fill=c_cockpit, outline=c_light)
    draw.ellipse([center - 6, 140, center + 6, 185], fill=c_cockpit_glint)

    # Nose Radome
    draw.polygon([(center, 50), (center - 12, 110), (center + 12, 110)], fill=(40, 45, 55, 255))
    return img

# 2. Su-35 Flanker-E (Titanium Blue & Dark Digital Flanker)
def draw_su35():
    img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    
    c_base = (70, 95, 125, 255)       # Russian Slate Flanker Blue
    c_shading = (45, 65, 90, 255)
    c_light = (110, 145, 180, 255)
    c_camo = (35, 50, 70, 255)        # Digital splinter
    c_cockpit = (20, 25, 35, 255)
    c_cockpit_glint = (100, 220, 255, 255)
    c_nozzle = (55, 60, 65, 255)
    c_burner = (0, 180, 255, 255)     # Supersonic blue burn
    c_missile = (240, 240, 240, 255)

    # 1. Dual Heavy Engines (y: 430..495)
    for off in [-44, 44]:
        draw.ellipse([center + off - 16, 435, center + off + 16, 495], fill=c_burner)
        draw.ellipse([center + off - 8, 435, center + off + 8, 470], fill=(220, 255, 255, 255))
        draw.rectangle([center + off - 18, 420, center + off + 18, 445], fill=c_nozzle)

    # 2. Swept Horizontal Tails (y: 390..470)
    draw.polygon([(center - 22, 400), (center - 130, 465), (center - 105, 485), (center - 22, 455)], fill=c_shading)
    draw.polygon([(center + 22, 400), (center + 130, 465), (center + 105, 485), (center + 22, 455)], fill=c_base)

    # 3. Flanker High-Aspect Wings (y: 190..390)
    draw.polygon([
        (center - 32, 220), (center - 225, 360), (center - 205, 395), (center - 38, 390)
    ], fill=c_shading, outline=c_light)
    draw.polygon([
        (center + 32, 220), (center + 225, 360), (center + 205, 395), (center + 38, 390)
    ], fill=c_base, outline=c_light)

    # Wingtip EW Pods (Khibiny)
    for off in [-220, 220]:
        draw.rectangle([center + off - 5, 310, center + off + 5, 385], fill=(45, 55, 70, 255))
        draw.ellipse([center + off - 5, 305, center + off + 5, 320], fill=(255, 100, 50, 255))

    # Camo Splinter patches
    draw.polygon([(center - 140, 320), (center - 100, 300), (center - 80, 350)], fill=c_camo)
    draw.polygon([(center + 120, 330), (center + 80, 290), (center + 100, 360)], fill=c_camo)

    # 4. Long Fuselage & Extended Strakes (LERX)
    draw.polygon([
        (center, 40),             # Long sharp needle nose
        (center - 22, 130),
        (center - 42, 240),
        (center - 45, 430),
        (center + 45, 430),
        (center + 42, 240),
        (center + 22, 130)
    ], fill=c_base, outline=c_light)

    # Stinger (Tail boom extending between engines)
    draw.polygon([(center - 10, 420), (center, 470), (center + 10, 420)], fill=(40, 50, 65, 255))

    # 5. Dual Cockpit (y: 110..220)
    draw.ellipse([center - 15, 115, center + 15, 215], fill=c_cockpit, outline=c_light)
    draw.ellipse([center - 7, 125, center + 7, 185], fill=c_cockpit_glint)

    # Radome nose
    draw.polygon([(center, 40), (center - 14, 100), (center + 14, 100)], fill=(30, 35, 45, 255))
    return img

# 3. Tu-22M3 / Heavy Strategic Bomber (Dark Obsidian Flying Wing Fortress)
def draw_bomber():
    img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    
    c_base = (55, 58, 65, 255)        # Stealth Obsidian Charcoal
    c_shading = (38, 40, 46, 255)
    c_light = (85, 90, 100, 255)
    c_trim = (255, 160, 20, 255)      # Amber tactical stripes
    c_cockpit = (20, 22, 28, 255)
    c_cockpit_glint = (255, 100, 50, 255) # Red-orange HUD glow
    c_bay = (25, 25, 30, 255)         # Open Bomb Bay

    # Massive Quad Engine Exhausts (y: 430..495)
    for off in [-65, -30, 30, 65]:
        draw.ellipse([center + off - 14, 435, center + off + 14, 495], fill=(255, 120, 30, 255))
        draw.ellipse([center + off - 7, 435, center + off + 7, 465], fill=(255, 255, 200, 255))
        draw.rectangle([center + off - 15, 420, center + off + 15, 445], fill=(40, 40, 45, 255))

    # Devasa Geniş Kanatlar (Delta Wing Heavy Bomber)
    draw.polygon([
        (center, 65),
        (center - 245, 380),
        (center - 215, 440),
        (center - 80, 435),
        (center + 80, 435),
        (center + 215, 440),
        (center + 245, 380)
    ], fill=c_base, outline=c_light)

    # Shading on Left half
    draw.polygon([
        (center, 65),
        (center - 245, 380),
        (center - 215, 440),
        (center, 435)
    ], fill=c_shading)

    # Panel Lines & Armor Plates
    draw.line([(center - 120, 260), (center - 180, 390)], fill=c_light, width=2)
    draw.line([(center + 120, 260), (center + 180, 390)], fill=c_light, width=2)
    draw.line([(center - 200, 370), (center - 80, 410)], fill=c_trim, width=3)
    draw.line([(center + 200, 370), (center + 80, 410)], fill=c_trim, width=3)

    # Central Bomb Bay Doors (y: 260..370)
    draw.rectangle([center - 28, 260, center + 28, 370], fill=c_bay, outline=c_trim)
    draw.line([(center, 260), (center, 370)], fill=(60, 60, 65, 255), width=2)

    # Cockpit Window Array
    draw.polygon([
        (center, 90), (center - 18, 140), (center + 18, 140)
    ], fill=c_cockpit, outline=c_light)
    draw.ellipse([center - 8, 100, center + 8, 130], fill=c_cockpit_glint)
    return img

# 4. S-37 / Su-47 Berkut (Forward-Swept Wing Sniper Dart)
def draw_s37():
    img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    
    c_base = (35, 42, 52, 255)        # Pitch-black composite stealth
    c_shading = (22, 26, 34, 255)
    c_light = (60, 75, 95, 255)
    c_neon = (0, 230, 255, 255)       # Sniper Cyan Laser Trims
    c_cockpit = (15, 20, 25, 255)
    c_cockpit_glint = (0, 240, 255, 255)

    # Dual Engines (y: 435..495)
    for off in [-35, 35]:
        draw.ellipse([center + off - 14, 435, center + off + 14, 495], fill=c_neon)
        draw.ellipse([center + off - 7, 435, center + off + 7, 465], fill=(240, 255, 255, 255))
        draw.rectangle([center + off - 15, 420, center + off + 15, 445], fill=(30, 35, 40, 255))

    # Canards (Foreplanes, y: 140..190)
    draw.polygon([(center - 15, 145), (center - 75, 180), (center - 18, 190)], fill=c_base, outline=c_neon)
    draw.polygon([(center + 15, 145), (center + 75, 180), (center + 18, 190)], fill=c_base, outline=c_neon)

    # FORWARD-SWEPT WINGS (İleriye Açılan Meşhur Berkut Kanatları! y: 190..390)
    # Wing tips reach FORWARD towards y=200!
    draw.polygon([
        (center - 28, 380),          # Root back
        (center - 235, 220),         # Tip forward!
        (center - 215, 200),
        (center - 25, 290)           # Root front
    ], fill=c_shading, outline=c_neon)

    draw.polygon([
        (center + 28, 380),
        (center + 235, 220),
        (center + 215, 200),
        (center + 25, 290)
    ], fill=c_base, outline=c_neon)

    # Fuselage & Sniper Needle Nose
    draw.polygon([
        (center, 30),                # Long Needle
        (center - 18, 120),
        (center - 32, 260),
        (center - 35, 430),
        (center + 35, 430),
        (center + 32, 260),
        (center + 18, 120)
    ], fill=c_base, outline=c_light)

    # Cockpit
    draw.ellipse([center - 12, 110, center + 12, 200], fill=c_cockpit, outline=c_neon)
    draw.ellipse([center - 6, 120, center + 6, 170], fill=c_cockpit_glint)

    # Sniper Laser Emitter on the tip
    draw.ellipse([center - 5, 25, center + 5, 38], fill=c_neon)
    return img

# 5. J-20 / Dreadnought Boss (5. Nesil Ağır Hayalet Lider)
def draw_j20():
    img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    
    c_base = (42, 45, 52, 255)        # Carbon Dark Stealth
    c_shading = (26, 28, 34, 255)
    c_light = (75, 82, 95, 255)
    c_boss_glow = (255, 30, 60, 255)  # Menacing Crimson Plasma Glow
    c_cockpit = (15, 18, 22, 255)
    c_cockpit_glint = (255, 80, 100, 255)

    # Twin Stealth 2D Thrust Vectoring Nozzles (y: 430..495)
    for off in [-48, 48]:
        draw.polygon([
            (center + off - 18, 430), (center + off + 18, 430),
            (center + off + 12, 495), (center + off - 12, 495)
        ], fill=c_boss_glow)
        draw.ellipse([center + off - 6, 430, center + off + 6, 470], fill=(255, 230, 240, 255))

    # Heavy Delta-Canard Design (y: 110..180)
    draw.polygon([(center - 24, 125), (center - 95, 175), (center - 26, 195)], fill=c_base, outline=c_boss_glow)
    draw.polygon([(center + 24, 125), (center + 95, 175), (center + 26, 195)], fill=c_base, outline=c_boss_glow)

    # Large Stealth Delta Wings (y: 220..420)
    draw.polygon([
        (center - 35, 230), (center - 240, 390), (center - 200, 425), (center - 45, 415)
    ], fill=c_shading, outline=c_boss_glow)
    draw.polygon([
        (center + 35, 230), (center + 240, 390), (center + 200, 425), (center + 45, 415)
    ], fill=c_base, outline=c_boss_glow)

    # All-Moving Vertical Stabilizers
    draw.polygon([(center - 60, 370), (center - 90, 445), (center - 65, 450)], fill=c_shading, outline=c_boss_glow)
    draw.polygon([(center + 60, 370), (center + 90, 445), (center + 65, 450)], fill=c_base, outline=c_boss_glow)

    # Chiseled Faceted Stealth Fuselage (y: 45..430)
    draw.polygon([
        (center, 40),             # Diamond nose
        (center - 25, 105),
        (center - 48, 220),
        (center - 55, 425),
        (center + 55, 425),
        (center + 48, 220),
        (center + 25, 105)
    ], fill=c_base, outline=c_light)

    # Glowing Boss Energy Conduits
    draw.line([(center - 35, 160), (center - 45, 360)], fill=c_boss_glow, width=3)
    draw.line([(center + 35, 160), (center + 45, 360)], fill=c_boss_glow, width=3)

    # Cockpit
    draw.ellipse([center - 14, 105, center + 14, 205], fill=c_cockpit, outline=c_boss_glow)
    draw.ellipse([center - 7, 115, center + 7, 175], fill=c_cockpit_glint)
    return img

print("Generating enemy aircraft sprites...")
save_enemy_sprite("enemy_mig29", draw_mig29(), ppu=540)
save_enemy_sprite("enemy_su35", draw_su35(), ppu=510)
save_enemy_sprite("enemy_bomber", draw_bomber(), ppu=480)
save_enemy_sprite("enemy_s37", draw_s37(), ppu=520)
save_enemy_sprite("enemy_j20", draw_j20(), ppu=470)
print("All 5 enemy jet sprites generated successfully!")

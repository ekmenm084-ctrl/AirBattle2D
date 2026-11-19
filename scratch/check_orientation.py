import os
import math
from PIL import Image, ImageDraw

output_dir = "My project/Assets/Resources"
os.makedirs(output_dir, exist_ok=True)
size = 144
center = (size - 1) * 0.5

def save_sprite(name, img):
    path = os.path.join(output_dir, f"{name}.png")
    img.save(path, "PNG")
    print(f"Generated {path} ({img.width}x{img.height})")
    
    meta_path = path + ".meta"
    with open("My project/Assets/Resources/bullet_tracer.png.meta", "r") as f:
        meta_content = f.read()
    import uuid, re
    new_guid = uuid.uuid5(uuid.NAMESPACE_DNS, name).hex
    meta_content = re.sub(r'guid: [a-f0-9]{32}', f'guid: {new_guid}', meta_content)
    # Set spritePixelsToUnits to 128
    meta_content = re.sub(r'spritePixelsToUnits: \d+', 'spritePixelsToUnits: 128', meta_content)
    with open(meta_path, "w") as f:
        f.write(meta_content)
    print(f"Generated meta: {meta_path} (guid: {new_guid})")

# 1. MiG-29 Fulcrum (Agile Red-Star Fighter)
def create_mig29():
    img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    pixels = img.load()
    
    c_body = (130, 45, 45, 255)         # Aggressive Crimson-Crimson Grey
    c_body_dark = (90, 25, 25, 255)     # Shading
    c_body_light = (175, 75, 75, 255)   # Highlights
    c_accent = (255, 60, 40, 255)       # Red Star tactical trim
    c_cockpit = (20, 25, 35, 255)       # Dark cockpit glass
    c_canopy = (255, 180, 50, 255)      # Amber gold canopy reflection
    c_missile = (230, 230, 230, 255)    # R-73 Archer missiles
    c_nozzle = (50, 50, 55, 255)
    c_burner = (255, 120, 0, 255)
    c_burner_core = (255, 240, 180, 255)

    for y in range(size):
        for x in range(size):
            dx = abs(x - center)
            # Jet faces DOWN (toward player at y=0, nose is at y=size-1 or top?)
            # Wait! In AirBattle2D, how are enemy sprites oriented?
            # Let's check plane2.png orientation!

# Let's check plane2.png orientation first!

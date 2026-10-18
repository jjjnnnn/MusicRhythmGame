"""Read-only Wii seal Yaz0/U8/BRCAD inspection. Derived files stay under Logs.

Authoritative structure/transform reference (pinned public decompilation):
https://github.com/conhlee/rhf/blob/4429cbcb000abff644683d98701f6ab8c1671e10/src/GameUtil/CellAnimStr.hpp
https://github.com/conhlee/rhf/blob/4429cbcb000abff644683d98701f6ab8c1671e10/src/GameUtil/CellAnim.cpp
https://github.com/conhlee/rhf/blob/4429cbcb000abff644683d98701f6ab8c1671e10/src/GameUtil/CellAnimManager.cpp
Community flour SPECIFICATION.md describes an earlier/different binary layout.
No game executable, Unity asset, or source image is modified.
"""
import argparse
import hashlib
import json
import re
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_BASE = ROOT / "Logs/SealExtraction/SOMJ01"


def yaz0(data):
    if data[:4] != b"Yaz0":
        return data
    size = struct.unpack_from(">I", data, 4)[0]
    out = bytearray()
    pos = 16
    while len(out) < size:
        code = data[pos]
        pos += 1
        for bit in range(7, -1, -1):
            if len(out) >= size:
                break
            if code & (1 << bit):
                out.append(data[pos])
                pos += 1
            else:
                first, second = data[pos:pos + 2]
                pos += 2
                back = ((first & 15) << 8) + second + 1
                count = first >> 4
                if count == 0:
                    count = data[pos] + 18
                    pos += 1
                else:
                    count += 2
                if back > len(out):
                    raise ValueError("Invalid Yaz0 back-reference")
                for _ in range(count):
                    out.append(out[-back])
    if len(out) != size:
        raise ValueError("Yaz0 output length mismatch")
    return bytes(out)


def u8_files(data):
    assert data[:4] == bytes.fromhex("55aa382d"), "Not U8"
    root_at = struct.unpack_from(">I", data, 4)[0]
    count = struct.unpack_from(">I", data, root_at + 8)[0]
    strings_at = root_at + count * 12
    stack = [(count, "")]
    entries = []
    for i in range(1, count):
        while i >= stack[-1][0]:
            stack.pop()
        name_type, off, length = struct.unpack_from(">III", data, root_at + i * 12)
        name_at = strings_at + (name_type & 0xFFFFFF)
        name = data[name_at:data.index(0, name_at)].decode("utf8")
        path = str(Path(stack[-1][1]) / name).replace("\\", "/")
        if name_type >> 24:
            stack.append((length, path))
        else:
            entries.append((path, off, data[off:off + length]))
    return entries


def brcad(data):
    version, unknown, sheet, control, width, height, count, unknown2 = struct.unpack_from(
        ">IIHHHHHH", data, 0)
    pos = 20
    sprites = []
    for i in range(count):
        n, unk = struct.unpack_from(">HH", data, pos)
        pos += 4
        parts = []
        for j in range(n):
            x, y, w, h, unk32, px, py, sx, sy, rot, fx, fy, opacity, pad = struct.unpack_from(
                ">HHHHIhhfffBBBB", data, pos)
            pos += 32
            parts.append(dict(texture=[x, y, w, h], position=[px, py],
                              scale=[sx, sy], rotation=rot, flip=[fx, fy], opacity=opacity,
                              paletteRelativeIndex=unk32 >> 16, reserved=unk32 & 65535,
                              unknown=unk32, padding=pad))
        sprites.append(dict(index=i, parts=parts, unknown=unk))
    anim_count, unknown3 = struct.unpack_from(">HH", data, pos)
    pos += 4
    animations = []
    for i in range(anim_count):
        start = pos
        n, animation_unknown = struct.unpack_from(">HH", data, pos)
        pos += 4
        frames = []
        for j in range(n):
            sprite, duration, frame_unknown, sx, sy, rot, opacity = struct.unpack_from(">HHIfffB", data, pos)
            pad = data[pos + 21:pos + 24].hex()
            pos += 24
            assert sprite < count, (i, j, sprite, count)
            px, py = struct.unpack(">hh", struct.pack(">I", frame_unknown))
            frames.append(dict(sprite=sprite, duration=duration, position=[px, py], scale=[sx, sy],
                               rotation=rot, opacity=opacity, padding=pad, unknown=frame_unknown))
        animations.append(dict(index=i, offset=start, frameCount=n, unknown=animation_unknown,
                               totalDuration=sum(f["duration"] for f in frames), frames=frames))
    return dict(version=version, unknown=unknown, sheet=sheet, control=control,
                dimensions=[width, height], spriteCount=count, unknown2=unknown2,
                animationCount=anim_count, unknown3=unknown3, sprites=sprites,
                animations=animations, parsedBytes=pos, fileBytes=len(data),
                trailingBytes=data[pos:].hex())


def decode_tpl(data, out_dir):
    """Decode the base levels of the game's RGB5A3 and RGBA8 tiled textures."""
    from PIL import Image
    magic, count, table = struct.unpack_from(">III", data)
    assert magic == 0x20AF30
    images = []
    summary = []
    for index in range(count):
        header, palette = struct.unpack_from(">II", data, table + 8 * index)
        h, w, fmt, off = struct.unpack_from(">HHII", data, header)
        assert fmt in (5, 6), fmt
        im = Image.new("RGBA", (w, h))
        pix = im.load()
        pos = off
        for by in range(0, h, 4):
            for bx in range(0, w, 4):
                for y in range(4):
                    for x in range(4):
                        i = y * 4 + x
                        if fmt == 5:
                            v = struct.unpack_from(">H", data, pos + i * 2)[0]
                            if v & 0x8000:
                                r, g, b = (v >> 10) & 31, (v >> 5) & 31, v & 31
                                rgba = ((r << 3) | (r >> 2), (g << 3) | (g >> 2),
                                        (b << 3) | (b >> 2), 255)
                            else:
                                a = (v >> 12) & 7
                                rgba = (((v >> 8) & 15) * 17, ((v >> 4) & 15) * 17,
                                        (v & 15) * 17, (a << 5) | (a << 2) | (a >> 1))
                        else:
                            a, r = data[pos + 2*i:pos + 2*i + 2]
                            g, b = data[pos + 32 + 2*i:pos + 34 + 2*i]
                            rgba = (r, g, b, a)
                        if bx+x < w and by+y < h:
                            pix[bx+x, by+y] = rgba
                pos += 32 if fmt == 5 else 64
        path = out_dir / f"texture_{index}.png"
        im.save(path)
        item = dict(index=index, width=w, height=h, format=fmt, dataOffset=off,
                    baseLevelBytes=pos-off, png=str(path))
        if index == 0:
            existing = Image.open(ROOT / "Assets/Resources/Art/Level_cellanim/Sprite/cellanim.tpl.png").convert("RGBA")
            item["existingProjectTexturePixelIdentical"] = existing.size == im.size and existing.tobytes() == im.tobytes()
            if existing.size == im.size:
                original_pixels=list(im.getdata())
                existing_pixels=list(existing.getdata())
                item["existingProjectTextureDifferentPixels"] = sum(a!=b for a,b in zip(original_pixels,existing_pixels))
                item["existingProjectTextureMaxChannelError"] = max(abs(x-y) for a,b in zip(original_pixels,existing_pixels) for x,y in zip(a,b))
                item["existingProjectTextureAlphaIdentical"] = existing.getchannel("A").tobytes()==im.getchannel("A").tobytes()
        summary.append(item)
        images.append(im)
    return images, summary


def render_pose_preview(parsed, texture, out_dir):
    """Contact sheet of direct BRCAD part poses; no interpolation or new artwork."""
    from PIL import Image, ImageDraw, ImageOps
    import math
    sprites = parsed["sprites"]
    def sprite_image(index, face=False):
        im = Image.new("RGBA", (384, 320))
        origin = (192, 220)
        parts = sprites[index]["parts"]
        for n, part in enumerate(parts):
            x,y,w,h = part["texture"]
            px,py = part["position"]
            sx,sy = part["scale"]
            if w == 0 or h == 0 or part["opacity"] == 0:
                continue
            cell = texture.crop((x,y,x+w,y+h))
            if part["flip"][0]:
                cell = ImageOps.mirror(cell)
            if part["flip"][1]:
                cell = ImageOps.flip(cell)
            cell = cell.resize((max(1,round(w*sx)),max(1,round(h*sy))),Image.Resampling.BICUBIC)
            if part["opacity"] != 255:
                cell.putalpha(cell.getchannel("A").point(lambda a:round(a*part["opacity"]/255)))
            cell = cell.rotate(-part["rotation"], resample=Image.Resampling.BICUBIC, expand=True)
            cx,cy = px-512+w*sx/2, py-512+h*sy/2
            im.alpha_composite(cell,(round(origin[0]+cx-cell.width/2),round(origin[1]+cy-cell.height/2)))
        # The original game links the face bank animation to invisible base part 3.
        if face and len(parts)>3:
            anchor=parts[3]
            px,py=anchor["position"]
            w,h=anchor["texture"][2:]
            sx,sy=anchor["scale"]
            cx,cy=px-512+w*sx/2,py-512+h*sy/2
            fi=sprite_image(59)
            fi=fi.crop((origin[0]-40,origin[1]-40,origin[0]+40,origin[1]+40))
            if sx<0:
                fi=ImageOps.mirror(fi)
            if sy<0:
                fi=ImageOps.flip(fi)
            fi=fi.resize((max(1,round(80*abs(sx))),max(1,round(80*abs(sy)))),Image.Resampling.BICUBIC)
            fi=fi.rotate(-anchor["rotation"],resample=Image.Resampling.BICUBIC,expand=True)
            im.alpha_composite(fi,(round(origin[0]+cx-fi.width/2),round(origin[1]+cy-fi.height/2)))
        return im
    chosen=[1,4,5,8,9,12,15,16,19,20]
    board=Image.new("RGB",(1920,len(chosen)*192),(223,237,244))
    draw=ImageDraw.Draw(board)
    for row,index in enumerate(chosen):
        animation=parsed["animations"][index]
        frames=animation["frames"]
        ids=[round(i*(len(frames)-1)/7) for i in range(8)]
        for col,key in enumerate(ids):
            frame=frames[key]
            im=sprite_image(frame["sprite"],True).resize((240,200),Image.Resampling.LANCZOS)
            # Display individual cell poses, preserving sprite and internal part transforms.
            board.paste(im,(col*240,row*192-8),im)
            draw.text((col*240+6,row*192+170),f"key {key}, sprite {frame['sprite']}, d={frame['duration']}",fill=(24,45,60))
        draw.text((8,row*192+4),f"{index}: {animation['name']}",fill=(8,25,38))
    board.save(out_dir/"goma_pose_contact_sheet.png")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base", type=Path, default=DEFAULT_BASE)
    parser.add_argument("--export-unity", action="store_true", help="Export just the original goma JSON into the authorized Unity resource folder")
    args = parser.parse_args()
    src = args.base / "files/content2/cellanim/goma/ver0/cellanim.szs"
    out_dir = args.base / "derived/goma_animation"
    out_dir.mkdir(parents=True, exist_ok=True)
    packed = src.read_bytes()
    data = yaz0(packed)
    entries = u8_files(data)
    manifest = dict(source=str(src), sourceSha256=hashlib.sha256(packed).hexdigest(),
                    compression=packed[:4].decode("ascii"), compressedBytes=len(packed),
                    decompressedBytes=len(data), archive="Nintendo U8", entries=[])
    entry_map = {name: contents for name, _, contents in entries}
    images, texture_summary = decode_tpl(entry_map["cellanim.tpl"], out_dir)
    manifest["textures"] = texture_summary
    for name, off, contents in entries:
        target = out_dir / "archive" / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(contents)
        item = dict(path=name, offset=off, size=len(contents),
                    magic=contents[:16].hex(), sha256=hashlib.sha256(contents).hexdigest())
        if name.lower().endswith(".brcad"):
            parsed = brcad(contents)
            label_name = "rcad_" + Path(name).stem + "_labels.h"
            labels = {int(v): k for k, v in re.findall(rb"#define\s+(\w+)\s+(\d+)", entry_map[label_name])}
            for animation in parsed["animations"]:
                animation["name"] = labels[animation["index"]].decode("ascii")
            parsed["coordinateOrigin"] = [512,512]
            parsed["frameRate"] = 60
            parsed["interpolation"] = "step; key frameCounts accumulate; per-actor speed and tempo may scale playback"
            parsed["references"] = [
                "https://github.com/conhlee/rhf/blob/4429cbcb000abff644683d98701f6ab8c1671e10/src/GameUtil/CellAnimStr.hpp",
                "https://github.com/conhlee/rhf/blob/4429cbcb000abff644683d98701f6ab8c1671e10/src/GameUtil/CellAnim.cpp",
                "https://github.com/conhlee/rhf/blob/4429cbcb000abff644683d98701f6ab8c1671e10/src/GameUtil/CellAnimManager.cpp"]
            assert parsed["parsedBytes"] == len(contents), name
            assert len(labels)==parsed["animationCount"], name
            for sprite in parsed["sprites"]:
                for part in sprite["parts"]:
                    x,y,w,h=part["texture"]
                    assert x+w<=parsed["dimensions"][0] and y+h<=parsed["dimensions"][1], (name,sprite["index"])
            (out_dir / (Path(name).name + ".json")).write_text(json.dumps(parsed, indent=2), encoding="utf8")
            if name == "goma.brcad":
                render_pose_preview(parsed,images[parsed["sheet"]],out_dir)
                if args.export_unity:
                    unity_dir=ROOT/"Assets/Resources/Art/Level_cellanim/OriginalGoma"
                    unity_dir.mkdir(parents=True,exist_ok=True)
                    (unity_dir/"goma.json").write_text(json.dumps(parsed,separators=(",",":")),encoding="utf8")
            item.update(spriteCount=parsed["spriteCount"], animationCount=parsed["animationCount"],
                        dimensions=parsed["dimensions"], trailingBytes=parsed["trailingBytes"])
        strings = [m.group().decode("ascii") for m in re.finditer(rb"[ -~]{4,}", contents)]
        if len(contents) < 250000:
            (out_dir / (Path(name).name + ".strings.txt")).write_text("\n".join(strings), encoding="utf8")
        manifest["entries"].append(item)
    (out_dir / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf8")
    print(json.dumps(manifest, indent=2))


if __name__ == "__main__":
    main()

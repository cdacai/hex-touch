"""构建期一次性脚本：生成 app.ico（PNG-in-ICO，零第三方依赖）。
运行：D:\\research\\.venv\\Scripts\\python.exe gen_icon.py
临时文件，不影响运行时（exe 不依赖 Python）。
"""
import os
import struct
import zlib

SIZES = [16, 24, 32, 48, 64, 256]


def _chunk(tag, data):
    return (struct.pack('>I', len(data)) + tag + data +
            struct.pack('>I', zlib.crc32(tag + data) & 0xFFFFFFFF))


def _png(size, rows):
    raw = b''.join(b'\x00' + bytes(r) for r in rows)
    ihdr = struct.pack('>IIBBBBB', size, size, 8, 6, 0, 0, 0)
    return (b'\x89PNG\r\n\x1a\n' + _chunk(b'IHDR', ihdr) +
            _chunk(b'IDAT', zlib.compress(raw, 9)) + _chunk(b'IEND', b''))


def _image(size):
    rows = []
    c = (size - 1) / 2.0
    r_out = size * 0.47
    for y in range(size):
        row = []
        for x in range(size):
            dx, dy = x - c, y - c
            d = (dx * dx + dy * dy) ** 0.5
            if d > r_out:
                row += [0, 0, 0, 0]
                continue
            t = min(1.0, d / r_out)
            red = int(58 + 55 * (1 - t))
            green = int(110 + 45 * (1 - t))
            blue = 252
            # 麦克风胶囊
            half_w = size * 0.09
            top = size * 0.26
            bot = size * 0.56
            in_rod = (abs(dx) <= half_w and top <= y <= bot and
                      (abs(dx - (-half_w)) > half_w * 0.0))
            if in_rod and abs(dx) <= half_w:
                # 圆角
                edge = min(y - top, bot - y)
                if edge >= half_w * 0.6 or abs(dx) <= half_w - (half_w * 0.6 - edge):
                    red = green = blue = 255
            # 支架弧
            if size >= 24:
                sx, sy = dx, y - size * 0.60
                dist = (sx * sx + sy * sy) ** 0.5
                if abs(dist - size * 0.16) < max(1.0, size * 0.035) and y >= size * 0.56:
                    red = green = blue = 255
            row += [red, green, blue, 255]
        rows.append(row)
    return _png(size, rows)


def main():
    images = [(s, _image(s)) for s in SIZES]
    header = struct.pack('<HHH', 0, 1, len(images))
    offset = 6 + 16 * len(images)
    entries = b''
    body = b''
    for size, data in images:
        w = 0 if size >= 256 else size
        entries += struct.pack('<BBBBHHII', w, w, 0, 0, 1, 32, len(data), offset)
        offset += len(data)
        body += data
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'assets', 'app.ico')
    with open(out, 'wb') as f:
        f.write(header + entries + body)
    print('app.ico written,', 6 + 16 * len(images) + len(body), 'bytes')


if __name__ == '__main__':
    main()

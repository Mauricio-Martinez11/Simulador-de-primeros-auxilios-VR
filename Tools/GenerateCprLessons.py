"""Assemble reviewed illustrated keyframes into local looping Unity lessons.

python Tools/GenerateCprLessons.py --ffmpeg PATH
Requires Pillow and ffmpeg with libx264. No API calls.
ArtSource/RCP contains the original four-pose atlases and generation prompts.
Intermediate frames and the encoder are development tools, never app assets.
"""
import argparse
import json
import math
import pathlib
import subprocess
from concurrent.futures import ThreadPoolExecutor
from PIL import Image, ImageOps

ROOT = pathlib.Path(__file__).resolve().parents[1]
NAMES = ['01_Seguridad', '02_Respuesta', '03_Respiracion',
         '04_Ayuda', '05_Manos', '06_Compresiones']
SIZE, FPS = (1280, 720), 24


def poses(source):
    atlas = Image.open(source).convert('RGB')
    w, h = atlas.size
    result = []
    for row in range(2):
        for column in range(2):
            cell = atlas.crop((column*w//2, row*h//2, (column+1)*w//2, (row+1)*h//2))
            # Conservar la anatomía: ajustar el fotograma completo sin estirar ni recortar una mano.
            result.append(ImageOps.pad(cell, SIZE, method=Image.Resampling.LANCZOS, color='white'))
    return result


def smooth(value):
    value = max(0, min(1, value))
    return value*value*(3-2*value)


def compression_frame(rest, time):
    # Una pequeña deformación local de la malla mueve juntos los hombros, los brazos
    # extendidos y el pecho. La cabeza, las piernas y los brazos en reposo permanecen inmóviles.
    # La deformación de una sola fuente evita manos fantasma por interpolación óptica.
    amount = .5*(1-math.cos(time*math.tau*110/60))*22
    width, height = SIZE
    def source(x, y):
        nx, ny = x/width, y/height
        lateral = smooth((nx-.18)/.08)*smooth((.55-nx)/.08)
        vertical = 1-smooth((ny-.50)/.24)
        return x, y-amount*lateral*vertical
    mesh = []
    for y in range(0, height, 24):
        for x in range(0, width, 32):
            right, bottom = min(x+32,width), min(y+24,height)
            quad = sum((source(a,b) for a,b in [(x,y),(x,bottom),(right,bottom),(right,y)]), ())
            mesh.append(((x,y,right,bottom),quad))
    return rest.transform(SIZE, Image.Transform.MESH, mesh, Image.Resampling.BICUBIC, fillcolor='white')


def encode(name, args):
    frames = poses(args.source / (name+'.png'))
    compression = name == '06_Compresiones'
    duration = 6
    # Poses sostenidas nítidas para gestos amplios, sin mezcla de fotogramas que invente
    # dedos adicionales. El ejemplo de compresión se mueve continuamente al ritmo de referencia.
    sequence = [0,1,2,3]
    if name == '02_Respuesta': sequence = [2,0,1,2,3,1]
    if name == '04_Ayuda': sequence = [0,1,2,3,3,1]
    if name == '05_Manos': sequence = [0,1,2,3,3,3]
    command = [str(args.ffmpeg), '-y', '-v', 'error', '-f', 'rawvideo',
               '-pix_fmt', 'rgb24', '-s', f'{SIZE[0]}x{SIZE[1]}', '-r', str(FPS), '-i', '-',
               '-an', '-c:v', 'libx264', '-threads', '2', '-profile:v', 'baseline',
               '-level', '3.1', '-crf', '18', '-pix_fmt', 'yuv420p',
               '-color_primaries', 'bt709', '-color_trc', 'bt709', '-colorspace', 'bt709',
               '-movflags', '+faststart', str(args.output/(name+'.mp4'))]
    process = subprocess.Popen(command, stdin=subprocess.PIPE)
    try:
        for number in range(duration*FPS):
            frame = compression_frame(frames[0],number/FPS) if compression else frames[sequence[number*len(sequence)//(duration*FPS)]]
            process.stdin.write(frame.tobytes())
    finally:
        process.stdin.close()
        result = process.wait()
    if result: raise RuntimeError(f'ffmpeg failed for {name}: {result}')
    frames[3 if name == '05_Manos' else 0].save(args.output/(name+'.png'))
    print(name+' listo', flush=True)
    return {'name': name, 'size': list(SIZE), 'fps': FPS, 'seconds': duration,
            'keyframes': 4, 'animation': 'local mesh' if compression else 'held poses',
            'compressionRate': 110 if compression else None}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--ffmpeg', type=pathlib.Path, required=True)
    parser.add_argument('--source', type=pathlib.Path, default=ROOT/'ArtSource/RCP')
    parser.add_argument('--output', type=pathlib.Path, default=ROOT/'Assets/Videos/RCP')
    parser.add_argument('--only', choices=NAMES)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    names = [args.only] if args.only else NAMES
    with ThreadPoolExecutor(max_workers=2) as pool:
        manifest = list(pool.map(lambda name: encode(name, args), names))
    if not args.only:
        (args.source/'animation-manifest.json').write_text(json.dumps(manifest, indent=2)+'\n')


if __name__ == '__main__':
    main()

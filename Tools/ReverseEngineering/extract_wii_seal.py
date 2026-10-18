"""Read a local WBFS (slot 0) and extract the DOL, file index and seal resources.

Original implementation using the documented Wii disc/WBFS layout. See Dolphin's
DiscIO/WbfsBlob.cpp, VolumeWii.cpp and DiscUtils.cpp for format references.
Input is always opened read-only. Outputs belong outside Unity Assets.
Requires the cryptography package, available in the bundled Python runtime.
"""
import argparse
import fnmatch
import hashlib
import json
import struct
from functools import lru_cache
from pathlib import Path, PurePosixPath
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes


def be32(data, offset=0):
    return struct.unpack_from('>I', data, offset)[0]


def decrypt(key, iv, data):
    context = Cipher(algorithms.AES(key), modes.CBC(iv)).decryptor()
    return context.update(data) + context.finalize()


class Wbfs:
    def __init__(self, path):
        self.file = Path(path).open('rb')
        head = self.file.read(512)
        if head[:4] != b'WBFS' or head[12] == 0:
            raise ValueError('Expected a WBFS file with an occupied first slot')
        self.hd_size, self.block_size = 1 << head[8], 1 << head[9]
        self.file.seek(self.hd_size)
        self.header = self.file.read(256)
        count = (143432 * 2 * 0x8000) // self.block_size
        self.table = struct.unpack('>' + 'H' * count, self.file.read(count * 2))

    def read(self, offset, length):
        output = bytearray()
        while length:
            block, within = divmod(offset, self.block_size)
            take = min(length, self.block_size - within)
            physical = self.table[block]
            if physical == 0:
                output.extend(bytes(take))
            else:
                self.file.seek(physical * self.block_size + within)
                chunk = self.file.read(take)
                if len(chunk) != take:
                    raise EOFError('Truncated WBFS block')
                output.extend(chunk)
            offset += take
            length -= take
        return bytes(output)


class WiiPartition:
    def __init__(self, disc):
        self.disc = disc
        groups = disc.read(0x40000, 32)
        found = []
        for group in range(4):
            count, table = struct.unpack_from('>II', groups, group * 8)
            if count > 100:
                raise ValueError('Invalid partition count')
            for i in range(count):
                offset, kind = struct.unpack('>II', disc.read(table * 4 + i * 8, 8))
                found.append((offset * 4, kind))
        self.offset = next(offset for offset, kind in found if kind == 0)
        ticket = disc.read(self.offset, 0x2c0)
        if ticket[0x1f1] != 0:
            raise ValueError('Only the retail Wii common-key index 0 is supported')
        common_key = bytes.fromhex('ebe42a225e8593e448d9c5457381aaf7')
        self.key = decrypt(common_key, ticket[0x1dc:0x1e4] + bytes(8), ticket[0x1bf:0x1cf])
        self.data_offset = self.offset + be32(ticket, 0x2b8) * 4
        if be32(self.read(0, 0x20), 0x18) != 0x5d1c9ea3:
            raise ValueError('Decrypted partition does not have the Wii disc magic')

    @lru_cache(maxsize=64)
    def cluster(self, index):
        raw = self.disc.read(self.data_offset + index * 0x8000, 0x8000)
        return decrypt(self.key, raw[0x3d0:0x3e0], raw[0x400:])

    def read(self, offset, length):
        output = bytearray()
        while length:
            block, within = divmod(offset, 0x7c00)
            take = min(length, 0x7c00 - within)
            output.extend(self.cluster(block)[within:within + take])
            offset += take
            length -= take
        return bytes(output)


def inspect_and_extract(source, destination, includes):
    disc = Wbfs(source)
    partition = WiiPartition(disc)
    destination = Path(destination).resolve()
    destination.mkdir(parents=True, exist_ok=True)
    boot = partition.read(0, 0x440)
    dol_offset, fst_offset, fst_size = [be32(boot, offset) * 4 for offset in (0x420, 0x424, 0x428)]
    header = partition.read(dol_offset, 256)
    sections = []
    for i in range(18):
        offset, address, size = [be32(header, base + 4 * i) for base in (0, 0x48, 0x90)]
        if size:
            sections.append(dict(offset=offset, address=address, size=size))
    dol_size = max(s['offset'] + s['size'] for s in sections)
    if dol_size > 0x4000000:
        raise ValueError('DOL size out of expected range')
    dol = partition.read(dol_offset, dol_size)
    (destination / 'sys').mkdir(exist_ok=True)
    (destination / 'sys' / 'main.dol').write_bytes(dol)
    fst = partition.read(fst_offset, fst_size)
    (destination / 'sys' / 'fst.bin').write_bytes(fst)
    count = be32(fst, 8)
    strings = count * 12
    stack, entries, extracted = [(count, '')], [], []
    for index in range(1, count):
        while index >= stack[-1][0]:
            stack.pop()
        first, offset, size = struct.unpack_from('>III', fst, index * 12)
        name_at = strings + (first & 0xffffff)
        name = fst[name_at:fst.index(0, name_at)].decode('utf-8', errors='replace')
        path = stack[-1][1] + name
        if first >> 24:
            stack.append((size, path + '/'))
            continue
        record = dict(path=path, offset=offset * 4, size=size)
        entries.append(record)
        if any(fnmatch.fnmatch(path.lower(), pattern.lower()) for pattern in includes):
            parts = PurePosixPath(path).parts
            if '..' in parts or PurePosixPath(path).is_absolute():
                raise ValueError('Unsafe file name in disc filesystem')
            target = destination.joinpath('files', *parts)
            target.parent.mkdir(parents=True, exist_ok=True)
            content = partition.read(offset * 4, size)
            target.write_bytes(content)
            extracted.append(dict(path=path, size=size, sha256=hashlib.sha256(content).hexdigest()))
    metadata = dict(source=str(Path(source).resolve()), discId=boot[:6].decode('ascii'),
                    title=boot[0x20:0x60].split(b'\0')[0].decode('ascii'),
                    version=boot[7], partitionOffset=partition.offset, dolOffset=dol_offset,
                    dolSize=dol_size, dolSha256=hashlib.sha256(dol).hexdigest(),
                    sections=sections, files=entries, extracted=extracted)
    (destination / 'manifest.json').write_text(json.dumps(metadata, ensure_ascii=False, indent=2), encoding='utf-8')
    print('Disc:', metadata['discId'], metadata['title'])
    print('DOL:', dol_size, 'bytes,', len(sections), 'sections')
    print('Filesystem:', len(entries), 'files; extracted:', len(extracted))
    for record in extracted:
        print(record['path'], record['size'])


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source')
    parser.add_argument('destination')
    parser.add_argument('--include', action='append', default=[])
    args = parser.parse_args()
    inspect_and_extract(args.source, args.destination, args.include or ['*goma*', '*rev_tengoku.brsar'])

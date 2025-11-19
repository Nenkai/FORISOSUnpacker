# FORISOSUnpacker

File unpacker (`PACK/*.cmp` files) for games built with the FORIS OS engine, used by VNs created by 0verflow, mainly intended for:

* S.S.D.S. Setsuna no Akogare

## Usage
Download the latest version from [Releases](https://github.com/Nenkai/BabylonsFallTools/releases).

* Extract all `.cmp` files in a folder: `FORISOSUnpacker.exe extract-all -i <directory> [-o <output dir>]`

> [!NOTE]  
> Arguments wrapped in `<>` are required and `[]` are optional.

## Research Notes

The engine uses an executable (entrypoint), main engine/front-end (`MainSystem.dll`) a bunch of modules (dlls), which exports `InitModule` and `ExitModule` functions.

`InitModule` usually registers a bunch of functions exposed by said module (linked with hashes of 0x10 bytes each?)

### Game Files
Game contents are in PACK files (`.CMP` extension), which also happen to be executables (but don't do anything). 
Essentially the CMP files are made up of a common executable (same across all CMPs, built with Borland C++ & packed with UPX for some odd reason unlike the rest of the modules), and a footer appended to the executable with the game data.

It is believed that the executable part is only there to fool people trying to open the CMP files.

The game seeks to the end of each CMP minus 8 bytes, to decrypt a magic of 4 bytes (`PACK`) and read an offset to the TOC which is also 4 bytes.

The TOC is decrypted (cheap XOR), and decompressed (custom LZ).

The executable/dll responsible for file system operations + decryption/decompression is `FileSystem.mod` (dll file).

### Encryption Key
The decryption key is part of the main executable (`ssds.exe`).

Execution goes as such: `ssds.exe` -> `MainSystem.dll` -> `FileSystem.mod`.

The key is stored in `ssds.exe` as a Resource called `Binary`, 0x10 bytes. `MainSystem.dll` is the one doing the fetching with `LoadResource` and `LockResource`.

#### Some Signatures
* Set encryption key (FileSystem.mod): offset `10003D80` & `10004130`, sig: `8B 44 24 ? 8B 08 89 0D`
* Open cmp file pattern (FileSystem.mod): `6A ? 68 ? ? ? ? 64 A1 ? ? ? ? 50 64 89 25 ? ? ? ? 83 EC ? A1 ? ? ? ? 53 55`


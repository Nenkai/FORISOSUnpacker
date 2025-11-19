namespace FORISOSUnpacker
{
    internal class Program
    {
        private static readonly byte[] Key = [0xF5, 0xCD, 0x8C, 0x8B, 0xDB, 0xD7, 0x82, 0x3D, 0x6F, 0x6B, 0xA4, 0xC7, 0x04, 0xD6, 0x6B, 0xBB];
        static void Main(string[] args)
        {
            // Game contents are in PACK files (.CMP extension), which also happen to be executables (but don't do anything).
            // They're UPX packed, but that doesn't matter too.

            // The executable/dll responsible for file system operations + decryption/decompression is FileSystem.mod.
            // There's an encryption for the toc of each pack (aka .CMP file, which btw are also executables but don't do anything).

            // The decryption key is part of the main executable. (ssds.exe).
            // Execution goes as such: ssds.exe -> MainSystem.dll -> FileSystem.mod.

            // Encryption key is set with the last exposed function of FileSystem
            // - offset: 10003D80 & 10004130, sig: 8B 44 24 ? 8B 08 89 0D

            // Open cmp file pattern: 6A ? 68 ? ? ? ? 64 A1 ? ? ? ? 50 64 89 25 ? ? ? ? 83 EC ? A1 ? ? ? ? 53 55

        }
    }
}

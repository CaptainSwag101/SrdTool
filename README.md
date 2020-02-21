# SrdTool
(HEAVILY WIP) An image extractor/injector for Danganronpa V3
I do not promise this program works very well or is easy to use, it's something old I wrote just to learn more about the SRD format.
It relies on the Scarlet image library which is now archived and no longer maintained.
I have no current plans of replacing its Scarlet dependency with something else as I know of no other decent image libraries that are compatible with the image formats used by DRV3, and I absolutely do not understand the formats well enough myself to be crazy enough to try and write my own conversion library.

Usage: SrdTool.exe <Input SRD file> [replacement PNG file] [texture ID number to replace] [generate mipmaps (true/false)]
If the only argument provided is the input SRD file, it will extract all textures in the file.

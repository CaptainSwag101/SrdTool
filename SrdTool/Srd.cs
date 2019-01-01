using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using Scarlet.Drawing;

namespace SrdTool
{
    class Srd
    {
        public List<SrdBlock> Blocks;

        public Srd()
        {
            Blocks = new List<SrdBlock>();
        }

        public void ExtractImages(string srdPath, bool removeMipmaps = true)
        {
            Console.WriteLine("Searching for texture data in {0}:", srdPath);
            // Iterate through blocks and find $TXR entries
            foreach (SrdBlock block in Blocks)
            {
                if (block.Type == "$TXR")
                {
                    BinaryReader txrReader = new BinaryReader(new MemoryStream(block.Data));
                    int unk1 = txrReader.ReadInt32();   // 01 00 00 00
                    short swizzle = txrReader.ReadInt16();
                    short dispWidth = txrReader.ReadInt16();
                    short dispHeight = txrReader.ReadInt16();
                    short scanline = txrReader.ReadInt16();
                    byte format = txrReader.ReadByte();
                    byte unk2 = txrReader.ReadByte();
                    byte palette = txrReader.ReadByte();
                    byte paletteId = txrReader.ReadByte();

                    BinaryReader rsiReader = new BinaryReader(new MemoryStream(block.Subdata));
                    SrdBlock imageInfoBlock = ReadBlock(ref rsiReader);

                    BinaryReader imageInfoReader = new BinaryReader(new MemoryStream(imageInfoBlock.Data));
                    imageInfoReader.ReadBytes(2);   // 06 05
                    byte unk3 = imageInfoReader.ReadByte();
                    byte mipmapCount = imageInfoReader.ReadByte();
                    byte[] unk4 = imageInfoReader.ReadBytes(4); // ???
                    byte[] unk5 = imageInfoReader.ReadBytes(4); // ???
                    int nameOffset = imageInfoReader.ReadInt32();

                    List<MipmapInfo> mipmaps = new List<MipmapInfo>();
                    for (int i = 0; i < mipmapCount; i++)
                    {
                        MipmapInfo m = new MipmapInfo
                        {
                            Start = imageInfoReader.ReadInt32() & 0x0FFFFFFF, // idk wtf the top byte is doing
                            Length = imageInfoReader.ReadInt32(),
                            Unk1 = imageInfoReader.ReadInt32(),
                            Unk2 = imageInfoReader.ReadInt32()
                        };

                        mipmaps.Add(m);
                    }


                    // TODO: Process palettes here.


                    // Read output image name.
                    imageInfoReader.BaseStream.Seek(nameOffset, SeekOrigin.Begin);
                    FileInfo imageFileInfo = new FileInfo(ReadNullTerminatedString(ref imageInfoReader));

                    // Find the associated SRDV file
                    string srdvPath = srdPath + "v";
                    if (!File.Exists(srdvPath))
                    {
                        Console.WriteLine("ERROR: No SRDV file found.");
                        return;
                    }

                    List<byte[]> images = new List<byte[]>();

                    // Uncomment this to remove all but the first mipmap (the main image)
                    if (removeMipmaps)
                        mipmaps.RemoveRange(1, mipmaps.Count - 1);


                    // Read image data based on mipmap info
                    for (int m = 0; m < mipmaps.Count; m++)
                    {
                        byte[] imageData;

                        using (BinaryReader srdvReader = new BinaryReader(new FileStream(srdvPath, FileMode.Open)))
                        {
                            srdvReader.BaseStream.Seek(mipmaps[m].Start, SeekOrigin.Begin);
                            imageData = srdvReader.ReadBytes(mipmaps[m].Length);

                            // TODO: Read palette data
                        }


                        // Determine pixel format
                        PixelDataFormat pixelFormat = PixelDataFormat.FormatBPTC;
                        switch (format)
                        {
                            case 0x0F:
                                pixelFormat = PixelDataFormat.FormatDXT1Rgb;
                                break;

                            case 0x11:
                                pixelFormat = PixelDataFormat.FormatDXT5;
                                break;

                            case 0x14:  // RGTC2 / BC5
                                pixelFormat = PixelDataFormat.FormatRGTC2;
                                break;

                            case 0x16:  // RCTC1 / BC4
                                pixelFormat = PixelDataFormat.FormatRGTC1;
                                break;

                            case 0x1C:
                                pixelFormat = PixelDataFormat.FormatBPTC;
                                break;
                        }

                        bool swizzled = ((swizzle & 1) == 0);

                        // Calculate mipmap dimensions
                        int mipWidth = (int)Math.Max(1, dispWidth / Math.Pow(2, m));
                        int mipHeight = (int)Math.Max(1, dispHeight / Math.Pow(2, m));

                        ImageBinary imageBinary = new ImageBinary(mipWidth, mipHeight, pixelFormat, imageData);
                        Bitmap tex = imageBinary.GetBitmap();


                        string mipmapName = imageFileInfo.Name;
                        if (m > 0)
                            mipmapName = mipmapName.Insert(mipmapName.Length - imageFileInfo.Extension.Length, string.Format(" ({0}x{1})", mipWidth.ToString(), mipHeight.ToString()));


                        FileStream imageOut = File.Create(mipmapName);
                        tex.Save(imageOut, System.Drawing.Imaging.ImageFormat.Bmp);
                        imageOut.Close();
                        Console.WriteLine("Sucessfully extracted texture data: {0}", mipmapName);
                    }
                }
            }
        }
        

        #region Static Functions
        public static Srd Open(string srdPath)
        {
            FileInfo srdInfo = new FileInfo(srdPath);
            if (!srdInfo.Exists)
            {
                Console.WriteLine("ERROR: Input file does not exist.");
                return null;
            }

            if (srdInfo.Extension.ToLower() != ".srd")
            {
                Console.WriteLine("ERROR: Input file does not have the \".srd\" extension.");
                return null;
            }
            

            BinaryReader srdReader = new BinaryReader(new FileStream(srdPath, FileMode.Open));
            Srd result = new Srd();

            // Read blocks
            while (srdReader.BaseStream.Position < srdReader.BaseStream.Length)
            {
                result.Blocks.Add(ReadBlock(ref srdReader));
            }

            return result;
        }

        private static SrdBlock ReadBlock(ref BinaryReader reader)
        {
            SrdBlock block = new SrdBlock();

            block.Type = new ASCIIEncoding().GetString(reader.ReadBytes(4));

            // Read raw data, then swap endianness
            byte[] b1 = reader.ReadBytes(4);
            Array.Reverse(b1);
            byte[] b2 = reader.ReadBytes(4);
            Array.Reverse(b2);
            byte[] b3 = reader.ReadBytes(4);
            Array.Reverse(b3);

            int dataLength = BitConverter.ToInt32(b1);
            int subdataLength = BitConverter.ToInt32(b2);
            int childCount = BitConverter.ToInt32(b3);

            if (dataLength > 0)
            {
                block.Data = reader.ReadBytes(dataLength);
                long padding = 16 - (dataLength % 16);
                if (padding != 16)
                    reader.BaseStream.Seek(padding, SeekOrigin.Current);
            }

            if (subdataLength > 0)
            {
                block.Subdata = reader.ReadBytes(subdataLength);
                long padding = 16 - (subdataLength % 16);
                if (padding != 16)
                    reader.BaseStream.Seek(padding, SeekOrigin.Current);
            }

            block.Children = new List<SrdBlock>();
            for (int i = 0; i < childCount; i++)
            {
                block.Children.Add(ReadBlock(ref reader));
            }

            return block;
        }

        // Annoyingly, there's no easy way to read a null-terminated ASCII string in .NET
        // (or maybe I'm just a moron), so we have to do it manually.
        private static string ReadNullTerminatedString(ref BinaryReader reader)
        {
            List<byte> rawString = new List<byte>();
            while (true)
            {
                byte b = reader.ReadByte();

                if (b == 0) break;

                rawString.Add(b);
            }
            string result = new ASCIIEncoding().GetString(rawString.ToArray());
            return result;
        }
        #endregion
    }

    struct SrdBlock
    {
        public string Type;
        public byte[] Data;
        public byte[] Subdata;
        public List<SrdBlock> Children;
    }

    struct MipmapInfo
    {
        public int Start;
        public int Length;
        public int Unk1;
        public int Unk2;
    }
}

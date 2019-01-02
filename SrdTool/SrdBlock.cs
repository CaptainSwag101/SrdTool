using Scarlet.Drawing;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;

namespace SrdTool
{
    class BlockHeader
    {
        public string BlockType;
        public int DataLength;
        public int SubdataLength;
        public int Unk; // Always 1 for $CFH blocks, 0 for everything else

        public BlockHeader(ref BinaryReader reader, string type)
        {
            BlockType = type;

            // Read raw data, then swap endianness
            byte[] b1 = reader.ReadBytes(4);
            Array.Reverse(b1);
            byte[] b2 = reader.ReadBytes(4);
            Array.Reverse(b2);
            byte[] b3 = reader.ReadBytes(4);
            Array.Reverse(b3);

            DataLength = BitConverter.ToInt32(b1, 0);
            SubdataLength = BitConverter.ToInt32(b2, 0);
            Unk = BitConverter.ToInt32(b3, 0);
        }
    }


    struct MipmapInfo
    {
        public int Start;
        public int Length;
        public int Unk1;
        public int Unk2;
    }


    abstract class Block
    { }


    class TxrBlock : Block
    {
        public BlockHeader Header;
        public int Unk1;
        public short Swizzle;
        public short DispWidth;
        public short DispHeight;
        public short Scanline;
        public byte Format;
        public byte Unk2;
        public byte Palette;
        public byte PaletteId;
        public RsiBlock ResourceInfo;

        public TxrBlock(ref BinaryReader reader)
        {
            Header = new BlockHeader(ref reader, "$TXR");

            Unk1 = reader.ReadInt32();   // 01 00 00 00
            Swizzle = reader.ReadInt16();
            DispWidth = reader.ReadInt16();
            DispHeight = reader.ReadInt16();
            Scanline = reader.ReadInt16();
            Format = reader.ReadByte();
            Unk2 = reader.ReadByte();
            Palette = reader.ReadByte();
            PaletteId = reader.ReadByte();

            BinaryReader rsiReader = new BinaryReader(new MemoryStream(reader.ReadBytes(Header.SubdataLength)));
            ResourceInfo = new RsiBlock(ref rsiReader);
        }

        public void ExtractImages(string srdvPath, bool extractMipmaps)
        {
            // Read image data based on resource info
            List<byte[]> images = new List<byte[]>();
            for (int m = 0; m < (extractMipmaps ? ResourceInfo.MipmapInfoList.Count : 1); m++)
            {
                byte[] imageData;

                using (BinaryReader srdvReader = new BinaryReader(new FileStream(srdvPath, FileMode.Open)))
                {
                    srdvReader.BaseStream.Seek(ResourceInfo.MipmapInfoList[m].Start, SeekOrigin.Begin);
                    imageData = srdvReader.ReadBytes(ResourceInfo.MipmapInfoList[m].Length);

                    // TODO: Read palette data
                }


                // Determine pixel format
                PixelDataFormat pixelFormat = PixelDataFormat.Undefined;
                switch (Format)
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

                bool swizzled = ((Swizzle & 1) == 0);

                // Calculate mipmap dimensions
                int mipWidth = (int)Math.Max(1, DispWidth / Math.Pow(2, m));
                int mipHeight = (int)Math.Max(1, DispHeight / Math.Pow(2, m));

                ImageBinary imageBinary = new ImageBinary(mipWidth, mipHeight, pixelFormat, imageData);
                Bitmap tex = imageBinary.GetBitmap();


                string mipmapName = ResourceInfo.OutputFilename;
                int extensionLength = mipmapName.Split('.').Last().Length + 1;

                if (m > 0)
                    mipmapName = mipmapName.Insert(mipmapName.Length - extensionLength, string.Format(" ({0}x{1})", mipWidth.ToString(), mipHeight.ToString()));
                
                
                ImageFormat imageFormat;
                switch (mipmapName.Split('.').Last().ToLower())
                {
                    case "bmp":
                        imageFormat = ImageFormat.Bmp;
                        break;

                    case "png":
                        imageFormat = ImageFormat.Png;
                        break;
                        
                    default:
                        imageFormat = ImageFormat.Png;
                        mipmapName += ".png";
                        break;
                }

                FileStream imageOut = File.Create(mipmapName);
                tex.Save(imageOut, imageFormat);
                imageOut.Close();
                Console.WriteLine("Sucessfully extracted texture data: {0}", mipmapName);
            }
        }
    }


    class RsiBlock : Block
    {
        public BlockHeader Header;
        public byte Unk1;   // 06
        public byte Unk2;   // 05
        public byte Unk3;
        public byte[] Unk4; // ???
        public byte[] Unk5; // ???
        public string OutputFilename;
        public List<MipmapInfo> MipmapInfoList;

        public RsiBlock(ref BinaryReader reader)
        {
            // Skip block type string, since we don't do that already because this is a nested block
            reader.ReadBytes(4);

            Header = new BlockHeader(ref reader, "$RSI");

            Unk1 = reader.ReadByte();
            Unk2 = reader.ReadByte();
            Unk3 = reader.ReadByte();
            byte mipmapCount = reader.ReadByte();
            Unk4 = reader.ReadBytes(4);
            Unk5 = reader.ReadBytes(4);
            int nameOffset = reader.ReadInt32();

            MipmapInfoList = new List<MipmapInfo>();
            for (int i = 0; i < mipmapCount; i++)
            {
                MipmapInfoList.Add(
                    new MipmapInfo
                    {
                        Start = reader.ReadInt32() & 0x0FFFFFFF, // idk wtf the top byte is doing
                        Length = reader.ReadInt32(),
                        Unk1 = reader.ReadInt32(),
                        Unk2 = reader.ReadInt32()
                    }
                );
            }


            // TODO: Process palettes here.


            // Read output image name.
            //reader.BaseStream.Seek(nameOffset, SeekOrigin.Begin);
            OutputFilename = ReadNullTerminatedString(ref reader);
        }

        // Annoyingly, there's no easy way to read a null-terminated ASCII string in .NET
        // (or maybe I'm just a moron), so we have to do it manually.
        private string ReadNullTerminatedString(ref BinaryReader reader)
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
    }


    class UnknownBlock : Block
    {
        public BlockHeader Header;
        public byte[] Data;
        public byte[] Subdata;

        public UnknownBlock(ref BinaryReader reader, string type)
        {
            Header = new BlockHeader(ref reader, type);

            if (Header.DataLength > 0)
            {
                Data = reader.ReadBytes(Header.DataLength);
                long padding = 16 - (Header.DataLength % 16);
                if (padding != 16)
                    reader.BaseStream.Seek(padding, SeekOrigin.Current);
            }

            if (Header.SubdataLength > 0)
            {
                Subdata = reader.ReadBytes(Header.SubdataLength);
                long padding = 16 - (Header.SubdataLength % 16);
                if (padding != 16)
                    reader.BaseStream.Seek(padding, SeekOrigin.Current);
            }
        }
    }
}

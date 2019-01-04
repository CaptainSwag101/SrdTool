using Scarlet.Drawing;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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

        public void WriteData(ref BinaryWriter writer)
        {
            writer.Write(new ASCIIEncoding().GetBytes(BlockType));

            // Swap endianness, then write raw data
            byte[] b1 = BitConverter.GetBytes(DataLength);
            Array.Reverse(b1);
            byte[] b2 = BitConverter.GetBytes(SubdataLength);
            Array.Reverse(b2);
            byte[] b3 = BitConverter.GetBytes(Unk);
            Array.Reverse(b3);

            writer.Write(b1);
            writer.Write(b2);
            writer.Write(b3);
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
    {
        public abstract void WriteData(ref BinaryWriter writer);
    }


    class TxrBlock : Block
    {
        public BlockHeader Header;
        public int Unk1;
        public short Swizzle;
        public short DispWidth;
        public short DispHeight;
        public short Scanline;
        public byte Format;
        public byte MipmapCount;
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
            MipmapCount = reader.ReadByte();
            Palette = reader.ReadByte();
            PaletteId = reader.ReadByte();

            BinaryReader rsiReader = new BinaryReader(new MemoryStream(reader.ReadBytes(Header.SubdataLength)));
            ResourceInfo = new RsiBlock(ref rsiReader);
            rsiReader.Close();
        }

        public override void WriteData(ref BinaryWriter writer)
        {
            // Adjust Header.SubdataLength based on the size of our RsiBlock
            Header.SubdataLength = 0x20 + ResourceInfo.Header.DataLength + (16 - (ResourceInfo.Header.DataLength % 16)) % 16;
            Header.WriteData(ref writer);

            writer.Write(BitConverter.GetBytes(Unk1));
            writer.Write(BitConverter.GetBytes(Swizzle));
            writer.Write(BitConverter.GetBytes(DispWidth));
            writer.Write(BitConverter.GetBytes(DispHeight));
            writer.Write(BitConverter.GetBytes(Scanline));
            writer.Write(Format);
            writer.Write(MipmapCount);
            writer.Write(Palette);
            writer.Write(PaletteId);

            ResourceInfo.WriteData(ref writer);

            // Somehow we lose the $CT0 block when we load the file,
            // so just generate a new one.
            int paddingLength = 16 - (int)(writer.BaseStream.Position % 16);
            if (paddingLength != 16)
            {
                byte[] padding = new byte[paddingLength];
                writer.Write(padding);
            }
            writer.Write(new ASCIIEncoding().GetBytes("$CT0"));
            byte[] zero = new byte[12];
            writer.Write(zero);
        }

        public void ExtractImages(string srdvPath, bool extractMipmaps)
        {
            // Read image data based on resource info
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

        public void ReplaceImages(string srdvPath, string replacementImagePath, bool generateMipmaps)
        {
            // NOTE: Since it would normally be a pain in the ass to carefully insert
            // our replacement texture data into the SRDV and shuffle all the existing
            // data around to ensure all other resource info isn't accidentally overwritten,
            // instead I'm just going to zero out the original texture data in the file,
            // and append our new texture data at the end of the SRDV file.

            // TODO: Make a second pass through the SRDV file afterward to
            // re -shuffle all our data around to reclaim the lost space.
            
            
            if (!File.Exists(replacementImagePath))
            {
                Console.WriteLine("ERROR: replacement image file does not exist.");
                return;
            }

            // Generate raw bitmap byte array in ARGB
            Bitmap raw = new Bitmap(File.OpenRead(replacementImagePath));

            // Regenerate image data based on resource info
            List<MipmapInfo> newMipmapInfoList = new List<MipmapInfo>();
            using (BinaryWriter srdvWriter = new BinaryWriter(File.OpenWrite(srdvPath)))
            {
                for (int m = 0; m < ResourceInfo.MipmapInfoList.Count; m++)
                {
                    // Make sure the old texture isn't somehow outside the file bounds
                    // (This can be caused by replacing a texture and then
                    // reverting the SRDV file but not the SRD file)
                    int fixedStart = (int)Math.Min(ResourceInfo.MipmapInfoList[m].Start, srdvWriter.BaseStream.Length);
                    srdvWriter.Seek(fixedStart, SeekOrigin.Begin);

                    // Zero out old texture data
                    byte[] zero = new byte[ResourceInfo.MipmapInfoList[m].Length];
                    srdvWriter.Write(zero);

                    // Seek to end of file
                    srdvWriter.Seek(0, SeekOrigin.End);

                    // If this is a regenerated mipmap or the main image, generate a resized bitmap to save
                    if (m == 0 || generateMipmaps)
                    {
                        Size resize = new Size(Math.Max(raw.Width / (int)Math.Pow(2, m), 1), Math.Max(raw.Height / (int)Math.Pow(2, m), 1));
                        Bitmap replacementImage = new Bitmap(raw, resize);

                        var bitmapData = replacementImage.LockBits(new Rectangle(0, 0, replacementImage.Width, replacementImage.Height), ImageLockMode.ReadOnly, replacementImage.PixelFormat);
                        var length = bitmapData.Stride * bitmapData.Height;
                        byte[] replacementImageData = new byte[length];

                        if (m == 0)
                            Scanline = (short)bitmapData.Stride;

                        // Copy bitmap to byte[], which seems to auto-swap from ARGB to BGRA
                        Marshal.Copy(bitmapData.Scan0, replacementImageData, 0, length);

                        // Uncomment this section if you need to manually swap from ARGB to BGRA
                        /*
                        for (int i = 0; i < length; i += 4)
                        {
                            byte[] swap = new byte[4];
                            Array.Copy(replacementImageData, i, swap, 0, 4);
                            Array.Reverse(swap);
                            Array.Copy(swap, 0, replacementImageData, i, 4);
                        }
                        */
                        replacementImage.UnlockBits(bitmapData);

                        // Create a new MipmapInfo to replace our old one
                        MipmapInfo replacementMipmapInfo = new MipmapInfo
                        {
                            Start = (int)srdvWriter.BaseStream.Position,
                            Length = replacementImageData.Length,
                            Unk1 = ResourceInfo.MipmapInfoList[m].Unk1,
                            Unk2 = ResourceInfo.MipmapInfoList[m].Unk2
                        };
                        newMipmapInfoList.Add(replacementMipmapInfo);

                        srdvWriter.Write(replacementImageData);

                        // TODO: Modify palette data


                        // Stop generating mipmaps smaller when we reach 1px by 1px
                        if (resize.Width == 1 && resize.Height == 1)
                            break;
                    }
                }
            }

            // Adjust the RsiBlock's header's data length
            ResourceInfo.Header.DataLength -= 16 * ResourceInfo.MipmapInfoList.Count;
            ResourceInfo.Header.DataLength += 16 * newMipmapInfoList.Count;

            // Replace old info with regenerated info
            ResourceInfo.MipmapInfoList = newMipmapInfoList;
            MipmapCount = (byte)ResourceInfo.MipmapInfoList.Count;
            DispWidth = (short)raw.Width;
            DispHeight = (short)raw.Height;
            Format = 0x01; // 32-bit BGRA

            raw.Dispose();
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

        public override void WriteData(ref BinaryWriter writer)
        {
            Header.WriteData(ref writer);

            writer.Write(Unk1);
            writer.Write(Unk2);
            writer.Write(Unk3);
            writer.Write((byte)MipmapInfoList.Count);
            writer.Write(Unk4);
            writer.Write(Unk5);
            writer.Write(BitConverter.GetBytes((int)(0x10 * (MipmapInfoList.Count + 1))));

            foreach (MipmapInfo mipmapInfo in MipmapInfoList)
            {
                writer.Write(BitConverter.GetBytes(mipmapInfo.Start | 0x40000000)); // This seems to be how vanilla files are?
                writer.Write(BitConverter.GetBytes(mipmapInfo.Length));
                writer.Write(BitConverter.GetBytes(mipmapInfo.Unk1));
                writer.Write(BitConverter.GetBytes(mipmapInfo.Unk2));
            }

            // Write file name
            writer.Write(new ASCIIEncoding().GetBytes(OutputFilename));
            // Write null terminator byte
            writer.Write((byte)0);
            // Pad with zeroes to nearest 16-byte boundary
            int paddingLength = 16 - (int)(writer.BaseStream.Position % 16);
            if (paddingLength != 16)
            {
                byte[] padding = new byte[paddingLength];
                writer.Write(padding);
            }
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

        public override void WriteData(ref BinaryWriter writer)
        {
            Header.WriteData(ref writer);
            
            if (Header.DataLength > 0 && Data != null)
            {
                writer.Write(Data);
                int paddingLength = 16 - (int)(writer.BaseStream.Position % 16);
                if (paddingLength != 16)
                {
                    byte[] padding = new byte[paddingLength];
                    writer.Write(padding);
                }
            }

            if (Header.SubdataLength > 0 && Subdata != null)
            {
                writer.Write(Subdata);
                int paddingLength = 16 - (int)(writer.BaseStream.Position % 16);
                if (paddingLength != 16)
                {
                    byte[] padding = new byte[paddingLength];
                    writer.Write(padding);
                }
            }
        }
    }
}

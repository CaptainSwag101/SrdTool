using Scarlet.Drawing;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SrdTool
{
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
            Utils.WritePadding(ref writer);

            // Somehow we lose the $CT0 block when we load the file,
            // so just generate a new one.
            writer.Write(new ASCIIEncoding().GetBytes("$CT0"));
            byte[] zero = new byte[12];
            writer.Write(zero);
        }

        public void ExtractImages(string srdvPath, string outputFolder, bool extractMipmaps)
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
                    case 0x01:
                        pixelFormat = PixelDataFormat.FormatArgb8888;
                        break;

                    case 0x02:
                        pixelFormat = PixelDataFormat.FormatBgr565;
                        break;

                    case 0x05:
                        pixelFormat = PixelDataFormat.FormatBgra4444;
                        break;

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

                if (ResourceInfo.Unk3 == 0x08)
                {
                    DispWidth = (short)Utils.PowerOfTwo(DispWidth);
                    DispHeight = (short)Utils.PowerOfTwo(DispHeight);
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

                FileStream imageOut = File.Create(outputFolder + '/' + mipmapName);
                tex.Save(imageOut, imageFormat);
                imageOut.Close();
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
            // re-shuffle all our data around to reclaim the lost space.


            if (!File.Exists(replacementImagePath))
            {
                Console.WriteLine("ERROR: Replacement image file does not exist.");
                return;
            }

            // Generate raw bitmap byte array in ARGB
            Bitmap raw = new Bitmap(File.OpenRead(replacementImagePath));

            // Regenerate image data based on resource info
            List<MipmapInfo> newMipmapInfoList = new List<MipmapInfo>();
            using (BinaryWriter srdvWriter = new BinaryWriter(File.OpenWrite(srdvPath)))
            {
                int m = 0;
                do
                {
                    // Make sure the old texture isn't somehow outside the file bounds
                    // (This can be caused by replacing a texture and then
                    // reverting the SRDV file but not the SRD file)
                    if (ResourceInfo.MipmapInfoList.Count >= m)
                    {
                        int fixedStart = (int)Math.Min(ResourceInfo.MipmapInfoList[m].Start, srdvWriter.BaseStream.Length);
                        srdvWriter.Seek(fixedStart, SeekOrigin.Begin);

                        // Zero out old texture data
                        byte[] zero = new byte[ResourceInfo.MipmapInfoList[m].Length];
                        srdvWriter.Write(zero);
                    }

                    // Seek to end of file
                    srdvWriter.Seek(0, SeekOrigin.End);

                    // Generate a resized bitmap to save
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

                    m++;
                } while (generateMipmaps);
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
}

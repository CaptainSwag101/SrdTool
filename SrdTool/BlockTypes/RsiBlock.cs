using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SrdTool
{
    struct MipmapInfo
    {
        public int Start;
        public int Length;
        public int Unk1;
        public int Unk2;
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
            OutputFilename = Utils.ReadNullTerminatedString(ref reader);
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
        }
    }
}

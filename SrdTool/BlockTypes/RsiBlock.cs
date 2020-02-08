using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SrdTool
{
    struct ResourceInfo
    {
        public int Unk1;    // Start/Offset
        public int Unk2;    // Length?
        public int Unk3;
        public int Unk4;
    }

    // TODO: This block actually seems to be a generic container for data,
    // not just texture/mipmap info. It seems like Unk3 might determine the
    // specific type of data or the layout?
    class RsiBlock : Block
    {
        public BlockHeader Header;
        public byte Unk1;   // 06, EXCEPT NOPE APPARENTLY IT CAN ALSO BE 04
        public byte Unk2;   // 05
        public byte Unk3;   // Layout/Data Type?
        public byte Unk4;   // when Unk3 == 04, this contains the resourceCount instead of Unk5?
        public int Unk5;    // when Unk3 == FF, this contains the resourceCount instead of Unk4?
        public int Unk6;    // Size of ResourceInfoList2 entries?
        public int Unk7;    // Offset of string data?
        public List<ResourceInfo> ResourceInfoList1;
        public List<byte[]> ResourceInfoList2;
        public List<string> StringData;

        public RsiBlock(ref BinaryReader reader)
        {
            // Skip block type string, since we don't do that already because this is a nested block
            reader.ReadBytes(4);

            Header = new BlockHeader(ref reader, "$RSI");

            long startPosition = reader.BaseStream.Position;

            Unk1 = reader.ReadByte();
            Unk2 = reader.ReadByte();
            Unk3 = reader.ReadByte();
            Unk4 = reader.ReadByte();
            Unk5 = reader.ReadInt32();
            Unk6 = reader.ReadInt32();
            Unk7 = reader.ReadInt32();
            
            // Read the primary resource info table
            ResourceInfoList1 = new List<ResourceInfo>();
            for (int i = 0; i < (Unk3 == 0xFF ? Unk5 : Unk4); i++)
            {
                ResourceInfoList1.Add(
                    new ResourceInfo
                    {
                        Unk1 = reader.ReadInt32(),
                        Unk2 = reader.ReadInt32(),
                        Unk3 = reader.ReadInt32(),
                        Unk4 = reader.ReadInt32()
                    }
                );
            }
            
            // If present, read the secondary resource info table
            if (Unk6 > 0)
            {
                ResourceInfoList2 = new List<byte[]>();
                for (int i = 0; i < (Unk3 == 0xFF ? Unk5 : Unk4); i++)
                {
                    byte[] entry = reader.ReadBytes(Unk6);
                    ResourceInfoList2.Add(entry);
                }
            }

            // Read string data
            reader.BaseStream.Seek(startPosition + Unk7, SeekOrigin.Begin);
            StringData = new List<string>();
            while (reader.BaseStream.Position < (startPosition + Header.DataLength))
            {
                string str = Utils.ReadNullTerminatedString(ref reader);

                if (string.IsNullOrEmpty(str))
                    break;

                StringData.Add(str);
            }
        }

        public override void WriteData(ref BinaryWriter writer)
        {
            throw new NotImplementedException();

            Header.WriteData(ref writer);

            writer.Write(Unk1);
            writer.Write(Unk2);
            writer.Write(Unk3);
            writer.Write((byte)ResourceInfoList1.Count);
            writer.Write(Unk5);
            writer.Write(Unk6);
            writer.Write(BitConverter.GetBytes((int)(0x10 * (ResourceInfoList1.Count + 1))));

            foreach (ResourceInfo mipmapInfo in ResourceInfoList1)
            {
                writer.Write(BitConverter.GetBytes(mipmapInfo.Unk1 | 0x40000000)); // This seems to be how vanilla files are?
                writer.Write(BitConverter.GetBytes(mipmapInfo.Unk2));
                writer.Write(BitConverter.GetBytes(mipmapInfo.Unk3));
                writer.Write(BitConverter.GetBytes(mipmapInfo.Unk4));
            }

            // Write file name
            writer.Write(new ASCIIEncoding().GetBytes(StringData.First()));
            // Write null terminator byte
            writer.Write((byte)0);
        }
    }
}

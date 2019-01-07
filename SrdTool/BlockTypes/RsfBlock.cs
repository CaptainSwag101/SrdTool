using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SrdTool
{
    class RsfBlock : Block
    {
        public BlockHeader Header;
        public byte[] Unk1;
        public byte[] Unk2;
        public byte[] Unk3;
        public byte[] Unk4;
        public string Name;

        public RsfBlock(ref BinaryReader reader)
        {
            Header = new BlockHeader(ref reader, "$RSF");

            // TODO: Actually verify that these values are always the same
            Unk1 = reader.ReadBytes(4); // 10 00 00 00 ???
            Unk2 = reader.ReadBytes(4); // FB DB 32 01 ???
            Unk3 = reader.ReadBytes(4); // 41 DC 32 01 ???
            Unk4 = reader.ReadBytes(4); // 00 00 00 00 ???
            Name = Utils.ReadNullTerminatedString(ref reader);
        }

        public override void WriteData(ref BinaryWriter writer)
        {
            Header.WriteData(ref writer);
            writer.Write(Unk1);
            writer.Write(Unk2);
            writer.Write(Unk3);
            writer.Write(Unk4);
            writer.Write(new ASCIIEncoding().GetBytes(Name));
            writer.Write((byte)0);
        }
    }
}

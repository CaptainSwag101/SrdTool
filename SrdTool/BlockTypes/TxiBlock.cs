using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SrdTool
{
    class TxiBlock : Block
    {
        public BlockHeader Header;
        public int Unk1;
        public int Unk2;
        public int Unk3;
        public byte[] Unk4; // 01 01 01 01 ???
        public int Unk5;
        public string OutputFilename;
        public RsiBlock ResourceBlock;

        public TxiBlock(ref BinaryReader reader)
        {
            Header = new BlockHeader(ref reader, "$TXI");
            Unk1 = reader.ReadInt32();
            Unk2 = reader.ReadInt32();
            Unk3 = reader.ReadInt32();
            Unk4 = reader.ReadBytes(4);
            Unk5 = reader.ReadInt32();
            OutputFilename = Utils.ReadNullTerminatedString(ref reader);

            Utils.ReadPadding(ref reader);

            ResourceBlock = new RsiBlock(ref reader);
        }

        public override void WriteData(ref BinaryWriter writer)
        {
            throw new NotImplementedException();
        }
    }
}

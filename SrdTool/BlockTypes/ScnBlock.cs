using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SrdTool
{
    class ScnBlock : Block
    {
        public BlockHeader Header;
        public byte[] Data;
        public RsiBlock ResourceBlock;

        public ScnBlock(ref BinaryReader reader)
        {
            Header = new BlockHeader(ref reader, "$SCN");
            Data = reader.ReadBytes(Header.DataLength);

            Utils.ReadPadding(ref reader);

            ResourceBlock = new RsiBlock(ref reader);
        }

        public override void WriteData(ref BinaryWriter writer)
        {
            throw new NotImplementedException();
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SrdTool
{
    class MshBlock : Block
    {
        public BlockHeader Header;
        public byte[] Data;
        public RsiBlock ResourceBlock;

        public MshBlock(ref BinaryReader reader)
        {
            Header = new BlockHeader(ref reader, "$MSH");
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

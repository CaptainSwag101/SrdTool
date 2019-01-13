using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SrdTool
{
    class Ct0Block : Block
    {
        public BlockHeader Header;

        public Ct0Block(ref BinaryReader reader)
        {
            Header = new BlockHeader(ref reader, "$CT0");
        }

        public override void WriteData(ref BinaryWriter writer)
        {
            throw new NotImplementedException();
        }
    }
}

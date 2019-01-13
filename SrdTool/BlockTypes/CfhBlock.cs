using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SrdTool
{
    class CfhBlock : Block
    {
        public BlockHeader Header;

        public CfhBlock(ref BinaryReader reader)
        {
            Header = new BlockHeader(ref reader, "$CFH");
        }

        public override void WriteData(ref BinaryWriter writer)
        {
            throw new NotImplementedException();
        }
    }
}

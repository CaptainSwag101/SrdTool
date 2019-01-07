using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SrdTool
{
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
                Utils.ReadPadding(ref reader);
            }

            if (Header.SubdataLength > 0)
            {
                Subdata = reader.ReadBytes(Header.SubdataLength);
                Utils.ReadPadding(ref reader);
            }
        }

        public override void WriteData(ref BinaryWriter writer)
        {
            Header.WriteData(ref writer);

            if (Header.DataLength > 0 && Data != null)
            {
                writer.Write(Data);
                Utils.WritePadding(ref writer);
            }

            if (Header.SubdataLength > 0 && Subdata != null)
            {
                writer.Write(Subdata);
                Utils.WritePadding(ref writer);
            }
        }
    }
}

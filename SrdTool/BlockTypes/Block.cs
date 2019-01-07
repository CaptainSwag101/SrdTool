using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SrdTool
{
    class BlockHeader
    {
        public string BlockType;
        public int DataLength;
        public int SubdataLength;
        public int Unk; // Always 1 for $CFH blocks, 0 for everything else

        public BlockHeader(ref BinaryReader reader, string type)
        {
            BlockType = type;

            // Read raw data, then swap endianness
            byte[] b1 = reader.ReadBytes(4);
            Array.Reverse(b1);
            byte[] b2 = reader.ReadBytes(4);
            Array.Reverse(b2);
            byte[] b3 = reader.ReadBytes(4);
            Array.Reverse(b3);

            DataLength = BitConverter.ToInt32(b1, 0);
            SubdataLength = BitConverter.ToInt32(b2, 0);
            Unk = BitConverter.ToInt32(b3, 0);
        }

        public void WriteData(ref BinaryWriter writer)
        {
            writer.Write(new ASCIIEncoding().GetBytes(BlockType));

            // Swap endianness, then write raw data
            byte[] b1 = BitConverter.GetBytes(DataLength);
            Array.Reverse(b1);
            byte[] b2 = BitConverter.GetBytes(SubdataLength);
            Array.Reverse(b2);
            byte[] b3 = BitConverter.GetBytes(Unk);
            Array.Reverse(b3);

            writer.Write(b1);
            writer.Write(b2);
            writer.Write(b3);
        }
    }

    abstract class Block
    {
        public abstract void WriteData(ref BinaryWriter writer);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SrdTool
{
    struct TreeEntry
    {
        public int StringOffset;
        public int Unk1;            // Offset of endpoint entry info?
        public int Unk2;            // ???
        public int Unk3;            // ???
    }

    struct TreeEndpoint
    {
        public int StringOffset;
        public int Unk1;            // ???
    }

    class TreBlock : Block
    {
        public BlockHeader Header;
        public int Unk1;    // Maximum tree depth?
        public short Unk2;  // Tree string count
        public short Unk3;  // Non-endpoint entry count?
        public short Unk4;  // Endpoint entry count?
        public short Unk5;  // ???
        public int Unk6;    // Offset of UnkTree data?
        public List<TreeEntry> TreeEntries;
        public List<TreeEndpoint> TreeEndpoints;
        public List<List<int>> UnkTree;
        public List<string> TreeStrings;
        public RsiBlock ResourceBlock;

        public TreBlock(ref BinaryReader reader)
        {
            Header = new BlockHeader(ref reader, "$TRE");

            long startPosition = reader.BaseStream.Position;

            Unk1 = reader.ReadInt32();
            Unk2 = reader.ReadInt16();
            Unk3 = reader.ReadInt16();
            Unk4 = reader.ReadInt16();
            Unk5 = reader.ReadInt16();
            Unk6 = reader.ReadInt32();

            // Read non-endpoint tree entries
            TreeEntries = new List<TreeEntry>();
            for (int i = 0; i < Unk3; i++)
            {
                TreeEntries.Add(new TreeEntry
                {
                    StringOffset = reader.ReadInt32(),
                    Unk1 = reader.ReadInt32(),
                    Unk2 = reader.ReadInt32(),
                    Unk3 = reader.ReadInt32(),
                });
            }

            // Read endpoint tree entries
            TreeEndpoints = new List<TreeEndpoint>();
            for (int i = 0; i < Unk4; i++)
            {
                TreeEndpoints.Add(new TreeEndpoint
                {
                    StringOffset = reader.ReadInt32(),
                    Unk1 = reader.ReadInt32()
                });
            }

            // Read unknown (tree structure?) data
            UnkTree = new List<List<int>>();
            for (int i = 0; i < Unk1; i++)
            {
                List<int> branch = new List<int>();
                for (int j = 0; j < (Unk3 + 1); j++)
                {
                    branch.Add(reader.ReadInt32());
                }
                UnkTree.Add(branch);
            }

            // Read the final entry in the unknown data
            List<int> lastBranch = new List<int>();
            lastBranch.Add(reader.ReadInt32());
            UnkTree.Add(lastBranch);


            // Read tree strings
            TreeStrings = new List<string>();
            for (int s = 0; s < (Unk3 + Unk5); s++)
            {
                string str = Utils.ReadNullTerminatedString(ref reader);
                TreeStrings.Add(str);
            }

            Utils.ReadPadding(ref reader);

            ResourceBlock = new RsiBlock(ref reader);
        }

        public override void WriteData(ref BinaryWriter writer)
        {
            throw new NotImplementedException();
        }
    }
}

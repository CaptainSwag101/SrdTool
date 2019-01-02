using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using Scarlet.Drawing;

namespace SrdTool
{
    class Srd
    {
        public string Filepath;
        public List<Block> Blocks;

        public static Srd FromFile(string srdPath)
        {
            Srd result = new Srd();

            FileInfo srdInfo = new FileInfo(srdPath);
            if (!srdInfo.Exists)
            {
                Console.WriteLine("ERROR: Input file does not exist.");
                return null;
            }

            if (srdInfo.Extension.ToLower() != ".srd")
            {
                Console.WriteLine("ERROR: Input file does not have the \".srd\" extension.");
                return null;
            }

            result.Filepath = srdPath;


            BinaryReader reader = new BinaryReader(new FileStream(srdPath, FileMode.Open));
            result.Blocks = new List<Block>();

            // Read blocks
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                Block block;

                string blockType = new ASCIIEncoding().GetString(reader.ReadBytes(4));
                switch (blockType)
                {
                    case "$TXR":
                        block = new TxrBlock(ref reader);
                        break;

                    default:
                        block = new UnknownBlock(ref reader, blockType);
                        break;
                }

                result.Blocks.Add(block);
            }

            return result;
        }

        public void ExtractImages(bool extractMipmaps = false)
        {
            Console.WriteLine("Searching for texture data in {0}:", Filepath);

            string srdvPath = Filepath + 'v';
            if (!File.Exists(srdvPath))
            {
                Console.WriteLine("ERROR: No SRDV file found.");
                return;
            }

            // Iterate through blocks and extract image data
            foreach (Block block in Blocks)
            {
                if (block is TxrBlock)
                    ((TxrBlock)block).ExtractImages(srdvPath, extractMipmaps);
            }
        }
    }
}

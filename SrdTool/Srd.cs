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
        public RsfBlock ResourceFolder;
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
                    case "$CFH":
                        block = new CfhBlock(ref reader);
                        break;

                    case "$CT0":
                        block = new Ct0Block(ref reader);
                        break;

                    case "$RSF":
                        block = new RsfBlock(ref reader);
                        result.ResourceFolder = (RsfBlock)block;
                        break;

                    case "$RSI":
                        block = new RsiBlock(ref reader);
                        break;

                    case "$TRE":
                        block = new TreBlock(ref reader);
                        break;

                    case "$TXI":
                        block = new TxiBlock(ref reader);
                        break;

                    case "$TXR":
                        block = new TxrBlock(ref reader);
                        break;

                    case "$VTX":
                        block = new VtxBlock(ref reader);
                        break;

                    default:
                        block = new UnknownBlock(ref reader, blockType);
                        break;
                }
                result.Blocks.Add(block);

                Utils.ReadPadding(ref reader);
            }

            reader.Close();
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

            // Create the folder given by the RSF block and extract images
            // into it instead of into the program's root directory
            Directory.CreateDirectory(ResourceFolder.Name);

            // Iterate through blocks and extract image data
            int txrIndex = 0;
            foreach (Block block in Blocks)
            {
                if (block is TxrBlock)
                {
                    Console.WriteLine(string.Format("Extracting texture index {0}: {1}", txrIndex++, ((TxrBlock)block).ResourceBlock.StringData));
                    ((TxrBlock)block).ExtractImages(srdvPath, ResourceFolder.Name, extractMipmaps);
                }
            }
        }

        public void ReplaceImages(string replacementImagePath, int indexToReplace, bool generateMipmaps)
        {
            Console.WriteLine("Searching for texture data in {0}:", Filepath);

            string srdvPath = Filepath + 'v';
            if (!File.Exists(srdvPath))
            {
                Console.WriteLine("ERROR: No SRDV file found.");
                return;
            }

            // Iterate through the TXR blocks and replace the requested block
            int txrIndex = 0;
            foreach (Block block in Blocks)
            {
                if (block is TxrBlock)
                {
                    if (txrIndex == indexToReplace)
                    {
                        Console.WriteLine(string.Format("Replacing texture index {0}: {1}", txrIndex, ((TxrBlock)block).ResourceBlock.StringData));
                        ((TxrBlock)block).ReplaceImages(srdvPath, replacementImagePath, generateMipmaps);
                    }
                    else
                    {
                        Console.WriteLine(string.Format("Skipping texture index {0}: {1}", txrIndex, ((TxrBlock)block).ResourceBlock.StringData));
                    }
                    txrIndex++;
                }
            }

            // Save the SDR file
            File.Delete(Filepath);
            BinaryWriter srdWriter = new BinaryWriter(File.OpenWrite(Filepath));
            foreach (Block block in Blocks)
            {
                block.WriteData(ref srdWriter);
                Utils.WritePadding(ref srdWriter);
            }
            srdWriter.Close();
        }
    }
}

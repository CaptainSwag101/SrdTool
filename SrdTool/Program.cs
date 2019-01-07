using System;

namespace SrdTool
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("SRD Tool by CaptainSwag101\n" +
                "Version 0.0.5, built on 2019/01/06\n");

            if (args.Length == 0)
            {
                Console.WriteLine("ERROR: No input file specified.");
                // Display usage info
                Console.WriteLine("Usage: SrdTool.exe <Input SRD file> [replacement PNG file] [texture ID to replace] [generate mipmaps (true/false)]");
                return;
            }


            Srd srd = Srd.FromFile(args[0]);
            if (srd == null) return;


            if (args.Length == 1)
            {
                srd.ExtractImages();
            }
            else if (args.Length > 1 && args.Length < 5)
            {
                if (!int.TryParse(args[2], out int indexToReplace))
                    indexToReplace = 0;
                if (!bool.TryParse(args[2], out bool generateMipmaps))
                    generateMipmaps = true;

                srd.ReplaceImages(args[1], indexToReplace, generateMipmaps);
            }
            else
            {
                // Display usage info
                Console.WriteLine("Usage: SrdTool.exe <Input SRD file> [replacement PNG file] [texture ID to replace] [generate mipmaps (true/false)]");
            }
        }
    }
}

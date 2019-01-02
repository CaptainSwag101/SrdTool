using System;

namespace SrdTool
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("SRD Tool by CaptainSwag101\n" +
                "Version 0.0.2, built on 2019/01/01\n");

            if (args.Length == 0)
            {
                Console.WriteLine("ERROR: No input file specified.");
                return;
            }

            Srd srd = Srd.FromFile(args[0]);

            if (srd == null)
                return;

            if (args.Length == 1)
            {
                srd.ExtractImages();
            }
            else if (args.Length == 3)
            {
                //srd.ReplaceImage(args[0], int.Parse(args[1]), args[2]);
            }
        }
    }
}

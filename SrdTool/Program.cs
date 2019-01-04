using System;

namespace SrdTool
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("SRD Tool by CaptainSwag101\n" +
                "Version 0.0.4, built on 2019/01/04\n");

            if (args.Length == 0)
            {
                Console.WriteLine("ERROR: No input file specified.");
                return;
            }


            Srd srd = Srd.FromFile(args[0]);
            if (srd == null) return;


            if (args.Length == 1)
            {
                srd.ExtractImages();
            }
            else if (args.Length == 4)
            {
                srd.ReplaceImages(args[1], int.Parse(args[2]), bool.Parse(args[3]));
            }
        }
    }
}

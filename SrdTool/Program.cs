using System;

namespace SrdTool
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("SRD Tool by CaptainSwag101\n" +
                "Version 0.0.2, built on 2019/01/01\n");

            Srd srd = new Srd(args[0]);
            if (args.Length == 1)
            {
                srd.ExtractImages();
            }
            else if (args.Length == 3)
            {
                //srd.ReplaceImage(args[0], int.Parse(args[1]), args[2]);
            }

            Console.Read();
        }
    }
}

using System;

namespace SrdTool
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("SRD Tool by CaptainSwag101\n" +
                "Version 0.0.1, built on 2018/12/30\n");

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

using System;

namespace SrdTool
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("SRD Tool by CaptainSwag101\n" +
                "Version 0.0.1, built on 2018/12/30\n");

            Srd srd = Srd.Open(args[0]);
            if (args.Length == 1)
            {
                srd.ExtractImages(args[0]);
            }
            else if (args.Length == 3)
            {
                //srd.ReplaceImage(args[0], int.Parse(args[1]), args[2]);
            }

            Console.Read();
        }
    }
}

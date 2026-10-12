using System;

namespace U2dos
{
    static class Program
    {
        static int Main(string[] args)
        {
            try { return Converter.Run(args, Console.Error); }
            catch (Exception e)
            {
                Console.Error.WriteLine("Chyba: " + e.Message);
                return ExitCodes.IoError;
            }
        }
    }
}

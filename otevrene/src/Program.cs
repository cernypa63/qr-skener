using System;

namespace Otevrene
{
    internal static class Program
    {
        private static int Main(string[] args) =>
            Aplikace.Spust(args, Zdroje.Nacti, Console.Out);
    }
}

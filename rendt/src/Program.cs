using System;

namespace RenDt
{
    internal static class Program
    {
        private static int Main(string[] args) =>
            Aplikace.Spust(args, AppDomain.CurrentDomain.BaseDirectory, () => DateTime.Now, Console.Out);
    }
}

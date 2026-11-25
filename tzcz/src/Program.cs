using System;

namespace TzCz
{
    internal static class Program
    {
        private static int Main(string[] args) =>
            Aplikace.Spust(args, () => DateTime.UtcNow, Console.Out);
    }
}

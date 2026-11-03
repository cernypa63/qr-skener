using System;

namespace Seet2nd
{
    internal static class Program
    {
        private static int Main(string[] args) =>
            Aplikace.Spust(args, AppDomain.CurrentDomain.BaseDirectory, new EetKlient());
    }
}

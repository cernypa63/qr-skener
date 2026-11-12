using System;

namespace SendFtp
{
    internal static class Program
    {
        private static int Main(string[] args) =>
            Aplikace.Spust(args, AppDomain.CurrentDomain.BaseDirectory, new Dpapi(), new KonzoleDotazy());
    }
}

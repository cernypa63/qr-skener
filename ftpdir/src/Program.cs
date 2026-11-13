using System;

namespace FtpDir
{
    internal static class Program
    {
        private static int Main(string[] args) =>
            Aplikace.Spust(args, AppDomain.CurrentDomain.BaseDirectory);
    }
}

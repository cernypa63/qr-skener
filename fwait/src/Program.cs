using System;

namespace Fwait
{
    static class Program
    {
        static int Main(string[] args)
        {
            try { return FileWaiter.Run(args); }
            catch (Exception) { return ExitCodes.CopyFailed; }
        }
    }
}

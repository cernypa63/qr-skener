using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Fwait.Tests
{
    public sealed class FileWaiterTests : IDisposable
    {
        readonly string dir = Path.Combine(Path.GetTempPath(), "fwait-test-" + Guid.NewGuid());

        public FileWaiterTests() => Directory.CreateDirectory(dir);
        public void Dispose() => Directory.Delete(dir, true);

        string P(string name) => Path.Combine(dir, name);

        [Fact]
        public void CopiesExistingFileImmediately()
        {
            File.WriteAllText(P("a.pdf"), "obsah");
            var sw = Stopwatch.StartNew();
            Assert.Equal(ExitCodes.Ok, FileWaiter.Run(new[] { P("a.pdf"), "10", P("out/b.pdf") }));
            Assert.True(sw.ElapsedMilliseconds < 2000);
            Assert.Equal("obsah", File.ReadAllText(P("out/b.pdf")));
            Assert.True(File.Exists(P("a.pdf")));
        }

        [Fact]
        public void WaitsForFileThatAppearsLater()
        {
            var writer = Task.Run(async () => { await Task.Delay(600); File.WriteAllText(P("late.txt"), "pozde"); });
            Assert.Equal(ExitCodes.Ok, FileWaiter.Run(new[] { P("late.txt"), "5", P("copy.txt") }));
            writer.Wait();
            Assert.Equal("pozde", File.ReadAllText(P("copy.txt")));
        }

        [Fact]
        public void TimeoutWritesErrorIntoTarget()
        {
            var sw = Stopwatch.StartNew();
            Assert.Equal(ExitCodes.Timeout, FileWaiter.Run(new[] { P("nic.pdf"), "1", P("cil.pdf") }));
            Assert.InRange(sw.ElapsedMilliseconds, 900, 3000);
            string text = File.ReadAllText(P("cil.pdf"));
            Assert.StartsWith("Chyba: soubor ", text);
            Assert.Contains("se do 1 s neobjevil", text);
        }

        [Fact]
        public void OverwritesExistingTarget()
        {
            File.WriteAllText(P("cil.txt"), "stara chyba");
            File.WriteAllText(P("src.txt"), "novy");
            Assert.Equal(ExitCodes.Ok, FileWaiter.Run(new[] { P("src.txt"), "0", P("cil.txt") }));
            Assert.Equal("novy", File.ReadAllText(P("cil.txt")));
        }

        [Fact]
        public void DirectoryTargetKeepsFileName()
        {
            Directory.CreateDirectory(P("slozka"));
            File.WriteAllText(P("sken.jpg"), "x");
            Assert.Equal(ExitCodes.Ok, FileWaiter.Run(new[] { P("sken.jpg"), "1", P("slozka") }));
            Assert.True(File.Exists(P("slozka/sken.jpg")));
        }

        [Fact]
        public void InvalidSecondsWritesError()
        {
            Assert.Equal(ExitCodes.Usage, FileWaiter.Run(new[] { P("a"), "abc", P("err.txt") }));
            Assert.Contains("neplatný počet sekund", File.ReadAllText(P("err.txt")));
        }

        [Fact]
        public void WrongArgumentCount() => Assert.Equal(ExitCodes.Usage, FileWaiter.Run(new[] { "a", "1" }));

        [Theory]
        [InlineData("30", 30)]
        [InlineData("1,5", 1.5)]
        [InlineData("0.5", 0.5)]
        public void ParsesSeconds(string s, double expected)
        {
            Assert.True(FileWaiter.TryParseSeconds(s, out double v));
            Assert.Equal(expected, v);
        }

        [Theory]
        [InlineData("-1")]
        [InlineData("")]
        [InlineData("1e3")]
        public void RejectsSeconds(string s) => Assert.False(FileWaiter.TryParseSeconds(s, out _));
    }
}

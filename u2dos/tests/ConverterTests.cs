using System;
using System.IO;
using System.Text;
using Xunit;

namespace U2dos.Tests
{
    public sealed class ConverterTests : IDisposable
    {
        readonly string dir = Path.Combine(Path.GetTempPath(), "u2dos-test-" + Guid.NewGuid());

        public ConverterTests()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Directory.CreateDirectory(dir);
        }

        public void Dispose() => Directory.Delete(dir, true);

        string P(string name) => Path.Combine(dir, name);

        byte[] ConvertBytes(byte[] input)
        {
            File.WriteAllBytes(P("in.txt"), input);
            Assert.Equal(ExitCodes.Ok, Converter.Run(new[] { P("in.txt"), P("out.txt") }, TextWriter.Null));
            return File.ReadAllBytes(P("out.txt"));
        }

        byte[] ConvertText(string utf8) => ConvertBytes(Encoding.UTF8.GetBytes(utf8));

        [Fact]
        public void CzechLettersToCp852()
        {
            // ž=A7 š=E7 č=9F ř=FD Ž=A6 Š=E6 Č=AC Ř=FC ů=85 ú=A3 á=A0 é=82 í=A1 ý=EC ó=A2 ě=D8 ď=D4 ť=9C ň=E5
            byte[] expected = { 0xA7, 0xE7, 0x9F, 0xFD, 0xA6, 0xE6, 0xAC, 0xFC, 0x85, 0xA3, 0xA0, 0x82, 0xA1, 0xEC, 0xA2, 0xD8, 0xD4, 0x9C, 0xE5 };
            Assert.Equal(expected, ConvertText("žščřŽŠČŘůúáéíýóěďťň"));
        }

        [Fact]
        public void LfBecomesCrLf() => Assert.Equal(Encoding.ASCII.GetBytes("a\r\nb\r\n\r\nc"), ConvertText("a\nb\n\nc"));

        [Fact]
        public void ExistingCrLfIsKept() => Assert.Equal(Encoding.ASCII.GetBytes("a\r\nb\r\n"), ConvertText("a\r\nb\n"));

        [Fact]
        public void BomIsDropped()
        {
            var input = new byte[] { 0xEF, 0xBB, 0xBF, (byte)'x', (byte)'\n' };
            Assert.Equal(Encoding.ASCII.GetBytes("x\r\n"), ConvertBytes(input));
        }

        [Fact]
        public void TypographicCharactersSubstituted() =>
            Assert.Equal(Encoding.ASCII.GetBytes("\"a\" 'b' 1-2 ... 5 EUR ?"), ConvertText("„a“ ‚b‘ 1–2 … 5 € 😀"));

        [Fact]
        public void CrLfAcrossBufferBoundary()
        {
            string text = new string('x', 65535) + "\r\n" + "y\n";
            byte[] result = ConvertText(text);
            Assert.Equal(65535 + 2 + 3, result.Length);
            Assert.Equal(Encoding.ASCII.GetBytes("\r\ny\r\n"), result[65535..]);
        }

        [Fact]
        public void MissingInput()
        {
            var err = new StringWriter();
            Assert.Equal(ExitCodes.InputMissing, Converter.Run(new[] { P("nic.txt"), P("out.txt") }, err));
            Assert.Contains("neexistuje", err.ToString());
            Assert.False(File.Exists(P("out.txt")));
        }

        [Fact]
        public void WrongArgumentCount() => Assert.Equal(ExitCodes.Usage, Converter.Run(new[] { "a" }, TextWriter.Null));

        [Fact]
        public void SameInputAndOutputRejected()
        {
            File.WriteAllText(P("a.txt"), "x");
            Assert.Equal(ExitCodes.Usage, Converter.Run(new[] { P("a.txt"), P("a.txt") }, TextWriter.Null));
            Assert.Equal("x", File.ReadAllText(P("a.txt")));
        }

        [Fact]
        public void CreatesOutputDirectory()
        {
            File.WriteAllText(P("a.txt"), "x\n");
            Assert.Equal(ExitCodes.Ok, Converter.Run(new[] { P("a.txt"), P("sub/b.txt") }, TextWriter.Null));
            Assert.True(File.Exists(P("sub/b.txt")));
        }
    }
}

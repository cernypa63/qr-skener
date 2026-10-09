using System;
using System.IO;
using Xunit;

namespace Ascan.Tests
{
    public class CliTests
    {
        [Fact]
        public void NoArgumentsSelectsScanner()
        {
            var o = Cli.Parse(new string[0]);
            Assert.Null(o.Error);
            Assert.Equal(Command.Select, o.Command);
        }

        [Theory]
        [InlineData("-h")]
        [InlineData("/?")]
        [InlineData("--help")]
        public void HelpSwitches(string arg) => Assert.Equal(Command.Help, Cli.Parse(new[] { arg }).Command);

        [Fact]
        public void ListSwitch() => Assert.Equal(Command.List, Cli.Parse(new[] { "-l" }).Command);

        [Fact]
        public void FileWithOptionsScans()
        {
            var o = Cli.Parse(new[] { "sken.png", "-d", "200", "-c", "seda" });
            Assert.Null(o.Error);
            Assert.Equal(Command.Scan, o.Command);
            Assert.Equal("sken.png", o.OutputPath);
            Assert.Equal(200, o.Dpi);
            Assert.Equal(ColorMode.Gray, o.Color);
        }

        [Fact]
        public void DefaultScanOptions()
        {
            var o = Cli.Parse(new[] { "a.jpg" });
            Assert.Equal(300, o.Dpi);
            Assert.Equal(ColorMode.Color, o.Color);
        }

        [Theory]
        [InlineData("-x")]
        [InlineData("-d")]
        [InlineData("-d", "abc")]
        [InlineData("-d", "10")]
        [InlineData("-c", "modra")]
        [InlineData("a.jpg", "b.jpg")]
        [InlineData("a.jpg", "-l")]
        public void InvalidArguments(params string[] args) => Assert.NotNull(Cli.Parse(args).Error);

        [Fact]
        public void OutputPathGetsJpgWhenNoExtension()
        {
            string path = Cli.ResolveOutputPath("doklad", out string format);
            Assert.EndsWith("doklad.jpg", path);
            Assert.True(Path.IsPathRooted(path));
            Assert.Equal(ImageFormats.Jpeg, format);
        }

        [Theory]
        [InlineData("a.PNG", ImageFormats.Png)]
        [InlineData("a.tiff", ImageFormats.Tiff)]
        [InlineData("a.bmp", ImageFormats.Bmp)]
        public void OutputFormatByExtension(string file, string expected)
        {
            Cli.ResolveOutputPath(file, out string format);
            Assert.Equal(expected, format);
        }

        [Fact]
        public void UnsupportedExtension() => Assert.Null(Cli.ResolveOutputPath("a.pdf", out _));

        [Fact]
        public void ChoiceEnterUsesDefault()
        {
            var c = Cli.ParseChoice("", 3, 1);
            Assert.Equal(ChoiceKind.Index, c.Kind);
            Assert.Equal(1, c.Index);
        }

        [Fact]
        public void ChoiceNumberIsOneBased()
        {
            var c = Cli.ParseChoice(" 3 ", 3, 0);
            Assert.Equal(ChoiceKind.Index, c.Kind);
            Assert.Equal(2, c.Index);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("4")]
        [InlineData("abc")]
        public void ChoiceInvalid(string input) => Assert.Equal(ChoiceKind.Invalid, Cli.ParseChoice(input, 3, 0).Kind);

        [Fact]
        public void ChoiceSpecialKeys()
        {
            Assert.Equal(ChoiceKind.WindowsDialog, Cli.ParseChoice("W", 2, 0).Kind);
            Assert.Equal(ChoiceKind.Quit, Cli.ParseChoice("q", 2, 0).Kind);
        }

        [Fact]
        public void IndexOfIgnoresCase()
        {
            var ids = new[] { "{A}\\0000", "{B}\\0001" };
            Assert.Equal(1, Cli.IndexOf(ids, "{b}\\0001"));
            Assert.Equal(-1, Cli.IndexOf(ids, null));
            Assert.Equal(-1, Cli.IndexOf(ids, "{C}"));
        }

        [Fact]
        public void WiaErrorMessages()
        {
            Assert.Equal("v podavači není papír", Cli.DescribeWiaError(unchecked((int)0x80210003)));
            Assert.Equal("chyba 0x80004005", Cli.DescribeWiaError(unchecked((int)0x80004005)));
        }

        [Fact]
        public void ConfigRoundTrip()
        {
            string path = Path.Combine(Path.GetTempPath(), "ascan-test-" + Guid.NewGuid(), "ascan.cfg");
            Assert.Null(new Config(path).DeviceId);
            new Config(path).Save("{6BDD1FC6-810F-11D0-BEC7-08002BE2092F}\\0000", "Canon LiDE 300");
            var loaded = new Config(path);
            Assert.Equal("{6BDD1FC6-810F-11D0-BEC7-08002BE2092F}\\0000", loaded.DeviceId);
            Assert.Equal("Canon LiDE 300", loaded.DeviceName);
            Directory.Delete(Path.GetDirectoryName(path), true);
        }
    }
}

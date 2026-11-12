using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using RenDt;
using Xunit;

namespace RenDt.Tests
{
    public class RenDtTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "rendt_" + Guid.NewGuid().ToString("N"));
        private readonly StringWriter _out = new StringWriter();
        private static readonly DateTime Cas = new DateTime(2026, 11, 2, 9, 4, 5);

        static RenDtTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        public RenDtTests() => Directory.CreateDirectory(_dir);

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch (Exception) { }
        }

        private int Spust(params string[] a) => Aplikace.Spust(a, _dir, () => Cas, _out);
        private string Err => File.ReadAllText(Path.Combine(_dir, Aplikace.SouborChyby), Encoding.GetEncoding(1250));

        private string Soubor(string jmeno)
        {
            var f = Path.Combine(_dir, jmeno);
            File.WriteAllText(f, "obsah");
            return f;
        }

        [Theory]
        [InlineData("faktura.pdf", "faktura_261102_090405.pdf")]
        [InlineData("README", "README_261102_090405")]
        [InlineData("a.tar.gz", "a.tar_261102_090405.gz")]
        [InlineData(".config", ".config_261102_090405")]
        [InlineData("Fa č. 1.PDF", "Fa č. 1_261102_090405.PDF")]
        public void NoveJmeno(string jmeno, string ocekavane) => Assert.Equal(ocekavane, Aplikace.NoveJmeno(jmeno, Cas));

        [Fact]
        public void NezavisleNaNastaveniWindows()
        {
            var puvodni = Thread.CurrentThread.CurrentCulture;
            try
            {
                foreach (var k in new[] { "cs-CZ", "en-US", "ar-SA", "th-TH", "fa-IR", "ja-JP" })
                {
                    var c = new CultureInfo(k);
                    c.DateTimeFormat.TimeSeparator = ".";
                    Thread.CurrentThread.CurrentCulture = c;
                    Assert.Equal("x_261102_090405.txt", Aplikace.NoveJmeno("x.txt", Cas));
                    Assert.Equal("x_261102_210405.txt", Aplikace.NoveJmeno("x.txt", Cas.AddHours(12)));
                }
            }
            finally { Thread.CurrentThread.CurrentCulture = puvodni; }
        }

        [Fact]
        public void Prejmenuje()
        {
            var f = Soubor("fa 1.pdf");
            Assert.Equal(0, Spust(f));
            var cil = Path.Combine(_dir, "fa 1_261102_090405.pdf");
            Assert.True(File.Exists(cil));
            Assert.False(File.Exists(f));
            Assert.Equal("obsah", File.ReadAllText(cil));
            Assert.Equal(cil, _out.ToString().Trim());
            Assert.False(File.Exists(Path.Combine(_dir, Aplikace.SouborChyby)));
        }

        [Fact]
        public void CilExistuje()
        {
            var f = Soubor("a.pdf");
            Soubor("a_261102_090405.pdf");
            Assert.Equal(3, Spust(f));
            Assert.True(File.Exists(f));
            Assert.Contains("už existuje", Err);
        }

        [Fact]
        public void NeexistujiciSoubor()
        {
            Assert.Equal(4, Spust(Path.Combine(_dir, "neni.pdf")));
            Assert.Contains("neexistuje", Err);
            Assert.Equal(4, Spust(_dir));
            Assert.Contains("adresář", Err);
        }

        [Fact]
        public void ChybneParametry()
        {
            Assert.Equal(2, Spust());
            Assert.Equal(2, Spust("a", "b"));
            Assert.Equal(2, Spust(" "));
            Assert.Contains("Použití", Err);
        }
    }
}

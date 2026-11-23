using System.Text;
using Xunit;

namespace Rozeslat.Tests
{
    public class SeznamAdresTests
    {
        static SeznamAdresTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        static Tabulka Z(string s, Encoding e = null) => SeznamAdres.NactiBajty((e ?? new UTF8Encoding(false)).GetBytes(s));

        [Fact]
        public void StrednikSeZahlavim()
        {
            var t = Z("Jméno;Firma;E-mail\r\nNovák;ABC;novak@abc.cz\r\nDvořák;\"X; Y\";dvorak@xy.cz\r\n");
            Assert.Equal(';', t.Oddelovac);
            Assert.True(SeznamAdres.MaZahlavi(t));
            Assert.Equal(2, SeznamAdres.NajdiSloupecSEmaily(t));
            var v = SeznamAdres.Adresy(t, 2, true);
            Assert.Equal(new[] { "novak@abc.cz", "dvorak@xy.cz" }, v.Platne);
            Assert.Equal("X; Y", t.Radky[2][1]);
            Assert.StartsWith("Sloupec 3: E-mail  (např. novak@abc.cz)", SeznamAdres.NazvySloupcu(t, true)[2]);
        }

        [Fact]
        public void CarkaBezZahlavi()
        {
            var t = Z("a@b.cz,Jan\nc@d.com,Petr");
            Assert.Equal(',', t.Oddelovac);
            Assert.False(SeznamAdres.MaZahlavi(t));
            Assert.Equal(2, SeznamAdres.Adresy(t, 0, false).Platne.Count);
        }

        [Fact]
        public void Tabulator() => Assert.Equal('\t', Z("jmeno\temail\nx\tx@y.cz").Oddelovac);

        [Fact]
        public void DuplicityANeplatne()
        {
            var t = Z("email\nA@B.cz\na@b.cz\nnení adresa\n\n<c@d.cz>, e@f.eu\n");
            var v = SeznamAdres.Adresy(t, 0, true);
            Assert.Equal(new[] { "A@B.cz", "c@d.cz", "e@f.eu" }, v.Platne);
            Assert.Equal(1, v.Duplicity);
            Assert.Single(v.Neplatne);
            Assert.Contains("řádek 4", v.Neplatne[0]);
        }

        [Fact]
        public void Windows1250()
        {
            var t = Z("Jméno;E-mail\nŽluťoučký kůň;kun@x.cz\n", Encoding.GetEncoding(1250));
            Assert.Equal(1250, t.Kodovani.CodePage);
            Assert.Equal("Žluťoučký kůň", t.Radky[1][0]);
        }

        [Fact]
        public void Utf8Bom()
        {
            var b = new UTF8Encoding(true);
            var data = new System.Collections.Generic.List<byte>(b.GetPreamble());
            data.AddRange(b.GetBytes("Příjmení;mail\nČech;cech@x.cz"));
            var t = SeznamAdres.NactiBajty(data.ToArray());
            Assert.Equal("Příjmení", t.Radky[0][0]);
        }

        [Fact]
        public void VicerakovePole()
        {
            var t = Z("pozn;mail\n\"řádek1\nřádek2\";a@b.cz\n");
            Assert.Equal(2, t.Radky.Count);
            Assert.Equal("a@b.cz", SeznamAdres.Adresy(t, 1, true).Platne[0]);
        }

        [Fact]
        public void KratsiRadky()
        {
            var t = Z("a;b;mail\nx\ny;z;q@w.cz\n");
            Assert.Equal(new[] { "q@w.cz" }, SeznamAdres.Adresy(t, 2, true).Platne);
        }

        [Fact]
        public void AdresaSeNemeni()
        {
            var t = Z("mail\no!neil@example.cz\n'a@b.cz'\n");
            var v = SeznamAdres.Adresy(t, 0, true);
            Assert.Equal(new[] { "a@b.cz" }, v.Platne);
            Assert.Single(v.Neplatne);
        }

        [Theory]
        [InlineData("novak@firma.cz", true)]
        [InlineData(" novak@firma.cz ", true)]
        [InlineData("novak@firma", false)]
        [InlineData("novak firma.cz", false)]
        [InlineData("a@b.cz, c@d.cz", false)]
        public void Platnost(string a, bool ok) => Assert.Equal(ok, SeznamAdres.JePlatnaAdresa(a));
    }
}

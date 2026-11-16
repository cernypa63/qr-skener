using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Otevrene.Tests
{
    public class OtevreneTests
    {
        private static OtevrenySoubor S(string ip, string uz, string cesta) =>
            new OtevrenySoubor { Ip = ip, Uzivatel = uz, Cesta = cesta };

        private static int Spust(List<OtevrenySoubor> data, out string text, params string[] args)
        {
            var w = new StringWriter();
            var kod = Aplikace.Spust(args, net => new KeyValuePair<string, List<OtevrenySoubor>>(net ? "net" : "smb", data), w);
            text = w.ToString();
            return kod;
        }

        [Fact]
        public void VypiseTabulkuSerazenouPodleIp()
        {
            var data = new List<OtevrenySoubor>
            {
                S("192.168.1.10", "FIRMA\\novak", @"D:\DATA\FAKTURY.DBF"),
                S("192.168.1.9", "FIRMA\\svoboda", @"D:\DATA\SKLAD.DBF"),
            };
            Assert.Equal(0, Spust(data, out var t));
            var radky = t.Split(new[] { "\r\n" }, StringSplitOptions.None);
            Assert.StartsWith("IP adresa", radky[0]);
            Assert.StartsWith("192.168.1.9 ", radky[1]);
            Assert.EndsWith(@"D:\DATA\SKLAD.DBF", radky[1]);
            Assert.StartsWith("192.168.1.10", radky[2]);
            Assert.Equal("192.168.1.10  FIRMA\\novak    D:\\DATA\\FAKTURY.DBF", radky[2]);
            Assert.Contains("Otevrenych souboru: 2", t);
        }

        [Fact]
        public void FiltrTextemIMaskou()
        {
            var data = new List<OtevrenySoubor>
            {
                S("192.168.1.10", "novak", @"D:\DATA\FAKTURY.DBF"),
                S("192.168.1.11", "svoboda", @"E:\JINE\a.txt"),
            };
            Assert.Equal(0, Spust(data, out var t, @"d:\data"));
            Assert.Contains("FAKTURY", t);
            Assert.DoesNotContain("a.txt", t);

            Assert.Equal(0, Spust(data, out t, "192.168.1.11"));
            Assert.Contains("a.txt", t);
            Assert.DoesNotContain("FAKTURY", t);

            Assert.Equal(0, Spust(data, out t, @"D:\DATA\*.dbf"));
            Assert.Contains("FAKTURY", t);
            Assert.DoesNotContain("a.txt", t);

            Assert.Equal(1, Spust(data, out t, "*.xls"));
            Assert.Contains("neodpovida", t);
        }

        [Fact]
        public void NicOtevreneVraciJedna()
        {
            Assert.Equal(1, Spust(new List<OtevrenySoubor>(), out var t));
            Assert.Contains("Zadny soubor", t);
        }

        [Fact]
        public void PrepinacNetAChybneParametry()
        {
            var w = new StringWriter();
            bool? pouzitoNet = null;
            Aplikace.Spust(new[] { "/NET" }, net => { pouzitoNet = net; return new KeyValuePair<string, List<OtevrenySoubor>>("x", new List<OtevrenySoubor>()); }, w);
            Assert.True(pouzitoNet);
            Assert.Equal(2, Spust(new List<OtevrenySoubor>(), out _, "a", "b"));
            Assert.Equal(2, Spust(new List<OtevrenySoubor>(), out _, "/?"));
        }

        [Fact]
        public void ChybyPristupuAJine()
        {
            var w = new StringWriter();
            Assert.Equal(3, Aplikace.Spust(new string[0], _ => throw new PristupOdepren("Pristup odepren"), w));
            Assert.Contains("spravce", w.ToString());
            Assert.Equal(5, Aplikace.Spust(new string[0], _ => throw new InvalidOperationException("x"), new StringWriter()));
        }

        [Fact]
        public void DoplniIpPodleUzivatele()
        {
            var soubory = new List<OtevrenySoubor> { S("", "novak", "a"), S("", "svoboda", "b"), S("", "dvorak", "c") };
            var sezeni = new List<Sezeni>
            {
                new Sezeni { Ip = Aplikace.BezLomitek(@"\\192.168.1.10"), Uzivatel = "NOVAK" },
                new Sezeni { Ip = "192.168.1.12", Uzivatel = "svoboda" },
                new Sezeni { Ip = "192.168.1.9", Uzivatel = "svoboda" },
            };
            Aplikace.DoplnIp(soubory, sezeni);
            Assert.Equal("192.168.1.10", soubory[0].Ip);
            Assert.Equal("192.168.1.9 nebo 192.168.1.12", soubory[1].Ip);
            Assert.Equal("?", soubory[2].Ip);
        }
    }
}

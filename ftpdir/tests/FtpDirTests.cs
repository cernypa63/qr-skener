using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using FtpDir;
using Xunit;

namespace FtpDir.Tests
{
    /// <summary>Minimální FTP server pro testy (jedno spojení, pasivní režim).</summary>
    internal sealed class TestFtpServer : IDisposable
    {
        private readonly TcpListener _l = new TcpListener(IPAddress.Loopback, 0);
        private readonly Thread _vlakno;
        public readonly List<string> Prikazy = new List<string>();
        public string Heslo = "tajne";
        public string Adresar = "import/faktury";
        public string[] Vypis;
        public Dictionary<string, string> Casy = new Dictionary<string, string>();
        public Encoding Kodovani = Encoding.UTF8;
        public int Port => ((IPEndPoint)_l.LocalEndpoint).Port;

        public TestFtpServer(params string[] vypis)
        {
            Vypis = vypis;
            _l.Start();
            _vlakno = new Thread(Obsluha) { IsBackground = true };
            _vlakno.Start();
        }

        private void Obsluha()
        {
            try
            {
                using var c = _l.AcceptTcpClient();
                var s = c.GetStream();
                var r = new StreamReader(s, Kodovani);
                void W(string t) { var b = Kodovani.GetBytes(t + "\r\n"); s.Write(b, 0, b.Length); }
                W("220-Vitejte");
                W("220 ready");
                TcpListener data = null;
                string radek;
                while ((radek = r.ReadLine()) != null)
                {
                    lock (Prikazy) Prikazy.Add(radek);
                    var p = radek.Split(' ', 2);
                    var arg = p.Length > 1 ? p[1] : "";
                    switch (p[0].ToUpperInvariant())
                    {
                        case "USER": W("331 heslo"); break;
                        case "PASS": W(arg == Heslo ? "230 OK" : "530 Login incorrect."); break;
                        case "CWD": W(arg == Adresar ? "250 OK" : "550 No such directory"); break;
                        case "TYPE": W("200 OK"); break;
                        case "PASV":
                            data = new TcpListener(IPAddress.Loopback, 0);
                            data.Start();
                            var dp = ((IPEndPoint)data.LocalEndpoint).Port;
                            W($"227 Entering Passive Mode (127,0,0,1,{dp / 256},{dp % 256})");
                            break;
                        case "LIST":
                            using (var dc = data.AcceptTcpClient())
                            {
                                W("150 Opening");
                                var b = Kodovani.GetBytes(string.Join("\r\n", Vypis) + "\r\n");
                                dc.GetStream().Write(b, 0, b.Length);
                            }
                            data.Stop();
                            W("226 Done");
                            break;
                        case "MDTM": W(Casy.TryGetValue(arg, out var t) ? "213 " + t : "550 Can't check for file existence"); break;
                        case "QUIT": W("221 Bye"); return;
                        default: W("500 unknown"); break;
                    }
                }
            }
            catch (Exception) { }
        }

        public void Dispose() => _l.Stop();
    }

    public class FtpDirTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "ftpdir_" + Guid.NewGuid().ToString("N"));
        private static readonly Encoding W1250;

        static FtpDirTests()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            W1250 = Encoding.GetEncoding(1250);
        }

        public FtpDirTests() => Directory.CreateDirectory(_dir);
        public void Dispose() { try { Directory.Delete(_dir, true); } catch (Exception) { } }

        private static readonly string[] VypisUnix =
        {
            "drwxr-xr-x    2 1002       olet_cz        200704 Nov 11 08:11 .",
            "drwxr-xr-x   12 1002       olet_cz          4096 Nov 11 07:48 ..",
            "drwxr-xr-x    2 1002       olet_cz          4096 Nov 11 07:48 archiv",
            "-rw-r--r--    1 1002       olet_cz         25805 Jan  3  2026 fa_2026010002.pdf",
            "-rw-r--r--    1 1002       olet_cz          7045 Jan  3  2026 fa_2026010001.pdf",
            "-rw-r--r--    1 1002       olet_cz         25821 Oct 21 01:23 fa_2026013496.pdf",
            "-rw-r--r--    1 1002       olet_cz            10 Oct 21 01:23 poznámka; s mezerou.txt",
        };

        private string Parametry(int port, string extra = "", string heslo = "tajne", string adresar = "import/faktury")
        {
            var f = Path.Combine(_dir, "param.txt");
            File.WriteAllText(f, $"host=127.0.0.1\r\nport={port}\r\nlogin=u\r\nheslo={heslo}\r\nadresar={adresar}\r\noutput={Path.Combine(_dir, "seznam.csv")}\r\n{extra}", W1250);
            return f;
        }

        private string Csv => File.ReadAllText(Path.Combine(_dir, "seznam.csv"), W1250);
        private string Err => File.ReadAllText(Path.Combine(_dir, Aplikace.SouborChyby), W1250);

        [Fact]
        public void RozborUnix()
        {
            var ted = new DateTime(2026, 11, 11, 9, 0, 0);
            var p = VypisFtp.Rozeber(VypisUnix, ted);
            Assert.Equal(5, p.Count);
            Assert.True(p[0].Adresar);
            Assert.Equal("fa_2026010002.pdf", p[1].Nazev);
            Assert.Equal(new DateTime(2026, 1, 3), p[1].Datum);
            Assert.Equal(25805, p[1].Velikost);
            Assert.Equal(new DateTime(2026, 10, 21, 1, 23, 0), p[3].Datum);
            Assert.Equal("poznámka; s mezerou.txt", p[4].Nazev);
        }

        [Fact]
        public void RozborUnixRokZpet()
        {
            var p = VypisFtp.Radek("-rw-r--r-- 1 ftp ftp 5 Dec 30 23:59 a.pdf", new DateTime(2027, 1, 2));
            Assert.Equal(new DateTime(2026, 12, 30, 23, 59, 0), p.Datum);
            p = VypisFtp.Radek("-rw-r--r-- 1 ftp 5 Dec 30  2025 b.pdf", new DateTime(2027, 1, 2));
            Assert.Equal("b.pdf", p.Nazev);
            Assert.Equal(5, p.Velikost);
            p = VypisFtp.Radek("lrwxrwxrwx 1 ftp ftp 5 Dec 30  2025 odkaz -> cil", DateTime.Now);
            Assert.Equal("odkaz", p.Nazev);
            Assert.Null(VypisFtp.Radek("total 1234", DateTime.Now));
        }

        [Fact]
        public void RozborWindows()
        {
            var p = VypisFtp.Radek("10-21-26  01:23PM                25821 fa 1.pdf", DateTime.Now);
            Assert.Equal("fa 1.pdf", p.Nazev);
            Assert.Equal(new DateTime(2026, 10, 21, 13, 23, 0), p.Datum);
            Assert.True(VypisFtp.Radek("10-21-26  01:23PM       <DIR>          archiv", DateTime.Now).Adresar);
        }

        [Fact]
        public void Mdtm()
        {
            Assert.Equal(new DateTime(2026, 1, 3, 1, 44, 4), VypisFtp.Mdtm("213 20260103014404"));
            Assert.Equal(new DateTime(2026, 1, 3, 1, 44, 4), VypisFtp.Mdtm("213 20260103014404.123"));
            Assert.Null(VypisFtp.Mdtm("550 Can't check for file existence"));
        }

        [Fact]
        public void VstupPrikladUzivatele()
        {
            var v = Vstup.Rozeber("host=replikant4575.thinline.cz\r\nlogin=olet_cz_1\r\nheslo=abc=d\r\nadresar=import-to-eshop/faktury-pdf/2026\r\noutput=C:\\A\\seznam.csv\r\n");
            Assert.Equal("replikant4575.thinline.cz", v.Host);
            Assert.Equal(21, v.Port);
            Assert.Equal("abc=d", v.Heslo);
            Assert.Equal("import-to-eshop/faktury-pdf/2026", v.Adresar);
            Assert.True(v.Hlavicka);
            Assert.Equal("*", v.Maska);
        }

        [Fact]
        public void VstupVolitelne()
        {
            var v = Vstup.Rozeber("; komentář\nHOST = ftp://server.cz:2121/\nlogin=a\nheslo=\nadresar=/\noutput=x.csv\nmaska=*.pdf;*.XML\nhlavicka=0\ntimeout=5\n");
            Assert.Equal("server.cz", v.Host);
            Assert.Equal(2121, v.Port);
            Assert.Equal("", v.Heslo);
            Assert.False(v.Hlavicka);
            Assert.Equal(5, v.TimeoutSekund);
            var m = Vstup.MaskaNaRegex(v.Maska);
            Assert.Matches(m, "fa.PDF");
            Assert.Matches(m, "a.xml");
            Assert.DoesNotMatch(m, "a.pdf.bak");
        }

        [Theory]
        [InlineData("login=a\nheslo=b\nadresar=c\noutput=d\n", "host")]
        [InlineData("host=h\nlogin=a\nadresar=c\noutput=d\n", "heslo")]
        [InlineData("host=h\nlogin=a\nheslo=b\nadresar=c\noutput=d\nport=x\n", "port")]
        [InlineData("host=h\nlogin=a\nheslo=b\nadresar=c\noutput=d\nhost=i\n", "vícekrát")]
        [InlineData("host=h\nlogin=a\nheslo=b\nadresar=c\noutput=d\nadresr=e\n", "Neznámý")]
        [InlineData("host=h\nlogin\n", "klíč=hodnota")]
        public void VstupChyby(string text, string cast)
        {
            var e = Assert.Throws<ChybaVstupu>(() => Vstup.Rozeber(text));
            Assert.Contains(cast, e.Message);
        }

        [Fact]
        public void CelyBehCsv()
        {
            using var s = new TestFtpServer(VypisUnix);
            s.Casy["fa_2026010001.pdf"] = "20260103014404";
            s.Casy["fa_2026013496.pdf"] = "20261021012318";
            Assert.Equal(0, Aplikace.Spust(new[] { Parametry(s.Port) }, _dir));
            var radky = Csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal("soubor;datum;velikost", radky[0]);
            Assert.Equal("fa_2026010001.pdf;03.01.2026 01:44:04;7045", radky[1]);
            Assert.Equal("fa_2026010002.pdf;03.01.2026 00:00:00;25805", radky[2]);
            Assert.Equal("fa_2026013496.pdf;21.10.2026 01:23:18;25821", radky[3]);
            Assert.Equal("\"poznámka; s mezerou.txt\";21.10.2026 01:23:00;10", radky[4]);
            Assert.Equal(5, radky.Length);
            Assert.False(File.Exists(Path.Combine(_dir, Aplikace.SouborChyby)));
            Assert.DoesNotContain(s.Prikazy, p => p == "MDTM archiv");
        }

        [Fact]
        public void MaskaBezHlavicky()
        {
            using var s = new TestFtpServer(VypisUnix);
            Assert.Equal(0, Aplikace.Spust(new[] { Parametry(s.Port, "maska=*.txt\r\nhlavicka=0\r\n") }, _dir));
            Assert.Equal("\"poznámka; s mezerou.txt\";21.10.2026 01:23:00;10\r\n", Csv);
        }

        [Fact]
        public void PrazdnyAdresar()
        {
            using var s = new TestFtpServer();
            Assert.Equal(0, Aplikace.Spust(new[] { Parametry(s.Port) }, _dir));
            Assert.Equal("soubor;datum;velikost\r\n", Csv);
        }

        [Fact]
        public void ChybneHesloSmazeStaryVystup()
        {
            File.WriteAllText(Path.Combine(_dir, "seznam.csv"), "stary");
            using var s = new TestFtpServer(VypisUnix);
            Assert.Equal(3, Aplikace.Spust(new[] { Parametry(s.Port, heslo: "spatne") }, _dir));
            Assert.Contains("Přihlášení se nepovedlo", Err);
            Assert.False(File.Exists(Path.Combine(_dir, "seznam.csv")));
        }

        [Fact]
        public void NeexistujiciAdresar()
        {
            using var s = new TestFtpServer(VypisUnix);
            Assert.Equal(3, Aplikace.Spust(new[] { Parametry(s.Port, adresar: "nic") }, _dir));
            Assert.Contains("Adresář nic nelze otevřít", Err);
        }

        [Fact]
        public void ServerNebezi()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            var port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            Assert.Equal(3, Aplikace.Spust(new[] { Parametry(port) }, _dir));
            Assert.Contains("Nelze se připojit", Err);
        }

        [Fact]
        public void ChybnyVstupAParametry()
        {
            Assert.Equal(2, Aplikace.Spust(new string[0], _dir));
            Assert.Contains("jedním parametrem", Err);
            Assert.Equal(2, Aplikace.Spust(new[] { Path.Combine(_dir, "neni.txt") }, _dir));
            Assert.Contains("nelze načíst", Err);
        }

        [Fact]
        public void NazvyVeWindows1250()
        {
            using var s = new TestFtpServer("-rw-r--r-- 1 ftp ftp 5 Jan  3  2026 účet.pdf") { Kodovani = W1250 };
            s.Casy["účet.pdf"] = "20260103014404";
            Assert.Equal(0, Aplikace.Spust(new[] { Parametry(s.Port, "hlavicka=0\r\n") }, _dir));
            Assert.Equal("účet.pdf;03.01.2026 01:44:04;5\r\n", Csv);
        }

        /// <summary>Spustí se jen s proměnnou FTPDIR_SKUTECNY = cesta k souboru parametrů skutečného serveru.</summary>
        [Fact]
        public void SkutecnyServer()
        {
            var f = Environment.GetEnvironmentVariable("FTPDIR_SKUTECNY");
            if (string.IsNullOrEmpty(f)) return;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var kod = Aplikace.Spust(new[] { f }, _dir);
            var v = Vstup.Nacti(f);
            Console.WriteLine("kód " + kod + ", " + sw.Elapsed.TotalSeconds.ToString("0.0") + " s");
            if (kod != 0) Console.WriteLine(Err);
            Assert.Equal(0, kod);
            var radky = File.ReadAllLines(v.Output, W1250);
            Console.WriteLine(radky.Length + " řádků: " + string.Join(" | ", radky.Take(3)) + " ... " + radky.Last());
        }
    }
}

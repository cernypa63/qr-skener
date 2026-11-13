using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using SendFtp;
using Xunit;

namespace SendFtp.Tests
{
    /// <summary>FTP server pro testy: soubory a adresáře jen v paměti.</summary>
    internal sealed class PametovyFtp : IDisposable
    {
        private readonly TcpListener _l = new TcpListener(IPAddress.Loopback, 0);
        public readonly Dictionary<string, byte[]> Soubory = new Dictionary<string, byte[]>();
        public readonly HashSet<string> Adresare = new HashSet<string> { "/" };
        public readonly List<string> Prikazy = new List<string>();
        public string Heslo = "tajne";
        public bool ZakazPrejmenovani;
        public int Port => ((IPEndPoint)_l.LocalEndpoint).Port;

        public PametovyFtp()
        {
            _l.Start();
            new Thread(() => { while (true) { try { var c = _l.AcceptTcpClient(); new Thread(() => Obsluha(c)) { IsBackground = true }.Start(); } catch { return; } } }) { IsBackground = true }.Start();
        }

        private static string Spoj(string cwd, string p) =>
            p.StartsWith("/") ? p.TrimEnd('/') : (cwd == "/" ? "/" + p : cwd + "/" + p);

        private void Obsluha(TcpClient c)
        {
            try
            {
                using (c)
                {
                    var s = c.GetStream();
                    var r = new StreamReader(s, Encoding.UTF8);
                    void W(string t) { var b = Encoding.UTF8.GetBytes(t + "\r\n"); s.Write(b, 0, b.Length); }
                    W("220 ready");
                    var cwd = "/";
                    TcpListener data = null;
                    string rnfr = null, radek;
                    while ((radek = r.ReadLine()) != null)
                    {
                        lock (Prikazy) Prikazy.Add(radek);
                        var p = radek.Split(' ', 2);
                        var arg = p.Length > 1 ? p[1] : "";
                        var cesta = Spoj(cwd, arg);
                        switch (p[0])
                        {
                            case "USER": W("331 heslo"); break;
                            case "PASS": W(arg == Heslo ? "230 OK" : "530 Login incorrect."); break;
                            case "TYPE": W("200 OK"); break;
                            case "CWD":
                                if (arg == "/") cesta = "/";
                                lock (Adresare) if (Adresare.Contains(cesta)) { cwd = cesta; W("250 OK"); } else W("550 No such directory");
                                break;
                            case "MKD": lock (Adresare) Adresare.Add(cesta); W("257 created"); break;
                            case "PASV":
                                data = new TcpListener(IPAddress.Loopback, 0);
                                data.Start();
                                var dp = ((IPEndPoint)data.LocalEndpoint).Port;
                                W($"227 Entering Passive Mode (127,0,0,1,{dp / 256},{dp % 256})");
                                break;
                            case "STOR":
                                using (var dc = data.AcceptTcpClient())
                                {
                                    W("150 Ok to send data");
                                    var ms = new MemoryStream();
                                    dc.GetStream().CopyTo(ms);
                                    lock (Soubory) Soubory[cesta] = ms.ToArray();
                                }
                                data.Stop();
                                W("226 Transfer complete");
                                break;
                            case "SIZE": lock (Soubory) W(Soubory.TryGetValue(cesta, out var b) ? "213 " + b.Length : "550 no file"); break;
                            case "DELE": lock (Soubory) W(Soubory.Remove(cesta) ? "250 deleted" : "550 no file"); break;
                            case "RNFR": rnfr = cesta; lock (Soubory) W(!ZakazPrejmenovani && Soubory.ContainsKey(cesta) ? "350 ready" : "550 denied"); break;
                            case "RNTO": lock (Soubory) { Soubory[cesta] = Soubory[rnfr]; Soubory.Remove(rnfr); } W("250 renamed"); break;
                            case "QUIT": W("221 Bye"); return;
                            default: W("500 unknown"); break;
                        }
                    }
                }
            }
            catch (Exception) { }
        }

        public void Dispose() => _l.Stop();
    }

    internal sealed class TestSifra : ISifra
    {
        public byte[] Zasifruj(byte[] d) => d.Select(b => (byte)(b ^ 0x5A)).Reverse().ToArray();
        public byte[] Desifruj(byte[] d) => d.Reverse().Select(b => (byte)(b ^ 0x5A)).ToArray();
    }

    internal sealed class TestDotazy : IDotazy
    {
        public readonly Queue<string> Odpovedi = new Queue<string>();
        public readonly List<string> Vypis = new List<string>();
        public string Cti(string vyzva, bool skryte) => Odpovedi.Dequeue();
        public void Pis(string text) => Vypis.Add(text);
    }

    public class SendFtpTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "sendftp_" + Guid.NewGuid().ToString("N"));
        private readonly PametovyFtp _ftp = new PametovyFtp();
        private readonly TestSifra _sifra = new TestSifra();
        private readonly TestDotazy _d = new TestDotazy();

        static SendFtpTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        public SendFtpTests()
        {
            Directory.CreateDirectory(_dir);
            _ftp.Adresare.Add("/import");
        }

        public void Dispose()
        {
            _ftp.Dispose();
            try { Directory.Delete(_dir, true); } catch (Exception) { }
        }

        private int Spust(params string[] a) => Aplikace.Spust(a, _dir, _sifra, _d);
        private string Err => File.ReadAllText(Path.Combine(_dir, Aplikace.SouborChyby), Encoding.GetEncoding(1250));
        private string Cfg => Path.Combine(_dir, Nastaveni.Soubor);

        private void Nastav()
        {
            var p = Path.Combine(_dir, "p.txt");
            File.WriteAllText(p, $"host=127.0.0.1\r\nport={_ftp.Port}\r\nlogin=u\r\nheslo=tajne\r\n");
            Assert.Equal(0, Spust("/nastav", p));
        }

        private string Soubor(string jmeno, string obsah)
        {
            var f = Path.Combine(_dir, jmeno);
            File.WriteAllText(f, obsah);
            return f;
        }

        [Fact]
        public void NastaveniZeSouboruJeZasifrovane()
        {
            Nastav();
            var text = File.ReadAllText(Cfg);
            Assert.DoesNotContain("tajne", text);
            Assert.DoesNotContain("127.0.0.1", text);
            var n = Nastaveni.Nacti(Cfg, _sifra);
            Assert.Equal(("127.0.0.1", _ftp.Port, "u", "tajne"), (n.Host, n.Port, n.Login, n.Heslo));
        }

        [Fact]
        public void NastaveniInteraktivne()
        {
            foreach (var o in new[] { "ftp://127.0.0.1/", _ftp.Port.ToString(), "u", "tajne" }) _d.Odpovedi.Enqueue(o);
            Assert.Equal(0, Spust("/nastav"));
            Assert.Equal(0, Spust("/info"));
            Assert.Contains(_d.Vypis, v => v == "Server: 127.0.0.1:" + _ftp.Port + ", login: u");
            Assert.DoesNotContain(_d.Vypis, v => v.Contains("tajne"));
        }

        [Fact]
        public void ChybneHesloSeNeulozi()
        {
            foreach (var o in new[] { "127.0.0.1", _ftp.Port.ToString(), "u", "spatne" }) _d.Odpovedi.Enqueue(o);
            Assert.Equal(3, Spust("/nastav"));
            Assert.Contains("Přihlášení se nepovedlo", Err);
            Assert.False(File.Exists(Cfg));
        }

        [Fact]
        public void OdeslaniSouboru()
        {
            Nastav();
            var f = Soubor("fa 1.pdf", "obsah faktury");
            Assert.Equal(0, Spust(f, "import"));
            Assert.Equal("obsah faktury", Encoding.UTF8.GetString(_ftp.Soubory["/import/fa 1.pdf"]));
            Assert.Single(_ftp.Soubory);
            Assert.Contains(_ftp.Prikazy, p => p == "STOR fa 1.pdf.part");
            Assert.False(File.Exists(Path.Combine(_dir, Aplikace.SouborChyby)));
        }

        [Fact]
        public void PrepsaniExistujiciho()
        {
            Nastav();
            _ftp.Soubory["/import/a.xml"] = new byte[] { 1 };
            Assert.Equal(0, Spust(Soubor("a.xml", "nove"), "/import"));
            Assert.Equal("nove", Encoding.UTF8.GetString(_ftp.Soubory["/import/a.xml"]));
        }

        [Fact]
        public void MaskaAVytvoreniAdresare()
        {
            Nastav();
            Soubor("a.pdf", "A");
            Soubor("b.pdf", "B");
            Soubor("c.txt", "C");
            Assert.Equal(0, Spust(Path.Combine(_dir, "*.pdf"), "import/2026/11"));
            Assert.Equal(new[] { "/import/2026/11/a.pdf", "/import/2026/11/b.pdf" }, _ftp.Soubory.Keys.OrderBy(k => k));
            Assert.Contains("/import/2026/11", _ftp.Adresare);
        }

        [Fact]
        public void PrejmenovaniZakazanoUklidi()
        {
            Nastav();
            _ftp.ZakazPrejmenovani = true;
            Assert.Equal(3, Spust(Soubor("a.pdf", "A"), "import"));
            Assert.Contains("nelze přejmenovat", Err);
            Assert.Empty(_ftp.Soubory);
        }

        [Fact]
        public void BezNastaveni()
        {
            Assert.Equal(2, Spust(Soubor("a.pdf", "A"), "import"));
            Assert.Contains("/nastav", Err);
        }

        [Fact]
        public void NeexistujiciSoubor()
        {
            Nastav();
            Assert.Equal(4, Spust(Path.Combine(_dir, "neni.pdf"), "import"));
            Assert.Equal(4, Spust(Path.Combine(_dir, "*.neni"), "import"));
            Assert.Contains("neexistuje", Err);
        }

        [Fact]
        public void PoskozeneNastaveni()
        {
            File.WriteAllText(Cfg, "SENDFTP-CFG-1\r\nnení base64\r\n");
            Assert.Equal(2, Spust(Soubor("a.pdf", "A"), "import"));
            Assert.Contains("nelze přečíst", Err);
        }

        [Fact]
        public void ChybnePouziti()
        {
            Assert.Equal(2, Spust());
            Assert.Contains("Použití", Err);
            Assert.Equal(2, Spust("jen_jeden"));
        }

        [Fact]
        public void DpapiNaWindows()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
            var d = new Dpapi();
            var z = d.Zasifruj(Encoding.UTF8.GetBytes("heslo"));
            Assert.Equal("heslo", Encoding.UTF8.GetString(d.Desifruj(z)));
        }
    }
}

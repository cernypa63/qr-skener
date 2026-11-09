using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Xunit;

namespace Seet2nd.Tests
{
    public class LicenceTests : IDisposable
    {
        /// <summary>Licence bez omezení pro testovací DIČ CZ00000019 podepsaná skutečným klíčem.</summary>
        public const string KodCZ00000019 = "0.GV3PBOKMX4Yi0jAUG92oj2dyW8sQ5_yib6JzYNmZL6o4p7b0w2RSIwxZZ2vfPVWC2yuswX628i0s-ZphszXACA";

        private readonly string _dir = Path.Combine(Path.GetTempPath(), "seet2nd-lic-" + Guid.NewGuid().ToString("N"));
        private readonly ECParameters _klic;
        private readonly ECParameters _verejny;

        public LicenceTests()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Directory.CreateDirectory(_dir);
            using (var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256))
            {
                _klic = ec.ExportParameters(true);
                _verejny = ec.ExportParameters(false);
            }
        }

        public void Dispose() => Directory.Delete(_dir, true);

        [Fact]
        public void SkutecnyKlicOveriVydanouLicenci()
        {
            var v = Licence.Over("CZ00000019", KodCZ00000019, DateTime.Today);
            Assert.True(v.Platna, v.Chyba);
            Assert.Null(v.PlatnaDo);
            Assert.False(Licence.Over("CZ00000027", KodCZ00000019, DateTime.Today).Platna);
        }

        [Fact]
        public void VytvorenaLicenceJdeOverit()
        {
            var kod = Podpis.Vytvor("CZ12345678", new DateTime(2027, 12, 31), _klic);
            Assert.StartsWith("20271231.", kod);
            var v = Licence.Over("CZ12345678", kod, new DateTime(2027, 12, 31), _verejny);
            Assert.True(v.Platna, v.Chyba);
            Assert.Equal("31.12.2027", v.PlatnostText);
        }

        [Fact]
        public void NeplatneLicence()
        {
            var kod = Podpis.Vytvor("CZ12345678", new DateTime(2027, 12, 31), _klic);
            Assert.Contains("skončila", Licence.Over("CZ12345678", kod, new DateTime(2028, 1, 1), _verejny).Chyba);
            Assert.Contains("Neplatný", Licence.Over("CZ87654321", kod, DateTime.Today, _verejny).Chyba);
            Assert.Contains("Neplatný", Licence.Over("CZ12345678", "20291231" + kod.Substring(8), DateTime.Today, _verejny).Chyba);
            Assert.Contains("Neplatný", Licence.Over("CZ12345678", "nesmysl", DateTime.Today, _verejny).Chyba);
            Assert.Contains("Chybí", Licence.Over("CZ12345678", "", DateTime.Today, _verejny).Chyba);
            Assert.False(Licence.Over("CZ12345678", kod, DateTime.Today).Platna);
        }

        [Fact]
        public void KlicZeSouboru()
        {
            var cesta = Path.Combine(_dir, "k.key");
            File.WriteAllText(cesta, Podpis.HlavickaKlice + "\r\nD=" + Convert.ToBase64String(_klic.D) + "\r\nX=" +
                Convert.ToBase64String(_klic.Q.X) + "\r\nY=" + Convert.ToBase64String(_klic.Q.Y) + "\r\n");
            var k = Podpis.NactiKlic(cesta);
            Assert.False(Podpis.PatriKSeet2nd(k));
            Assert.True(Licence.Over("CZ12345678", Podpis.Vytvor("CZ12345678", null, k), DateTime.Today, _verejny).Platna);
        }

        private sealed class Klient : IEetKlient
        {
            public bool Volano, Playground;
            public Odpoved Odesli(bool playground, X509Certificate2 cert, TrzbaData data, bool prvniZaslani, int timeoutMs)
            {
                Volano = true; Playground = playground;
                return new Odpoved { Stav = Stav.OK, Pok = "x" };
            }
        }

        private (int kod, string vystup, Klient klient) Spust(string playground, string licence)
        {
            string p12;
            using (var rsa = RSA.Create(2048))
            {
                var req = new CertificateRequest("CN=Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                using (var c = req.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(30)))
                {
                    p12 = Path.Combine(_dir, "eet.p12");
                    File.WriteAllBytes(p12, c.Export(X509ContentType.Pkcs12, "a"));
                }
            }
            var vystup = Path.Combine(_dir, "res.xml");
            var vstup = Path.Combine(_dir, "in.xml");
            File.WriteAllText(vstup, $"<eet2_data><cert>{p12}</cert><pass>a</pass><DIC>CZ00000019</DIC><PlayGround>{playground}</PlayGround>" +
                $"<trzba>1</trzba><doklad>D1</doklad><output>{vystup}</output>{licence}</eet2_data>");
            var k = new Klient();
            var r = Aplikace.Spust(new[] { vstup }, _dir, k);
            return (r, File.Exists(vystup) ? File.ReadAllText(vystup, Aplikace.Win1250) : "", k);
        }

        [Fact]
        public void OstreBezLicenceNeodesle()
        {
            var (kod, vystup, k) = Spust("0", "");
            Assert.Equal(4, kod);
            Assert.False(k.Volano);
            Assert.Contains("<stav>LICENCE</stav>", vystup);
            Assert.Contains("<chyba>1</chyba>", vystup);
        }

        [Fact]
        public void OstreSLicenciOdesle()
        {
            var (kod, _, k) = Spust("0", "<licence>" + KodCZ00000019 + "</licence>");
            Assert.Equal(0, kod);
            Assert.True(k.Volano);
            Assert.False(k.Playground);
        }

        [Fact]
        public void PlaygroundLicenciNepotrebuje()
        {
            var (kod, _, k) = Spust("1", "<licence>spatny</licence>");
            Assert.Equal(0, kod);
            Assert.True(k.Playground);
        }
    }
}

using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using Xunit;

namespace Seet2nd.Tests
{
    public class Seet2ndTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "seet2nd-" + Guid.NewGuid().ToString("N"));

        public Seet2ndTests()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Directory.CreateDirectory(_dir);
        }

        public void Dispose() => Directory.Delete(_dir, true);

        private sealed class FalesnyKlient : IEetKlient
        {
            public TrzbaData Data;
            public bool Playground, PrvniZaslani;
            public Odpoved Vysledek = new Odpoved { Stav = Stav.OK, Pok = "3b3817ad-23fc-4e8c-b05c-986a62bcf4bb-ff", Test = true, DatPrij = "2026-10-31T15:01:04+01:00" };

            public Odpoved Odesli(bool playground, X509Certificate2 cert, TrzbaData data, bool prvniZaslani, int timeoutMs)
            {
                Playground = playground; Data = data; PrvniZaslani = prvniZaslani;
                Vysledek.DatOdesl = "2026-10-31T15:01:03+01:00";
                return Vysledek;
            }
        }

        private string TestovaciCert(string heslo)
        {
            using (var rsa = RSA.Create(2048))
            {
                var req = new CertificateRequest("CN=Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                using (var c = req.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(30)))
                {
                    var cesta = Path.Combine(_dir, "eet.p12");
                    File.WriteAllBytes(cesta, c.Export(X509ContentType.Pkcs12, heslo));
                    return cesta;
                }
            }
        }

        private string VstupniSoubor(string telo)
        {
            var cesta = Path.Combine(_dir, "vstup.xml");
            File.WriteAllBytes(cesta, Aplikace.Win1250.GetBytes("<?xml version=\"1.0\" encoding=\"WINDOWS-1250\"?>\r\n<eet2_data>\r\n" + telo + "</eet2_data>\r\n"));
            return cesta;
        }

        private string Vstup(string cert, string output, string navic = "", string trzba = "123,12") =>
            VstupniSoubor($"<cert>{cert}</cert>\r\n<pass>aaaa1111</pass>\r\n<DIC>CZ00000019</DIC>\r\n<PlayGround>1</PlayGround>\r\n" +
                $"<trzba>{trzba}</trzba>\r\n<doklad>DD261526</doklad>\r\n<output>{output}</output>\r\n{navic}");

        [Theory]
        [InlineData("123,12", "123.12")]
        [InlineData("123.1", "123.10")]
        [InlineData("5", "5.00")]
        [InlineData("-5,5", "-5.50")]
        [InlineData(" 1 234,00 ", "1234.00")]
        [InlineData("0", "0.00")]
        [InlineData("1,234", null)]
        [InlineData("abc", null)]
        [InlineData("123456789", null)]
        public void Castka(string vstup, string ocekavano) => Assert.Equal(ocekavano, Seet2nd.Vstup.Castka(vstup));

        [Fact]
        public void UspesneOdeslaniZapiseVysledek()
        {
            var cert = TestovaciCert("aaaa1111");
            var output = Path.Combine(_dir, "výsledek.xml");
            var klient = new FalesnyKlient();

            var kod = Aplikace.Spust(new[] { Vstup(cert, output) }, _dir, klient);

            Assert.Equal(0, kod);
            Assert.False(File.Exists(Path.Combine(_dir, Aplikace.SouborChyby)));
            Assert.True(klient.Playground);
            Assert.True(klient.PrvniZaslani);
            Assert.Equal("123.12", klient.Data.Castka);
            Assert.Equal("P1", klient.Data.IdPokl);
            Assert.Equal("11", klient.Data.IdJednotky);
            Assert.Equal("DD261526", klient.Data.PoradCis);
            Assert.Equal("CZ00000019", klient.Data.Dic);

            var text = File.ReadAllText(output, Aplikace.Win1250);
            Assert.StartsWith("<?xml version=\"1.0\" encoding=\"windows-1250\"?>", text);
            var doc = new XmlDocument();
            doc.Load(output);
            Assert.Equal("0", doc.SelectSingleNode("/eet2_result/chyba").InnerText);
            Assert.Equal("OK", doc.SelectSingleNode("/eet2_result/stav").InnerText);
            Assert.Equal(klient.Vysledek.Pok, doc.SelectSingleNode("/eet2_result/POK").InnerText);
            Assert.Equal(klient.Data.DatTrzby, doc.SelectSingleNode("/eet2_result/dat_trzby").InnerText);
            Assert.Equal("123,12", doc.SelectSingleNode("/eet2_result/trzba").InnerText);
        }

        [Fact]
        public void ChybaEetSeZapiseDoVystupu()
        {
            var cert = TestovaciCert("aaaa1111");
            var output = Path.Combine(_dir, "res.xml");
            var klient = new FalesnyKlient { Vysledek = new Odpoved { Stav = Stav.CHYBA, Kod = 3, Zprava = "Chybná struktura – žluťoučký kůň" } };

            Assert.Equal(1, Aplikace.Spust(new[] { Vstup(cert, output, "<id_pokl>POK1</id_pokl><id_jednotky>12</id_jednotky>") }, _dir, klient));
            Assert.Equal("POK1", klient.Data.IdPokl);
            Assert.Equal("12", klient.Data.IdJednotky);
            var doc = new XmlDocument();
            doc.Load(output);
            Assert.Equal("1", doc.SelectSingleNode("/eet2_result/chyba").InnerText);
            Assert.Equal("CHYBA", doc.SelectSingleNode("/eet2_result/stav").InnerText);
            Assert.Equal("3", doc.SelectSingleNode("/eet2_result/kod_chyby").InnerText);
            Assert.Equal("Chybná struktura – žluťoučký kůň", doc.SelectSingleNode("/eet2_result/zprava").InnerText);
        }

        [Fact]
        public void SpatneHesloCertifikatuJeChybaVeVystupu()
        {
            var cert = TestovaciCert("jine-heslo");
            var output = Path.Combine(_dir, "res.xml");
            Assert.Equal(1, Aplikace.Spust(new[] { Vstup(cert, output) }, _dir, new FalesnyKlient()));
            var doc = new XmlDocument();
            doc.Load(output);
            Assert.Equal("1", doc.SelectSingleNode("/eet2_result/chyba").InnerText);
            Assert.Contains("Certifikát nelze načíst", doc.SelectSingleNode("/eet2_result/zprava").InnerText);
        }

        [Fact]
        public void OpakovaneOdeslaniPouzijePuvodniDatum()
        {
            var cert = TestovaciCert("aaaa1111");
            var klient = new FalesnyKlient();
            Assert.Equal(0, Aplikace.Spust(new[] { Vstup(cert, Path.Combine(_dir, "res.xml"), "<prvni_zaslani>0</prvni_zaslani><dat_trzby>2026-10-31T15:01:03+01:00</dat_trzby>") }, _dir, klient));
            Assert.False(klient.PrvniZaslani);
            Assert.Equal("2026-10-31T15:01:03+01:00", klient.Data.DatTrzby);
        }

        [Theory]
        [InlineData("<eet2_data><cert>a</cert></eet2_data", "nelze načíst jako XML")]
        [InlineData("<jine/>", "<eet2_data>")]
        [InlineData("<eet2_data><cert>c.p12</cert><DIC>CZ1</DIC><PlayGround>1</PlayGround><trzba>1</trzba><doklad>1</doklad><output>o.xml</output></eet2_data>", "Neplatné DIČ")]
        [InlineData("<eet2_data><cert>c.p12</cert><DIC>CZ00000019</DIC><PlayGround>1</PlayGround><trzba>1</trzba><output>o.xml</output></eet2_data>", "<doklad>")]
        [InlineData("<eet2_data><cert>c.p12</cert><DIC>CZ00000019</DIC><PlayGround>2</PlayGround><trzba>1</trzba><doklad>1</doklad><output>o.xml</output></eet2_data>", "<PlayGround>")]
        [InlineData("<eet2_data><cert>c.p12</cert><DIC>CZ00000019</DIC><PlayGround>1</PlayGround><trzba>12x</trzba><doklad>1</doklad><output>o.xml</output></eet2_data>", "Neplatná částka")]
        [InlineData("<eet2_data><cert>c.p12</cert><DIC>CZ00000019</DIC><PlayGround>1</PlayGround><trzba>1</trzba><doklad>1</doklad><output>o.xml</output><prvni_zaslani>0</prvni_zaslani></eet2_data>", "<dat_trzby>")]
        public void ChybnyVstupZapiseErr(string obsah, string ocekavanyText)
        {
            var cesta = Path.Combine(_dir, "vstup.xml");
            File.WriteAllText(cesta, obsah);
            Assert.Equal(2, Aplikace.Spust(new[] { cesta }, _dir, new FalesnyKlient()));
            var err = File.ReadAllText(Path.Combine(_dir, Aplikace.SouborChyby), Aplikace.Win1250);
            Assert.Contains(ocekavanyText, err);
        }

        [Fact]
        public void ChybejiciSouborAParametr()
        {
            Assert.Equal(2, Aplikace.Spust(new[] { Path.Combine(_dir, "neni.xml") }, _dir, new FalesnyKlient()));
            Assert.Contains("neexistuje", File.ReadAllText(Path.Combine(_dir, Aplikace.SouborChyby), Aplikace.Win1250));
            Assert.Equal(2, Aplikace.Spust(new string[0], _dir, new FalesnyKlient()));
        }

        [Fact]
        public void RozborOdpovedi()
        {
            var ok = EetKlient.Rozbor("<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\"><soapenv:Body><eet:Odpoved xmlns:eet=\"http://fs.gov.cz/eet/schema/v4\">" +
                "<eet:Hlavicka uuid_zpravy=\"u1\" dat_prij=\"2026-10-31T15:01:04+01:00\"/><eet:Potvrzeni pok=\"abc-ff\" test=\"true\"/><eet:Varovani kod_varov=\"1\">Pozor</eet:Varovani></eet:Odpoved></soapenv:Body></soapenv:Envelope>");
            Assert.Equal(Stav.OK, ok.Stav);
            Assert.Equal("abc-ff", ok.Pok);
            Assert.True(ok.Test);
            Assert.Equal("2026-10-31T15:01:04+01:00", ok.DatPrij);
            Assert.Single(ok.Varovani);

            var chyba = EetKlient.Rozbor("<Envelope><Body><Odpoved><Chyba kod=\"4\" test=\"true\">Neplatny podpis SOAP zpravy</Chyba></Odpoved></Body></Envelope>");
            Assert.Equal(Stav.CHYBA, chyba.Stav);
            Assert.Equal(4, chyba.Kod);

            Assert.Equal(Stav.NEDORUCENO, EetKlient.Rozbor("<Odpoved><Chyba kod=\"-1\">Docasna chyba</Chyba></Odpoved>").Stav);
            Assert.Null(EetKlient.Rozbor("<html/>"));
        }

        [Fact]
        public void PodpisLzeOveritVerejnymKlicem()
        {
            using (var cert = new X509Certificate2(TestovaciCert("x"), "x"))
            {
                var data = new TrzbaData { Dic = "CZ00000019", IdJednotky = "11", IdPokl = "P1", PoradCis = "D1", DatTrzby = "2026-10-31T15:01:03+01:00", Castka = "1.00" };
                var xml = EetXml.PodepsanaZprava(cert, "e4a1b2c3-1234-4abc-8def-0123456789ab", "2026-10-31T15:01:03+01:00", true, data);
                var doc = new XmlDocument();
                doc.LoadXml(xml);
                var ns = new XmlNamespaceManager(doc.NameTable);
                ns.AddNamespace("ds", EetXml.NsDs);
                var podpis = Convert.FromBase64String(doc.SelectSingleNode("//ds:SignatureValue", ns).InnerText);
                var signedInfo = EetXml.KanonickeSignedInfo(doc.SelectSingleNode("//ds:DigestValue", ns).InnerText);
                Assert.True(cert.GetRSAPublicKey().VerifyData(Encoding.UTF8.GetBytes(signedInfo), podpis, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
            }
        }

        /// <summary>Skutečné odeslání do Playgroundu – spustí se, jen když je nastavena proměnná EET_PLAYGROUND_DIR.</summary>
        [Fact]
        public void Playground()
        {
            var adresar = Environment.GetEnvironmentVariable("EET_PLAYGROUND_DIR");
            if (string.IsNullOrEmpty(adresar)) return;
            var heslo = File.ReadAllText(Path.Combine(adresar, "password_pokladni_cert_playground.txt")).Trim();
            var output = Path.Combine(_dir, "res.xml");
            var vstup = VstupniSoubor($"<cert>{Path.Combine(adresar, "CA_EET-Playground-CZ00000019.p12")}</cert><pass>{heslo}</pass><DIC>CZ00000019</DIC>" +
                "<PlayGround>1</PlayGround><trzba>123,12</trzba><doklad>T" + DateTime.Now.ToString("yyMMddHHmmss") + "</doklad>" + $"<output>{output}</output>");
            var kod = Aplikace.Spust(new[] { vstup }, _dir, new EetKlient());
            var vysledek = File.ReadAllText(output, Aplikace.Win1250);
            Assert.True(kod == 0, vysledek);
            Assert.Matches("<POK>[0-9a-f-]{39}</POK>", vysledek);
        }
    }
}

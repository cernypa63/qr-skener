using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;

namespace Seet2nd
{
    /// <summary>
    /// Návratové kódy: 0 = tržba zaevidována, 1 = EET chyba nebo nedoručeno (viz výstupní soubor),
    /// 2 = chyba vstupu (viz SEET2ND.err), 3 = výstupní soubor nelze zapsat (viz SEET2ND.err).
    /// </summary>
    public static class Aplikace
    {
        public const string SouborChyby = "SEET2ND.err";

        public static Encoding Win1250 => Encoding.GetEncoding(1250);

        public static int Spust(string[] args, string adresarExe, IEetKlient klient)
        {
            var errSoubor = Path.Combine(adresarExe, SouborChyby);
            try { File.Delete(errSoubor); } catch (Exception) { }

            if (args.Length != 1)
            {
                ZapisChybu(errSoubor, "Program se volá s jedním parametrem: SEET2ND.exe cesta_k_souboru_vstupnich_hodnot");
                return 2;
            }

            Vstup v;
            try
            {
                v = Vstup.Nacti(args[0]);
            }
            catch (ChybaVstupu e)
            {
                ZapisChybu(errSoubor, e.Message + Environment.NewLine + "Soubor: " + args[0]);
                return 2;
            }

            var datTrzby = EetXml.Datum(v.DatTrzby ?? DateTimeOffset.Now);
            var data = new TrzbaData
            {
                Dic = v.Dic, IdJednotky = v.IdJednotky, IdPokl = v.IdPokl,
                PoradCis = v.Doklad, DatTrzby = datTrzby, Castka = v.Trzba,
            };

            Odpoved o;
            try
            {
                X509Certificate2 cert;
                try
                {
                    cert = new X509Certificate2(v.Cert, v.Pass, X509KeyStorageFlags.UserKeySet);
                }
                catch (CryptographicException e)
                {
                    throw new ChybaCertifikatu("Certifikát nelze načíst (chybné heslo nebo poškozený soubor " + v.Cert + "): " + e.Message.Trim());
                }
                if (!cert.HasPrivateKey) throw new ChybaCertifikatu("Certifikát " + v.Cert + " neobsahuje soukromý klíč.");
                if (DateTime.Now > cert.NotAfter) throw new ChybaCertifikatu("Platnost certifikátu skončila " + cert.NotAfter.ToString("d.M.yyyy", CultureInfo.InvariantCulture) + ".");
                using (cert)
                    o = klient.Odesli(v.PlayGround, cert, data, v.PrvniZaslani, v.TimeoutSekund * 1000);
            }
            catch (ChybaCertifikatu e)
            {
                o = new Odpoved { Stav = Stav.CHYBA, Zprava = e.Message, DatOdesl = EetXml.Datum(DateTimeOffset.Now) };
            }
            catch (Exception e) when (e is FileNotFoundException || e is CryptographicException || e is UnauthorizedAccessException)
            {
                o = new Odpoved { Stav = Stav.CHYBA, Zprava = "Chyba certifikátu nebo podpisu: " + e.Message.Trim(), DatOdesl = EetXml.Datum(DateTimeOffset.Now) };
            }

            try
            {
                ZapisVysledek(v.Output, v, data, o);
            }
            catch (Exception e)
            {
                ZapisChybu(errSoubor, "Výsledek nelze zapsat do " + v.Output + ": " + e.Message + Environment.NewLine +
                    "Výsledek: " + o.Stav + " POK=" + o.Pok + " " + o.Zprava);
                return 3;
            }
            return o.Stav == Stav.OK ? 0 : 1;
        }

        private sealed class ChybaCertifikatu : Exception
        {
            public ChybaCertifikatu(string m) : base(m) { }
        }

        public static void ZapisChybu(string soubor, string text)
        {
            try
            {
                File.WriteAllText(soubor, DateTime.Now.ToString("d.M.yyyy HH:mm:ss", CultureInfo.InvariantCulture) + Environment.NewLine + text + Environment.NewLine, Win1250);
            }
            catch (Exception) { }
        }

        public static void ZapisVysledek(string soubor, Vstup v, TrzbaData d, Odpoved o)
        {
            var nastaveni = new XmlWriterSettings { Encoding = Win1250, Indent = true };
            var docasny = soubor + ".tmp";
            using (var w = XmlWriter.Create(docasny, nastaveni))
            {
                w.WriteStartDocument();
                w.WriteStartElement("eet2_result");
                w.WriteElementString("chyba", o.Stav == Stav.OK ? "0" : "1");
                w.WriteElementString("stav", o.Stav.ToString());
                w.WriteElementString("POK", o.Pok);
                w.WriteElementString("dat_trzby", d.DatTrzby);
                w.WriteElementString("dat_odesl", o.DatOdesl);
                w.WriteElementString("dat_prij", o.DatPrij);
                w.WriteElementString("kod_chyby", o.Kod?.ToString(CultureInfo.InvariantCulture) ?? "");
                w.WriteElementString("zprava", o.Zprava);
                foreach (var kv in o.Varovani)
                {
                    w.WriteStartElement("varovani");
                    w.WriteAttributeString("kod", kv.Key);
                    w.WriteString(kv.Value);
                    w.WriteEndElement();
                }
                w.WriteElementString("uuid_zpravy", o.UuidZpravy);
                w.WriteElementString("PlayGround", v.PlayGround ? "1" : "0");
                w.WriteElementString("prvni_zaslani", v.PrvniZaslani ? "1" : "0");
                w.WriteElementString("DIC", d.Dic);
                w.WriteElementString("id_pokl", d.IdPokl);
                w.WriteElementString("id_jednotky", d.IdJednotky);
                w.WriteElementString("trzba", d.Castka.Replace('.', ','));
                w.WriteElementString("doklad", d.PoradCis);
                w.WriteEndElement();
                w.WriteEndDocument();
            }
            if (File.Exists(soubor)) File.Delete(soubor);
            File.Move(docasny, soubor);
        }
    }
}

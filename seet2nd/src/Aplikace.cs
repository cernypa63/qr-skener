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
    /// 2 = chyba vstupu (viz SEET2ND.err), 3 = výstupní soubor nelze zapsat (viz SEET2ND.err),
    /// 4 = chybí nebo je neplatná licence pro ostré prostředí (tržba se neodeslala, viz výstupní soubor).
    /// Ověření licence: SEET2ND.exe /licence DIČ kód (vypíše výsledek, návratový kód 0 nebo 4).
    /// </summary>
    public static class Aplikace
    {
        public const string SouborChyby = "SEET2ND.err";

        public static Encoding Win1250 => Encoding.GetEncoding(1250);

        public static int Spust(string[] args, string adresarExe, IEetKlient klient)
        {
            var errSoubor = Path.Combine(adresarExe, SouborChyby);
            try { File.Delete(errSoubor); } catch (Exception) { }

            if (args.Length == 3 && string.Equals(args[0], "/licence", StringComparison.OrdinalIgnoreCase))
                return OverLicenci(args[1], args[2]);

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

            Licence.Vysledek lic = null;
            Odpoved o;
            if (!v.PlayGround && !(lic = Licence.Over(v.Dic, v.Licence, DateTime.Today)).Platna)
                o = new Odpoved { Stav = Stav.LICENCE, Zprava = lic.Chyba, DatOdesl = EetXml.Datum(DateTimeOffset.Now) };
            else
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

            if (lic != null && lic.Platna && lic.PlatnaDo.HasValue && (lic.PlatnaDo.Value - DateTime.Today).TotalDays < 30)
                o.Varovani.Add(new System.Collections.Generic.KeyValuePair<string, string>("LIC", "Licence pro DIČ " + v.Dic + " končí " + lic.PlatnostText + "."));

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
            return o.Stav == Stav.OK ? 0 : o.Stav == Stav.LICENCE ? 4 : 1;
        }

        private static int OverLicenci(string dicText, string kod)
        {
            var dic = Vstup.NormalizujDic(dicText);
            var lic = dic == null ? null : Licence.Over(dic, kod, DateTime.Today);
            Console.WriteLine(dic == null ? "Neplatné DIČ: " + dicText
                : lic.Platna ? "Licence pro " + dic + " je platná, platnost: " + lic.PlatnostText + "."
                : lic.Chyba);
            return lic != null && lic.Platna ? 0 : 4;
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

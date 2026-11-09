using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;

namespace Seet2nd
{
    public enum Stav { OK, CHYBA, NEDORUCENO, LICENCE }

    public sealed class Odpoved
    {
        public Stav Stav;
        public string Pok = "";
        public bool Test;
        public int? Kod;
        public string Zprava = "";
        public string UuidZpravy = "";
        public string DatOdesl = "";
        public string DatPrij = "";
        public string DatOdmit = "";
        public readonly List<KeyValuePair<string, string>> Varovani = new List<KeyValuePair<string, string>>();
    }

    public interface IEetKlient
    {
        Odpoved Odesli(bool playground, X509Certificate2 cert, TrzbaData data, bool prvniZaslani, int timeoutMs);
    }

    public sealed class EetKlient : IEetKlient
    {
        public const string UrlPlayground = "https://pg.trzbyeet.gov.cz/eet/services/EETServiceSOAP/v4";
        public const string UrlProdukce = "https://trzbyeet.gov.cz/eet/services/EETServiceSOAP/v4";

        public Odpoved Odesli(bool playground, X509Certificate2 cert, TrzbaData data, bool prvniZaslani, int timeoutMs)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var uuid = Guid.NewGuid().ToString();
            var datOdesl = EetXml.Datum(DateTimeOffset.Now);
            var bytes = Encoding.UTF8.GetBytes(EetXml.PodepsanaZprava(cert, uuid, datOdesl, prvniZaslani, data));

            Odpoved o;
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(playground ? UrlPlayground : UrlProdukce);
                req.Method = "POST";
                req.ContentType = "text/xml; charset=utf-8";
                req.Headers["SOAPAction"] = "http://fs.gov.cz/eet/OdeslaniTrzby";
                req.Timeout = timeoutMs;
                req.ReadWriteTimeout = timeoutMs;
                req.ContentLength = bytes.Length;
                using (var s = req.GetRequestStream()) s.Write(bytes, 0, bytes.Length);
                using (var resp = (HttpWebResponse)req.GetResponse())
                    o = Rozbor(Precti(resp)) ?? Nedoruceno("Neplatná odpověď serveru EET (HTTP " + (int)resp.StatusCode + ").");
            }
            catch (WebException e) when (e.Status == WebExceptionStatus.Timeout)
            {
                o = Nedoruceno("Překročena mezní doba odezvy (" + timeoutMs / 1000 + " s).");
            }
            catch (WebException e) when (e.Response is HttpWebResponse r)
            {
                using (r) o = Rozbor(Precti(r)) ?? Nedoruceno("Neplatná odpověď serveru EET (HTTP " + (int)r.StatusCode + ").");
            }
            catch (WebException e)
            {
                o = Nedoruceno("Bez spojení se serverem EET: " + e.Message);
            }
            catch (IOException e)
            {
                o = Nedoruceno("Bez spojení se serverem EET: " + e.Message);
            }
            if (o.UuidZpravy.Length == 0) o.UuidZpravy = uuid;
            o.DatOdesl = datOdesl;
            return o;
        }

        private static string Precti(HttpWebResponse r)
        {
            using (var s = r.GetResponseStream())
            using (var rd = new StreamReader(s, Encoding.UTF8))
                return rd.ReadToEnd();
        }

        private static Odpoved Nedoruceno(string zprava) => new Odpoved { Stav = Stav.NEDORUCENO, Zprava = zprava };

        public static Odpoved Rozbor(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml)) return null;
            var doc = new XmlDocument { XmlResolver = null };
            try { doc.LoadXml(xml); } catch (XmlException) { return null; }

            XmlElement Prvni(string nazev) => doc.SelectSingleNode("//*[local-name()='" + nazev + "']") as XmlElement;

            var o = new Odpoved();
            var hlavicka = Prvni("Hlavicka");
            if (hlavicka != null)
            {
                o.UuidZpravy = hlavicka.GetAttribute("uuid_zpravy");
                o.DatPrij = hlavicka.GetAttribute("dat_prij");
                o.DatOdmit = hlavicka.GetAttribute("dat_odmit");
            }
            foreach (XmlElement w in doc.SelectNodes("//*[local-name()='Varovani']"))
                o.Varovani.Add(new KeyValuePair<string, string>(w.GetAttribute("kod_varov"), w.InnerText.Trim()));

            var potvrzeni = Prvni("Potvrzeni");
            if (potvrzeni != null)
            {
                o.Stav = Stav.OK;
                o.Pok = potvrzeni.GetAttribute("pok");
                o.Test = potvrzeni.GetAttribute("test") == "true";
                return o;
            }
            var chyba = Prvni("Chyba");
            if (chyba != null)
            {
                o.Kod = int.TryParse(chyba.GetAttribute("kod"), out var k) ? k : 999;
                o.Zprava = chyba.InnerText.Trim();
                o.Test = chyba.GetAttribute("test") == "true";
                o.Stav = o.Kod < 0 ? Stav.NEDORUCENO : Stav.CHYBA;
                return o;
            }
            var fault = Prvni("Fault");
            if (fault != null)
            {
                o.Stav = Stav.CHYBA;
                o.Zprava = "SOAP chyba: " + ((fault.SelectSingleNode("*[local-name()='faultstring']")?.InnerText) ?? fault.InnerText).Trim();
                return o;
            }
            return null;
        }
    }
}

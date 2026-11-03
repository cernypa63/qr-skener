using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Seet2nd
{
    /// <summary>
    /// Sestavení podepsané SOAP zprávy EET 2.0 (schéma v4).
    /// Podepisované části (Body a SignedInfo) se skládají rovnou v kanonickém tvaru
    /// (exclusive c14n s PrefixList "v4", resp. "soapenv v4").
    /// </summary>
    public static class EetXml
    {
        public const string NsSoap = "http://schemas.xmlsoap.org/soap/envelope/";
        public const string NsV4 = "http://fs.gov.cz/eet/schema/v4";
        public const string NsWsu = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";
        public const string NsWsse = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
        public const string NsDs = "http://www.w3.org/2000/09/xmldsig#";
        public const string NsEc = "http://www.w3.org/2001/10/xml-exc-c14n#";
        private const string X509Type = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-x509-token-profile-1.0#X509v3";
        private const string B64Type = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary";

        public static string Datum(DateTimeOffset t) =>
            t.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);

        private static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace("\"", "&quot;")
            .Replace("\t", "&#x9;").Replace("\n", "&#xA;").Replace("\r", "&#xD;");

        public static string KanonickeBody(string uuid, string datOdesl, bool prvniZaslani, TrzbaData d) =>
            "<soapenv:Body xmlns:soapenv=\"" + NsSoap + "\" xmlns:v4=\"" + NsV4 + "\" xmlns:wsu=\"" + NsWsu + "\" wsu:Id=\"id-body\">" +
            "<v4:Trzba>" +
            "<v4:Hlavicka dat_odesl=\"" + Esc(datOdesl) + "\" prvni_zaslani=\"" + (prvniZaslani ? "true" : "false") + "\" uuid_zpravy=\"" + Esc(uuid) + "\"></v4:Hlavicka>" +
            "<v4:Data celk_trzba=\"" + Esc(d.Castka) + "\" dat_trzby=\"" + Esc(d.DatTrzby) + "\" eic_popl=\"" + Esc(d.Dic) + "\"" +
            " id_jednotky=\"" + Esc(d.IdJednotky) + "\" id_pokl=\"" + Esc(d.IdPokl) + "\" porad_cis=\"" + Esc(d.PoradCis) + "\"></v4:Data>" +
            "</v4:Trzba></soapenv:Body>";

        public static string KanonickeSignedInfo(string digestB64) =>
            "<ds:SignedInfo xmlns:ds=\"" + NsDs + "\" xmlns:soapenv=\"" + NsSoap + "\" xmlns:v4=\"" + NsV4 + "\">" +
            "<ds:CanonicalizationMethod Algorithm=\"" + NsEc + "\"><ec:InclusiveNamespaces xmlns:ec=\"" + NsEc + "\" PrefixList=\"soapenv v4\"></ec:InclusiveNamespaces></ds:CanonicalizationMethod>" +
            "<ds:SignatureMethod Algorithm=\"http://www.w3.org/2001/04/xmldsig-more#rsa-sha256\"></ds:SignatureMethod>" +
            "<ds:Reference URI=\"#id-body\"><ds:Transforms><ds:Transform Algorithm=\"" + NsEc + "\"><ec:InclusiveNamespaces xmlns:ec=\"" + NsEc + "\" PrefixList=\"v4\"></ec:InclusiveNamespaces></ds:Transform></ds:Transforms>" +
            "<ds:DigestMethod Algorithm=\"http://www.w3.org/2001/04/xmlenc#sha256\"></ds:DigestMethod>" +
            "<ds:DigestValue>" + digestB64 + "</ds:DigestValue></ds:Reference></ds:SignedInfo>";

        public static string PodepsanaZprava(X509Certificate2 cert, string uuid, string datOdesl, bool prvniZaslani, TrzbaData data)
        {
            var body = KanonickeBody(uuid, datOdesl, prvniZaslani, data);
            string digest;
            using (var sha = SHA256.Create())
                digest = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(body)));
            var signedInfo = KanonickeSignedInfo(digest);
            string podpis;
            using (var rsa = cert.GetRSAPrivateKey())
            {
                if (rsa == null) throw new CryptographicException("Certifikát neobsahuje soukromý klíč RSA.");
                podpis = Convert.ToBase64String(rsa.SignData(Encoding.UTF8.GetBytes(signedInfo), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
            }
            return "<soapenv:Envelope xmlns:soapenv=\"" + NsSoap + "\" xmlns:v4=\"" + NsV4 + "\"><soapenv:Header>" +
                "<wsse:Security xmlns:wsse=\"" + NsWsse + "\" xmlns:wsu=\"" + NsWsu + "\">" +
                "<wsse:BinarySecurityToken EncodingType=\"" + B64Type + "\" ValueType=\"" + X509Type + "\" wsu:Id=\"X509-1\">" +
                Convert.ToBase64String(cert.RawData) + "</wsse:BinarySecurityToken>" +
                "<ds:Signature xmlns:ds=\"" + NsDs + "\">" + signedInfo +
                "<ds:SignatureValue>" + podpis + "</ds:SignatureValue>" +
                "<ds:KeyInfo><wsse:SecurityTokenReference><wsse:Reference URI=\"#X509-1\" ValueType=\"" + X509Type + "\"/></wsse:SecurityTokenReference></ds:KeyInfo>" +
                "</ds:Signature></wsse:Security></soapenv:Header>" + body + "</soapenv:Envelope>";
        }
    }

    /// <summary>Údaje oblasti Data – při opakovaném zaslání se nemění.</summary>
    public sealed class TrzbaData
    {
        public string Dic, IdJednotky, IdPokl, PoradCis, DatTrzby, Castka;
    }
}

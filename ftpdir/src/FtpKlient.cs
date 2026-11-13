using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace FtpDir
{
    public sealed class ChybaFtp : Exception
    {
        public ChybaFtp(string zprava) : base(zprava) { }
    }

    /// <summary>Jednoduchý FTP klient (pasivní režim) jen pro čtení výpisu adresáře.</summary>
    public sealed class FtpKlient : IDisposable
    {
        private readonly TcpClient _ridici;
        private readonly Stream _proud;
        private readonly int _timeoutMs;
        private Encoding _kodovani = new UTF8Encoding(false);

        public FtpKlient(string host, int port, int timeoutMs)
        {
            _timeoutMs = timeoutMs;
            _ridici = Pripoj(host, port, timeoutMs, "FTP server " + host + ":" + port);
            _proud = _ridici.GetStream();
            var o = Cti();
            if (o.Kod != 220) throw new ChybaFtp("FTP server neodpověděl správně: " + o.Text);
        }

        public struct Odpoved
        {
            public int Kod;
            public string Text;
        }

        private static TcpClient Pripoj(string host, int port, int timeoutMs, string popis)
        {
            var c = new TcpClient();
            try
            {
                var t = c.ConnectAsync(host, port);
                if (!t.Wait(timeoutMs)) throw new ChybaFtp("Nelze se připojit na " + popis + " (vypršel čas).");
            }
            catch (AggregateException e)
            {
                c.Dispose();
                throw new ChybaFtp("Nelze se připojit na " + popis + ": " + e.GetBaseException().Message);
            }
            catch (ChybaFtp)
            {
                c.Dispose();
                throw;
            }
            c.ReceiveTimeout = timeoutMs;
            c.SendTimeout = timeoutMs;
            return c;
        }

        private string CtiRadek()
        {
            var b = new List<byte>();
            while (true)
            {
                int x;
                try { x = _proud.ReadByte(); }
                catch (IOException) { throw new ChybaFtp("FTP server neodpovídá (vypršel čas)."); }
                if (x < 0) throw new ChybaFtp("FTP server ukončil spojení.");
                if (x == '\n') break;
                if (x != '\r') b.Add((byte)x);
            }
            return _kodovani.GetString(b.ToArray());
        }

        public Odpoved Cti()
        {
            var r = CtiRadek();
            var text = new StringBuilder(r);
            if (r.Length < 3 || !int.TryParse(r.Substring(0, 3), out var kod))
                throw new ChybaFtp("Nesrozumitelná odpověď FTP serveru: " + r);
            if (r.Length > 3 && r[3] == '-')
            {
                var konec = r.Substring(0, 3) + " ";
                do { r = CtiRadek(); text.Append(' ').Append(r); }
                while (!r.StartsWith(konec, StringComparison.Ordinal));
            }
            return new Odpoved { Kod = kod, Text = text.ToString() };
        }

        private void Posli(string prikaz)
        {
            var b = _kodovani.GetBytes(prikaz + "\r\n");
            try { _proud.Write(b, 0, b.Length); }
            catch (IOException e) { throw new ChybaFtp("Spojení s FTP serverem přerušeno: " + e.Message); }
        }

        public Odpoved Prikaz(string prikaz)
        {
            Posli(prikaz);
            return Cti();
        }

        public void Prihlas(string login, string heslo)
        {
            var o = Prikaz("USER " + login);
            if (o.Kod == 331 || o.Kod == 332) o = Prikaz("PASS " + heslo);
            if (o.Kod != 230 && o.Kod != 202) throw new ChybaFtp("Přihlášení se nepovedlo (chybný login nebo heslo?): " + o.Text);
        }

        public void ZmenAdresar(string adresar)
        {
            var o = Prikaz("CWD " + adresar);
            if (o.Kod != 250 && o.Kod != 200) throw new ChybaFtp("Adresář " + adresar + " nelze otevřít: " + o.Text);
        }

        /// <summary>Server poslal při posledním výpisu jen část adresáře (Pure-FTPd: „Output truncated to N matches“).</summary>
        public bool PosledniVypisZkraceny { get; private set; }

        /// <summary>Výpis adresáře (LIST), volby např. "-r" = obráceně podle názvu.</summary>
        public List<string> List(string volby = null)
        {
            Prikaz("TYPE A");
            var o = Prikaz("PASV");
            var m = Regex.Match(o.Text, @"(\d+),(\d+),(\d+),(\d+),(\d+),(\d+)");
            if (o.Kod != 227 || !m.Success) throw new ChybaFtp("Server nepodporuje pasivní režim: " + o.Text);
            var ip = IPAddress.Parse(m.Groups[1].Value + "." + m.Groups[2].Value + "." + m.Groups[3].Value + "." + m.Groups[4].Value);
            var serverIp = ((IPEndPoint)_ridici.Client.RemoteEndPoint).Address;
            if (Neverejna(ip) && !Neverejna(serverIp)) ip = serverIp;
            var port = int.Parse(m.Groups[5].Value) * 256 + int.Parse(m.Groups[6].Value);

            byte[] data;
            using (var dc = Pripoj(ip.ToString(), port, _timeoutMs, "datový kanál FTP " + ip + ":" + port))
            {
                o = Prikaz(string.IsNullOrEmpty(volby) ? "LIST" : "LIST " + volby);
                if (o.Kod != 150 && o.Kod != 125) throw new ChybaFtp("Výpis adresáře se nepovedl: " + o.Text);
                using (var ms = new MemoryStream())
                {
                    try { dc.GetStream().CopyTo(ms); }
                    catch (IOException) { throw new ChybaFtp("Výpis adresáře se nepodařilo přenést (vypršel čas)."); }
                    data = ms.ToArray();
                }
            }
            o = Cti();
            if (o.Kod != 226 && o.Kod != 250) throw new ChybaFtp("Výpis adresáře se nepovedl: " + o.Text);
            PosledniVypisZkraceny = o.Text.IndexOf("truncated", StringComparison.OrdinalIgnoreCase) >= 0;

            string text;
            try { text = new UTF8Encoding(false, true).GetString(data); }
            catch (DecoderFallbackException)
            {
                _kodovani = Encoding.GetEncoding(1250);
                text = _kodovani.GetString(data);
            }
            return new List<string>(text.Replace("\r\n", "\n").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries));
        }

        /// <summary>Přesné časy změny (MDTM). Příkazy se posílají po dávkách bez čekání na odpověď.</summary>
        public Dictionary<string, DateTime> Mdtm(IList<string> soubory, int davka = 100)
        {
            var vysledek = new Dictionary<string, DateTime>(StringComparer.Ordinal);
            for (int i = 0; i < soubory.Count; i += davka)
            {
                int n = Math.Min(davka, soubory.Count - i);
                var sb = new StringBuilder();
                for (int j = 0; j < n; j++) sb.Append("MDTM ").Append(soubory[i + j]).Append("\r\n");
                var b = _kodovani.GetBytes(sb.ToString());
                try { _proud.Write(b, 0, b.Length); }
                catch (IOException e) { throw new ChybaFtp("Spojení s FTP serverem přerušeno: " + e.Message); }
                for (int j = 0; j < n; j++)
                {
                    var d = VypisFtp.Mdtm(Cti().Text);
                    if (d.HasValue) vysledek[soubory[i + j]] = d.Value;
                }
            }
            return vysledek;
        }

        private static bool Neverejna(IPAddress ip)
        {
            var b = ip.GetAddressBytes();
            return b.Length == 4 && (b[0] == 10 || b[0] == 127 || b[0] == 0 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254));
        }

        public void Dispose()
        {
            try { Posli("QUIT"); Cti(); } catch (Exception) { }
            _ridici.Dispose();
        }
    }
}

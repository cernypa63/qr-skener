using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Management;
using System.Runtime.InteropServices;

namespace Otevrene
{
    /// <summary>
    /// Windows Server 2012 a novější: WMI MSFT_SmbOpenFile (u každého souboru je IP adresa klienta).
    /// Starší Windows (nebo /net): NetFileEnum + NetSessionEnum, IP se přiřadí podle uživatele.
    /// </summary>
    internal static class Zdroje
    {
        private const int ErrorAccessDenied = 5;
        private const int ErrorMoreData = 234;
        private const int MaxPreferredLength = -1;

        public static KeyValuePair<string, List<OtevrenySoubor>> Nacti(bool jenNetApi)
        {
            if (!jenNetApi)
            {
                var smb = NactiSmb();
                if (smb != null) return new KeyValuePair<string, List<OtevrenySoubor>>("SMB", smb);
            }
            var soubory = NactiNetFile();
            Aplikace.DoplnIp(soubory, NactiSezeni());
            return new KeyValuePair<string, List<OtevrenySoubor>>("NetFileEnum, IP podle uzivatele", soubory);
        }

        /// <summary>null = tento Windows WMI třídu MSFT_SmbOpenFile nemá.</summary>
        private static List<OtevrenySoubor> NactiSmb()
        {
            try
            {
                var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\SMB");
                scope.Connect();
                var dotaz = new ObjectQuery("SELECT FileId, ClientComputerName, ClientUserName, Path FROM MSFT_SmbOpenFile");
                var vysledek = new List<OtevrenySoubor>();
                using (var hledani = new ManagementObjectSearcher(scope, dotaz))
                    foreach (ManagementObject o in hledani.Get())
                        using (o)
                            vysledek.Add(new OtevrenySoubor
                            {
                                Id = Convert.ToUInt64(o["FileId"] ?? 0UL),
                                Ip = Convert.ToString(o["ClientComputerName"]) ?? "",
                                Uzivatel = Convert.ToString(o["ClientUserName"]) ?? "",
                                Cesta = Convert.ToString(o["Path"]) ?? "",
                            });
                return vysledek;
            }
            catch (ManagementException e) when (e.ErrorCode == ManagementStatus.AccessDenied)
            {
                throw new PristupOdepren("Pristup odepren: seznam otevrenych souboru smi zjistit jen spravce.");
            }
            catch (UnauthorizedAccessException)
            {
                throw new PristupOdepren("Pristup odepren: seznam otevrenych souboru smi zjistit jen spravce.");
            }
            catch (ManagementException) { return null; }
            catch (COMException) { return null; }
        }

        private static List<OtevrenySoubor> NactiNetFile()
        {
            var vysledek = new List<OtevrenySoubor>();
            IntPtr buf;
            int precteno, celkem;
            var kod = NetFileEnum(null, null, null, 3, out buf, MaxPreferredLength, out precteno, out celkem, IntPtr.Zero);
            try
            {
                Kontrola(kod, "NetFileEnum");
                var velikost = Marshal.SizeOf(typeof(FILE_INFO_3));
                for (var i = 0; i < precteno; i++)
                {
                    var f = (FILE_INFO_3)Marshal.PtrToStructure(new IntPtr(buf.ToInt64() + (long)i * velikost), typeof(FILE_INFO_3));
                    vysledek.Add(new OtevrenySoubor { Id = (uint)f.fi3_id, Uzivatel = f.fi3_username ?? "", Cesta = f.fi3_pathname ?? "" });
                }
            }
            finally { if (buf != IntPtr.Zero) NetApiBufferFree(buf); }
            return vysledek;
        }

        private static List<Sezeni> NactiSezeni()
        {
            var vysledek = new List<Sezeni>();
            IntPtr buf;
            int precteno, celkem;
            var kod = NetSessionEnum(null, null, null, 10, out buf, MaxPreferredLength, out precteno, out celkem, IntPtr.Zero);
            try
            {
                Kontrola(kod, "NetSessionEnum");
                var velikost = Marshal.SizeOf(typeof(SESSION_INFO_10));
                for (var i = 0; i < precteno; i++)
                {
                    var s = (SESSION_INFO_10)Marshal.PtrToStructure(new IntPtr(buf.ToInt64() + (long)i * velikost), typeof(SESSION_INFO_10));
                    vysledek.Add(new Sezeni { Ip = Aplikace.BezLomitek(s.sesi10_cname), Uzivatel = s.sesi10_username ?? "" });
                }
            }
            finally { if (buf != IntPtr.Zero) NetApiBufferFree(buf); }
            return vysledek;
        }

        private static void Kontrola(int kod, string funkce)
        {
            if (kod == 0 || kod == ErrorMoreData) return;
            if (kod == ErrorAccessDenied)
                throw new PristupOdepren("Pristup odepren: seznam otevrenych souboru smi zjistit jen spravce.");
            throw new Exception(funkce + ": " + new Win32Exception(kod).Message + " (kod " + kod + ")");
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct FILE_INFO_3
        {
            public int fi3_id;
            public int fi3_permissions;
            public int fi3_num_locks;
            public string fi3_pathname;
            public string fi3_username;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SESSION_INFO_10
        {
            public string sesi10_cname;
            public string sesi10_username;
            public int sesi10_time;
            public int sesi10_idle_time;
        }

        [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int NetFileEnum(string servername, string basepath, string username, int level,
            out IntPtr bufptr, int prefmaxlen, out int entriesread, out int totalentries, IntPtr resumeHandle);

        [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int NetSessionEnum(string servername, string uncClientName, string username, int level,
            out IntPtr bufptr, int prefmaxlen, out int entriesread, out int totalentries, IntPtr resumeHandle);

        [DllImport("netapi32.dll")]
        private static extern int NetApiBufferFree(IntPtr buffer);
    }
}

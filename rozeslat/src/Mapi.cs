using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Rozeslat
{
    // Simple MAPI: e-mail předá výchozímu poštovnímu programu, ten ho odešle
    // a uloží do Odeslané pošty.
    public static class Mapi
    {
        const uint MAPI_LOGON_UI = 0x1, MAPI_DIALOG = 0x8, MAPI_TO = 1;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        class ZpravaW
        {
            public int ulReserved;
            public string lpszSubject, lpszNoteText, lpszMessageType, lpszDateReceived, lpszConversationID;
            public int flFlags;
            public IntPtr lpOriginator;
            public int nRecipCount;
            public IntPtr lpRecips;
            public int nFileCount;
            public IntPtr lpFiles;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        class AdresatW
        {
            public int ulReserved, ulRecipClass;
            public string lpszName, lpszAddress;
            public int ulEIDSize;
            public IntPtr lpEntryID;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        class SouborW
        {
            public int ulReserved, flFlags, nPosition;
            public string lpszPathName, lpszFileName;
            public IntPtr lpFileType;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        class ZpravaA
        {
            public int ulReserved;
            public string lpszSubject, lpszNoteText, lpszMessageType, lpszDateReceived, lpszConversationID;
            public int flFlags;
            public IntPtr lpOriginator;
            public int nRecipCount;
            public IntPtr lpRecips;
            public int nFileCount;
            public IntPtr lpFiles;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        class AdresatA
        {
            public int ulReserved, ulRecipClass;
            public string lpszName, lpszAddress;
            public int ulEIDSize;
            public IntPtr lpEntryID;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        class SouborA
        {
            public int ulReserved, flFlags, nPosition;
            public string lpszPathName, lpszFileName;
            public IntPtr lpFileType;
        }

        [DllImport("MAPI32.DLL", EntryPoint = "MAPISendMailW", CharSet = CharSet.Unicode)]
        static extern uint MAPISendMailW(IntPtr sess, IntPtr hwnd, ZpravaW msg, uint flags, uint rsv);

        [DllImport("MAPI32.DLL", EntryPoint = "MAPISendMail", CharSet = CharSet.Ansi)]
        static extern uint MAPISendMailA(IntPtr sess, IntPtr hwnd, ZpravaA msg, uint flags, uint rsv);

        static bool bezW;

        static IntPtr Pole<T>(IList<T> polozky, List<Action> uklid)
        {
            if (polozky.Count == 0) return IntPtr.Zero;
            int vel = Marshal.SizeOf(typeof(T));
            IntPtr p = Marshal.AllocHGlobal(vel * polozky.Count);
            for (int i = 0; i < polozky.Count; i++)
            {
                IntPtr q = IntPtr.Add(p, i * vel);
                Marshal.StructureToPtr(polozky[i], q, false);
                uklid.Add(() => Marshal.DestroyStructure(q, typeof(T)));
            }
            uklid.Add(() => Marshal.FreeHGlobal(p));
            return p;
        }

        /// <summary>Vrací 0 = předáno k odeslání, jinak kód chyby MAPI.</summary>
        public static uint Odesli(string adresa, string predmet, string text, IList<string> prilohy, bool zobrazit, IntPtr okno)
        {
            uint fl = MAPI_LOGON_UI | (zobrazit ? MAPI_DIALOG : 0);
            // Některé programy vyžadují na konci textu prázdný řádek kvůli umístění příloh.
            text = (text ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n");
            if (prilohy.Count > 0) text += "\r\n";
            var uklid = new List<Action>();
            try
            {
                if (!bezW)
                {
                    try
                    {
                        var ad = new[] { new AdresatW { ulRecipClass = (int)MAPI_TO, lpszName = adresa, lpszAddress = "SMTP:" + adresa } };
                        var so = new List<SouborW>();
                        foreach (var p in prilohy) so.Add(new SouborW { nPosition = -1, lpszPathName = p, lpszFileName = Path.GetFileName(p) });
                        var z = new ZpravaW { lpszSubject = predmet, lpszNoteText = text, nRecipCount = 1, lpRecips = Pole(ad, uklid), nFileCount = so.Count, lpFiles = Pole(so, uklid) };
                        return MAPISendMailW(IntPtr.Zero, okno, z, fl, 0);
                    }
                    catch (EntryPointNotFoundException) { bezW = true; }
                    foreach (var u in uklid) u();
                    uklid.Clear();
                }
                var adA = new[] { new AdresatA { ulRecipClass = (int)MAPI_TO, lpszName = adresa, lpszAddress = "SMTP:" + adresa } };
                var soA = new List<SouborA>();
                foreach (var p in prilohy) soA.Add(new SouborA { nPosition = -1, lpszPathName = p, lpszFileName = Path.GetFileName(p) });
                var zA = new ZpravaA { lpszSubject = predmet, lpszNoteText = text, nRecipCount = 1, lpRecips = Pole(adA, uklid), nFileCount = soA.Count, lpFiles = Pole(soA, uklid) };
                return MAPISendMailA(IntPtr.Zero, okno, zA, fl, 0);
            }
            finally
            {
                foreach (var u in uklid) u();
            }
        }

        public static string PopisChyby(uint kod)
        {
            switch (kod)
            {
                case 0: return "odesláno";
                case 1: return "zrušeno uživatelem";
                case 2: return "poštovní program e-mail odmítl (chyba 2)";
                case 3: return "nepodařilo se přihlásit do poštovního programu (chyba 3)";
                case 5: return "nedostatek paměti (chyba 5)";
                case 11: return "příloha nenalezena (chyba 11)";
                case 12: return "přílohu nelze otevřít (chyba 12)";
                case 14: return "neznámý adresát (chyba 14)";
                case 21: return "neplatná adresa (chyba 21)";
                case 26: return "poštovní program tuto funkci nepodporuje (chyba 26)";
                default: return "chyba " + kod;
            }
        }
    }
}

using System;
using Microsoft.Win32;

namespace Rozeslat
{
    // Výchozí poštovní program a jeho bitová verze (Outlook musí mít stejnou jako tento program).
    public static class PostovniProgram
    {
        public static string Nazev()
        {
            foreach (var koren in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                using (var k = koren.OpenSubKey(@"Software\Clients\Mail"))
                {
                    var n = k?.GetValue("") as string;
                    if (!string.IsNullOrEmpty(n)) return n;
                }
            }
            return "";
        }

        static string Hodnota(RegistryView pohled, string cesta, string nazev)
        {
            try
            {
                using (var b = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, pohled))
                using (var k = b.OpenSubKey(cesta))
                    return k?.GetValue(nazev) as string;
            }
            catch { return null; }
        }

        /// <summary>true = Outlook je 32bitový, false = 64bitový, null = nevím.</summary>
        public static bool? OutlookJe32()
        {
            if (!Nazev().ToLowerInvariant().Contains("outlook")) return null;
            if (!Environment.Is64BitOperatingSystem) return true;
            var pl = Hodnota(RegistryView.Registry64, @"SOFTWARE\Microsoft\Office\ClickToRun\Configuration", "Platform");
            if (pl != null) return pl.Equals("x86", StringComparison.OrdinalIgnoreCase);
            foreach (var v in new[] { "16.0", "15.0", "14.0" })
            {
                var b = Hodnota(RegistryView.Registry64, @"SOFTWARE\Microsoft\Office\" + v + @"\Outlook", "Bitness");
                if (b != null) return b.Equals("x86", StringComparison.OrdinalIgnoreCase);
                if (Hodnota(RegistryView.Registry32, @"SOFTWARE\Microsoft\Office\" + v + @"\Outlook", "Bitness") != null) return true;
            }
            return null;
        }
    }
}

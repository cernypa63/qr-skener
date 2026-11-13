using System.Security.Cryptography;
using System.Text;

namespace SendFtp
{
    /// <summary>
    /// Šifrování Windows DPAPI pro celý počítač: soubor nastavení přečte jen SENDFTP na tomto počítači
    /// (pod kterýmkoli uživatelem), zkopírovaný na jiný počítač je k ničemu.
    /// </summary>
    public sealed class Dpapi : ISifra
    {
        private static readonly byte[] Entropie = Encoding.ASCII.GetBytes("SENDFTP-nastaveni-1");

        public byte[] Zasifruj(byte[] data) => ProtectedData.Protect(data, Entropie, DataProtectionScope.LocalMachine);
        public byte[] Desifruj(byte[] data) => ProtectedData.Unprotect(data, Entropie, DataProtectionScope.LocalMachine);
    }
}

# Vlastní vzdálená správa – RustDesk s vlastním serverem

Náhrada TeamVieweru: zákazník spustí jeden exe, Vy vidíte jeho plochu, ovládáte myš a klávesnici a kopírujete soubory oběma směry.
Spojení jde přes **Váš vlastní server** (ne přes cizí servery).

Použité verze: server RustDesk 1.1.16, klient RustDesk 1.5.3 (oficiální, zdarma, open source).

## 1. Server (jen jednou)

Server potřebuje veřejnou IP adresu (nebo doménu) a otevřené porty:

| Port | Protokol | K čemu |
|---|---|---|
| 21115 | TCP | test typu sítě |
| 21116 | TCP + UDP | ID server, navázání spojení |
| 21117 | TCP | relay (přenos obrazu, když se PC nespojí napřímo) |

### Varianta A – linuxový VPS (doporučeno)
1. Objednejte VPS s Ubuntu 22.04 nebo 24.04 (stačí nejmenší, 1 CPU, 1 GB RAM).
2. Nahrajte `server-linux/instalace.sh` a spusťte jako root: `bash instalace.sh`
3. Skript vypíše **klíč serveru** a veřejnou IP. Obojí si poznamenejte.

Data serveru jsou v `/opt/rustdesk-server`. Soubor `id_ed25519` je tajný klíč serveru – zazálohujte ho a nikomu nedávejte.
Když ho ztratíte, vytvoří se nový a všechny klientské exe bude nutné vyrobit znovu.

### Varianta B – Windows Server 2016
1. Stáhněte `rustdesk-server-windows-x86_64-unsigned.zip`
   z https://github.com/rustdesk/rustdesk-server/releases/tag/1.1.16 a vyberte z něj `hbbs.exe` a `hbbr.exe`.
2. Stáhněte NSSM z https://nssm.cc/download a vezměte `win64\nssm.exe`.
3. Všechny tři soubory dejte do složky s `server-windows/instalace.bat` a spusťte ho jako správce.
4. Skript vytvoří služby RustDesk_hbbs a RustDesk_hbbr, otevře porty ve Windows Firewallu a vypíše **klíč serveru**.
5. Na routeru přesměrujte na server porty TCP 21115–21117 a UDP 21116.

Data serveru jsou v `C:\RustDeskServer`.

## 2. Klientský exe

Ve Windows spusťte v PowerShellu (adresu a klíč nahraďte svými):
```
powershell -ExecutionPolicy Bypass -File klient\vytvor_klienta.ps1 -Server 123.45.67.89 -Klic "KLIC_SERVERU"
```
Skript stáhne oficiální `rustdesk-1.5.3-x86_64.exe` a uloží ho pod jménem
`rustdesk-licensed-XXXXXXXX.exe`. V tomto jménu je zakódovaná adresa Vašeho serveru a klíč.
**Jméno souboru se nesmí měnit**, jinak se klient připojí na veřejné servery RustDesk.

Tento exe pošlete zákazníkům. Stejný exe použijte i na svém PC.

## 3. Používání
- Zákazník spustí exe (nic se neinstaluje) a nadiktuje Vám **ID** a **heslo**.
- Vy na svém PC zadáte ID, kliknete na Připojit a zadáte heslo.
- Soubory: v okně spojení tlačítko „Přenos souborů“ (oba směry).
- Okna správce (UAC): když má zákazník spuštěný exe bez práv správce, takové okno neuvidíte ani neovládnete.
  Řešení: zákazník spustí exe pravým tlačítkem „Spustit jako správce“, nebo RustDesk nainstaluje (nabízí to sám).
- Pro trvalý přístup bez zákazníka: RustDesk na jeho PC nainstalujte a nastavte trvalé heslo.

## 4. Kontrola, že vše jde přes Váš server
V klientovi: ☰ → Nastavení → Síť → ID/Relay server. Musí tam být adresa Vašeho serveru a klíč.
Dole v okně klienta musí být „Připraveno“. Když je tam chyba klíče, klíč v názvu exe nesedí se serverem.

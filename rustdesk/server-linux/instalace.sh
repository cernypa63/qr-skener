#!/bin/bash
# Instalace RustDesk serveru (hbbs + hbbr) na Linux (Ubuntu/Debian). Spustit jako root:
#   bash instalace.sh
set -e
VERZE=1.1.16
ADR=/opt/rustdesk-server
apt-get update -qq && apt-get install -y -qq unzip curl
id rustdesk >/dev/null 2>&1 || useradd --system --home-dir $ADR --shell /usr/sbin/nologin rustdesk
mkdir -p $ADR && cd $ADR
curl -fsSL -o server.zip https://github.com/rustdesk/rustdesk-server/releases/download/$VERZE/rustdesk-server-linux-amd64.zip
unzip -oq server.zip
systemctl stop rustdesk-hbbs rustdesk-hbbr 2>/dev/null || true
cp amd64/hbbs amd64/hbbr amd64/rustdesk-utils . && rm -rf amd64 server.zip
chown -R rustdesk:rustdesk $ADR
for s in hbbs hbbr; do
cat > /etc/systemd/system/rustdesk-$s.service <<U
[Unit]
Description=RustDesk $s
After=network.target
[Service]
User=rustdesk
Group=rustdesk
WorkingDirectory=$ADR
ExecStart=$ADR/$s
Restart=always
NoNewPrivileges=true
[Install]
WantedBy=multi-user.target
U
done
systemctl daemon-reload
systemctl enable rustdesk-hbbs rustdesk-hbbr
systemctl restart rustdesk-hbbs rustdesk-hbbr
# Firewall: pravidla se pridaji jen kdyz je ufw zapnuty (nezapina se, aby se neodriznul SSH).
if command -v ufw >/dev/null && ufw status | grep -q "Status: active"; then
  ufw allow 21115:21117/tcp; ufw allow 21116/udp
else
  echo "POZOR: ufw neni zapnuty. Pokud ma VPS firewall u poskytovatele, otevrete v nem TCP 21115-21117 a UDP 21116."
fi
for i in $(seq 1 15); do [ -f $ADR/id_ed25519.pub ] && break; sleep 1; done
echo
echo "Server bezi. Klic serveru (key) pro klienty:"
cat $ADR/id_ed25519.pub; echo
echo "Verejna IP adresa serveru:"
curl -fsS https://api.ipify.org; echo

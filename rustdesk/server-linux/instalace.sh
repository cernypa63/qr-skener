#!/bin/bash
# Instalace RustDesk serveru (hbbs + hbbr) na Linux (Ubuntu/Debian). Spustit jako root:
#   bash instalace.sh
set -e
VERZE=1.1.16
ADR=/opt/rustdesk-server
apt-get update -qq && apt-get install -y -qq unzip curl
mkdir -p $ADR && cd $ADR
curl -fsSL -o server.zip https://github.com/rustdesk/rustdesk-server/releases/download/$VERZE/rustdesk-server-linux-amd64.zip
unzip -oq server.zip && cp amd64/hbbs amd64/hbbr amd64/rustdesk-utils . && rm -rf amd64 server.zip
for s in hbbs hbbr; do
cat > /etc/systemd/system/rustdesk-$s.service <<U
[Unit]
Description=RustDesk $s
After=network.target
[Service]
WorkingDirectory=$ADR
ExecStart=$ADR/$s
Restart=always
[Install]
WantedBy=multi-user.target
U
done
systemctl daemon-reload
systemctl enable --now rustdesk-hbbs rustdesk-hbbr
if command -v ufw >/dev/null; then
  ufw allow 22/tcp; ufw allow 21115:21117/tcp; ufw allow 21116/udp; ufw --force enable
fi
for i in 1 2 3 4 5 6 7 8 9 10; do [ -f $ADR/id_ed25519.pub ] && break; sleep 1; done
echo
echo "Server bezi. Klic serveru (key) pro klienty:"
cat $ADR/id_ed25519.pub; echo
echo "Verejna IP adresa serveru:"
curl -fsS https://api.ipify.org; echo

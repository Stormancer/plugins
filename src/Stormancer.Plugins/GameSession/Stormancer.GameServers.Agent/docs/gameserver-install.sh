echo "deb http://security.ubuntu.com/ubuntu focal-security main" | sudo tee /etc/apt/sources.list.d/focal-security.list
apt-get update
apt-get install libssl1.1 -y
dpkg -L libssl1.1
apt install dotnet-sdk-8.0 -y
apt install docker.io -y
mkdir /home/agent
cd /home/agent
dotnet new tool-manifest
dotnet tool install --local Stormancer.GameServers.Agent --version 0.5.1.2
dotnet tool restore
cat << EOF | base64 --decode >> gameserver-agent.pfx
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
MPgPAgIH0A==
EOF
tee appsettings.json << EOF
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "Agent": {
    "MinPort":40000,
    "MaxPort":40999,
    "Id":"karmazoo-gs-asia-2",
    "PublicIp": "103.23.208.251",
    "MaxMemory": 2000000000,
    "MaxCpu": 2,
    "Region":"asia",
    "PrivateKeyPassword": ",:7GW5aaXL{fT=\\\=",
    "PrivateKeyPath": "gameserver-agent.pfx",
    "Authority":"https://dev-stormancer.eu.auth0.com/",
    "Audience":"https://karma-1-admin.stormancer.com"
  }
}
EOF
tee /etc/systemd/system/gameservers-agent.service << EOF
[Unit]
Description=Game server agent
After=network-online.target

[Service]
Type=simple

User=root

ExecStart=/usr/bin/dotnet tool run stormancer-gameservers-agent
WorkingDirectory=/home/agent

Restart=on-failure

TimeoutStopSec=30

[Install]
WantedBy=multi-user.target
EOF
systemctl daemon-reload
systemctl enable gameservers-agent
systemctl start gameservers-agent
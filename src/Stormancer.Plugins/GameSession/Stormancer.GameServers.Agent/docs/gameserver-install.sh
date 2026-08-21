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
<base64 .pfx file content>
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
    "Id":"<agent id>",
    "PublicIp": "<agent public ip>",
    "MaxMemory": 2000000000,
    "MaxCpu": 2,
    "Region":"<agent region>",
    "PrivateKeyPassword": "<password for the pfx file>",
    "PrivateKeyPath": "gameserver-agent.pfx",
    "Authority":"<oauth authority>",
    "Audience":"<cluster's admin api url>"
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
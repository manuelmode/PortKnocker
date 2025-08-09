Quick MikroTik config

Goal: Only open Winbox (8291) after a correct 3-step knock (TCP 60722 → UDP 56321 → UDP 52913) from the same source IP. Access stays open for 5 minutes (changeable).

Works on RouterOS v6/v7. Paste in /ip firewall (Terminal).

1.	Address lists (auto-filled by the rules; no manual entries needed):
•	pk.stage1, pk.stage2: temporary “in-progress” knockers
•	pk.allowed: sources that completed the sequence
2.	Rules (order matters. Place them near the top of your input chain, after “established/related allow”, before general drops):

/ip firewall filter
# 0) Allow established/related first (you likely already have these)
add chain=input connection-state=established,related action=accept comment="allow established/related"

# 1) Final allow: once knocked, allow Winbox (and any other ports you want)
add chain=input src-address-list=pk.allowed protocol=tcp dst-port=8291 action=accept comment="PK: allow Winbox for knocked IPs"
# (Optional) allow SSH, etc:
# add chain=input src-address-list=pk.allowed protocol=tcp dst-port=22 action=accept comment="PK: allow SSH for knocked IPs"

# 2) Knock #1 -> add to stage1 for 30s
add chain=input protocol=tcp dst-port=60722 action=add-src-to-address-list \
    address-list=pk.stage1 address-list-timeout=30s comment="PK: step1 TCP 60722"

# 3) Knock #2 -> must be in stage1, then move to stage2 for 30s
add chain=input src-address-list=pk.stage1 protocol=udp dst-port=56321 action=add-src-to-address-list \
    address-list=pk.stage2 address-list-timeout=30s comment="PK: step2 UDP 56321"

# 4) Knock #3 (final) -> must be in stage2, then add to allowed for 5m
add chain=input src-address-list=pk.stage2 protocol=udp dst-port=52913 action=add-src-to-address-list \
    address-list=pk.allowed address-list-timeout=5m comment="PK: step3 UDP 52913 -> ALLOWED 5m"

# 5) (Recommended) Drop invalid
add chain=input connection-state=invalid action=drop comment="drop invalid"

# 6) Your normal allowlist rules (LAN mgmt subnets, etc) can go here

# 7) Default drop (keep at the end)
add chain=input action=drop comment="drop everything else"

Optional: ICMP “payload size” knock (advanced)

If you want to add (or replace with) an ICMP step using custom payload sizes (so you can knock from anywhere using ping), RouterOS can match on packet-size.

Packet-size on MikroTik = payload + 28 bytes

Example for a single ICMP step if you enter 100 bytes (packet-size 128):

/ip firewall filter
add chain=input protocol=icmp icmp-options=8:0 packet-size=128 action=add-src-to-address-list \
    address-list=pk.stage1 address-list-timeout=30s comment="PK: step1 ICMP echo payload 100B (packet-size 128)"

You can mix ICMP with TCP/UDP steps. Just remember to compute packet-size = payload + 28.
(Your desktop app’s ICMP step size should match the payload value you set in your knocking profile.)



Build your own package


Build your own package
Prereqs: Windows 10/11

.NET SDK 8.x (run dotnet --version to confirm)

Git

A) Portable ZIP (fastest) User Powershell

1 . Clone:

git clone https://github.com/manuelmode/PortKnocker.git
cd <repo>/PortKnocker

2. Publish a self-contained build (no .NET runtime required): 

dotnet publish PortKnocker.csproj -c Release -r win-x64 ^
  -p:SelfContained=true -p:PublishSingleFile=true -p:PublishTrimmed=false ^
  -p:IncludeNativeLibrariesForSelfExtract=true
Grab the output:

3. Grab the Output:
PortKnocker\bin\Release\net8.0-windows\win-x64\publish\
You’ll see PortKnocker.exe there. Zip the publish folder and share it:


4. Compress-Archive -Path .\bin\Release\net8.0-windows\win-x64\publish\* `
  -DestinationPath ..\PortKnocker-win-x64.zip -Force
  
Notes
• The app reads/writes profiles.json next to the EXE, so keep it in the same folder.



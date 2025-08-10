# What Is PortKnocker?
**PortKnocker** is a simple, fast, and secure way to open hidden MikroTik ports on demand.  
With just a click, it sends a custom knock sequence to your router—unlocking services like **Winbox** or **SSH** for a limited time.  
No VPN, no constant open ports—just stealthy, on-demand access.

## Key Features

### 1. Flexible Knock Sequences
- Define up to four steps in any mix of TCP, UDP or ICMP.  
- Specify the exact port order for your knock sequence.  

### 2. Adjustable Timing
- Customizable delays between knock steps (in milliseconds).  
- Configurable TCP timeout settings to keep connections responsive.  

### 3. Profile Management
- Save, edit, or delete destination profiles (hostname/IP + knock steps) for quick reuse.  
- One-click to fire the knock—no typing required.  

### 4. Auto-Knock Monitor
- Watches your outgoing traffic for attempts to reach a protected port.  
- Automatically runs the correct knock profile for the destination, so you don’t have to.  

### 5. DNS-Friendly
- Accepts either IP addresses or hostnames (with background DNS resolution).  

### 6. Built-In Logging
- Track knock activities through a visible log panel in the app.  
- Optionally log to a file for troubleshooting or auditing.  

### 7. Non-Intrusive Design
- The app simply sends packets—it does not alter firewall configurations.  



# Quick MikroTik config. 
### Based on the default Mikrotik firewall rules (for SoHo)

Goal: Only open Winbox (8291) after a correct 3-step knock (TCP 60722 → UDP 56321 → UDP 52913) from the same source IP. Access stays open for 30 seconds (changeable).

Works on RouterOS v6/v7. Paste in /ip firewall (Terminal).

### Rules (order matters. Place them near the top of your input chain, after “established/related allow”, before general drops):

```
/interface list
add name=WAN comment=defconf
add name=LAN comment=defconf

/interface list member
add list=WAN interface=ether1 comment=defconf
add list=LAN interface=bridge comment=defconf

/ip firewall filter
add chain=input action=accept connection-state=established,related,untracked comment="defconf: accept established,related,untracked"

# Allow WinBox after successful knock (address-list 'knocked')
add chain=input protocol=tcp dst-port=8291 src-address-list=knocked action=accept comment="WinBox after knock"

# Knock steps: TCP 60722 -> UDP 56321 -> UDP 52913
add chain=input protocol=tcp dst-port=60722 action=add-src-to-address-list address-list=step1 address-list-timeout=30s comment="Knock step 1"
add chain=input protocol=udp dst-port=56321 src-address-list=step1 action=add-src-to-address-list address-list=step2 address-list-timeout=30s comment="Knock step 2"
add chain=input protocol=udp dst-port=52913 src-address-list=step2 action=add-src-to-address-list address-list=knocked address-list-timeout=5m comment="Final step -> allow"

# ^End of port knocking additions
add chain=input action=drop connection-state=invalid  comment="defconf: drop invalid"
add chain=input action=accept protocol=icmp  comment="defconf: accept ICMP"
add chain=input action=accept dst-address=127.0.0.1  comment="defconf: accept to local loopback (for CAPsMAN)"

# Default drop (last in input chain)
add chain=input action=drop in-interface-list=!LAN  comment="defconf: drop all not coming from LAN"
add chain=forward action=accept ipsec-policy=in,ipsec  comment="defconf: accept in ipsec policy"
add chain=forward action=accept ipsec-policy=out,ipsec  comment="defconf: accept out ipsec policy"

# hw-offload=yes only on 7.18+
add chain=forward action=fasttrack-connection connection-state=established,related hw-offload=yes comment="defconf: fasttrack"
add chain=forward action=accept connection-state=established,related,untracked comment="defconf: accept established,related, untracked"
add chain=forward action=drop connection-state=invalid  comment="defconf: drop invalid"
add chain=forward action=drop in-interface-list=WAN connection-nat-state=!dstnat connection-state=new comment="defconf: drop all from WAN not DSTNATed"

/ip firewall nat
add chain=srcnat action=masquerade out-interface-list=WAN ipsec-policy=out,none comment="defconf: masquerade"
```

### Optional: ICMP “payload size” knock (advanced)

If you want to add (or replace with) an ICMP step using custom payload sizes (so you can knock from anywhere using ping), RouterOS can match on packet-size.

Packet-size on MikroTik = payload + 28 bytes

Example for a single ICMP step if you enter 100 bytes (packet-size 128):

```
/ip firewall filter
add chain=input protocol=icmp icmp-options=8:0 packet-size=128 action=add-src-to-address-list \
    address-list=pk.stage1 address-list-timeout=30s comment="PK: step1 ICMP echo payload 100B (packet-size 128)"
```
You can mix ICMP with TCP/UDP steps. Just remember to compute packet-size = payload + 28.
(Your desktop app’s ICMP step size should match the payload value you set in your knocking profile.)


# Build your own package


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



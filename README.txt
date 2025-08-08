PortKnocker by manuelmode
A tiny Windows 10/11 app to send classic port knocks (up to 4 steps, TCP/UDP), save destinations as profiles, and auto-knock when it sees you connecting to Winbox/SSH/whatever. Built with WPF/.NET.

Features
4-step knocks — each step can be TCP or UDP with the exact order you set

Adjustable timing — per-step delay (ms) and TCP timeout (ms)

Profiles — save Name + IP/Host + steps; Save/Update/Delete and Knock

Auto-Knock Monitor — watches outbound traffic to chosen ports (defaults 58291, 8291) and auto-fires the matching profile for that IP/host

Mappings — tell the monitor what to do:

Match types: AnyIp, ExactIp, Cidr

Optional port filter per rule

“First match wins”

DNS supported — use hostnames instead of raw IPs

Logs — on-screen log + rolling log files

Portable data — profiles.json lives next to the EXE

Modern UI — Windows 11 style chrome & app icon

Installer (optional) — WiX/MSI recipe available

Quick Start
Download & run the EXE (or build from source below).

Fill Destination → IP/Host and up to 4 Knock Steps.

Set Step delay (e.g. 200ms) and TCP timeout (e.g. 400ms).

Click Save / Update to store the profile.

(Optional) In Auto-Knock Mappings, add a rule:

Type: ExactIp (or AnyIp / Cidr)

Pattern: the same IP/host

Ports (CSV): usually 58291 (you can leave empty for “any”)

Profile: pick the profile you saved

Enable the Auto-Knock Monitor. When the app sees an outbound connection to a watched port, it will knock automatically for that IP/host.

Or just press Knock to do it manually.

Tip: When you save a new destination, the app can auto-create a matching mapping (ExactIp + ports 58291) if you leave that preference on.

Profiles & Mappings (JSON)
profiles.json sits next to PortKnocker.exe and looks like this:

json
Copy
Edit
{
  "Profiles": [
    {
      "Name": "FusionWAN1",
      "IpAddress": "208.90.215.146",
      "Steps": [
        { "Protocol": "TCP", "Port": 60722 },
        { "Protocol": "UDP", "Port": 56321 },
        { "Protocol": "UDP", "Port": 52913 }
      ],
      "DelayMs": 200,
      "TcpTimeoutMs": 400
    }
  ],
  "Mappings": [
    {
      "Name": "FusionWAN1",
      "Type": "ExactIp",
      "Pattern": "208.90.215.146",
      "Ports": [58291],
      "ProfileName": "FusionWAN1"
    }
  ],
  "WatchPorts": [58291, 8291]
}
Profiles → what to knock

Mappings → when/where to auto-knock

WatchPorts → the monitor listens for these outbound ports at startup

Build from Source
Requirements: .NET 8 SDK, Windows 10/11

powershell
Copy
Edit
# clone your repo
git clone https://github.com/<you>/PortKnocker.git
cd PortKnocker

# build
dotnet build -c Release

# run
dotnet run -c Release
MSI installer (optional): If you use the WiX setup included in the repo, build the Setup project from Visual Studio (or follow the included notes in the installer folder).

Logs
In-app: the lower panel shows activity

Files: %AppData%\PortKnocker\logs\PortKnocker_YYYY-MM-DD.txt

Use Open Logs in the UI to jump there quickly

FAQ
Does it support hostnames?
Yep. We resolve the name each time before knocking and before matching an ExactIp mapping.

Where is my data stored?
profiles.json sits in the same folder as the EXE so you can move it around with the app.

Should I commit profiles.json to GitHub?
Only if it doesn’t contain sensitive targets. Otherwise add it to .gitignore.

bin/obj in git?
Nope. Ignore them.

Troubleshooting
“The calling thread cannot access this object…”
Fixed in recent builds (background events dispatch to the UI thread). If you still see it, update to the latest.

Auto-knock didn’t trigger

Check Mappings order (“first match wins”)

Confirm Watch ports contains the port you hit

Verify the rule’s Type/Pattern matches your destination

Hostnames
Make sure DNS resolves from the machine running PortKnocker.

Contributing
PRs and issues welcome. Keep it simple and avoid adding heavyweight dependencies. If you send a feature, include a small note in the README and a screenshot if it changes the UI.

License
MIT. Do what you want; no warranties. Use responsibly and only against networks you control.
# Authorized Network Exposure Scanner

A low-impact TCP exposure check for one exact IPv4 address selected by the service operator. It checks 11 common ports sequentially with a 500 ms per-port timeout and performs TCP connection attempts only: no exploits, authentication attempts, payloads, or service/banner collection. An open port means a TCP connection was accepted, not that a vulnerability was confirmed.

## Configure

Set these environment variables on the server:

- `NETWORK_SCANNER_TARGET`: one private IPv4 address on a network you own or are explicitly authorized to assess (RFC 1918), or a loopback address for local testing. Public, link-local, IPv6, and user-submitted target addresses are rejected. The target is fixed by the operator; the browser cannot choose a host.
- `NETWORK_SCANNER_TOKEN`: a private random bearer token of at least 32 characters. Keep it in your deployment's secret manager; do not commit it or send it to anyone.

For local PowerShell testing, prompt for the token without echoing or putting it in shell history:

```powershell
$env:NETWORK_SCANNER_TARGET = Read-Host "Authorized private IPv4 target"
$secureToken = Read-Host "Scanner token (at least 32 characters)" -AsSecureString
$env:NETWORK_SCANNER_TOKEN = [Net.NetworkCredential]::new("", $secureToken).Password
dotnet run
Remove-Item Env:NETWORK_SCANNER_TARGET, Env:NETWORK_SCANNER_TOKEN
```

Open `/network-scanner`. The UI requests explicit authorization and asks for the configured token for each scan; it does not persist the token. The endpoint permits at most three scans per minute per app instance.

The scanner runs from the server's network, not the visitor's computer. Use only a deployment you control on the same network as the explicitly configured target. For production, require HTTPS, keep the endpoint private or behind your organization’s authentication/VPN, and configure your firewall/hosting provider. The in-memory rate limit is not a substitute for access control.

## Results

The fixed check covers FTP, SSH, Telnet, HTTP, HTTPS, SMB, MySQL, RDP, PostgreSQL, Redis, and alternate HTTP. Results distinguish `open`, `closed-or-filtered`, and `timeout-or-filtered`; firewalls and network conditions can make an open service appear filtered. Guidance is generic hardening advice, not a vulnerability finding, CVE match, or penetration test.

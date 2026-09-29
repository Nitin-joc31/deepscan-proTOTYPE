# DeepScan

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

DeepScan is an image deepfake analysis app. The browser sends an image to the same-origin ASP.NET Core API; the server calls SightEngine and returns its score, a threshold-based label, and the face count.

The repository also includes the privacy-first [Cyber Safety Toolkit](cyber-safety-toolkit/), with a local password review, a k-anonymous breach lookup, and a cryptographically random passphrase generator.

It also includes an [Authorized Network Exposure Scanner](network-scanner/) that checks a single administrator-configured private host using TCP connections only.

## Run locally

Requirements: .NET 10 SDK and SightEngine API credentials.

Set `SIGHTENGINE_API_USER` and `SIGHTENGINE_API_SECRET` in the server process environment using a local secret manager or your IDE's environment settings (do not put real keys in source control or shell history), then run:

```sh
dotnet run
```

On Windows PowerShell, you can enter the secret without echoing it or placing it in command history:

```powershell
$env:SIGHTENGINE_API_USER = Read-Host "SightEngine API user"
$secureSecret = Read-Host "SightEngine API secret" -AsSecureString
$env:SIGHTENGINE_API_SECRET = [Net.NetworkCredential]::new("", $secureSecret).Password
$env:NETWORK_SCANNER_TARGET = Read-Host "Authorized private IPv4 target (optional)"
$secureScanToken = Read-Host "Scanner token, 32+ characters (optional)" -AsSecureString
$env:NETWORK_SCANNER_TOKEN = [Net.NetworkCredential]::new("", $secureScanToken).Password
dotnet run
Remove-Item Env:SIGHTENGINE_API_USER, Env:SIGHTENGINE_API_SECRET, Env:NETWORK_SCANNER_TARGET, Env:NETWORK_SCANNER_TOKEN
```

Open the local URL printed by ASP.NET Core. The `/` route serves `index.html`; `/api/health` reports whether server credentials are configured. Credentials are never entered into or stored by the browser. The server sends them only to SightEngine.

For a container deployment:

```sh
docker build -t deepscan .
docker run --rm -p 8080:8080 -e SIGHTENGINE_API_USER -e SIGHTENGINE_API_SECRET -e NETWORK_SCANNER_TARGET -e NETWORK_SCANNER_TOKEN deepscan
```

Set configured values as secrets in your hosting platform rather than adding values to this repository or a Docker image. The app listens on port 8080 in the container.
`NETWORK_SCANNER_TARGET` and `NETWORK_SCANNER_TOKEN` are optional; leave them unset to keep the network scanner disabled.

GitHub Pages is static hosting and cannot run the analysis API. Deploy the ASP.NET Core app (or its container) to a server that supports .NET 10. The previous GitHub Pages deployment is not a working scanner unless you also deploy a compatible same-origin API.

## Security and privacy

- SightEngine credentials remain in server environment configuration and are not sent to the browser.
- Images are validated as JPEG, PNG, or WEBP, limited to 25 MB, and checked against their file signatures. The app does not keep uploads after request processing. ASP.NET Core can temporarily buffer multipart uploads to server storage; secure that storage and use a trusted host.
- The scan endpoint is limited to 30 requests per minute per app instance. This in-memory global limit is a basic safeguard, not authentication or a distributed quota. Before making the service public, put it behind authentication and an edge or gateway with shared rate limits and abuse monitoring.
- Use HTTPS in production. Do not expose SightEngine keys through source files, frontend settings, logs, or container build arguments.
- The API returns a threshold-based label from the successful SightEngine score and face count. It does not create a heuristic score when the provider fails.
- The network scanner is disabled unless the operator sets `NETWORK_SCANNER_TARGET` to one private IPv4 address and `NETWORK_SCANNER_TOKEN` to a 32+ character secret. Set them only in server/deployment secrets; the scanner performs TCP connections to that fixed host, never a user-selected target. Use only on networks you own or have explicit authorization to assess.
- For Docker, provide scanner settings as deployment environment secrets, separately from the SightEngine credentials.

## Limitations

- Image analysis only; video is not supported.
- The exposure scanner is not a vulnerability detector or a penetration test. It cannot identify software versions or confirm a CVE; firewalls may affect results.
- SightEngine's output is probabilistic and not definitive. Treat it as one signal rather than proof of authenticity or manipulation.
- Scans require server-side SightEngine credentials, a working network connection, and available provider quota.

## Project files

```text
DeepScan.csproj   ASP.NET Core application
Program.cs        Same-origin SightEngine proxy and validation
index.html        Browser UI
network-scanner/  Fixed-target, authenticated TCP exposure check
cyber-safety-toolkit/ Local password and passphrase helpers
Dockerfile        Multi-stage container build
README.md         Setup, deployment, and security notes
```

See [network scanner setup and safety scope](network-scanner/README.md) for its operator-only configuration.

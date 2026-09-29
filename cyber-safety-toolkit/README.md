# Cyber Safety Toolkit

A small, client-side password review, breach lookup, and 10-word random passphrase generator.

## Use it

Open `index.html` from a static HTTPS host, or run it locally from `localhost`. For example, with .NET 10 installed:

```powershell
dotnet run
```

The existing DeepScan server hosts the toolkit at `/cyber-safety-toolkit/`. It can also be deployed as a static GitHub Pages subfolder; breach lookups require a secure browser context (HTTPS or localhost).

## Privacy and breach lookups

- Password length/common-pattern checks and SHA-1 hashing happen locally in the browser.
- The app sends only the first 5 hexadecimal characters of the SHA-1 hash to the [Have I Been Pwned Pwned Passwords range API](https://haveibeenpwned.com/API/v3#PwnedPasswords). It compares the returned hash suffixes locally. This is the service's k-anonymity approach: the password and complete hash are not sent.
- The request uses `Add-Padding: true` to reduce response-size leakage. The API response is not persisted, and the app does not use local storage, cookies, analytics, or a backend for this check.
- SHA-1 is used only because the breach service's range protocol requires it; it is not an endorsement of SHA-1 for password storage or security design.
- Any user-entered password is cleared from the input after a lookup attempt. The generated phrase exists on the page only until it is copied or cleared.
- The breach corpus is incomplete and changes over time. A miss is not proof a password is safe; a match means it should not be used.

## Limits

The local password checklist is intentionally basic. It does not measure entropy, know whether a password was reused, or replace a password manager or multifactor authentication. Never paste a password into a page unless you trust its source and have checked that its implementation respects your privacy.

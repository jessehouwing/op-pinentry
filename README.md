# op-pinentry

A minimal GnuPG `pinentry` replacement for Windows that fetches your GPG key
passphrase from [1Password](https://1password.com) on demand via the `op`
CLI, instead of showing a passphrase dialog.

- Nothing is cached ahead of time: the passphrase is only requested from
  1Password when `gpg-agent` actually asks for it.
- After that, `gpg-agent`'s own normal (short-lived) passphrase cache
  applies, exactly as it would with the standard `pinentry`.
- No console window ever appears; it's built as a windowless (`WinExe`)
  self-contained executable.

## Requirements

- Windows
- [1Password desktop app](https://1password.com/downloads/windows/) with
  "Integrate with 1Password CLI" enabled (Settings → Developer)
- [1Password CLI (`op`)](https://developer.1password.com/docs/cli/get-started/)
  on your `PATH`
- GnuPG (e.g. [Gpg4win](https://gpg4win.org/))
- A 1Password item containing your GPG key passphrase

## Build

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
# or for ARM64 Windows:
dotnet publish -c Release -r win-arm64 --self-contained true
```

The published `pinentry-1password.exe` will be in
`bin\Release\net10.0\<rid>\publish\`.

Prebuilt, signed-provenance binaries for `win-x64` and `win-arm64` are also
published on the [Releases](../../releases) page for every tagged version,
along with SHA256 checksums and a
[build provenance attestation](https://docs.github.com/en/actions/security-guides/using-artifact-attestations-to-establish-provenance-for-builds).
Verify a downloaded binary with:

```powershell
gh attestation verify pinentry-1password-win-x64.exe --owner jessehouwing
```

## Install

1. Copy `pinentry-1password.exe` into `%APPDATA%\gnupg\`.
2. Add to `%APPDATA%\gnupg\gpg-agent.conf`:

   ```
   pinentry-program C:\Users\<you>\AppData\Roaming\gnupg\pinentry-1password.exe
   ```

3. Restart the agent:

   ```powershell
   gpgconf --kill gpg-agent
   ```

## Configuration

Set these environment variables (e.g. in your user environment, or wrap the
`pinentry-program` invocation) to point at your own 1Password item:

| Variable                        | Default                                              | Description                                   |
|----------------------------------|-------------------------------------------------------|------------------------------------------------|
| `OP_PINENTRY_SECRET_REFERENCE`   | `op://Private/GPG signing key passphrase/password`     | `op://` secret reference to the passphrase item |
| `OP_PINENTRY_ACCOUNT`            | `my.1password.com`                                     | 1Password account shorthand (see `op account list`) |

## How it works

`gpg-agent` talks to `pinentry` using the
[Assuan protocol](https://www.gnupg.org/documentation/manuals/assuan/). This
tool implements just enough of it: it answers `OK` to setup commands
(`SETDESC`, `SETPROMPT`, `OPTION`, ...), and on `GETPIN` it shells out to
`op read <reference> --account <account>` and returns the result as the pin.

## Caveat

This requires the 1Password desktop app to be running and unlocked. If it's
locked when GnuPG needs a signature, `op read` fails and GnuPG will report a
passphrase error rather than falling back to a manual prompt.

## License

MIT

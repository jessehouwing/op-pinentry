// Minimal Assuan-protocol pinentry replacement.
//
// On a GETPIN request it fetches a GPG key passphrase from 1Password
// on-demand via the `op` CLI (nothing is cached ahead of time - the
// passphrase is only ever requested from 1Password when gpg-agent actually
// needs it, and gpg-agent's normal short-lived cache applies afterwards).
// Any other request type (SETDESC/SETPROMPT/OPTION/etc.) is acknowledged
// with a plain OK, since we don't need to render any UI for them.
//
// Configuration is via environment variables (set them in gpg-agent.conf's
// pinentry-program invocation, or globally):
//   OP_PINENTRY_SECRET_REFERENCE  op:// secret reference, e.g.
//                                 op://Private/GPG signing key passphrase/password
//   OP_PINENTRY_ACCOUNT           1Password account shorthand (op account list)

using System.Diagnostics;
using System.Text;

var opSecretReference = Environment.GetEnvironmentVariable("OP_PINENTRY_SECRET_REFERENCE")
    ?? "op://Private/GPG signing key passphrase/password";
var opAccount = Environment.GetEnvironmentVariable("OP_PINENTRY_ACCOUNT")
    ?? "my.1password.com";

var stdin = Console.OpenStandardInput();
var stdout = Console.OpenStandardOutput();
using var reader = new StreamReader(stdin, Encoding.UTF8);
using var writer = new StreamWriter(stdout, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };

writer.WriteLine("OK Pleased to meet you (1Password bridge)");

string? line;
while ((line = reader.ReadLine()) != null)
{
    line = line.Trim();
    if (line.Length == 0)
    {
        continue;
    }

    var spaceIndex = line.IndexOf(' ');
    var command = (spaceIndex < 0 ? line : line[..spaceIndex]).ToUpperInvariant();

    switch (command)
    {
        case "BYE":
            writer.WriteLine("OK closing connection");
            return 0;

        case "GETPIN":
            HandleGetPin(writer, opSecretReference, opAccount);
            break;

        default:
            // SETDESC / SETPROMPT / SETOK / SETCANCEL / SETTITLE / SETERROR / OPTION / CONFIRM / MESSAGE / etc.
            writer.WriteLine("OK");
            break;
    }
}

return 0;

static void HandleGetPin(StreamWriter writer, string opSecretReference, string opAccount)
{
    try
    {
        var passphrase = ReadPassphraseFromOnePassword(opSecretReference, opAccount);
        if (string.IsNullOrEmpty(passphrase))
        {
            throw new InvalidOperationException("empty passphrase from 1Password");
        }

        writer.WriteLine("D " + EncodeAssuan(passphrase));
        writer.WriteLine("OK");
    }
    catch (Exception)
    {
        writer.WriteLine("ERR 83886179 Could not fetch passphrase from 1Password <1password>");
    }
}

static string ReadPassphraseFromOnePassword(string opSecretReference, string opAccount)
{
    var startInfo = new ProcessStartInfo("op", $"read \"{opSecretReference}\" --account {opAccount}")
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
    };

    using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("failed to start op");
    var output = process.StandardOutput.ReadToEnd();
    process.WaitForExit();

    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException("op read failed");
    }

    return output.TrimEnd('\r', '\n');
}

static string EncodeAssuan(string s)
{
    var sb = new StringBuilder();
    foreach (var ch in s)
    {
        switch (ch)
        {
            case '%':
                sb.Append("%25");
                break;
            case '\r':
                sb.Append("%0D");
                break;
            case '\n':
                sb.Append("%0A");
                break;
            default:
                sb.Append(ch);
                break;
        }
    }

    return sb.ToString();
}

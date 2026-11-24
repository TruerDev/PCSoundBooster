using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace PCSoundBooster;

internal static class ApoSetup
{
    private const string Sha256 = "7403BE7427BBE1936A40DDED082829B6E217FC4F5990FEE5CBA501F0AE055AFA";

    internal static void VerifyPayload()
    {
        using var installer = OpenPayload();
        Verify(installer);
    }

    internal static async Task<int> InstallAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"PCSoundBooster-APO-{Guid.NewGuid():N}.exe");
        try
        {
            using (var source = OpenPayload())
            using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await source.CopyToAsync(target);

            // Keep the verified file open for reading until the elevated process exits.
            using var package = File.OpenRead(path);
            Verify(package);
            using var process = Process.Start(new ProcessStartInfo(path)
            {
                UseShellExecute = true,
                Verb = "runas"
            }) ?? throw new IOException("Не удалось открыть установщик аудиодвижка.");
            await process.WaitForExitAsync();
            return process.ExitCode;
        }
        finally
        {
            try { File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static Stream OpenPayload() =>
        typeof(ApoSetup).Assembly.GetManifestResourceStream("EqualizerAPO.Setup") ??
        throw new IOException("В сборке отсутствует установщик Equalizer APO.");

    private static void Verify(Stream installer)
    {
        if (Convert.ToHexString(SHA256.HashData(installer)) != Sha256)
            throw new IOException("Контрольная сумма установщика Equalizer APO не совпала.");
    }
}

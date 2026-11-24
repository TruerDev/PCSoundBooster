using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace PCSoundBooster;

internal sealed class ApoConfig(string directory)
{
    internal const string Include = "Include: PCSoundBooster.txt";
    private const string GainPrefix = "Preamp: ";
    private const string GainSuffix = " dB";
    private const string Header = "# PCSoundBooster";
    private string MainPath => Path.Combine(directory, "config.txt");
    private string GainPath => Path.Combine(directory, "PCSoundBooster.txt");

    internal static ApoConfig? Find()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\EqualizerAPO");
        var install = key?.GetValue("InstallPath") as string;
        var config = key?.GetValue("ConfigPath") as string;
        return install is not null && config is not null &&
               File.Exists(Path.Combine(install, "EqualizerAPO.dll")) &&
               File.Exists(Path.Combine(config, "config.txt"))
            ? new ApoConfig(config) : null;
    }

    internal bool IsConnected => HasInclude(File.ReadAllText(MainPath));

    internal void Connect()
    {
        var original = File.ReadAllBytes(MainPath);
        using var reader = new StreamReader(new MemoryStream(original));
        if (HasInclude(reader.ReadToEnd()))
        {
            if (!File.Exists(GainPath))
                SetGain(100);
            return;
        }

        if (reader.CurrentEncoding.CodePage is 1200 or 1201 or 12000 or 12001)
            throw new IOException("Сохраните config.txt в UTF-8 для Equalizer APO.");

        if (File.Exists(GainPath))
            EnsureOwned();
        else
            SetGain(100);

        var backup = Path.Combine(directory, $"config.pcsoundbooster-{Guid.NewGuid():N}.bak");
        // An existing Device or Stage directive could hide an Include at the end of the file.
        var addition = Encoding.ASCII.GetBytes($"{Header}\r\n{Include}\r\n");
        var bomLength = original.AsSpan().StartsWith(Encoding.UTF8.Preamble) ? 3 : 0;
        Replace(MainPath, [.. original[..bomLength], .. addition, .. original[bomLength..]], backup, original);
    }

    internal static double Decibels(int percent) => 20 * Math.Log10(percent / 100.0);

    internal int ReadGain()
    {
        var lines = EnsureOwned();
        var line = lines.FirstOrDefault(line => line.StartsWith(GainPrefix, StringComparison.Ordinal));
        if (line is not null && line.EndsWith(GainSuffix, StringComparison.Ordinal) &&
            double.TryParse(line[GainPrefix.Length..^GainSuffix.Length], NumberStyles.Float,
                CultureInfo.InvariantCulture, out var db) && double.IsFinite(db))
        {
            var percent = Math.Round(100 * Math.Pow(10, db / 20));
            if (percent is >= 100 and <= 500)
                return (int)percent;
        }
        throw new IOException("Не удалось прочитать усиление из PCSoundBooster.txt.");
    }

    internal void SetGain(int percent)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(percent, 100);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(percent, 500);
        if (File.Exists(GainPath))
            EnsureOwned();

        // shortcut: linear gain can clip on loud peaks, add a limiter before offering distortion protection.
        var content = FormattableString.Invariant(
            $"{Header}\r\nStage: post-mix\r\n{GainPrefix}{Decibels(percent):0.0000}{GainSuffix}\r\n");
        Replace(GainPath, Encoding.UTF8.GetBytes(content));
    }

    private string[] EnsureOwned()
    {
        var lines = File.ReadAllLines(GainPath);
        if (lines.FirstOrDefault() != Header)
            throw new IOException("Файл PCSoundBooster.txt уже используется другой конфигурацией.");
        return lines;
    }

    private static bool HasInclude(string text) => Regex.IsMatch(text,
        @"^[ \t]*Include[ \t]*:[ \t]*(?:\.[\\/])?(?i:PCSoundBooster\.txt)\r?$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    private static void Replace(string path, byte[] content, string? backup = null, byte[]? expected = null)
    {
        var temporary = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(temporary, content);
            if (expected is not null && !File.ReadAllBytes(path).SequenceEqual(expected))
                throw new IOException("Конфигурация Equalizer APO изменилась. Повторите подключение.");
            if (File.Exists(path))
                File.Replace(temporary, path, backup);
            else if (expected is null)
                File.Move(temporary, path);
            else
                throw new IOException("Файл config.txt больше недоступен.");
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;

namespace PCSoundBooster;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args is ["--self-test"])
        {
            SelfTest();
            return 0;
        }

        using var instance = new Mutex(true, @"Local\PCSoundBooster", out var first);
        if (first)
        {
            var application = new Application();
            var panel = new BoosterWindow();
            application.MainWindow = panel;
            if (args is not ["--tray"])
                application.Startup += (_, _) => panel.OpenPanel(TrayPlacement.DefaultAnchor());
            application.Run();
        }
        return 0;
    }

    private static void SelfTest()
    {
        ApoSetup.VerifyPayload();
        var size = new System.Drawing.Size(370, 280);
        var screen = new System.Drawing.Rectangle(-1920, 0, 1920, 1080);
        var bottom = new System.Drawing.Rectangle(-1920, 0, 1920, 1040);
        var left = new System.Drawing.Rectangle(-1880, 0, 1880, 1080);
        Check(TrayPlacement.GetOrigin(screen, bottom, new System.Drawing.Point(-12, 1060), size) ==
              new System.Drawing.Point(-370, 760));
        Check(TrayPlacement.GetOrigin(screen, left, new System.Drawing.Point(-1910, 350), size) ==
              new System.Drawing.Point(-1880, 210));
        var directory = Directory.CreateTempSubdirectory("PCSoundBooster-");
        try
        {
            var config = new ApoConfig(directory.FullName);
            var main = Path.Combine(directory.FullName, "config.txt");
            var original = Encoding.UTF8.GetBytes("# Настройки пользователя\r\nDevice: Headphones\r\nPreamp: -3 dB");
            File.WriteAllBytes(main, original);
            config.Connect();
            config.Connect();
            Check(config.IsConnected && config.ReadGain() == 100);
            Check(File.ReadAllBytes(main).TakeLast(original.Length).SequenceEqual(original));
            Check(File.ReadAllText(main).IndexOf(ApoConfig.Include, StringComparison.Ordinal) <
                  File.ReadAllText(main).IndexOf("Device:", StringComparison.Ordinal));
            Check(File.ReadAllText(main).Split(ApoConfig.Include).Length == 2);
            Check(File.ReadAllBytes(directory.GetFiles("*.bak").Single().FullName).SequenceEqual(original));

            foreach (var percent in new[] { 100, 125, 200, 275, 500, 100 })
            {
                config.SetGain(percent);
                Check(config.ReadGain() == percent);
            }
            Check(Math.Abs(ApoConfig.Decibels(500) - 13.9794) < 0.001);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    private static void Check(bool result)
    {
        if (!result)
            throw new InvalidOperationException("PCSoundBooster self-test failed.");
    }
}

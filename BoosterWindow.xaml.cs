using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using Point = System.Drawing.Point;

namespace PCSoundBooster;

public partial class BoosterWindow : Window
{
    private readonly DispatcherTimer applyTimer = new() { Interval = TimeSpan.FromMilliseconds(175) };
    private readonly Forms.NotifyIcon tray;
    private readonly Forms.ToolStripMenuItem trayToggle;
    private readonly Icon trayIcon;
    private const string StartupKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupName = "PCSoundBooster";
    private ApoConfig? config;
    private Point anchor;
    private long deactivatedAt;
    private bool connected;
    private bool closing;
    private bool installing;
    private bool dismissing;

    public BoosterWindow()
    {
        InitializeComponent();
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Открыть панель", null, (_, _) => OpenPanel(Forms.Control.MousePosition));
        trayToggle = new Forms.ToolStripMenuItem("Включить усиление", null, (_, _) =>
        {
            Enabled.IsChecked = Enabled.IsChecked != true;
            ApplyGain();
        });
        menu.Items.Add(trayToggle);
        menu.Items.Add("Выход", null, (_, _) => CloseAndReset());
        using var iconStream = Application.GetResourceStream(new Uri(
            "/PCSoundBooster;component/Booster.ico", UriKind.Relative)).Stream;
        var iconSize = Forms.SystemInformation.SmallIconSize;
        trayIcon = new Icon(iconStream, iconSize.Width, iconSize.Height);
        tray = new Forms.NotifyIcon
        {
            Icon = trayIcon,
            Text = "PCSoundBooster",
            ContextMenuStrip = menu,
            Visible = true
        };
        tray.MouseClick += (_, args) =>
        {
            if (args.Button != Forms.MouseButtons.Left)
                return;
            if (IsVisible)
                DismissPanel();
            else if (Stopwatch.GetElapsedTime(deactivatedAt).TotalMilliseconds >= 200)
                OpenPanel(Forms.Control.MousePosition);
        };
        SourceInitialized += (_, _) => TrayPlacement.HideFromTaskSwitcher(this);
        SizeChanged += (_, _) => Dispatcher.InvokeAsync(() =>
        {
            if (IsVisible)
                TrayPlacement.Position(this, anchor);
        }, DispatcherPriority.Loaded);
        Deactivated += (_, _) =>
        {
            deactivatedAt = Stopwatch.GetTimestamp();
            DismissPanel();
        };
        PreviewKeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape)
            {
                args.Handled = true;
                DismissPanel();
            }
        };
        Closed += (_, _) =>
        {
            tray.Dispose();
            trayIcon.Dispose();
            menu.Dispose();
        };

        applyTimer.Tick += (_, _) => ApplyGain();
        Gain.ValueChanged += (_, _) =>
        {
            DisplayGain();
            if (connected && Enabled.IsChecked == true)
            {
                applyTimer.Stop();
                applyTimer.Start();
            }
        };
        Enabled.Click += (_, _) => ApplyGain();
        ConnectButton.Click += (_, _) => RefreshConnection(connect: true);
        RefreshButton.Click += (_, _) => RefreshConnection();
        InstallButton.Click += async (_, _) => await InstallEngineAsync();
        SettingsButton.Checked += (_, _) => ShowSettings();
        SettingsButton.Unchecked += (_, _) => SettingsPanel.Visibility = Visibility.Collapsed;
        StartupToggle.Click += (_, _) =>
        {
            if (!Run(UpdateStartup))
                StartupToggle.IsChecked = StartupToggle.IsChecked != true;
        };
        ResetButton.Click += (_, _) =>
        {
            Enabled.IsChecked = false;
            Gain.Value = 100;
            ApplyGain();
        };
        ExitButton.Click += (_, _) => CloseAndReset();

        DisplayGain();
        RefreshConnection();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!closing)
        {
            e.Cancel = true;
            DismissPanel();
        }
        base.OnClosing(e);
    }

    internal void OpenPanel(Point position)
    {
        anchor = position;
        dismissing = false;
        Panel.IsHitTestVisible = true;
        Panel.BeginAnimation(OpacityProperty, null);
        PanelMotion.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, null);
        Panel.Opacity = SystemParameters.ClientAreaAnimation ? 0 : 1;
        Show();
        UpdateLayout();
        TrayPlacement.Position(this, anchor);
        Activate();
        if (SystemParameters.ClientAreaAnimation)
        {
            Panel.BeginAnimation(OpacityProperty, Ease(0, 1, 165));
            PanelMotion.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, Ease(7, 0, 190));
        }
    }

    private void DismissPanel()
    {
        if (!IsVisible || dismissing)
            return;
        dismissing = true;
        Panel.IsHitTestVisible = false;

        if (!SystemParameters.ClientAreaAnimation)
        {
            FinishDismiss();
            return;
        }
        var fade = Ease(Panel.Opacity, 0, 110);
        fade.Completed += (_, _) =>
        {
            if (dismissing)
                FinishDismiss();
        };
        Panel.BeginAnimation(OpacityProperty, fade);
        PanelMotion.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, Ease(PanelMotion.Y, 5, 110));
    }

    private void FinishDismiss()
    {
        Hide();
        SettingsButton.IsChecked = false;
        dismissing = false;
    }

    private static DoubleAnimation Ease(double start, double end, int milliseconds) =>
        new(start, end, TimeSpan.FromMilliseconds(milliseconds))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

    private void ShowSettings()
    {
        Run(() =>
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupKey);
            StartupToggle.IsChecked = key?.GetValue(StartupName) is string;
        });
        SettingsPanel.Visibility = Visibility.Visible;
        if (SystemParameters.ClientAreaAnimation)
            SettingsPanel.BeginAnimation(OpacityProperty, Ease(0, 1, 160));
    }

    private void UpdateStartup()
    {
        using var key = Registry.CurrentUser.CreateSubKey(StartupKey);
        if (StartupToggle.IsChecked == true)
        {
            var executable = Environment.ProcessPath ?? throw new IOException("Не удалось определить путь приложения.");
            key.SetValue(StartupName, $"\"{executable}\" --tray", RegistryValueKind.String);
        }
        else
            key.DeleteValue(StartupName, throwOnMissingValue: false);
    }

    private void CloseAndReset()
    {
        if (installing)
            return;
        applyTimer.Stop();
        if (!Run(() =>
            {
                if (connected && config!.ReadGain() != 100)
                    config.SetGain(100);
            }))
        {
            OpenPanel(IsVisible ? anchor : TrayPlacement.DefaultAnchor());
            return;
        }
        closing = true;
        Close();
    }

    private async Task InstallEngineAsync()
    {
        installing = true;
        InstallButton.IsEnabled = RefreshButton.IsEnabled = false;
        SetStatus("Подтвердите запрос Windows и выберите колонки или наушники в установщике Equalizer APO.");
        DismissPanel();

        string? message = null;
        try
        {
            var exitCode = await ApoSetup.InstallAsync();
            message = exitCode switch
            {
                0 => null,
                3010 => "Перезагрузите Windows для завершения установки аудиодвижка.",
                _ => $"Установщик Equalizer APO завершился с кодом {exitCode}."
            };
        }
        catch (Win32Exception error) when (error.NativeErrorCode == 1223)
        {
            message = "Установка отменена. Её можно запустить снова.";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or Win32Exception)
        {
            message = error.Message;
        }
        finally
        {
            installing = false;
            InstallButton.IsEnabled = RefreshButton.IsEnabled = true;
        }

        RefreshConnection(connect: message is null);
        if (message is not null)
            SetStatus(message);
        else if (connected)
            SetStatus("Настройки усиления подключены. Если установщик запросил перезагрузку, перезагрузите Windows.");
        OpenPanel(anchor);
    }

    private void RefreshConnection(bool connect = false)
    {
        applyTimer.Stop();
        connected = false;
        config = null;
        var success = Run(() =>
        {
            config = ApoConfig.Find();
            if (config is null)
            {
                SetStatus("Для первого запуска установите встроенный Equalizer APO. Windows запросит права администратора.");
                return;
            }
            if (connect)
                config.Connect();

            var linked = config.IsConnected;
            var percent = linked ? config.ReadGain() : 100;
            if (percent > 100)
                Gain.Value = percent;
            Enabled.IsChecked = percent > 100;
            connected = linked;
            SetStatus(connected
                ? PlaybackStatus()
                : "В Equalizer APO нужно добавить настройку усиления.");
        });

        Gain.IsEnabled = Enabled.IsEnabled = trayToggle.Enabled = ResetButton.IsEnabled = connected;
        trayToggle.Checked = connected && Enabled.IsChecked == true;
        ConnectButton.Visibility = config is not null && !connected ? Visibility.Visible : Visibility.Collapsed;
        InstallButton.Visibility = config is null ? Visibility.Visible : Visibility.Collapsed;
        RefreshButton.Visibility = connected && success ? Visibility.Collapsed : Visibility.Visible;
        SetupActions.Visibility = connected && success ? Visibility.Collapsed : Visibility.Visible;
    }

    private void DisplayGain()
    {
        PercentText.Text = $"{Gain.Value:0}%";
        DecibelText.Text = $"+{ApoConfig.Decibels((int)Gain.Value):0.0} дБ";
    }

    private void ApplyGain()
    {
        applyTimer.Stop();
        if (!connected)
            return;

        Run(() =>
        {
            if (!config!.IsConnected)
            {
                RefreshConnection();
                return;
            }
            var percent = Enabled.IsChecked == true ? (int)Gain.Value : 100;
            config.SetGain(percent);
            trayToggle.Checked = Enabled.IsChecked == true;
            SetStatus(PlaybackStatus());
        });
    }

    private string PlaybackStatus() => Enabled.IsChecked == true
        ? "Усиление включено. Громкие пики могут искажаться."
        : "Усиление выключено.";

    private bool Run(Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SecurityException or Win32Exception)
        {
            SetStatus(error.Message);
            SetupActions.Visibility = Visibility.Visible;
            RefreshButton.Visibility = Visibility.Visible;
            return false;
        }
    }

    private void SetStatus(string message)
    {
        Status.Text = message;
        UIElementAutomationPeer.FromElement(Status)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }
}

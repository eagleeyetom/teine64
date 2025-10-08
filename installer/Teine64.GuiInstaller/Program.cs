using System;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Teine64Installer;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal sealed class MainForm : Form
{
    private readonly Button _installBtn;
    private readonly Button _uninstallBtn;
    private readonly Button _exitBtn;
    private readonly CheckBox _autostart;
    private readonly ProgressBar _progress;
    private readonly Label _status;
    private readonly string _installDir;
    private readonly string _exePathTarget;
    private byte[]? _payload;

    private const string ProductNameConst = "Teine64";
    private const string VersionConst = "0.2.0"; // sync manually or with build prop injection

    public MainForm()
    {
        Text = "Teine64 Installer";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new System.Drawing.Size(420, 210);

        _installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductNameConst);
        _exePathTarget = Path.Combine(_installDir, "Teine64.exe");

    var lbl = new Label { AutoSize = true, Text = "Install Teine64 (NativeAOT) per-user into:", Left = 12, Top = 12 };
        var pathBox = new TextBox { Left = 12, Top = 32, Width = 390, ReadOnly = true, Text = _exePathTarget };

        _autostart = new CheckBox { Left = 12, Top = 60, Width = 300, Text = "Start with Windows" };

        _installBtn = new Button { Text = "Install", Left = 12, Width = 80, Top = 100 }; _installBtn.Click += (_, __) => Install();
        _uninstallBtn = new Button { Text = "Uninstall", Left = 100, Width = 80, Top = 100 }; _uninstallBtn.Click += (_, __) => Uninstall();
        _exitBtn = new Button { Text = "Exit", Left = 188, Width = 80, Top = 100 }; _exitBtn.Click += (_, __) => Close();

        _progress = new ProgressBar { Left = 12, Top = 140, Width = 390, Style = ProgressBarStyle.Blocks, Minimum = 0, Maximum = 100, Value = 0 };
        _status = new Label { Left = 12, Top = 170, Width = 390, AutoSize = false, Text = "Ready" };

        Controls.AddRange(new Control[] { lbl, pathBox, _autostart, _installBtn, _uninstallBtn, _exitBtn, _progress, _status });

        Load += (_, __) => DetectState();
    }

    private void DetectState()
    {
        if (File.Exists(_exePathTarget))
        {
            _status.Text = "Installed";
            _installBtn.Enabled = true;
            _uninstallBtn.Enabled = true;
        }
        else
        {
            _status.Text = "Not installed";
            _uninstallBtn.Enabled = false;
        }
    }

    private void Install()
    {
        try
        {
            _installBtn.Enabled = _uninstallBtn.Enabled = false;
            Directory.CreateDirectory(_installDir);
            _status.Text = "Extracting payload...";
            _progress.Value = 10;
            _payload ??= LoadPayload();
            if (_payload.Length == 0) throw new InvalidOperationException("Embedded payload empty.");
            File.WriteAllBytes(_exePathTarget, _payload);
            _progress.Value = 70;

            if (_autostart.Checked)
                SetAutostart(true);

            RegisterUninstall();

            _progress.Value = 100;
            _status.Text = "Installed successfully.";

            // Launch
            try { Process.Start(new ProcessStartInfo(_exePathTarget) { UseShellExecute = true }); } catch { }
        }
        catch (Exception ex)
        {
            _status.Text = "Error: " + ex.Message;
        }
        finally
        {
            DetectState();
        }
    }

    private void Uninstall()
    {
        try
        {
            _installBtn.Enabled = _uninstallBtn.Enabled = false;
            _status.Text = "Uninstalling...";
            _progress.Value = 30;

            // Stop running instances
            foreach (var p in Process.GetProcessesByName("Teine64"))
            {
                try { p.CloseMainWindow(); if (!p.WaitForExit(500)) p.Kill(); } catch { }
            }

            if (File.Exists(_exePathTarget))
            {
                try { File.Delete(_exePathTarget); } catch { }
            }

            if (Directory.Exists(_installDir) && Directory.GetFiles(_installDir).Length == 0)
            {
                try { Directory.Delete(_installDir); } catch { }
            }

            RemoveAutostart();
            RemoveUninstallReg();
            _progress.Value = 100;
            _status.Text = "Uninstalled.";
        }
        catch (Exception ex)
        {
            _status.Text = "Error: " + ex.Message;
        }
        finally
        {
            DetectState();
        }
    }

    private byte[] LoadPayload()
    {
        if (_payload is { Length: > 0 }) return _payload;
        // Embedded resource logical name: Teine64Payload
        var asm = Assembly.GetExecutingAssembly();
        var resName = "Teine64Payload"; // logical name from csproj
        // Some MSBuild will prefix with default namespace; attempt both
        string? fullName = null;
        foreach (var n in asm.GetManifestResourceNames())
            if (n.EndsWith(resName, StringComparison.OrdinalIgnoreCase)) { fullName = n; break; }
        if (fullName == null) throw new InvalidOperationException("Embedded payload resource not found.");
        using var s = asm.GetManifestResourceStream(fullName)!;
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    private void SetAutostart(bool enable)
    {
        try
        {
            using var run = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", writable: true)!;
            if (enable)
                run.SetValue(ProductNameConst, '"' + _exePathTarget + '"');
            else if (run.GetValue(ProductNameConst) != null)
                run.DeleteValue(ProductNameConst, false);
        }
        catch { }
    }

    private void RemoveAutostart() => SetAutostart(false);

    private void RegisterUninstall()
    {
        try
        {
            string keyPath = $"Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\{ProductNameConst}";
            using var k = Registry.CurrentUser.CreateSubKey(keyPath)!;
            k.SetValue("DisplayName", ProductNameConst);
            k.SetValue("DisplayVersion", VersionConst);
            k.SetValue("Publisher", ProductNameConst);
            k.SetValue("InstallDate", DateTime.UtcNow.ToString("yyyyMMdd"));
            k.SetValue("InstallLocation", _installDir);
            k.SetValue("DisplayIcon", _exePathTarget);
            k.SetValue("NoModify", 1, RegistryValueKind.DWord);
            k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            // For user uninstall re-run installer with /remove (not implemented); we can point to this installer itself
            var self = Environment.ProcessPath!;
            k.SetValue("UninstallString", "\"" + self + "\" /uninstall");
        }
        catch { }
    }

    private void RemoveUninstallReg()
    {
        try
        {
            string keyPath = $"Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\{ProductNameConst}";
            Registry.CurrentUser.DeleteSubKeyTree(keyPath, false);
        }
        catch { }
    }
}

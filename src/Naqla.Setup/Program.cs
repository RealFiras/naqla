using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;

namespace Naqla.Setup;

internal static class Program
{
    internal static readonly string InstallDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Naqla");
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Naqla.Development";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Manifest = ".naqla-installed-files.json";
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--extract-only"))
        {
            // Build verification only: no registry, startup, shortcuts, or launch.
            var index = Array.IndexOf(args, "--extract-only");
            Extract(Path.GetFullPath(args[index + 1])); return 0;
        }
        if (args.Contains("--install"))
        {
            // Explicit unattended installation uses the exact GUI transaction.
            try
            {
                var allowed = new HashSet<string>(["--install", "--start-with-windows", "--launch", "--download-engines", "--engine-cache", "--offline-cache"], StringComparer.Ordinal);
                string? cache = null;
                for (var i = 0; i < args.Length; i++)
                {
                    if (!allowed.Contains(args[i])) return 2;
                    if (args[i] == "--engine-cache") { if (++i >= args.Length) return 2; cache = Path.GetFullPath(args[i]); }
                }
                if (EngineDownloads.MissingRuntime().Length != 0) return 3;
                Install(args.Contains("--start-with-windows"), args.Contains("--download-engines"), cache, !args.Contains("--offline-cache"), null).GetAwaiter().GetResult();
                if (args.Contains("--launch")) Launch();
                return 0;
            }
            catch { return 1; }
        }
        if (args.Contains("--remove-from-temp"))
        {
            try { Uninstall(); MessageBox.Show("Naqla development build was removed. Your files are preserved.", "Naqla"); return 0; }
            catch (Exception error) { MessageBox.Show(error.Message, "Naqla removal failed"); return 1; }
        }
        if (args.Contains("--uninstall"))
        {
            if (MessageBox.Show("Remove Naqla development build? Converted files and original files will be preserved.", "Uninstall Naqla", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return 0;
            var temporary = Path.Combine(Path.GetTempPath(), "Naqla-Uninstall-" + Guid.NewGuid().ToString("N") + ".exe");
            File.Copy(Environment.ProcessPath!, temporary, false);
            Process.Start(new ProcessStartInfo(temporary, "--remove-from-temp") { UseShellExecute = false });
            return 0;
        }
        using var form = CreateForm(); Application.Run(form); return 0;
    }
    private static Form CreateForm()
    {
        var form = new Form { Text = "Install Naqla — نقلة", ClientSize = new Size(510, 410), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, StartPosition = FormStartPosition.CenterScreen, Font = new Font("Segoe UI", 10) };
        form.Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        var title = new Label { Text = "Naqla · 0.8.0-beta.1", AutoSize = true, Font = new Font("Segoe UI", 17, FontStyle.Bold), Location = new Point(24, 22) };
        var description = new Label { Text = "Background tray utility · Drag a file, then press Shift.\nPixel wheel · local image, video, audio and PDF conversion.\nLocal diagnostics ON · no filenames or paths logged.\n\nInstalls for your Windows account, without administrator access.", AutoSize = false, Size = new Size(460, 110), Location = new Point(24, 65) };
        using var existingRun = Registry.CurrentUser.OpenSubKey(RunKey);
        var startup = new CheckBox { Text = "Start Naqla with Windows", Checked = !Directory.Exists(InstallDirectory) || existingRun?.GetValue("Naqla") is string existing && existing == $"\"{Path.Combine(InstallDirectory, "Naqla.App.exe")}\"", AutoSize = true, Location = new Point(26, 188) };
        var launch = new CheckBox { Text = "Launch Naqla after installation", Checked = true, AutoSize = true, Location = new Point(26, 220) };
        var engines = new CheckBox { Text = "Download FFmpeg / HEIF once from official sources (internet)", Checked = true, AutoSize = true, Location = new Point(26, 252), Font = new Font("Segoe UI", 9) };
        var status = new Label { Text = "Files stay local. Optional model packs are installed separately.", AutoSize = false, Size = new Size(460, 40), Location = new Point(26, 285), Font = new Font("Segoe UI", 9) };
        var prerequisites = new LinkLabel { Text = "Microsoft Visual C++ x64 runtime", AutoSize = true, Location = new Point(26, 342), Font = new Font("Segoe UI", 8) };
        prerequisites.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo("https://learn.microsoft.com/cpp/windows/latest-supported-vc-redist") { UseShellExecute = true });
        var button = new Button { Text = "Install development build", Size = new Size(230, 40), Location = new Point(250, 340) };
        button.Click += async (_, _) =>
        {
            if (EngineDownloads.MissingRuntime().Length != 0)
            {
                MessageBox.Show("Install Microsoft's current Visual C++ x64 Redistributable using the official link, then run this installer again. No SDK is required.", "Naqla prerequisite"); return;
            }
            button.Enabled = startup.Enabled = launch.Enabled = engines.Enabled = false; form.ControlBox = false;
            try
            {
                // Copy checkbox state before leaving UI thread.
                var startWithWindows = startup.Checked;
                var downloadEngines = engines.Checked;
                var progress = new Progress<string>(text => status.Text = text);
                await Task.Run(() => Install(startWithWindows, downloadEngines, null, true, progress));
                if (launch.Checked) Launch();
                MessageBox.Show("Installed. Naqla runs in the system tray with no main window.\nRight-click its icon → Manual testing guide…\nDiagnostics → Show diagnostic status… / Open log folder", "Naqla development build");
                form.Close();
            }
            catch (Exception error) { MessageBox.Show(error.Message, "Naqla installation failed"); button.Enabled = startup.Enabled = launch.Enabled = engines.Enabled = true; form.ControlBox = true; }
        };
        form.Controls.AddRange([title, description, startup, launch, engines, status, prerequisites, button]); return form;
    }
    private static void Launch() => Process.Start(new ProcessStartInfo(Path.Combine(InstallDirectory, "Naqla.App.exe")) { UseShellExecute = false });
    private static List<string> Extract(string directory)
    {
        using var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("Naqla.Payload.zip") ?? throw new InvalidOperationException("Payload missing. Build using scripts/build.ps1.");
        return InstallationFiles.Extract(payload, directory);
    }
    private static void StopApp()
    {
        try { using var signal = EventWaitHandle.OpenExisting(@"Local\Naqla.Exit.0.1"); signal.Set(); }
        catch (WaitHandleCannotBeOpenedException) { return; }
        // The app cancels active work and cleans up its own temporary output before stopping.
        try
        {
            using var mutex = Mutex.OpenExisting(@"Local\Naqla.Tray.0.1");
            if (!mutex.WaitOne(TimeSpan.FromSeconds(30))) throw new IOException("Naqla is still converting. Exit from the tray and try again.");
            mutex.ReleaseMutex();
        }
        catch (WaitHandleCannotBeOpenedException) { }
        catch (AbandonedMutexException) { }
    }
    private static async Task Install(bool startup, bool downloadEngines, string? cache, bool allowNetwork, IProgress<string>? progress)
    {
        StopApp();
        var parent = Path.GetDirectoryName(InstallDirectory)!; InstallationFiles.GuardDirectory(InstallDirectory);
        var stage = Path.Combine(parent, ".Naqla-stage-" + Guid.NewGuid().ToString("N")); var backup = Path.Combine(parent, ".Naqla-backup-" + Guid.NewGuid().ToString("N"));
        var committed = false;
        var activated = false;
        using var previousRun = Registry.CurrentUser.OpenSubKey(RunKey);
        var runValue = previousRun?.GetValue("Naqla"); var runKind = runValue is null ? RegistryValueKind.String : previousRun!.GetValueKind("Naqla");
        using var previousUninstall = Registry.CurrentUser.OpenSubKey(UninstallKey);
        var uninstallValues = previousUninstall?.GetValueNames().ToDictionary(name => name, name => (Value: previousUninstall.GetValue(name)!, Kind: previousUninstall.GetValueKind(name)));
        try
        {
        var files = Extract(stage);
        if (downloadEngines)
        {
            await EngineDownloads.Prepare(stage, cache ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Naqla", "InstallerCache"), allowNetwork, progress).ConfigureAwait(false);
            files = Directory.GetFiles(stage, "*", SearchOption.AllDirectories).Select(x => Path.GetRelativePath(stage, x)).ToList();
        }
        var uninstaller = Path.Combine(InstallDirectory, "Uninstall-Naqla.exe");
        File.Copy(Environment.ProcessPath!, Path.Combine(stage, "Uninstall-Naqla.exe"), false); files.Add("Uninstall-Naqla.exe");
        InstallationFiles.WriteManifest(stage, files);
        if (Directory.Exists(InstallDirectory)) { InstallationFiles.PreserveUnowned(InstallDirectory, stage); Directory.Move(InstallDirectory, backup); }
        try { Directory.Move(stage, InstallDirectory); }
        catch { if (Directory.Exists(backup)) Directory.Move(backup, InstallDirectory); throw; }
        activated = true;
        var executable = Path.Combine(InstallDirectory, "Naqla.App.exe");
        using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
        {
            if (startup) key.SetValue("Naqla", $"\"{executable}\""); else key.DeleteValue("Naqla", false);
        }
        using var uninstall = Registry.CurrentUser.CreateSubKey(UninstallKey);
        uninstall.SetValue("DisplayName", "Naqla (beta)");
        uninstall.SetValue("DisplayVersion", "0.8.0-beta.1"); uninstall.SetValue("Publisher", "Naqla");
        uninstall.SetValue("InstallLocation", InstallDirectory); uninstall.SetValue("DisplayIcon", executable);
        uninstall.SetValue("UninstallString", $"\"{uninstaller}\" --uninstall");
        uninstall.SetValue("NoModify", 1); uninstall.SetValue("NoRepair", 1);
        StartMenuShortcut(remove: false);
        committed = true;
        }
        catch (Exception installError)
        {
            if (activated && !committed)
            {
                try
                {
                    Directory.Move(InstallDirectory, stage);
                    if (Directory.Exists(backup)) Directory.Move(backup, InstallDirectory);
                    using var run = Registry.CurrentUser.CreateSubKey(RunKey);
                    if (runValue is null) run.DeleteValue("Naqla", false); else run.SetValue("Naqla", runValue, runKind);
                    Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
                    if (uninstallValues is not null) { using var restored = Registry.CurrentUser.CreateSubKey(UninstallKey); foreach (var pair in uninstallValues) restored.SetValue(pair.Key, pair.Value.Value, pair.Value.Kind); }
                }
                catch (Exception rollbackError) { throw new AggregateException("Installation failed and automatic rollback needs review. The previous installation backup was retained.", installError, rollbackError); }
            }
            throw;
        }
        finally
        {
            InstallationFiles.RemoveOwnedStage(parent, stage);
            if (committed) InstallationFiles.RemoveOwnedStage(parent, backup);
        }
    }
    private static void Uninstall()
    {
        StopApp();
        var manifestPath = Path.Combine(InstallDirectory, Manifest);
        if (!File.Exists(manifestPath)) throw new IOException("Installation file manifest is missing; automatic removal stopped to protect unrelated files.");
        StartMenuShortcut(remove: true);
        InstallationFiles.RemoveKnown(InstallDirectory);
        using (var run = Registry.CurrentUser.OpenSubKey(RunKey, true))
        {
            if (run?.GetValue("Naqla") is string value && value == $"\"{Path.Combine(InstallDirectory, "Naqla.App.exe")}\"") run.DeleteValue("Naqla", false);
        }
        Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
        // A short, hidden, per-user cleanup process deletes only this known temp copy
        // after this process exits. No reboot, service, or administrator rights.
        var self = Environment.ProcessPath!;
        var temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(self).StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(self).StartsWith("Naqla-Uninstall-", StringComparison.Ordinal)) return;
        var literal = self.Replace("'", "''");
        var script = $"Wait-Process -Id {Environment.ProcessId} -ErrorAction SilentlyContinue; Remove-Item -LiteralPath '{literal}' -Force -ErrorAction SilentlyContinue";
        var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
        Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe"), $"-NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand {encoded}") { UseShellExecute = false, CreateNoWindow = true });
    }
    private static void StartMenuShortcut(bool remove)
    {
        // Shell-link files need an STA COM apartment, including unattended installs.
        var thread = new Thread(() =>
        {
            object? shell = null, shortcut = null;
            try
            {
                var directory = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                if (string.IsNullOrWhiteSpace(directory)) return;
                InstallationFiles.GuardDirectory(directory);
                Directory.CreateDirectory(directory);
                var path = InstallationFiles.Target(directory, "Naqla.lnk");
                if (remove && !File.Exists(path)) return;
                var type = Type.GetTypeFromProgID("WScript.Shell"); if (type is null) return;
                shell = Activator.CreateInstance(type)!; shortcut = ((dynamic)shell).CreateShortcut(path);
                var executable = Path.Combine(InstallDirectory, "Naqla.App.exe");
                if (File.Exists(path) && !string.Equals((string)((dynamic)shortcut).TargetPath, executable, StringComparison.OrdinalIgnoreCase)) return;
                if (remove) { File.Delete(path); return; }
                ((dynamic)shortcut).TargetPath = executable;
                ((dynamic)shortcut).Arguments = "";
                ((dynamic)shortcut).WorkingDirectory = InstallDirectory;
                ((dynamic)shortcut).Description = "Naqla — Drag a file, then press Shift";
                ((dynamic)shortcut).IconLocation = executable + ",0";
                ((dynamic)shortcut).Save();
            }
            catch (Exception) { /* Optional shortcut must not corrupt an otherwise valid installation. */ }
            finally
            {
                if (shortcut is not null && Marshal.IsComObject(shortcut)) Marshal.FinalReleaseComObject(shortcut);
                if (shell is not null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
            }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
    }
}

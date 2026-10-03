using HyperSploit.Adb;
using HyperSploit.Xiaomi;
namespace HyperSploit;

public static class ManagementCli {
    private static string Read(string prompt) {
        Console.Write(prompt + ": ");
        return Console.ReadLine() ?? throw new EndOfStreamException();
    }
    private static bool Confirm(string action) => Read(action + "? Type yes").Equals("yes", StringComparison.OrdinalIgnoreCase);
    private static void Output(string text, bool error) {
        if (error) Console.Error.WriteLine(text); else Console.WriteLine(text);
    }
    private static void Output(AdbResult result) {
        if (result.Stdout.Length > 0) Console.Write(result.Stdout);
        if (result.Stderr.Length > 0) Console.Error.Write(result.Stderr);
    }
    public static async Task RunAsync() {
        var runner = new AdbCommandRunner();
        var store = new DeviceSelectionStore();
        var discovery = new DeviceDiscovery(runner);
        var selected = store.Load();
        while (true) {
            try {
                Console.WriteLine("\nHyperSploit Next\n================");
                var current = (await discovery.ListAsync()).FirstOrDefault(d => d.Serial == selected);
                Console.WriteLine($"Device: {current?.Model ?? "None/disconnected"}");
                Console.WriteLine($"ADB: {current?.Transport ?? "Wireless"} ({current?.RawState ?? "not selected"})");
                if (selected != null) Console.WriteLine(selected);
            } catch (Exception e) when (e is IOException or TimeoutException or ArgumentException) {
                Console.WriteLine($"ADB unavailable: {e.Message}");
            }
            Console.WriteLine("1. Device information\n2. ADB shell\n3. Applications\n4. File transfer\n5. Logcat\n6. Reboot\n7. Xiaomi diagnostics\n8. Compatibility report\n9. Fastboot information\n10. Xiaomi error lookup\n11. Wireless setup / select device\n12. Doctor\n0. Exit");
            try {
                var choice = Read("Choice").Trim();
                if (choice == "0") return;
                if (choice == "11") { selected = await WirelessCli.RunAsync(selected); continue; }
                using var cancellation = new CancellationTokenSource();
                ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
                Console.CancelKeyPress += cancel;
                try {
                    var ct = cancellation.Token;
                    var device = new SelectedDeviceCommands(runner, selected);
                    switch (choice) {
                        case "1":
                            await device.ArgumentsAsync(["shell", "getprop"], ct);
                            var info = await discovery.ReadAsync(selected!, ct);
                            Console.WriteLine($"Model: {info.Get("ro.product.model")}\nVendor: {info.Vendor}\nAndroid: {info.Get("ro.build.version.release")}\nOS: {info.Os}\nBootloader: {info.Bootloader}");
                            foreach (var key in new[] { "ro.product.manufacturer", "ro.product.brand", "ro.product.device", "ro.product.name", "ro.build.version.sdk", "ro.build.version.security_patch", "ro.build.fingerprint", "ro.boot.flash.locked", "ro.boot.vbmeta.device_state", "ro.boot.verifiedbootstate" })
                                Console.WriteLine($"{key}:\n  {info.Get(key)}");
                            break;
                        case "2":
                            await device.ArgumentsAsync(["shell"], ct);
                            var shellChoice = Read("1. Interactive\n2. One-shot\n0. Back\nChoice");
                            if (shellChoice == "1") {
                                Console.WriteLine("Opening selected device shell. Type exit to return.");
                                await new AdbShell(device).InteractiveAsync(ct);
                            } else if (shellChoice == "2") {
                                var executable = Read("Executable (e.g. ls; empty = back)");
                                if (executable.Length == 0) break;
                                var arguments = new List<string> { executable };
                                Console.WriteLine("Enter each argument separately, without quotes. Empty line runs.");
                                while (true) { var arg = Read("Argument"); if (arg.Length == 0) break; arguments.Add(arg); }
                                var seconds = Read("Timeout seconds (empty = 20)");
                                var duration = seconds.Length == 0 ? 20 : double.Parse(seconds, System.Globalization.CultureInfo.InvariantCulture);
                                if (!double.IsFinite(duration) || duration <= 0 || duration > 86400) throw new ArgumentException("Timeout must be 1-86400 seconds.");
                                Output(await new AdbShell(new SelectedDeviceCommands(new AdbCommandRunner(TimeSpan.FromSeconds(duration)), selected)).RunAsync(arguments, ct));
                            }
                            break;
                        case "3":
                            await device.ArgumentsAsync(["shell"], ct);
                            var packages = new PackageManager(device);
                            switch (Read("1. List/search\n2. Info\n3. Install APK\n4. Uninstall\n0. Back\nChoice")) {
                                case "1":
                                    foreach (var package in await packages.ListAsync(Read("Filter (empty = all)"), ct)) Console.WriteLine(package);
                                    break;
                                case "2": Output(await packages.InfoAsync(Read("Package"), ct)); break;
                                case "3":
                                    var apk = Read("Local APK path");
                                    if (Confirm($"Install {apk} on {selected}")) {
                                        Console.WriteLine("Installing; Ctrl+C cancels.");
                                        await packages.InstallAsync(apk, true, Output, ct);
                                        Console.WriteLine("Installation succeeded.");
                                    }
                                    break;
                                case "4":
                                    var remove = Read("Package");
                                    if (Confirm($"Uninstall {remove} from {selected} (removes app data)")) {
                                        await packages.UninstallAsync(remove, true, ct);
                                        Console.WriteLine("Uninstallation succeeded.");
                                    }
                                    break;
                            }
                            break;
                        case "4":
                            await device.ArgumentsAsync(["shell"], ct);
                            var transfer = new FileTransfer(device);
                            switch (Read("1. Push\n2. Pull\n0. Back\nChoice")) {
                                case "1":
                                    var local = Read("Local file path");
                                    var remote = Read("Absolute Android destination");
                                    if (Confirm($"Push to {selected}:{remote} (may replace remote file)")) {
                                        Console.WriteLine("Transferring; Ctrl+C cancels.");
                                        await transfer.PushAsync(local, remote, Output, ct);
                                        Console.WriteLine("Push succeeded.");
                                    }
                                    break;
                                case "2":
                                    var source = Read("Absolute Android file path");
                                    var destination = Read("Local destination file path");
                                    var overwrite = File.Exists(destination) && Confirm($"Overwrite {destination}");
                                    if (File.Exists(destination) && !overwrite) break;
                                    Console.WriteLine("Transferring; Ctrl+C cancels.");
                                    await transfer.PullAsync(source, destination, overwrite, Output, ct);
                                    Console.WriteLine("Pull succeeded.");
                                    break;
                            }
                            break;
                        case "5":
                            await device.ArgumentsAsync(["logcat"], ct);
                            var logcat = new Logcat(device);
                            switch (Read("1. Stream/save\n2. Clear\n0. Back\nChoice")) {
                                case "1":
                                    var tag = Read("Tag (empty = all)");
                                    var text = Read("Text filter (empty = all)");
                                    var save = Read("Save file (empty = console only)");
                                    var replace = save.Length > 0 && File.Exists(save) && Confirm($"Overwrite {save}");
                                    if (save.Length > 0 && File.Exists(save) && !replace) break;
                                    Console.WriteLine("Streaming; Ctrl+C stops and returns to menu.");
                                    await logcat.StreamAsync(tag, text, Output, save.Length == 0 ? null : save, replace, ct);
                                    break;
                                case "2":
                                    if (Confirm($"Clear logs on {selected}")) { await logcat.ClearAsync(ct); Console.WriteLine("Logs cleared."); }
                                    break;
                            }
                            break;
                        case "6":
                            await device.ArgumentsAsync(["reboot"], ct);
                            var mode = Read("1. Android\n2. Recovery\n3. Bootloader\n0. Back\nChoice") switch {
                                "1" => (RebootMode?)RebootMode.Android, "2" => RebootMode.Recovery,
                                "3" => RebootMode.Bootloader, _ => null
                            };
                            if (mode != null && Confirm($"Reboot {selected} to {mode}")) {
                                await new RebootControls(device).RebootAsync(mode.Value, true, ct);
                                Console.WriteLine("Reboot requested. Wireless connection will disconnect.");
                            }
                            break;
                        case "7":
                            await DiagnosticsCli.XiaomiAsync(discovery, selected, ct);
                            break;
                        case "8":
                            await DiagnosticsCli.CompatibilityAsync(discovery, selected, ct);
                            break;
                        case "9":
                            await DiagnosticsCli.FastbootAsync(ct);
                            break;
                        case "10":
                            var error = XiaomiErrorDatabase.Lookup(Read("Xiaomi code or full error message"));
                            DiagnosticsCli.Write($"Category: {error.Category}\n{error.Explanation}\nSafe troubleshooting:\n{error.Troubleshooting}");
                            break;
                        case "12":
                            EnvironmentDiagnostics.Print();
                            var version = await runner.RunAsync(["version"], cancellationToken: ct);
                            version.EnsureSuccess(); Output(version);
                            Console.WriteLine($"Visible devices: {(await discovery.ListAsync(ct)).Count}");
                            Console.WriteLine("Use same Wi-Fi and Wireless debugging.\nOffline: reconnect. Unauthorized: authorize.\nStandard iSH is i386; check uname -m.");
                            break;
                        default: Console.WriteLine("Choose 0-12."); break;
                    }
                } catch (OperationCanceledException) { Console.WriteLine("Operation stopped."); }
                finally { Console.CancelKeyPress -= cancel; }
            } catch (EndOfStreamException) { return; }
            catch (Exception e) when (e is IOException or TimeoutException or ArgumentException or UnauthorizedAccessException or FormatException or OverflowException) {
                Console.WriteLine($"Error: {e.Message}");
            }
        }
    }
}

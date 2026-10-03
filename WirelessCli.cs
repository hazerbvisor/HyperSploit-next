using HyperSploit.Adb;

namespace HyperSploit;

public static class WirelessCli {
    private static string? Read(string prompt) {
        Console.Write(prompt + ": ");
        return Console.ReadLine()?.Trim();
    }
    public static async Task<string?> RunAsync(string? selection = null) {
        var runner = new AdbCommandRunner();
        var wireless = new WirelessAdb(runner);
        var discovery = new DeviceDiscovery(runner);
        var store = new DeviceSelectionStore();
        var selected = selection ?? store.Load();
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancel;
        try {
            while (!cancellation.IsCancellationRequested) {
                Console.WriteLine("\nWireless setup");
                Console.WriteLine("1. Pair wireless device\n2. Connect/reconnect device\n3. Disconnect\n4. List devices\n5. Select device\n6. Device information\n7. Xiaomi/HyperOS information\n8. Doctor\n0. Back");
                var choice = Read("Choice");
                if (choice is null or "0") return selected;
                try {
                    var ct = cancellation.Token;
                    switch (choice) {
                        case "1":
                            var pairing = Read("Pairing IP:PORT");
                            if (pairing == null) return selected;
                            var code = Read("Six-digit pairing code");
                            if (code == null) return selected;
                            Console.WriteLine(await wireless.PairAsync(pairing, code, ct));
                            Console.WriteLine("Now connect using the debugging port.");
                            break;
                        case "2":
                            var endpoint = Read("Debugging IP:PORT (empty = last address)");
                            if (endpoint == null) return selected;
                            if (endpoint.Length == 0) endpoint = new ConfigStore().Load().LastWirelessAddress ?? selected ?? "";
                            Console.WriteLine(await wireless.ConnectAsync(endpoint, ct));
                            try { new ConfigStore().Update(config => config.LastWirelessAddress = endpoint); }
                            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Console.WriteLine($"Connected; cannot remember address: {e.Message}"); }
                            break;
                        case "3":
                            var disconnect = Read("IP:PORT (empty = selected)");
                            if (disconnect == null) return selected;
                            Console.WriteLine(await wireless.DisconnectAsync(
                                disconnect.Length == 0 ? selected ?? "" : disconnect, ct));
                            break;
                        case "4":
                        case "5":
                            var devices = await discovery.ListAsync(ct);
                            if (devices.Count == 0) Console.WriteLine("No devices. Pair and connect first.");
                            for (var i = 0; i < devices.Count; i++) {
                                var d = devices[i];
                                Console.WriteLine($"{i + 1}. {d.Model} [{d.RawState}]");
                                Console.WriteLine($"   {d.Serial}" + (d.Serial == selected ? " (selected)" : ""));
                            }
                            if (choice == "5" && devices.Count > 0) {
                                var number = Read("Device number (0 = cancel)");
                                if (number == null) return selected;
                                if (int.TryParse(number, out var index) && index > 0 && index <= devices.Count) {
                                    selected = devices[index - 1].Serial;
                                    try { store.Save(selected); }
                                    catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
                                        Console.WriteLine($"Selected for this session; cannot remember: {e.Message}");
                                    }
                                }
                            }
                            break;
                        case "6":
                        case "7":
                            if (selected == null) { Console.WriteLine("Select a device first (5)."); break; }
                            var info = await discovery.ReadAsync(selected, ct);
                            Console.WriteLine(info.Get("ro.product.model"));
                            Console.WriteLine($"Vendor: {info.Vendor}");
                            Console.WriteLine($"Codename: {info.Get("ro.product.device")}");
                            Console.WriteLine($"Android: {info.Get("ro.build.version.release")}");
                            Console.WriteLine($"OS: {info.Os}");
                            Console.WriteLine($"ADB: {info.Device.Transport}");
                            Console.WriteLine($"Bootloader: {info.Bootloader}");
                            Console.WriteLine($"Verified Boot: {info.Get("ro.boot.verifiedbootstate")}");
                            if (choice == "6")
                                foreach (var key in new[] { "ro.product.manufacturer", "ro.product.brand",
                                    "ro.product.name", "ro.build.version.sdk", "ro.build.version.security_patch",
                                    "ro.build.fingerprint", "ro.boot.flash.locked", "ro.boot.vbmeta.device_state" })
                                    Console.WriteLine($"{key}:\n  {info.Get(key)}");
                            break;
                        case "8":
                            await Doctor.RunAsync(runner, ct);
                            break;
                        default: Console.WriteLine("Choose 0-8."); break;
                    }
                } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return selected; }
                catch (Exception e) when (e is IOException or TimeoutException or ArgumentException or UnauthorizedAccessException) {
                    Console.WriteLine($"Error: {e.Message}");
                }
            }
        } finally { Console.CancelKeyPress -= cancel; }
        return selected;
    }
}

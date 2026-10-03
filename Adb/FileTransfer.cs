namespace HyperSploit.Adb;

public sealed class FileTransfer(SelectedDeviceCommands device) {
    public static void ValidateRemote(string path) {
        if (!path.StartsWith('/') || path.Contains('\0') || path.Contains('\n') || path.Contains('\r'))
            throw new ArgumentException("Use an absolute Android path.");
    }
    public Task<AdbResult> PushAsync(string local, string remote, Action<string, bool> progress, CancellationToken ct = default) {
        ValidateRemote(remote);
        var path = Path.GetFullPath(local);
        if (!File.Exists(path)) throw new FileNotFoundException("Local file not found.", path);
        return device.StreamAsync(["push", path, remote], progress, ct);
    }
    public async Task PullAsync(string remote, string local, bool overwrite, Action<string, bool> progress, CancellationToken ct = default) {
        ValidateRemote(remote);
        var path = Path.GetFullPath(local);
        if (Directory.Exists(path)) throw new IOException("Choose a local file name, not a directory.");
        if (File.Exists(path) && !overwrite) throw new IOException("Local file exists. Confirm overwrite first.");
        // Stage beside the destination so failed/cancelled pulls never damage existing files.
        var staging = Path.Combine(Path.GetDirectoryName(path)!, ".hypersploit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        var temp = Path.Combine(staging, "download");
        try {
            await device.StreamAsync(["pull", remote, temp], progress, ct);
            if (!File.Exists(temp)) throw new IOException("Remote path must be a file.");
            ct.ThrowIfCancellationRequested();
            File.Move(temp, path, overwrite);
        } finally { Directory.Delete(staging, true); }
    }
}

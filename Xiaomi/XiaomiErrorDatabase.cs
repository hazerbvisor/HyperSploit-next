namespace HyperSploit.Xiaomi;

public sealed record XiaomiError(string Match, string Explanation, string Category, string Troubleshooting);
/// <summary>Offline reference only. No Xiaomi requests or account data are read or sent.</summary>
public static class XiaomiErrorDatabase {
    private static readonly XiaomiError[] Errors = [
        new("10008", "Mi Unlock status binding commonly reports this as a network/service error. The code alone does not identify the cause.", "Binding / service",
            "Check connectivity, automatic date/time and Xiaomi service availability. Retry later through the official settings UI; retain the full message for Xiaomi support."),
        new("86006", "Reported during Mi Unlock status binding when the account/device binding cannot be completed. Meaning can vary by firmware.", "Binding",
            "Check that the intended Xiaomi account is signed in and follow the official device/account eligibility guidance. Contact Xiaomi support with the full message."),
        new("couldn't verify device", "Official verification could not establish the required device/account association.", "Verification",
            "Confirm the same account in device settings and the official Mi Unlock tool. Review Mi Unlock status in Developer options and Xiaomi's current instructions."),
        new("current account is not bound", "The official tool reports that this account is not bound to this device.", "Account binding",
            "Check the account on both the device and official tool. Use the official Mi Unlock status settings flow if Xiaomi permits binding."),
        new("please unlock [number] hours later", "A message containing a required waiting period may indicate an official account/device waiting requirement.", "Waiting period",
            "Read the full message and wait the exact period stated by Xiaomi. Check again with the official tool; this lookup cannot determine eligibility."),
        new("account application area", "The official service reports a region or account application eligibility mismatch.", "Eligibility / region",
            "Review the region and account requirements in Xiaomi's official guidance. Contact support; do not alter requests or misrepresent account/device region."),
        new("device is not activated", "The official service reports that device activation is required.", "Activation",
            "Complete the normal Xiaomi activation/sign-in flow with the authorized account and check connectivity. Contact support if activation fails.")
    ];
    public static XiaomiError Lookup(string input) {
        var text = input.Trim().Replace('’', '\'').Replace('‘', '\'');
        foreach (var error in Errors) {
            if (error.Category == "Waiting period") {
                if (System.Text.RegularExpressions.Regex.IsMatch(text, @"please unlock (?:after )?\d+ hours?(?: later)?", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return error;
                continue;
            }
            var numeric = error.Match.All(char.IsDigit);
            if (numeric ? System.Text.RegularExpressions.Regex.IsMatch(text, $@"(?<!\d){error.Match}(?!\d)") : text.Contains(error.Match, StringComparison.OrdinalIgnoreCase)) return error;
        }
        return new("Unknown", "Unknown: no known code or message matched. Codes vary by firmware and official tool version.", "Unknown",
            "Keep the complete error message and consult Xiaomi's official help or support. Remove personal account details before sharing.");
    }
}

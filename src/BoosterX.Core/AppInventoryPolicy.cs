namespace BoosterX.Core;

public sealed record UninstallCommand(string Executable, string Arguments);

public static class AppInventoryPolicy
{
    public const int MaximumCommandLength = 32767;
    private static readonly string[] BlockedHosts = ["cmd.exe","powershell.exe","pwsh.exe","wscript.exe","cscript.exe","mshta.exe","rundll32.exe","regsvr32.exe"];
    private static readonly string[] CriticalNames = ["Windows Security","Microsoft Defender","WebView2 Runtime","Visual C++","Windows Desktop Runtime",".NET Runtime",".NET Host","Windows SDK","Display Driver","Graphics Driver","Chipset Driver","Audio Driver","Antivirus","Anti-virus","Endpoint Protection",
        "Malwarebytes","Bitdefender","Kaspersky","Avast","ESET","Norton","McAfee","Sophos","CrowdStrike","SentinelOne"];
    public static string? ProtectionReason(string name, bool systemComponent, bool framework = false, bool nonRemovable = false)
    {
        if (systemComponent || framework || nonRemovable) return "System, framework, update or non-removable component";
        return CriticalNames.Any(token => name.Contains(token, StringComparison.OrdinalIgnoreCase)) ? "Shared runtime, driver or security component" : null;
    }
    public static UninstallCommand Parse(string command)
    {
        if (string.IsNullOrWhiteSpace(command) || command.Length > MaximumCommandLength || command.Contains('\0'))
            throw new InvalidDataException("The registered uninstall command is invalid.");
        command = command.Trim(); string executable, arguments;
        if (command[0] == '"')
        {
            int end = command.IndexOf('"', 1);
            if (end < 0) throw new InvalidDataException("The registered executable has an unmatched quote.");
            executable = command[1..end]; arguments = command[(end+1)..].TrimStart();
        }
        else
        {
            int end = -1, search = 0;
            while (search < command.Length)
            {
                int candidate = command.IndexOf(".exe",search,StringComparison.OrdinalIgnoreCase);
                if (candidate < 0) break;
                if (candidate + 4 == command.Length || char.IsWhiteSpace(command[candidate+4])) { end = candidate; break; }
                search = candidate + 4;
            }
            if (end < 0) throw new InvalidDataException("Use Windows Installed apps for this uninstall command.");
            executable = command[..(end+4)]; arguments = command[(end+4)..].TrimStart();
        }
        string file = executable.Replace('/', '\\').Split('\\')[^1];
        if (!file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || BlockedHosts.Contains(file, StringComparer.OrdinalIgnoreCase)
            || executable.StartsWith(@"\\", StringComparison.Ordinal) || executable.Contains('"')
            || !(executable.Length >= 3 && char.IsAsciiLetter(executable[0]) && executable[1] == ':' && executable[2] is '\\' or '/'))
            throw new InvalidDataException("Use Windows Installed apps for this unsupported uninstall host.");
        return new(executable, arguments);
    }
}

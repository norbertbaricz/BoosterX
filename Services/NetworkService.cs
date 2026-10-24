using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using BoosterX.Core;
using Microsoft.Win32;

namespace BoosterX.App.Services;

public sealed record AdapterItem(string Id, string Name, string Description, string Dns, long Speed)
{
    public override string ToString() => $"{Name} — {Description}";
}
public sealed record DnsSnapshot(string AdapterId, string[] StaticServers, string RegistrationMode);
public sealed record DnsPreset(string Name, string[] Servers)
{
    public override string ToString() => Name;
}

public sealed class NetworkService(BoosterOptions options, SystemCommand commands)
{
    public static readonly IReadOnlyList<DnsPreset> Presets =
    [
        new("Cloudflare — 1.1.1.1 / 1.0.0.1", ["1.1.1.1", "1.0.0.1"]),
        new("Google — 8.8.8.8 / 8.8.4.4", ["8.8.8.8", "8.8.4.4"]),
        new("Quad9 — 9.9.9.9 / 149.112.112.112", ["9.9.9.9", "149.112.112.112"]),
        new("Automatic — router / DHCP", [])
    ];

    public static IReadOnlyList<AdapterItem> ListAdapters()
    {
        var result = new List<AdapterItem>();
        foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            try
            {
                if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback
                    || !Guid.TryParse(adapter.Id,out _) || new[] { "WFP Native MAC", "LightWeight Filter", "QoS Packet Scheduler", "Native WiFi Filter" }
                        .Any(filter => adapter.Description.Contains(filter,StringComparison.OrdinalIgnoreCase))) continue;
                IPInterfaceProperties ip = adapter.GetIPProperties();
                if (!ip.UnicastAddresses.Any(address => address.Address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)) continue;
                result.Add(new(adapter.Id,adapter.Name,adapter.Description,string.Join(", ",ip.DnsAddresses),adapter.Speed));
            }
            catch (Exception ex) when (ex is NetworkInformationException or NotSupportedException) { }
        }
        return result.DistinctBy(adapter => adapter.Id,StringComparer.OrdinalIgnoreCase).OrderBy(adapter => adapter.Name,StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    public static DnsSnapshot Capture(string id)
    {
        NetworkInterface adapter = Find(id);
        string guid = Guid.Parse(adapter.Id).ToString("B");
        using var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = hive.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\" + guid)
                        ?? throw new IOException("The adapter's IPv4 configuration was not found.");
        string value = key.GetValue("NameServer") as string ?? "";
        string[] servers = value.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        ValidateServers(servers);
        var settings = new NativeMethods.DnsInterfaceSettings { Version = 1 };
        uint code = NativeMethods.GetInterfaceDnsSettings(Guid.Parse(adapter.Id), ref settings);
        if (code != 0) throw new Win32Exception((int)code);
        try
        {
            string registration = settings.RegistrationEnabled == 0 ? "none" : settings.RegisterAdapterName != 0 ? "both" : "primary";
            return new(adapter.Id, servers, registration);
        }
        finally { NativeMethods.FreeInterfaceDnsSettings(ref settings); }
    }

    public async Task SetDnsAsync(string adapterId, string[] servers, string? registrationMode = null)
    {
        ValidateServers(servers);
        registrationMode ??= Capture(adapterId).RegistrationMode;
        if (registrationMode is not ("none" or "primary" or "both")) throw new InvalidDataException("Invalid DNS registration mode.");
        string name = Find(adapterId).Name; // Resolve the GUID again; interface names may have changed since backup.
        if (servers.Length == 0)
        {
            int code = await commands.RunAsync("netsh.exe", ["interface", "ipv4", "set", "dnsservers", "name=" + name, "source=dhcp", "register=" + registrationMode]);
            _ = SystemCommand.CheckExit(code, "Automatic DNS restored.");
        }
        else
        {
            int code = await commands.RunAsync("netsh.exe", ["interface", "ipv4", "set", "dnsservers", "name=" + name,
                "source=static", "address=" + servers[0], "register=" + registrationMode, "validate=no"]);
            _ = SystemCommand.CheckExit(code, "Primary DNS set.");
            for (int i = 1; i < servers.Length; i++)
            {
                code = await commands.RunAsync("netsh.exe", ["interface", "ipv4", "add", "dnsservers", "name=" + name,
                    "address=" + servers[i], "index=" + (i + 1), "validate=no"]);
                _ = SystemCommand.CheckExit(code, "Secondary DNS set.");
            }
        }
        DnsSnapshot actual = Capture(adapterId);
        if (!actual.StaticServers.SequenceEqual(servers, StringComparer.OrdinalIgnoreCase) || actual.RegistrationMode != registrationMode)
            throw new IOException("Windows did not retain the requested DNS setting. A policy may control this adapter.");
        _ = NativeMethods.DnsFlushResolverCache();
    }

    public async Task<string> DiagnoseAsync(string adapterId, CancellationToken cancellationToken)
    {
        NetworkInterface adapter = Find(adapterId);
        IPInterfaceProperties ip = adapter.GetIPProperties();
        var lines = new List<string>
        {
            "Adapter: " + adapter.Name + " / " + adapter.Description,
            "Link rate (not an Internet speed test): " + (adapter.Speed / 1_000_000d).ToString("0.##") + " Mbps",
            "Addresses: " + string.Join(", ", ip.UnicastAddresses.Select(a => a.Address)),
            "DNS: " + string.Join(", ", ip.DnsAddresses),
            "DNS suffix: " + ip.DnsSuffix
        };
        var targets = new List<string>();
        IPAddress? gateway = ip.GatewayAddresses.Select(g => g.Address).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
        if (gateway != null) targets.Add(gateway.ToString());
        targets.Add("1.1.1.1");
        foreach (string target in targets.Distinct())
        {
            var times = new List<long>();
            for (int i = 0; i < options.PingSamples; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using var ping = new Ping();
                    PingReply reply = await ping.SendPingAsync(target, options.PingTimeoutMilliseconds);
                    if (reply.Status == IPStatus.Success) times.Add(reply.RoundtripTime);
                }
                catch (PingException) { }
            }
            lines.Add($"ICMP {target}: {(times.Count == 0 ? "no replies" : $"{times.Average():0.0} ms average")}, "
                + $"{options.PingSamples - times.Count}/{options.PingSamples} unanswered. ICMP may be filtered; VPNs/routes may select a different interface.");
        }
        var clock = Stopwatch.StartNew();
        try
        {
            _ = await Dns.GetHostAddressesAsync("www.microsoft.com", cancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            lines.Add($"System DNS lookup (possibly cached): {clock.Elapsed.TotalMilliseconds:0.0} ms.");
        }
        catch (Exception ex) when (ex is SocketException or TimeoutException) { lines.Add("DNS lookup: " + ex.Message); }
        lines.Add("DNS changes affect name resolution; they do not guarantee lower in-game ping or more bandwidth.");
        return string.Join(Environment.NewLine, lines);
    }

    private static NetworkInterface Find(string id)
    {
        if (!Guid.TryParse(id, out _)) throw new InvalidDataException("Invalid adapter identifier.");
        return NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n => string.Equals(n.Id, id, StringComparison.OrdinalIgnoreCase))
               ?? throw new IOException("Network adapter is no longer present.");
    }

    private static void ValidateServers(string[] servers)
    {
        if (servers.Length > 16 || servers.Any(s => !IPAddress.TryParse(s, out var address) || address.AddressFamily != AddressFamily.InterNetwork))
            throw new InvalidDataException("Only a bounded list of IPv4 DNS servers is supported.");
    }
}

using System.Collections.Generic;

namespace PortKnocker
{
    public enum KnockProtocol
    {
        TCP,
        UDP
    }

    public class KnockStep
    {
        public int Port { get; set; }
        public KnockProtocol Protocol { get; set; } = KnockProtocol.TCP;
    }

    public class KnockProfile
    {
        public string Name { get; set; } = "";
        public string IpAddress { get; set; } = "";
        public List<KnockStep> Steps { get; set; } = new()
        {
            new KnockStep(),
            new KnockStep(),
            new KnockStep(),
            new KnockStep()
        };
    }

    public enum MappingType
    {
        AnyIp,      // Port-only rule
        ExactIp,    // String match on dest IP
        Cidr        // 192.168.1.0/24
    }

    public class AutoKnockMapping
    {
        public string Name { get; set; } = "";         // Friendly name for the rule
        public MappingType Type { get; set; } = MappingType.AnyIp;
        public string Pattern { get; set; } = "";      // exact IP or CIDR (or empty for AnyIp)
        public List<int> Ports { get; set; } = new();  // empty = any port
        public string ProfileName { get; set; } = "";  // which saved profile to use
    }

    // Root of persisted state (backward compatible with old profiles.json)
    public class AppState
    {
        public int Version { get; set; } = 1;
        public List<KnockProfile> Profiles { get; set; } = new();
        public List<AutoKnockMapping> Mappings { get; set; } = new();
    }
}

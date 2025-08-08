using System;
using System.Net;

namespace PortKnocker
{
    public static class IpUtils
    {
        public static bool TryParseCidr(string cidr, out uint network, out uint mask)
        {
            network = 0; mask = 0;
            if (string.IsNullOrWhiteSpace(cidr)) return false;

            var parts = cidr.Split('/');
            if (parts.Length != 2) return false;

            if (!IPAddress.TryParse(parts[0].Trim(), out var baseIp)) return false;
            if (!int.TryParse(parts[1].Trim(), out var prefix)) return false;
            if (baseIp.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;
            if (prefix < 0 || prefix > 32) return false;

            mask = prefix == 0 ? 0 : 0xFFFFFFFFu << (32 - prefix);
            network = ToUInt(baseIp) & mask;
            return true;
        }

        public static bool IpInCidr(string ip, string cidr)
        {
            if (!IPAddress.TryParse(ip, out var addr)) return false;
            if (addr.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;
            if (!TryParseCidr(cidr, out var net, out var mask)) return false;

            var val = ToUInt(addr);
            return (val & mask) == net;
        }

        private static uint ToUInt(IPAddress ip)
        {
            var b = ip.GetAddressBytes(); // IPv4 assumed
            if (BitConverter.IsLittleEndian) Array.Reverse(b);
            return BitConverter.ToUInt32(b, 0);
        }
    }
}

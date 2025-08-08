using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PortKnocker
{
    public class PortKnockerService
    {
        /// <summary>Delay between each knock step (milliseconds).</summary>
        public int StepDelayMs { get; set; } = 200;

        /// <summary>Timeout for a TCP connect attempt (milliseconds).</summary>
        public int TcpTimeoutMs { get; set; } = 400;

        public async Task KnockAsync(string ip, List<KnockStep> steps, Action<string>? log = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(ip))
                throw new ArgumentException("IP is required", nameof(ip));
            if (steps == null || steps.Count == 0)
                throw new ArgumentException("At least one step is required", nameof(steps));

            // Resolve host/IP with null-safety and prefer IPv4
            IPAddress ipAddr;
            var host = ip.Trim();

            if (!IPAddress.TryParse(host, out ipAddr))
            {
                var entries = await Dns.GetHostAddressesAsync(host).ConfigureAwait(false);
                if (entries == null || entries.Length == 0)
                    throw new Exception($"Could not resolve host '{host}'.");

                IPAddress? chosen = null;
                foreach (var a in entries)
                {
                    if (a.AddressFamily == AddressFamily.InterNetwork) { chosen = a; break; }
                }
                ipAddr = chosen ?? entries[0];
            }

            foreach (var step in steps)
            {
                if (ct.IsCancellationRequested) break;
                if (step == null || step.Port <= 0) continue;

                try
                {
                    if (step.Protocol == KnockProtocol.UDP)
                    {
                        await SendUdpAsync(ipAddr, step.Port, log, ct).ConfigureAwait(false);
                    }
                    else
                    {
                        await SendTcpSynAsync(ipAddr, step.Port, log, ct).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    log?.Invoke($"Step {step.Protocol}:{step.Port} -> {ex.Message}");
                }

                if (StepDelayMs > 0)
                    await Task.Delay(StepDelayMs, ct).ConfigureAwait(false);
            }

            log?.Invoke("Knock sequence completed.");
        }

        private static async Task SendUdpAsync(IPAddress ip, int port, Action<string>? log, CancellationToken ct)
        {
            using var udp = new UdpClient();
            udp.Client.SendTimeout = 300;
            udp.Client.ReceiveTimeout = 300;

            var endpoint = new IPEndPoint(ip, port);
            var payload = Array.Empty<byte>(); // empty datagram is fine for knocking

            // UdpClient doesn't accept a CancellationToken on SendAsync; rely on short timeouts + disposal
            await udp.SendAsync(payload, payload.Length, endpoint).ConfigureAwait(false);
            log?.Invoke($"UDP -> {endpoint}");
        }

        private async Task SendTcpSynAsync(IPAddress ip, int port, Action<string>? log, CancellationToken ct)
        {
            using var tcp = new TcpClient();

            // Start connect and a timeout race
            var connectTask = tcp.ConnectAsync(ip, port);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var delayTask = Task.Delay(TcpTimeoutMs, timeoutCts.Token);

            var completed = await Task.WhenAny(connectTask, delayTask).ConfigureAwait(false);
            if (completed == connectTask)
            {
                // Connected; immediately close. We only needed to send SYN / attempt.
                log?.Invoke($"TCP CONNECTED -> {ip}:{port}");
            }
            else
            {
                log?.Invoke($"TCP SYN sent (timeout) -> {ip}:{port}");
            }
            // Disposing TcpClient will close the socket (RST if connected), which is fine for knocking
        }
    }
}

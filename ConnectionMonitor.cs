using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace PortKnocker
{
    public class ConnectionMonitor : IDisposable
    {
        private readonly TimeSpan _interval = TimeSpan.FromSeconds(1);
        private CancellationTokenSource? _cts;
        private Task? _task;
        private readonly object _lock = new();

        private readonly HashSet<string> _seen = new(); // key: remoteIP:remotePort:state

        public bool Enabled { get; private set; } = false;

        public event Action<string, int>? NewOutboundConnection; // remoteIp, remotePort

        public List<int> WatchPorts { get; } = new() { 8291 }; // default

        public void Start()
        {
            if (Enabled) return;
            Enabled = true;
            _cts = new CancellationTokenSource();
            _task = Task.Run(() => LoopAsync(_cts.Token));
        }

        public void Stop()
        {
            Enabled = false;
            _cts?.Cancel();
        }

        private async Task LoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    ScanOnce();
                }
                catch { /* best effort */ }

                await Task.Delay(_interval, ct);
            }
        }

        private void ScanOnce()
        {
            var props = IPGlobalProperties.GetIPGlobalProperties();
            var conns = props.GetActiveTcpConnections();

            // we track "new" outbound connections to any watched port
            foreach (var c in conns)
            {
                // Ignore non-established transitions for “new” detection, use unique key to find deltas
                var key = $"{c.RemoteEndPoint.Address}:{c.RemoteEndPoint.Port}:{c.State}";
                if (!WatchPorts.Contains(c.RemoteEndPoint.Port)) continue;

                lock (_lock)
                {
                    if (_seen.Add(key))
                    {
                        // First time seeing this connection state; if it’s new and in SynSent/Established, notify.
                        if (c.State == TcpState.SynSent || c.State == TcpState.Established)
                        {
                            NewOutboundConnection?.Invoke(c.RemoteEndPoint.Address.ToString(), c.RemoteEndPoint.Port);
                        }
                    }
                }
            }

            // Trim the set to avoid unbounded growth:
            // rebuild current keys for watched ports only
            var currentKeys = new HashSet<string>(
                conns.Where(c => WatchPorts.Contains(c.RemoteEndPoint.Port))
                     .Select(c => $"{c.RemoteEndPoint.Address}:{c.RemoteEndPoint.Port}:{c.State}"));

            lock (_lock)
            {
                _seen.RemoveWhere(k => !currentKeys.Contains(k));
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace PortKnocker
{
    public partial class MainWindow : Window
    {
        private AppState _state = new();
        private readonly PortKnockerService _knocker = new();
        private readonly ConnectionMonitor _monitor = new();
        private readonly SemaphoreSlim _knockLock = new(1, 1);

        public MainWindow()
        {
            InitializeComponent();

            // Default watch ports & autorun monitor
            TxtWatchPorts.Text = "58291,8291";

            LoadStateAndBind();
            ProfilesList.SelectionChanged += ProfilesList_SelectionChanged;
            _monitor.NewOutboundConnection += Monitor_NewOutboundConnection;

            // Start monitoring immediately
            TryEnableMonitorAtStartup();

            AppendLog("Ready.");
        }

        // --- Optional Mica/Backdrop (works with normal chrome) ---
        const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        const int DWMSBT_MAINWINDOW = 2;
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    int backdrop = DWMSBT_MAINWINDOW;
                    _ = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
                }
            }
            catch { /* ignore if not supported */ }
        }

        // ----------------- State & Binding -----------------
        private void LoadStateAndBind()
        {
            _state = ProfileStore.LoadState();

            ProfilesList.ItemsSource = null;
            ProfilesList.ItemsSource = _state.Profiles.OrderBy(p => p.Name).ToList();

            MapProfile.ItemsSource = _state.Profiles.Select(p => p.Name).OrderBy(s => s).ToList();
            RebuildMappingsList();
        }

        private void SaveState()
        {
            ProfileStore.SaveState(_state);
            MapProfile.ItemsSource = _state.Profiles.Select(p => p.Name).OrderBy(s => s).ToList();
            RebuildMappingsList();
        }

        private void RebuildMappingsList()
        {
            var items = _state.Mappings.Select(m =>
            {
                var ports = (m.Ports == null || m.Ports.Count == 0) ? "any" : string.Join(",", m.Ports);
                return $"{m.Name}  |  {m.Type}  |  {m.Pattern}  |  ports: {ports}  |  profile: {m.ProfileName}";
            }).ToList();

            MappingsList.ItemsSource = null;
            MappingsList.ItemsSource = items;
        }

        // ----------------- UI helpers -----------------
        private KnockProfile CurrentFromUI()
        {
            var steps = new List<KnockStep>
            {
                StepFromUI(Port1, Proto1),
                StepFromUI(Port2, Proto2),
                StepFromUI(Port3, Proto3),
                StepFromUI(Port4, Proto4)
            }.Where(s => s.Port > 0).ToList();

            int delay = ParseIntSafe(TxtDelay.Text, 200);
            int tcpTimeout = ParseIntSafe(TxtTcpTimeout.Text, 400);
            _knocker.StepDelayMs = delay;
            _knocker.TcpTimeoutMs = tcpTimeout;

            return new KnockProfile
            {
                Name = TxtName.Text.Trim(),
                IpAddress = TxtIp.Text.Trim(),
                Steps = steps
            };
        }

        private static KnockStep StepFromUI(TextBox portBox, ComboBox protoBox)
        {
            var port = ParseIntSafe(portBox.Text, 0);
            var proto = (protoBox.SelectedItem as ComboBoxItem)?.Content?.ToString()?.ToUpperInvariant() == "UDP"
                ? KnockProtocol.UDP
                : KnockProtocol.TCP;

            return new KnockStep { Port = port, Protocol = proto };
        }

        private static int ParseIntSafe(string? s, int fallback) => int.TryParse(s, out var v) ? v : fallback;

        private void PopulateUI(KnockProfile p)
        {
            TxtName.Text = p.Name;
            TxtIp.Text = p.IpAddress;

            var steps = p.Steps.Concat(Enumerable.Repeat(new KnockStep(), 4)).Take(4).ToList();
            SetStep(steps[0], Port1, Proto1);
            SetStep(steps[1], Port2, Proto2);
            SetStep(steps[2], Port3, Proto3);
            SetStep(steps[3], Port4, Proto4);
        }

        private static void SetStep(KnockStep s, TextBox portBox, ComboBox protoBox)
        {
            portBox.Text = s.Port > 0 ? s.Port.ToString() : "";
            var isUdp = s.Protocol == KnockProtocol.UDP;
            foreach (ComboBoxItem item in protoBox.Items)
            {
                if ((item.Content?.ToString()?.ToUpperInvariant() == "UDP") == isUdp)
                {
                    protoBox.SelectedItem = item;
                    break;
                }
            }
        }

        // ----------------- Events -----------------
        private void ProfilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProfilesList.SelectedItem is KnockProfile p) PopulateUI(p);
        }

        private async void BtnKnock_Click(object sender, RoutedEventArgs e)
        {
            var profile = CurrentFromUI();
            if (string.IsNullOrWhiteSpace(profile.IpAddress))
            {
                MessageBox.Show("Please enter a valid IP/Host.", "PortKnocker", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (profile.Steps.Count == 0)
            {
                MessageBox.Show("Please add at least one knock step.", "PortKnocker", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await KnockProfileAsync(profile);
        }

        private async Task KnockProfileAsync(KnockProfile profile)
        {
            if (!_knockLock.Wait(0))
            {
                AppendLog("Knock already in progress.");
                return;
            }

            try
            {
                var seq = string.Join(" , ", profile.Steps.Select(s => $"{s.Protocol}:{s.Port}"));
                AppendLog($"Knocking {profile.IpAddress} ({seq})");
                await _knocker.KnockAsync(profile.IpAddress, profile.Steps, AppendLog);
            }
            catch (Exception ex)
            {
                Logger.File($"[KnockProfileAsync] {ex}");
                AppendLog($"Error: {ex.Message}");
            }
            finally
            {
                _knockLock.Release();
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var p = CurrentFromUI();
            if (string.IsNullOrWhiteSpace(p.Name))
            {
                MessageBox.Show("Enter a Name for this destination.", "PortKnocker", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(p.IpAddress))
            {
                MessageBox.Show("Enter IP/Host for this destination.", "PortKnocker", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (p.Steps.Count == 0)
            {
                MessageBox.Show("Enter at least one knock step.", "PortKnocker", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var existing = _state.Profiles.FirstOrDefault(x => string.Equals(x.Name, p.Name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.IpAddress = p.IpAddress;
                existing.Steps = p.Steps;
            }
            else
            {
                _state.Profiles.Add(p);
            }

            // Auto-create/update mapping for this destination
            var defaultPorts = new List<int> { 58291 };
            var map = _state.Mappings.FirstOrDefault(m => string.Equals(m.Name, p.Name, StringComparison.OrdinalIgnoreCase));
            if (map == null)
            {
                _state.Mappings.Add(new AutoKnockMapping
                {
                    Name = p.Name,
                    Type = MappingType.ExactIp,
                    Pattern = p.IpAddress,
                    Ports = defaultPorts,
                    ProfileName = p.Name
                });
                AppendLog($"Auto-mapping created for \"{p.Name}\" (ExactIp {p.IpAddress}, ports: 58291).");
            }
            else
            {
                map.Type = MappingType.ExactIp;
                map.Pattern = p.IpAddress;
                map.Ports = (map.Ports == null || map.Ports.Count == 0) ? defaultPorts : map.Ports;
                map.ProfileName = p.Name;
                AppendLog($"Auto-mapping updated for \"{p.Name}\".");
            }

            SaveState();
            LoadStateAndBind();
            AppendLog($"Saved \"{p.Name}\".");
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (ProfilesList.SelectedItem is not KnockProfile p)
            {
                MessageBox.Show("Select a saved destination to delete.", "PortKnocker", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            _state.Profiles.RemoveAll(x => x.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase));
            SaveState();
            LoadStateAndBind();
            AppendLog($"Deleted \"{p.Name}\".");
        }

        private void BtnToggleMonitor_Click(object sender, RoutedEventArgs e)
        {
            if (_monitor.Enabled)
            {
                _monitor.Stop();
                BtnToggleMonitor.Content = "Enable";
                AppendLog("Auto-knock monitor disabled.");
                return;
            }

            EnableMonitorFromTextbox();
        }

        private void TryEnableMonitorAtStartup()
        {
            try
            {
                EnableMonitorFromTextbox();
            }
            catch (Exception ex)
            {
                Logger.File($"[EnableMonitorAtStartup] {ex}");
            }
        }

        private void EnableMonitorFromTextbox()
        {
            // watch ports = user CSV ∪ all mapping ports
            var userPorts = TxtWatchPorts.Text
                .Split(',', ';', ' ', '\t')
                .Select(s => s.Trim())
                .Where(s => int.TryParse(s, out _))
                .Select(int.Parse)
                .Where(p => p > 0 && p <= 65535);

            var mappingPorts = new List<int>();
            foreach (var m in _state.Mappings)
            {
                if (m.Ports != null && m.Ports.Count > 0)
                    mappingPorts.AddRange(m.Ports);
            }

            var ports = userPorts.Concat(mappingPorts).Distinct().ToList();
            if (ports.Count == 0) ports.Add(8291);

            _monitor.WatchPorts.Clear();
            _monitor.WatchPorts.AddRange(ports);
            _monitor.Start();
            BtnToggleMonitor.Content = "Disable";
            AppendLog($"Auto-knock monitor enabled for ports: {string.Join(", ", ports)}");
        }

        private void BtnOpenLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Logger.File("[OpenLogs] --------");
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = Logger.LogPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppendLog($"Could not open log: {ex.Message}");
                Logger.File($"[OpenLogError] {ex}");
            }
        }

		private async void Monitor_NewOutboundConnection(string remoteIp, int remotePort)
		{
			try
			{
				var matched = TryResolveProfileFor(remoteIp, remotePort);
				if (matched != null)
				{
					AppendLog($"Detected outbound to {remoteIp}:{remotePort}. Auto-knocking using profile \"{matched.Name}\"...");
					var prof = new KnockProfile { IpAddress = remoteIp, Steps = matched.Steps };
					await Dispatcher.InvokeAsync(async () => await KnockProfileAsync(prof));
					return;
				}

				// Fallback: read UI on the UI thread
				var current = await Dispatcher.InvokeAsync(() => CurrentFromUI());
				if (current.Steps.Count == 0) return;

				AppendLog($"Detected outbound to {remoteIp}:{remotePort}. No mapping matched; auto-knocking using current UI sequence...");
				var autoProfile = new KnockProfile { IpAddress = remoteIp, Steps = current.Steps };
				await Dispatcher.InvokeAsync(async () => await KnockProfileAsync(autoProfile));
			}
			catch (Exception ex)
			{
				Logger.File($"[Monitor_NewOutboundConnection] {ex}");
				// Don’t rethrow — swallow so the background thread doesn’t crash the app
			}
		}

        private KnockProfile? TryResolveProfileFor(string remoteIp, int remotePort)
        {
            foreach (var type in new[] { MappingType.ExactIp, MappingType.Cidr, MappingType.AnyIp })
            {
                var candidates = _state.Mappings.Where(m => m.Type == type);
                foreach (var m in candidates)
                {
                    if (m.Ports != null && m.Ports.Count > 0 && !m.Ports.Contains(remotePort)) continue;

                    bool ipMatch = type switch
                    {
                        MappingType.AnyIp => true,
                        MappingType.ExactIp => HostOrIpMatches(remoteIp, m.Pattern),
                        MappingType.Cidr => !string.IsNullOrWhiteSpace(m.Pattern) && IpUtils.IpInCidr(remoteIp, m.Pattern.Trim()),
                        _ => false
                    };

                    if (!ipMatch) continue;

                    var profile = _state.Profiles.FirstOrDefault(p =>
                        string.Equals(p.Name, m.ProfileName, StringComparison.OrdinalIgnoreCase));

                    if (profile != null) return profile;
                }
            }
            return null;
        }

        private static bool HostOrIpMatches(string actualIp, string? pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return false;

            var pat = pattern.Trim();
            if (IPAddress.TryParse(pat, out _))
                return string.Equals(actualIp, pat, StringComparison.OrdinalIgnoreCase);

            try
            {
                var addrs = Dns.GetHostAddresses(pat);
                foreach (var a in addrs)
                {
                    if (a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                        string.Equals(a.ToString(), actualIp, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch (Exception ex)
            {
                Logger.File($"[HostOrIpMatches] DNS error for '{pat}': {ex}");
            }
            return false;
        }

        private void BtnMapAddUpdate_Click(object sender, RoutedEventArgs e)
        {
            var name = MapName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Enter a rule name.", "PortKnocker", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var typeStr = (MapType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "AnyIp";
            if (!Enum.TryParse<MappingType>(typeStr, out var type)) type = MappingType.AnyIp;

            var pattern = MapPattern.Text.Trim();
            if (type == MappingType.ExactIp && string.IsNullOrEmpty(pattern))
            {
                MessageBox.Show("ExactIp mapping requires a Pattern (IP or Hostname).", "PortKnocker", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (type == MappingType.Cidr && string.IsNullOrEmpty(pattern))
            {
                MessageBox.Show("CIDR mapping requires a Pattern like 192.168.1.0/24.", "PortKnocker", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var profileName = MapProfile.SelectedItem?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(profileName))
            {
                MessageBox.Show("Select a profile to use for this mapping.", "PortKnocker", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var ports = MapPorts.Text.Split(',', ';', ' ', '\t')
                .Select(s => s.Trim()).Where(s => int.TryParse(s, out _)).Select(int.Parse)
                .Where(p => p > 0 && p <= 65535).Distinct().ToList();

            var existing = _state.Mappings.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Type = type;
                existing.Pattern = pattern;
                existing.Ports = ports;
                existing.ProfileName = profileName;
            }
            else
            {
                _state.Mappings.Add(new AutoKnockMapping
                {
                    Name = name,
                    Type = type,
                    Pattern = pattern,
                    Ports = ports,
                    ProfileName = profileName
                });
            }

            SaveState();
            AppendLog($"Mapping \"{name}\" saved.");
            RebuildMappingsList();
        }

        private void BtnMapDelete_Click(object sender, RoutedEventArgs e)
        {
            var name = MapName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Select or type the rule name to delete.", "PortKnocker", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            _state.Mappings.RemoveAll(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
            SaveState();
            MapName.Text = ""; MapPattern.Text = ""; MapPorts.Text = "";
            AppendLog($"Mapping \"{name}\" deleted.");
            RebuildMappingsList();
        }

        private void MappingsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var idx = MappingsList.SelectedIndex;
            if (idx < 0 || idx >= _state.Mappings.Count) return;
            var m = _state.Mappings[idx];

            MapName.Text = m.Name;
            MapPattern.Text = m.Pattern ?? "";
            MapPorts.Text = (m.Ports == null || m.Ports.Count == 0) ? "" : string.Join(",", m.Ports);

            foreach (ComboBoxItem item in MapType.Items)
            {
                if (string.Equals(item.Content?.ToString(), m.Type.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    MapType.SelectedItem = item;
                    break;
                }
            }

            MapProfile.SelectedItem = _state.Profiles.FirstOrDefault(p => p.Name.Equals(m.ProfileName, StringComparison.OrdinalIgnoreCase))?.Name;
        }

        private void AppendLog(string line)
        {
            var msg = $"[{DateTime.Now:HH:mm:ss}] {line}";
            Logger.File(msg);
            Dispatcher.Invoke(() =>
            {
                TxtLog.AppendText(msg + Environment.NewLine);
                TxtLog.ScrollToEnd();
            });
        }
    }
}

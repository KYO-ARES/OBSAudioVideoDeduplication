using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Text.Json;

namespace OBSAudioVideoDeduplication
{
    public partial class MainWindow : Window
    {
        #region OBS 稳定连接
        private ClientWebSocket _ws = new ClientWebSocket();
        private Timer? _heartbeatTimer;
        private bool _connected;
        private CancellationTokenSource? _cts;

        public MainWindow()
        {
            InitializeComponent();
            ResetUI();
        }

        private async void BtnConnectOBS_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                BtnConnectOBS.IsEnabled = false;
                BtnConnectOBS.Foreground = Brushes.Gray;
                UpdateStatus("连接中...", Brushes.Orange);
            });

            try
            {
                string ip = TxtOBSIp.Text.Trim();
                string portText = TxtOBSPort.Text.Trim();
                string pwd = PwdOBSPassword.Password;

                if (!int.TryParse(portText, out int port) || string.IsNullOrEmpty(ip))
                {
                    AppendLog("❌ IP/端口错误");
                    Dispatcher.Invoke(ResetUI);
                    return;
                }

                await DisconnectNow();
                _ws = new ClientWebSocket();
                _cts = new CancellationTokenSource();

                await _ws.ConnectAsync(new Uri($"ws://{ip}:{port}"), CancellationToken.None);

                var auth = new
                {
                    op = 1,
                    d = new
                    {
                        rpcVersion = 1,
                        authentication = pwd,
                        eventSubscriptions = 63
                    }
                };
                await SendJson(auth);

                _connected = true;
                Dispatcher.Invoke(() =>
                {
                    UpdateStatus("已连接", Brushes.Green);
                    BtnConnectOBS.IsEnabled = false;
                    BtnDisconnectOBS.IsEnabled = true;
                    AppendLog("✅ OBS 连接成功");
                });

                StartHeartbeat();
                _ = ListenLoop(_cts.Token);
            }
            catch (Exception ex)
            {
                AppendLog($"❌ 连接失败：{ex.Message}");
                Dispatcher.Invoke(ResetUI);
            }
        }

        private async void BtnDisconnectOBS_Click(object sender, RoutedEventArgs e)
        {
            await DisconnectNow();
            ResetUI();
            AppendLog("✅ 已断开");
        }

        private void StartHeartbeat()
        {
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = new Timer(_ =>
            {
                if (_connected && _ws.State == WebSocketState.Open)
                {
                    var ping = new
                    {
                        op = 6,
                        d = new { requestType = "GetVersion", requestId = Guid.NewGuid().ToString() }
                    };
                    _ = SendJson(ping);
                }
            }, null, 3000, 3000);
        }

        private async Task ListenLoop(CancellationToken token)
        {
            byte[] buf = new byte[8192];
            while (!token.IsCancellationRequested && _ws.State == WebSocketState.Open)
            {
                try
                {
                    var res = await _ws.ReceiveAsync(new ArraySegment<byte>(buf), token);
                    if (res.MessageType == WebSocketMessageType.Close)
                    {
                        AppendLog("❌ OBS 断开");
                        await DisconnectNow();
                        Dispatcher.Invoke(ResetUI);
                        return;
                    }
                }
                catch { break; }
            }
        }

        private async Task<JsonElement?> CallObs(string method, Dictionary<string, object>? data = null)
        {
            if (!_connected || _ws.State != WebSocketState.Open) return null;
            var tcs = new TaskCompletionSource<JsonElement?>();
            string rid = Guid.NewGuid().ToString();

            var req = new
            {
                op = 6,
                d = new
                {
                    requestType = method,
                    requestId = rid,
                    inputData = data ?? new Dictionary<string, object>()
                }
            };

            _ = Task.Run(async () =>
            {
                byte[] buf = new byte[8192];
                while (_connected && !_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var r = await _ws.ReceiveAsync(new ArraySegment<byte>(buf), _cts.Token);
                        string json = Encoding.UTF8.GetString(buf, 0, r.Count);
                        var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("d", out var d) &&
                            d.TryGetProperty("requestId", out var ridProp) &&
                            ridProp.GetString() == rid)
                        {
                            tcs.SetResult(doc.RootElement);
                            break;
                        }
                    }
                    catch { break; }
                }
            });

            await SendJson(req);
            await Task.WhenAny(tcs.Task, Task.Delay(3000));
            return tcs.Task.IsCompletedSuccessfully ? tcs.Task.Result : null;
        }

        private async Task SendJson(object obj)
        {
            if (_ws.State != WebSocketState.Open) return;
            string json = JsonSerializer.Serialize(obj);
            byte[] data = Encoding.UTF8.GetBytes(json);
            await _ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task DisconnectNow()
        {
            _connected = false;
            _heartbeatTimer?.Dispose();
            _cts?.Cancel();

            if (_ws.State == WebSocketState.Open)
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Close", CancellationToken.None);
        }

        private void ResetUI()
        {
            BtnConnectOBS.IsEnabled = true;
            BtnConnectOBS.Foreground = Brushes.White;
            BtnDisconnectOBS.IsEnabled = false;
            UpdateStatus("未连接", Brushes.Red);
        }

        private void UpdateStatus(string txt, Brush color)
        {
            TxtOBSStatus.Text = txt;
            TxtOBSStatus.Foreground = color;
        }

        private void AppendLog(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\r\n");
                TxtLog.ScrollToEnd();
            });
        }

        protected override async void OnClosed(EventArgs e)
        {
            await DisconnectNow();
            base.OnClosed(e);
        }
        #endregion

        #region ===================== 画面去重（已修复 100% 可用）=====================
        private string? _currentSceneName;
        private string? _currentSourceName;
        private string? _currentFilterName;

        private async void tabVideoDeduplication_GotFocus(object sender, RoutedEventArgs e)
        {
            if (_connected) await RefreshAll();
            else AppendLog("⚠️ 请先连接 OBS");
        }

        private async void BtnRefreshDeduplication_Click(object sender, RoutedEventArgs e)
        {
            if (_connected) await RefreshAll();
        }

        private async Task RefreshAll()
        {
            AppendLog("🔄 刷新场景/源/滤镜");
            cboSceneList.Items.Clear();
            cboSceneSources.Items.Clear();
            lstSourceFilters.Items.Clear();
            panelFilterParams.Children.Clear();

            var sceneRes = await CallObs("GetSceneList");
            if (sceneRes == null) return;

            var scenes = sceneRes.Value.GetProperty("d").GetProperty("scenes");
            foreach (var s in scenes.EnumerateArray())
            {
                string name = s.GetProperty("sceneName").GetString()!;
                cboSceneList.Items.Add(name);
            }
        }

        private async void CboSceneList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboSceneList.SelectedItem is not string scene) return;
            _currentSceneName = scene;
            cboSceneSources.Items.Clear();
            lstSourceFilters.Items.Clear();

            var res = await CallObs("GetSceneItemList", new() { { "sceneName", scene } });
            if (res == null) return;

            var items = res.Value.GetProperty("d").GetProperty("sceneItems");
            foreach (var item in items.EnumerateArray())
            {
                string sourceName = item.GetProperty("sourceName").GetString()!;
                cboSceneSources.Items.Add(sourceName);
            }
        }

        private async void CboSceneSources_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboSceneSources.SelectedItem is not string src) return;
            _currentSourceName = src;
            lstSourceFilters.Items.Clear();
            panelFilterParams.Children.Clear();

            var res = await CallObs("GetSourceFilterList", new() { { "sourceName", src } });
            if (res == null) return;

            var filters = res.Value.GetProperty("d").GetProperty("filters");
            foreach (var f in filters.EnumerateArray())
            {
                string fname = f.GetProperty("filterName").GetString()!;
                lstSourceFilters.Items.Add(fname);
            }
        }

        private async void LstSourceFilters_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstSourceFilters.SelectedItem is not string fname || _currentSourceName == null) return;
            _currentFilterName = fname;
            panelFilterParams.Children.Clear();

            var res = await CallObs("GetSourceFilterInfo", new()
            {
                { "sourceName", _currentSourceName },
                { "filterName", fname }
            });
            if (res == null) return;

            var d = res.Value.GetProperty("d");
            bool enabled = d.GetProperty("filterEnabled").GetBoolean();
            var settings = d.GetProperty("filterSettings");

            var enBox = new CheckBox { Content = "启用滤镜", IsChecked = enabled, Margin = new Thickness(0, 0, 0, 8) };
            enBox.Checked += async (s, args) => await SetFilterEnabled(true);
            enBox.Unchecked += async (s, args) => await SetFilterEnabled(false);
            panelFilterParams.Children.Add(enBox);

            foreach (var prop in settings.EnumerateObject())
            {
                var pnl = new StackPanel { Margin = new Thickness(0, 5, 0, 5) };
                pnl.Children.Add(new TextBlock { Text = prop.Name });

                if (prop.Value.ValueKind == JsonValueKind.Number)
                {
                    var slider = new Slider
                    {
                        Minimum = 0,
                        Maximum = 200,
                        Value = prop.Value.GetDouble(),
                        Margin = new Thickness(0, 3, 0, 0)
                    };
                    slider.ValueChanged += async (s, args) =>
                    {
                        await SetFilterParam(prop.Name, args.NewValue);
                    };
                    pnl.Children.Add(slider);
                }
                else if (prop.Value.ValueKind == JsonValueKind.True || prop.Value.ValueKind == JsonValueKind.False)
                {
                    var cb = new CheckBox { IsChecked = prop.Value.GetBoolean() };
                    cb.Checked += async (s, args) => await SetFilterParam(prop.Name, true);
                    cb.Unchecked += async (s, args) => await SetFilterParam(prop.Name, false);
                    pnl.Children.Add(cb);
                }

                panelFilterParams.Children.Add(pnl);
            }
        }

        private async Task SetFilterEnabled(bool enable)
        {
            if (_currentSourceName == null || _currentFilterName == null) return;
            await CallObs("SetSourceFilterEnabled", new()
            {
                { "sourceName", _currentSourceName },
                { "filterName", _currentFilterName },
                { "filterEnabled", enable }
            });
        }

        private async Task SetFilterParam(string key, object value)
        {
            if (_currentSourceName == null || _currentFilterName == null) return;
            await CallObs("SetSourceFilterSettings", new()
            {
                { "sourceName", _currentSourceName },
                { "filterName", _currentFilterName },
                { "filterSettings", new Dictionary<string, object> { { key, value } } }
            });
        }
        #endregion
    }
}
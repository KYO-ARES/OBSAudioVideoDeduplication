using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Text.Json;

namespace OBSAudioVideoDeduplication
{
    public partial class MainWindow : Window
    {
        private ClientWebSocket _ws = new ClientWebSocket();
        private Timer? _heartbeatTimer;
        private bool _connected;
        private CancellationTokenSource? _listenCts;

        public MainWindow()
        {
            InitializeComponent();
            ResetUI();
        }

        // 连接 OBS
        private async void BtnConnectOBS_Click(object sender, RoutedEventArgs e)
        {
            // 立即锁按钮
            Dispatcher.Invoke(() =>
            {
                BtnConnectOBS.IsEnabled = false;
                BtnConnectOBS.Foreground = Brushes.Gray;
                UpdateStatus("连接中...", Brushes.Orange);
                AppendLog("▶ 开始连接 OBS...");
            });

            try
            {
                string ip = TxtOBSIp.Text.Trim();
                string portStr = TxtOBSPort.Text.Trim();
                string pwd = PwdOBSPassword.Password;

                if (!int.TryParse(portStr, out int port) || string.IsNullOrEmpty(ip))
                {
                    AppendLog("❌ IP / 端口无效");
                    Dispatcher.Invoke(ResetUI);
                    return;
                }

                // 关闭旧连接
                await DisconnectInternal();

                _ws = new ClientWebSocket();
                _listenCts = new CancellationTokenSource();

                // 连接
                await _ws.ConnectAsync(new Uri($"ws://{ip}:{port}"), CancellationToken.None);

                // OBS V5 认证
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

                // 连接成功
                _connected = true;
                Dispatcher.Invoke(() =>
                {
                    UpdateStatus("已连接", Brushes.Green);
                    BtnConnectOBS.IsEnabled = false;
                    BtnDisconnectOBS.IsEnabled = true;
                    AppendLog("✅ OBS 连接成功！");
                });

                // 启动心跳 + 监听消息
                StartHeartbeat();
                _ = ListenLoop(_listenCts.Token);
            }
            catch (Exception ex)
            {
                AppendLog($"❌ 连接失败：{ex.Message}");
                Dispatcher.Invoke(ResetUI);
            }
        }

        // 断开 OBS
        private async void BtnDisconnectOBS_Click(object sender, RoutedEventArgs e)
        {
            await DisconnectInternal();
            Dispatcher.Invoke(ResetUI);
            AppendLog("✅ 已手动断开");
        }

        // 心跳保活（3秒一次，彻底解决OBS 5秒断开）
        private void StartHeartbeat()
        {
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = new Timer(_ =>
            {
                if (_connected && _ws.State == WebSocketState.Open)
                {
                    var msg = new
                    {
                        op = 6,
                        d = new { requestType = "GetVersion", requestId = Guid.NewGuid().ToString() }
                    };
                    _ = SendJson(msg);
                }
            }, null, 1000, 3000);
        }

        // 持续监听（必须，否则OBS强制断开）
        private async Task ListenLoop(CancellationToken token)
        {
            byte[] buffer = new byte[8192];
            try
            {
                while (_ws.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    var res = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                    if (res.MessageType == WebSocketMessageType.Close)
                    {
                        AppendLog("❌ OBS 服务器断开了连接");
                        await DisconnectInternal();
                        Dispatcher.Invoke(ResetUI);
                        return;
                    }
                }
            }
            catch
            {
                await DisconnectInternal();
                Dispatcher.Invoke(ResetUI);
            }
        }

        // 发送JSON消息
        private async Task SendJson(object obj)
        {
            if (_ws.State != WebSocketState.Open) return;
            string json = JsonSerializer.Serialize(obj);
            byte[] data = Encoding.UTF8.GetBytes(json);
            await _ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        // 内部断开逻辑
        private async Task DisconnectInternal()
        {
            _connected = false;
            _heartbeatTimer?.Dispose();
            _listenCts?.Cancel();

            if (_ws.State == WebSocketState.Open)
            {
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "ClosedByClient", CancellationToken.None);
            }
        }

        // 重置按钮和状态
        private void ResetUI()
        {
            BtnConnectOBS.IsEnabled = true;
            BtnConnectOBS.Foreground = Brushes.White;
            BtnDisconnectOBS.IsEnabled = false;
            UpdateStatus("未连接", Brushes.Red);
        }

        // 更新状态栏
        private void UpdateStatus(string txt, Brush color)
        {
            TxtOBSStatus.Text = txt;
            TxtOBSStatus.Foreground = color;
        }

        // 日志输出
        private void AppendLog(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\r\n");
                TxtLog.ScrollToEnd();
            });
        }

        // 窗口关闭时清理
        protected override async void OnClosed(EventArgs e)
        {
            await DisconnectInternal();
            base.OnClosed(e);
        }
    }
}
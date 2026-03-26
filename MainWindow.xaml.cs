// 路径：OBSAudioVideoDeduplication/MainWindow.xaml.cs
using OBSAudioVideoDeduplication.Helpers;
using OBSAudioVideoDeduplication.Models;
using Serilog;
using System;
using System.Windows;
using System.Windows.Media;

namespace OBSAudioVideoDeduplication
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ILogger _logger;
        private readonly AppConfig _appConfig;

        public MainWindow()
        {
            InitializeComponent();
            // 初始化日志和配置
            _appConfig = ConfigHelper.AppConfig;
            _logger = LogHelper.GetLogger("MainWindow");

            // 加载配置到UI
            LoadConfigToUI();

            // 绑定按钮事件
            BtnConnectOBS.Click += BtnConnectOBS_Click;
            BtnDisconnectOBS.Click += BtnDisconnectOBS_Click;
            BtnInstallVST.Click += BtnInstallVST_Click;
            BtnInstallVirtualAudio.Click += BtnInstallVirtualAudio_Click;
            BtnInstallTemplate.Click += BtnInstallTemplate_Click;

            // 实时日志输出到文本框
            var logTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            logTimer.Tick += (s, e) =>
            {
                try
                {
                    // 读取最新日志（取最后20行）
                    var logFilePath = System.IO.Path.Combine(_appConfig.LogFilePath, $"obs-deduplication-{DateTime.Now:yyyyMMdd}.log");
                    if (System.IO.File.Exists(logFilePath))
                    {
                        var lines = System.IO.File.ReadAllLines(logFilePath);
                        var lastLines = lines.Length > 20 ? lines[^20..] : lines;
                        TxtLog.Text = string.Join(Environment.NewLine, lastLines);
                        // 滚动到最后一行
                        TxtLog.ScrollToEnd();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "更新日志面板失败");
                }
            };
            logTimer.Start();

            _logger.Information("主窗口初始化完成");
        }

        /// <summary>
        /// 加载配置到UI控件
        /// </summary>
        private void LoadConfigToUI()
        {
            try
            {
                TxtOBSIp.Text = _appConfig.OBSWebSocketIp;
                TxtOBSPort.Text = _appConfig.OBSWebSocketPort.ToString();
                _logger.Information("配置已加载到UI");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "加载配置到UI失败");
                MessageBox.Show("加载配置失败，使用默认值", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 保存UI配置到配置文件
        /// </summary>
        private void SaveConfigFromUI()
        {
            try
            {
                _appConfig.OBSWebSocketIp = TxtOBSIp.Text;
                if (int.TryParse(TxtOBSPort.Text, out var port))
                {
                    _appConfig.OBSWebSocketPort = port;
                }
                ConfigHelper.SaveConfig(_appConfig);
                _logger.Information("UI配置已保存");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "保存UI配置失败");
                MessageBox.Show("保存配置失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region 按钮事件
        /// <summary>
        /// 连接OBS按钮（阶段2实现逻辑，当前仅占位）
        /// </summary>
        private void BtnConnectOBS_Click(object sender, RoutedEventArgs e)
        {
            _logger.Information("点击连接OBS按钮（阶段2实现），IP：{Ip}，端口：{Port}", TxtOBSIp.Text, TxtOBSPort.Text);
            TxtOBSStatus.Text = "连接中...";
            TxtOBSStatus.Foreground = Brushes.Orange;

            // 模拟连接
            System.Threading.Tasks.Task.Delay(1000).ContinueWith(t =>
            {
                Dispatcher.Invoke(() =>
                {
                    TxtOBSStatus.Text = "未实现（阶段2开发）";
                    TxtOBSStatus.Foreground = Brushes.Yellow;
                    BtnConnectOBS.IsEnabled = false;
                    BtnDisconnectOBS.IsEnabled = true;
                });
            });
        }

        /// <summary>
        /// 断开OBS按钮（阶段2实现逻辑，当前仅占位）
        /// </summary>
        private void BtnDisconnectOBS_Click(object sender, RoutedEventArgs e)
        {
            _logger.Information("点击断开OBS按钮（阶段2实现）");
            TxtOBSStatus.Text = "未连接";
            TxtOBSStatus.Foreground = Brushes.Red;
            BtnConnectOBS.IsEnabled = true;
            BtnDisconnectOBS.IsEnabled = false;
        }

        /// <summary>
        /// 安装VST插件按钮（阶段3实现逻辑，当前仅占位）
        /// </summary>
        private void BtnInstallVST_Click(object sender, RoutedEventArgs e)
        {
            _logger.Information("点击安装VST插件按钮（阶段3实现）");
            MessageBox.Show("VST插件安装功能将在阶段3开发", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 安装虚拟声卡按钮（阶段3实现逻辑，当前仅占位）
        /// </summary>
        private void BtnInstallVirtualAudio_Click(object sender, RoutedEventArgs e)
        {
            _logger.Information("点击安装虚拟声卡按钮（阶段3实现）");
            MessageBox.Show("虚拟声卡安装功能将在阶段3开发", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 安装专业模板插件按钮（阶段3实现逻辑，当前仅占位）
        /// </summary>
        private void BtnInstallTemplate_Click(object sender, RoutedEventArgs e)
        {
            _logger.Information("点击安装专业模板插件按钮（阶段3实现）");
            MessageBox.Show("专业模板插件安装功能将在阶段3开发", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        #endregion

        /// <summary>
        /// 窗口关闭时保存配置
        /// </summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            SaveConfigFromUI();
            _logger.Information("主窗口关闭，配置已保存");
            base.OnClosing(e);
        }
    }
}
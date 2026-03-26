// 路径：OBSAudioVideoDeduplication/App.xaml.cs
using OBSAudioVideoDeduplication.Helpers;
using OBSAudioVideoDeduplication.Models;
using Serilog;
using System;
using System.Windows;

namespace OBSAudioVideoDeduplication
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private readonly ILogger _logger;

        public App()
        {
            // 初始化配置和日志
            var config = ConfigHelper.AppConfig;
            LogHelper.Initialize(config.LogFilePath);
            _logger = LogHelper.GetLogger("App");

            // 全局异常捕获
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        /// <summary>
        /// UI线程未处理异常
        /// </summary>
        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            _logger.Error(e.Exception, "UI线程未处理异常");
            MessageBox.Show($"程序发生UI异常：{e.Exception.Message}\n详情请查看日志", "异常提示", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        /// <summary>
        /// 非UI线程未处理异常
        /// </summary>
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            _logger.Error(ex, "非UI线程未处理异常（程序即将退出）");
            // ✅ 修复：将MessageBoxImage.Critical改为MessageBoxImage.Error
            MessageBox.Show($"程序发生严重异常：{ex?.Message}\n程序即将退出，详情请查看日志", "严重异常", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>
        /// 程序退出时清理资源
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
            // 保存配置
            ConfigHelper.SaveConfig(ConfigHelper.AppConfig);
            // 关闭日志
            LogHelper.Close();
            _logger.Information("程序正常退出，退出码：{ExitCode}", e.ApplicationExitCode);
            base.OnExit(e);
        }
    }
}
// 路径：OBSAudioVideoDeduplication/Helpers/LogHelper.cs
using Serilog;
using Serilog.Events;
using System;
using System.IO;

namespace OBSAudioVideoDeduplication.Helpers
{
    /// <summary>
    /// 日志辅助类（单例模式）
    /// </summary>
    public static class LogHelper
    {
        private static bool _isInitialized = false;

        /// <summary>
        /// 初始化日志配置（日志输出到项目目录Logs文件夹）
        /// </summary>
        /// <param name="logPath">日志保存路径</param>
        public static void Initialize(string logPath)
        {
            if (_isInitialized) return;

            // 创建日志目录（项目目录下的Logs）
            if (!Directory.Exists(logPath))
            {
                Directory.CreateDirectory(logPath);
                GetLogger("LogHelper").Information("创建日志目录：{LogPath}", logPath);
            }

            // 配置Serilog：文件+控制台输出，按天滚动，保留30天
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    path: Path.Combine(logPath, "obs-deduplication-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            _isInitialized = true;
            Log.Information("日志系统初始化完成，日志路径：{LogPath}", logPath);
        }

        /// <summary>
        /// 获取分类日志器（按模块分类）
        /// </summary>
        /// <param name="categoryName">模块名称</param>
        /// <returns>日志器</returns>
        public static ILogger GetLogger(string categoryName)
        {
            if (!_isInitialized)
            {
                // 未初始化时使用默认路径（项目目录Logs）
                Initialize(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs"));
            }
            return Log.Logger.ForContext("SourceContext", categoryName);
        }

        /// <summary>
        /// 关闭日志（程序退出时调用）
        /// </summary>
        public static void Close()
        {
            Log.CloseAndFlush();
            _isInitialized = false;
        }
    }
}
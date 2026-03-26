// 路径：OBSAudioVideoDeduplication/Models/AppConfig.cs
using System;
using System.IO;

namespace OBSAudioVideoDeduplication.Models
{
    /// <summary>
    /// 应用全局配置模型
    /// </summary>
    public class AppConfig
    {
        /// <summary>
        /// 配置版本（用于版本迁移）
        /// </summary>
        public string ConfigVersion { get; set; } = "1.0.0";

        /// <summary>
        /// 程序版本
        /// </summary>
        public string AppVersion { get; set; } = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

        /// <summary>
        /// OBS WebSocket默认端口
        /// </summary>
        public int OBSWebSocketPort { get; set; } = 445;

        /// <summary>
        /// OBS WebSocket IP地址
        /// </summary>
        public string OBSWebSocketIp { get; set; } = "127.0.0.1";

        /// <summary>
        /// OBS WebSocket密码（默认空）
        /// </summary>
        public string OBSWebSocketPassword { get; set; } = string.Empty;

        /// <summary>
        /// 日志文件保存路径（项目目录下的Logs文件夹）
        /// </summary>
        public string LogFilePath { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

        /// <summary>
        /// 配置文件保存路径（项目目录下的Config文件夹）
        /// </summary>
        public string ConfigFilePath { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "config.json");
    }
}
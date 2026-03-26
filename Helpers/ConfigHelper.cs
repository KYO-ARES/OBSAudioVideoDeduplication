// 路径：OBSAudioVideoDeduplication/Helpers/ConfigHelper.cs
using Newtonsoft.Json;
using OBSAudioVideoDeduplication.Models;
using System;
using System.IO;

namespace OBSAudioVideoDeduplication.Helpers
{
    /// <summary>
    /// 配置辅助类（单例模式）
    /// </summary>
    public static class ConfigHelper
    {
        private static AppConfig _appConfig;
        private static readonly object _lockObj = new object();

        /// <summary>
        /// 获取全局配置（懒加载）
        /// </summary>
        public static AppConfig AppConfig
        {
            get
            {
                if (_appConfig == null)
                {
                    lock (_lockObj)
                    {
                        if (_appConfig == null)
                        {
                            _appConfig = LoadConfig();
                        }
                    }
                }
                return _appConfig;
            }
        }

        /// <summary>
        /// 加载配置文件（从项目目录Config文件夹加载）
        /// </summary>
        /// <returns>应用配置</returns>
        private static AppConfig LoadConfig()
        {
            var logger = LogHelper.GetLogger("ConfigHelper");
            try
            {
                // 默认配置路径改为项目目录下的Config/config.json
                var defaultConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "config.json");

                // 配置文件不存在则创建默认配置
                if (!File.Exists(defaultConfigPath))
                {
                    logger.Information("配置文件不存在，创建默认配置：{ConfigPath}", defaultConfigPath);
                    var defaultConfig = new AppConfig
                    {
                        ConfigFilePath = defaultConfigPath
                    };
                    SaveConfig(defaultConfig);
                    return defaultConfig;
                }

                // 读取并反序列化配置
                var configContent = File.ReadAllText(defaultConfigPath);
                var config = JsonConvert.DeserializeObject<AppConfig>(configContent);

                // 版本兼容检查（基础版仅做日志提示）
                if (config.ConfigVersion != "1.0.0")
                {
                    logger.Warning("配置版本不匹配（当前：{ConfigVersion}，预期：1.0.0），建议重新生成配置", config.ConfigVersion);
                }

                logger.Information("配置文件加载成功：{ConfigPath}", defaultConfigPath);
                return config;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "加载配置文件失败，使用默认配置");
                return new AppConfig();
            }
        }

        /// <summary>
        /// 保存配置到文件（保存到项目目录Config文件夹）
        /// </summary>
        /// <param name="config">要保存的配置</param>
        public static void SaveConfig(AppConfig config)
        {
            var logger = LogHelper.GetLogger("ConfigHelper");
            try
            {
                // 创建配置目录（Config文件夹）
                var configDir = Path.GetDirectoryName(config.ConfigFilePath);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                    logger.Information("创建配置目录：{ConfigDir}", configDir);
                }

                // 序列化并保存
                var jsonContent = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(config.ConfigFilePath, jsonContent);

                // 更新内存中的配置
                _appConfig = config;

                logger.Information("配置文件保存成功：{ConfigPath}", config.ConfigFilePath);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "保存配置文件失败");
                throw; // 向上抛出，让调用方处理
            }
        }
    }
}
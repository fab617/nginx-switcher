using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace nginx_switcher
{
    /// <summary>
    /// 配置文件状态更新事件参数
    /// </summary>
    public class ConfigFileStatusUpdatedEventArgs : EventArgs
    {
        /// <summary>
        /// 配置文件索引
        /// </summary>
        public int Index { get; set; }
        
        /// <summary>
        /// 新状态
        /// </summary>
        public string Status { get; set; }
    }
    
    /// <summary>
    /// 应用程序上下文，用于管理应用程序的状态
    /// </summary>
    public class Context
    {
        /// <summary>
        /// nginx配置文件列表
        /// </summary>
        public List<NginxConfigFile> NginxConfigFiles { get; private set; }

        /// <summary>
        /// 配置文件状态更新事件
        /// </summary>
        public event EventHandler<ConfigFileStatusUpdatedEventArgs> ConfigFileStatusUpdated;
        
        /// <summary>
        /// 内容更新事件，用于通知Form1更新内容（包括配置文件列表和任务栏图标）
        /// </summary>
        public event EventHandler ContentUpdateRequested;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="mainForm">关联的主窗口</param>
        public Context(Form1 mainForm)
        {
            NginxConfigFiles = new List<NginxConfigFile>();
        }

        /// <summary>
        /// 更新配置文件状态
        /// </summary>
        /// <param name="index">配置文件索引</param>
        /// <param name="status">新状态</param>
        public void UpdateConfigFileStatus(int index, string status)
        {
            if (index >= 0 && index < NginxConfigFiles.Count)
            {
                // 更新配置文件状态
                NginxConfigFiles[index].Status = status;
                
                // 触发配置文件状态更新事件
                ConfigFileStatusUpdated?.Invoke(this, new ConfigFileStatusUpdatedEventArgs { Index = index, Status = status });
                
                // 触发内容更新事件，更新任务栏图标
                TriggerContentUpdate();
            }
        }

        /// <summary>
        /// 从配置文件中读取配置文件列表
        /// </summary>
        public void LoadConfigFiles()
        {
            // 清空现有配置文件列表
            NginxConfigFiles.Clear();
            
            // 读取配置
            ConfigManager.AppConfig config = ConfigManager.ReadConfig();
            
            // 获取配置文件列表
            List<string> configFilePaths = config.Nginx.Configs.Config.Select(c => c.Entry).ToList();
            
            // 显示配置文件列表
            foreach (string filePath in configFilePaths)
            {
                // 创建nginx配置文件对象
                NginxConfigFile nginxConfigFile = new NginxConfigFile(filePath);
                NginxConfigFiles.Add(nginxConfigFile);
            }
            
            // 触发内容更新事件，更新配置文件列表和任务栏图标
            TriggerContentUpdate();
        }
        
        /// <summary>
        /// 触发内容更新事件
        /// </summary>
        public void TriggerContentUpdate()
        {
            ContentUpdateRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            // 清空配置文件列表
            NginxConfigFiles.Clear();
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace nginx_switcher
{
    /// <summary>
    /// 配置管理类，用于处理配置文件的读取和写入
    /// </summary>
    public static class ConfigManager
    {
        private const string ConfigFileName = "nginx-switcher.config";
        private static readonly string ConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
        private static readonly XmlSerializer serializer = new XmlSerializer(typeof(AppConfig));
        
        /// <summary>
        /// 应用配置类
        /// </summary>
        [Serializable]
        public class AppConfig
        {
            /// <summary>
            /// nginx配置
            /// </summary>
            public NginxConfig Nginx { get; set; }
            
            /// <summary>
            /// 构造函数
            /// </summary>
            public AppConfig()
            {
                Nginx = new NginxConfig();
            }
        }
        
        /// <summary>
        /// nginx配置类
        /// </summary>
        [Serializable]
        public class NginxConfig
        {
            /// <summary>
            /// nginx.exe路径
            /// </summary>
            public string Path { get; set; }
            
            /// <summary>
            /// 配置文件列表
            /// </summary>
            public Configs Configs { get; set; }
            
            /// <summary>
            /// 构造函数
            /// </summary>
            public NginxConfig()
            {
                Path = string.Empty;
                Configs = new Configs();
            }
        }
        
        /// <summary>
        /// 配置文件列表类
        /// </summary>
        [Serializable]
        public class Configs
        {
            /// <summary>
            /// nginx配置列表
            /// </summary>
            [System.Xml.Serialization.XmlElement("Config")]
            public List<Config> Config { get; set; }
            
            /// <summary>
            /// 构造函数
            /// </summary>
            public Configs()
            {
                Config = new List<Config>();
            }
        }
        
        /// <summary>
        /// 配置类
        /// </summary>
        [Serializable]
        public class Config
        {
            /// <summary>
            /// 配置文件路径
            /// </summary>
            public string Entry { get; set; }
            
            /// <summary>
            /// 工作目录
            /// </summary>
            public string WorkDir { get; set; }
            
            /// <summary>
            /// 构造函数
            /// </summary>
            public Config()
            {
                Entry = string.Empty;
                WorkDir = string.Empty;
            }
            
            /// <summary>
            /// 构造函数
            /// </summary>
            /// <param name="entry">配置文件路径</param>
            public Config(string entry)
            {
                Entry = entry;
                WorkDir = GenerateWorkDir(entry);
                CreateWorkDir();
            }
            
            /// <summary>
            /// 生成工作目录
            /// </summary>
            /// <param name="entry">配置文件路径</param>
            /// <returns>工作目录路径</returns>
            private string GenerateWorkDir(string entry)
            {
                string compressedPath = CompressPath(entry);
                return Path.Combine("nginx_work", compressedPath);
            }
            
            /// <summary>
            /// 压缩路径，参考logback压缩类路径方式
            /// </summary>
            /// <param name="path">原始路径</param>
            /// <returns>压缩后的路径</returns>
            private string CompressPath(string path)
            {
                // 替换路径分隔符为下划线
                string compressed = path.Replace(Path.DirectorySeparatorChar, '_');
                compressed = compressed.Replace(Path.AltDirectorySeparatorChar, '_');
                
                // 替换非法字符
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    compressed = compressed.Replace(c, '_');
                }
                
                // 限制长度
                if (compressed.Length > 64)
                {
                    // 保留文件名部分，压缩目录部分
                    string fileName = Path.GetFileName(compressed);
                    string dirPart = compressed.Substring(0, compressed.Length - fileName.Length);
                    
                    // 压缩目录部分：保留每个目录的第一个字符
                    string[] dirs = dirPart.Split('_');
                    string compressedDirs = string.Join("_", dirs.Select(d => d.Length > 0 ? d.Substring(0, 1) : string.Empty));
                    
                    compressed = compressedDirs + fileName;
                    
                    // 如果仍然过长，直接截断
                    if (compressed.Length > 255)
                    {
                        compressed = compressed.Substring(0, 255);
                    }
                }
                while (compressed.Contains("__")) {
                    compressed = compressed.Replace("__", "_");
                }
                
                return compressed;
            }
            
            /// <summary>
            /// 创建工作目录
            /// </summary>
            private void CreateWorkDir()
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string fullWorkDir = Path.Combine(appDir, WorkDir);
                if (!Directory.Exists(fullWorkDir))
                {
                    Directory.CreateDirectory(fullWorkDir);
                }
            }
        }
        
        /// <summary>
        /// 读取配置
        /// </summary>
        /// <returns>配置对象，如果配置文件不存在或读取失败则返回默认配置</returns>
        public static AppConfig ReadConfig()
        {
            if (File.Exists(ConfigFilePath))
            {
                try
                {
                    using (FileStream stream = new FileStream(ConfigFilePath, FileMode.Open))
                    {
                        return (AppConfig)serializer.Deserialize(stream);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("读取配置文件失败: " + ex.Message);
                }
            }
            
            // 返回默认配置
            return new AppConfig();
        }
        
        /// <summary>
        /// 写入配置
        /// </summary>
        /// <param name="config">配置对象</param>
        public static void WriteConfig(AppConfig config)
        {
            try
            {
                using (FileStream stream = new FileStream(ConfigFilePath, FileMode.Create))
                {
                    serializer.Serialize(stream, config);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("写入配置文件失败: " + ex.Message);
            }
        }
        
        /// <summary>
        /// 更新nginx配置文件列表
        /// </summary>
        /// <param name="newConfigFiles">新扫描到的nginx配置文件列表</param>
        public static void UpdateNginxConfigFiles(List<NginxConfigFile> newConfigFiles)
        {
            // 读取现有配置
            AppConfig config = ReadConfig();
            
            // 提取新扫描到的配置文件路径
            List<string> newFilePaths = newConfigFiles.Select(f => f.FilePath).ToList();
            
            // 合并配置文件列表，保留现有配置文件路径
            foreach (string newFilePath in newFilePaths)
            {
                if (!config.Nginx.Configs.Config.Any(c => c.Entry.Equals(newFilePath, StringComparison.OrdinalIgnoreCase)))
                {
                    config.Nginx.Configs.Config.Add(new Config(newFilePath));
                }
            }
            
            // 写入更新后的配置
            WriteConfig(config);
        }
        
        /// <summary>
        /// 验证nginx.exe路径是否有效
        /// </summary>
        /// <param name="nginxPath">nginx.exe路径</param>
        /// <returns>是否有效</returns>
        public static bool IsNginxPathValid(string nginxPath)
        {
            if (string.IsNullOrEmpty(nginxPath))
                return false;
            
            // 检查文件是否存在
            if (!File.Exists(nginxPath))
                return false;
            
            // 检查文件名是否为nginx.exe
            return Path.GetFileName(nginxPath).Equals("nginx.exe", StringComparison.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// 扫描启动目录下的nginx.exe
        /// </summary>
        /// <returns>nginx.exe路径，如果没有找到则返回空字符串</returns>
        public static string ScanNginxExe()
        {
            string startupPath = AppDomain.CurrentDomain.BaseDirectory;
            
            // 搜索当前目录下的nginx.exe
            string nginxPath = Path.Combine(startupPath, "nginx.exe");
            if (IsNginxPathValid(nginxPath))
                return nginxPath;
            
            // 搜索子目录下的nginx.exe
            string[] directories = Directory.GetDirectories(startupPath);
            foreach (string directory in directories)
            {
                nginxPath = Path.Combine(directory, "nginx.exe");
                if (IsNginxPathValid(nginxPath))
                    return nginxPath;
            }
            
            return string.Empty;
        }
    }
}
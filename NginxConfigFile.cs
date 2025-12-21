using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace nginx_switcher
{
    /// <summary>
    /// nginx配置文件类，用于存储nginx配置文件的信息
    /// </summary>
    [Serializable]
    public class NginxConfigFile
    {
        /// <summary>
        /// 配置文件路径
        /// </summary>
        public string FilePath { get; set; }
        
        /// <summary>
        /// 配置文件名
        /// </summary>
        public string FileName { get; set; }
        
        /// <summary>
        /// 配置文件所在目录
        /// </summary>
        public string ConfigDirectory { get; set; }
        
        /// <summary>
        /// 监听端口
        /// </summary>
        public string Port { get; set; }
        
        /// <summary>
        /// 所有监听端口（包括引用文件的，去重）
        /// </summary>
        public List<string> AllPorts { get; set; }
        
        /// <summary>
        /// 引用的配置文件列表
        /// </summary>
        public List<string> ReferencedFiles { get; set; }
        
        /// <summary>
        /// 配置状态
        /// </summary>
        public string Status { get; set; }
        
        /// <summary>
        /// 配置文件是否存在
        /// </summary>
        public bool ConfigExists { get; set; }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="filePath">配置文件路径</param>
        public NginxConfigFile(string filePath)
        {
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            ConfigDirectory = Path.GetDirectoryName(filePath);
            Port = GetPortFromConfig(filePath);
            Status = "已停止";
            ConfigExists = File.Exists(filePath);
            AllPorts = new List<string>();
            ReferencedFiles = new List<string>();
            
            // 解析配置文件
            ParseConfig();
        }
        
        /// <summary>
        /// 无参构造函数，用于序列化
        /// </summary>
        public NginxConfigFile()
        {
            FilePath = string.Empty;
            FileName = string.Empty;
            ConfigDirectory = string.Empty;
            Port = "未配置";
            Status = "已停止";
            ConfigExists = false;
            AllPorts = new List<string>();
            ReferencedFiles = new List<string>();
        }
        
        /// <summary>
        /// 解析配置文件，包括引用文件和监听端口
        /// </summary>
        private void ParseConfig()
        {
            if (!ConfigExists)
            {
                return;
            }
            
            try
            {
                // 存储所有引用的文件路径
                List<string> allFiles = new List<string>();
                allFiles.Add(FilePath);
                
                // 递归解析引用的文件
                ParseReferencedFiles(FilePath, ref allFiles);
                
                // 更新引用文件列表
                ReferencedFiles = allFiles;
                
                // 解析所有文件中的监听端口
                HashSet<string> uniquePorts = new HashSet<string>();
                foreach (string file in allFiles)
                {
                    if (File.Exists(file))
                    {
                        string content = File.ReadAllText(file);
                        // 使用正则表达式匹配监听端口
                        MatchCollection matches = Regex.Matches(content, @"listen\s+([0-9]+);", RegexOptions.IgnoreCase);
                        foreach (Match match in matches)
                        {
                            if (match.Success)
                            {
                                uniquePorts.Add(match.Groups[1].Value);
                            }
                        }
                    }
                }
                
                // 更新所有端口列表
                AllPorts = uniquePorts.ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine("解析配置文件失败: " + ex.Message);
            }
        }
        
        /// <summary>
        /// 递归解析引用的配置文件
        /// </summary>
        /// <param name="filePath">当前配置文件路径</param>
        /// <param name="allFiles">所有文件列表</param>
        private void ParseReferencedFiles(string filePath, ref List<string> allFiles)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string content = File.ReadAllText(filePath);
                    // 匹配include指令，使用更健壮的正则表达式
                    // 支持空格、制表符、注释等
                    MatchCollection matches = Regex.Matches(content, @"include\s+([^;]+);", RegexOptions.IgnoreCase | RegexOptions.Multiline);
                    
                    foreach (Match match in matches)
                    {
                        if (match.Success)
                        {
                            string includePath = match.Groups[1].Value.Trim();
                            string fullIncludePath = string.Empty;
                            
                            // 处理相对路径
                            if (!Path.IsPathRooted(includePath))
                            {
                                string fileDir = Path.GetDirectoryName(filePath);
                                fullIncludePath = Path.Combine(fileDir, includePath);
                            }
                            else
                            {
                                fullIncludePath = includePath;
                            }
                            
                            // 处理通配符
                            if (fullIncludePath.Contains("*") || fullIncludePath.Contains("?"))
                            {
                                string dir = Path.GetDirectoryName(fullIncludePath);
                                string pattern = Path.GetFileName(fullIncludePath);
                                
                                if (System.IO.Directory.Exists(dir))
                                {
                                    string[] files = System.IO.Directory.GetFiles(dir, pattern);
                                    foreach (string file in files)
                                    {
                                        if (!allFiles.Contains(file))
                                        {
                                            allFiles.Add(file);
                                            ParseReferencedFiles(file, ref allFiles);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // 处理单个文件
                                if (File.Exists(fullIncludePath) && !allFiles.Contains(fullIncludePath))
                                {
                                    allFiles.Add(fullIncludePath);
                                    ParseReferencedFiles(fullIncludePath, ref allFiles);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("解析引用文件失败: " + ex.Message);
            }
        }
        
        /// <summary>
        /// 从配置文件中提取监听端口
        /// </summary>
        /// <param name="filePath">配置文件路径</param>
        /// <returns>监听端口</returns>
        private string GetPortFromConfig(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string content = File.ReadAllText(filePath);
                    // 使用正则表达式匹配监听端口
                    Match match = Regex.Match(content, @"listen\s+([0-9]+);", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("读取配置文件失败: " + ex.Message);
            }
            
            return "未配置";
        }
    }
    
    /// <summary>
    /// nginx配置文件列表类，用于存储所有nginx配置文件信息
    /// </summary>
    [Serializable]
    public class NginxConfigList
    {
        /// <summary>
        /// nginx配置文件列表
        /// </summary>
        public List<NginxConfigFile> ConfigFiles { get; set; }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        public NginxConfigList()
        {
            ConfigFiles = new List<NginxConfigFile>();
        }
    }
}
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace nginx_switcher
{
    /// <summary>
    /// Nginx进程监控器，用于定期扫描nginx进程状态
    /// </summary>
    public class NginxProcessMonitor
    {
        /// <summary>
        /// 定时器，用于定期扫描nginx进程
        /// </summary>
        private System.Windows.Forms.Timer _timer;
        
        /// <summary>
        /// 关联的上下文
        /// </summary>
        private Context _context;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="context">关联的上下文</param>
        public NginxProcessMonitor(Context context)
        {
            _context = context;
            InitializeTimer();
        }
        
        /// <summary>
        /// 初始化定时器
        /// </summary>
        private void InitializeTimer()
        {
            // 创建定时器，每隔5秒扫描一次nginx进程
            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 5000;
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }
        
        /// <summary>
        /// 定时器事件处理程序
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Timer_Tick(object sender, EventArgs e)
        {
            // 扫描nginx进程，更新配置文件状态
            ScanNginxProcesses();
        }
        
        /// <summary>
        /// 扫描nginx进程，更新配置文件状态
        /// </summary>
        private void ScanNginxProcesses()
        {
            try
            {
                // 读取配置
                ConfigManager.AppConfig config = ConfigManager.ReadConfig();
                
                // 遍历所有配置文件，检查对应的nginx.pid文件
                for (int i = 0; i < _context.NginxConfigFiles.Count; i++)
                {
                    NginxConfigFile configFile = _context.NginxConfigFiles[i];
                    bool isRunning = false;
                    
                    // 找到对应的Config对象
                    ConfigManager.Config configItem = config.Nginx.Configs.Config.FirstOrDefault(c => c.Entry.Equals(configFile.FilePath, StringComparison.OrdinalIgnoreCase));
                    if (configItem != null)
                    {
                        // 获取工作目录
                        string workDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configItem.WorkDir);
                        // 构建pid文件路径
                        string pidFilePath = Path.Combine(workDir, "logs", "nginx.pid");
                        
                        // 检查pid文件是否存在
                        if (File.Exists(pidFilePath))
                        {
                            try
                            {
                                // 读取pid文件内容
                                string pidText = File.ReadAllText(pidFilePath).Trim();
                                if (int.TryParse(pidText, out int pid))
                                {
                                    // 检查进程是否存在
                                    Process process = Process.GetProcessById(pid);
                                    isRunning = process != null && process.ProcessName.Equals("nginx", StringComparison.OrdinalIgnoreCase);
                                }
                            }
                            catch (ArgumentException)
                            {
                                // 进程不存在
                                isRunning = false;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("检查nginx进程失败: " + ex.Message);
                                isRunning = false;
                            }
                        }
                    }
                    
                    // 如果状态发生变化，调用Context的方法更新
                    string newStatus = isRunning ? "运行中" : "已停止";
                    if (configFile.Status != newStatus)
                    {
                        _context.UpdateConfigFileStatus(i, newStatus);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("扫描nginx进程失败: " + ex.Message);
            }
        }
        
        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Dispose();
                _timer = null;
            }
        }
    }
}
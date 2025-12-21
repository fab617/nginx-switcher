using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;

namespace nginx_switcher
{
    public partial class Form1 : Form
    {
        // 委托定义，用于从Context类触发图标更新
        public delegate void UpdateTrayIconDelegate();
        
        // 文件浏览对话框
        private OpenFileDialog openFileDialog;
        
        
        
        // 图标缓存，key为进程数，value为生成的图标
        private Dictionary<int, Icon> trayIconCache = new Dictionary<int, Icon>();
        
        // 上次显示的进程数，用于判断是否需要更新图标
        private int lastProcessCount = -1;
        
        // 应用程序上下文
        public Context Context { get; set; }
        

        public Form1()
        {
            InitializeComponent();
            // 隐藏主窗口
            this.Hide();
            this.ShowInTaskbar = false;
            
            // 确保SplitContainer右侧面板固定200像素宽度
            // 设置初始SplitterDistance，确保值大于0
            UpdateSplitterDistance();
            
            // 添加Resize事件处理程序，确保右侧面板始终固定200像素宽度
            this.Resize += (sender, e) => 
            {
                UpdateSplitterDistance();
            };
            
            // 添加ListView选择事件处理程序，用于控制按钮的可用状态
            listView1.SelectedIndexChanged += new EventHandler(listView1_SelectedIndexChanged);
            
            // 初始化按钮状态
            UpdateButtonsEnabledState();
            
            // 应用启动时执行配置检查流程
            CheckNginxConfig();
            
            // 更新nginx路径标签
            UpdateNginxPathLabel();
        }
        
        /// <summary>
        /// 初始化Context并订阅事件
        /// </summary>
        /// <param name="context">Context实例</param>
        public void InitializeContext(Context context)
        {
            this.Context = context;
            
            // 订阅配置文件状态更新事件
            context.ConfigFileStatusUpdated += Context_ConfigFileStatusUpdated;
            
            // 订阅内容更新事件
            context.ContentUpdateRequested += Context_ContentUpdateRequested;
        }
        
        // 更新SplitterDistance，确保值大于0
        private void UpdateSplitterDistance()
        {
            // 确保SplitterDistance大于0，避免ArgumentOutOfRangeException
            this.splitContainer1.SplitterDistance = Math.Max(1, this.ClientSize.Width - 200);
        }
        
        // 更新nginx路径标签
        private void UpdateNginxPathLabel()
        {
            // 确保lblNginxPath已经初始化
            if (lblNginxPath == null)
                return;
            
            // 读取配置
            ConfigManager.AppConfig config = ConfigManager.ReadConfig();
            
            // 验证nginx.exe路径是否有效
            if (ConfigManager.IsNginxPathValid(config.Nginx.Path))
            {
                // 路径有效，显示到文件夹
                string nginxDir = Path.GetDirectoryName(config.Nginx.Path);
                lblNginxPath.Text = "当前nginx位置：\r\n" + nginxDir;
                lblNginxPath.ForeColor = System.Drawing.Color.Green;
            }
            else
            {
                // 路径无效或未配置，红色提示
                lblNginxPath.Text = "当前nginx位置：\r\n未配置nginx";
                lblNginxPath.ForeColor = System.Drawing.Color.Red;
            }
        }
        
        // 配置检查流程
        private void CheckNginxConfig()
        {
            // 读取配置
            ConfigManager.AppConfig config = ConfigManager.ReadConfig();
            
            // 验证nginx.exe路径是否有效
            if (ConfigManager.IsNginxPathValid(config.Nginx.Path))
            {
                // 路径有效，扫描nginx配置文件
                ScanNginxConfigFiles();
                return;
            }
            
            // 路径无效，扫描启动目录下的nginx.exe
            string scannedNginxPath = ConfigManager.ScanNginxExe();
            if (!string.IsNullOrEmpty(scannedNginxPath))
            {
                // 找到nginx.exe，保存到配置文件
                config.Nginx.Path = scannedNginxPath;
                ConfigManager.WriteConfig(config);
                // 更新nginx路径标签
                UpdateNginxPathLabel();
                // 扫描nginx配置文件
                ScanNginxConfigFiles();
                return;
            }
            
            // 没有找到nginx.exe，显示文件浏览框让用户选择
            ShowNginxFileDialog();
        }
        
        // 显示文件浏览框，让用户选择nginx.exe路径
        private void ShowNginxFileDialog()
        {
            // 确保openFileDialog已经初始化
            if (openFileDialog == null)
            {
                openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "nginx.exe文件|nginx.exe|所有文件|*.*";
                openFileDialog.Title = "选择nginx.exe路径";
            }
            
            // 使用ShowDialog(this)确保对话框显示在窗口前面
            DialogResult result = openFileDialog.ShowDialog(this);
            if (result == DialogResult.OK)
            {
                // 用户选择了文件，保存到配置文件
                ConfigManager.AppConfig config = ConfigManager.ReadConfig();
                config.Nginx.Path = openFileDialog.FileName;
                ConfigManager.WriteConfig(config);
                // 更新nginx路径标签
                UpdateNginxPathLabel();
                // 扫描nginx配置文件
                ScanNginxConfigFiles();
            }
        }
        
        // "配置nginx"按钮点击事件处理程序
        private void btnConfigNginx_Click(object sender, EventArgs e)
        {
            // ShowNginxFileDialog必须在主线程执行
            ShowNginxFileDialog();
            
            // 将耗时操作放在后台线程执行
            Task.Run(() =>
            {
                // 更新nginx路径标签（UI操作）
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() => UpdateNginxPathLabel()));
                }
                else
                {
                    UpdateNginxPathLabel();
                }
                
                // 扫描nginx配置文件（耗时操作）
                ScanNginxConfigFiles();
            });
        }
        
        // 扫描nginx配置文件
        private void ScanNginxConfigFiles()
        {
            // 读取配置
            ConfigManager.AppConfig config = ConfigManager.ReadConfig();
            
            // 验证nginx.exe路径是否有效
            if (ConfigManager.IsNginxPathValid(config.Nginx.Path))
            {
                // 获取nginx安装目录
                string nginxDir = Path.GetDirectoryName(config.Nginx.Path);
                // nginx配置文件目录
                string confDir = Path.Combine(nginxDir, "conf");
                
                // 扫描到的配置文件列表
                List<NginxConfigFile> scannedConfigFiles = new List<NginxConfigFile>();
                
                // 检查配置文件目录是否存在
                if (Directory.Exists(confDir))
                {
                    // 扫描配置文件目录下的.conf文件
                    string[] configFiles = Directory.GetFiles(confDir, "*.conf");
                    foreach (string filePath in configFiles)
                    {
                        // 检查配置文件是否包含http配置块
                        if (IsMainConfigFile(filePath))
                        {
                            // 创建nginx配置文件对象
                            NginxConfigFile nginxConfigFile = new NginxConfigFile(filePath);
                            scannedConfigFiles.Add(nginxConfigFile);
                        }
                    }
                }
                
                // 更新配置文件列表，不删除不存在的配置文件
                ConfigManager.UpdateNginxConfigFiles(scannedConfigFiles);
            }
            
            // 通过Context加载配置文件并触发更新事件
            if (Context != null)
            {
                Context.LoadConfigFiles();
            }
        }
        
        // 从配置文件中读取配置文件列表并显示
        private void DisplayConfigFilesFromConfig()
        {
            // 通过Context加载配置文件并触发更新事件
            if (Context != null)
            {
                Context.LoadConfigFiles();
            }
        }
        
        // 检查配置文件是否包含http配置块
        private bool IsMainConfigFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string content = File.ReadAllText(filePath);
                    // 使用正则表达式匹配http配置块
                    return System.Text.RegularExpressions.Regex.IsMatch(content, @"http\s*\{", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("读取配置文件失败: " + ex.Message);
            }
            
            return false;
        }
        
        // 自动调整列宽
        private void AutoResizeColumns()
        {
            // 首先根据列标题大小调整，确保标题完全可见
            listView1.Columns[0].AutoResize(ColumnHeaderAutoResizeStyle.HeaderSize);
            listView1.Columns[1].AutoResize(ColumnHeaderAutoResizeStyle.HeaderSize);
            listView1.Columns[2].AutoResize(ColumnHeaderAutoResizeStyle.HeaderSize);
            
            // 然后根据列内容调整，确保内容完全可见
            listView1.Columns[0].AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
            listView1.Columns[1].AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
            listView1.Columns[2].AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
        }
        
        // ListView选择变化事件，用于控制按钮的可用性
        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            // 启动和停止按钮在单选或多选时可用
            btnStart.Enabled = listView1.SelectedItems.Count > 0;
            btnStop.Enabled = listView1.SelectedItems.Count > 0;
            
            // 删除配置文件按钮在单选或多选时可用
            btnDeleteConfig.Enabled = listView1.SelectedItems.Count > 0;
            
            // 编辑按钮只在单项选择时可用
            btnEdit.Enabled = listView1.SelectedItems.Count == 1;
        }
        
        // 初始化按钮状态
        private void UpdateButtonsEnabledState()
        {
            // 初始状态下，ListView中没有选中项
            btnStart.Enabled = false;
            btnStop.Enabled = false;
            btnDeleteConfig.Enabled = false;
            btnEdit.Enabled = false;
        }
        
        /// <summary>
        /// 更新ListView中指定索引项的状态
        /// </summary>
        /// <param name="index">项目索引</param>
        /// <param name="status">状态文本</param>
        public void UpdateListViewItemStatus(int index, string status)
        {
            if (index >= 0 && index < listView1.Items.Count)
            {
                listView1.Items[index].SubItems[2].Text = status;
            }
        }
        
        /// <summary>
        /// 配置文件状态更新事件处理程序
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">事件参数</param>
        private void Context_ConfigFileStatusUpdated(object sender, ConfigFileStatusUpdatedEventArgs e)
        {
            // 确保在UI线程上执行
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateListViewItemStatus(e.Index, e.Status)));
            }
            else
            {
                UpdateListViewItemStatus(e.Index, e.Status);
            }
        }
        
        /// <summary>
        /// 任务栏图标更新事件处理程序
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">事件参数</param>
        private void Context_TrayIconUpdateRequested(object sender, EventArgs e)
        {
            // 确保在UI线程上执行
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateTrayIcon()));
            }
            else
            {
                UpdateTrayIcon();
            }
        }
        
        /// <summary>
        /// 内容更新事件处理程序
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">事件参数</param>
        private void Context_ContentUpdateRequested(object sender, EventArgs e)
        {
            // 确保在UI线程上执行
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => HandleContentUpdate()));
            }
            else
            {
                HandleContentUpdate();
            }
        }
        
        /// <summary>
        /// 处理内容更新
        /// </summary>
        private void HandleContentUpdate()
        {
            // 更新配置文件列表显示
            UpdateConfigFilesDisplay();
            
            // 更新任务栏图标
            UpdateTrayIcon();
        }
        
        /// <summary>
        /// 更新配置文件列表显示
        /// </summary>
        private void UpdateConfigFilesDisplay()
        {
            // 清空ListView
            listView1.Items.Clear();
            
            // 确保Context存在且有配置文件
            if (Context != null && Context.NginxConfigFiles.Count > 0)
            {
                // 遍历配置文件列表，创建ListViewItem
                foreach (NginxConfigFile configFile in Context.NginxConfigFiles)
                {
                    // 构建端口显示文本
                    string portsText = configFile.AllPorts.Count > 0 ? string.Join(", ", configFile.AllPorts) : "未配置";
                    
                    // 计算引用文件数
                    int referencedFileCount = configFile.ReferencedFiles.Count - 1; // 减去主文件本身
                    
                    // 构建第一列文本：主配置文件路径 (+n)
                    string mainFilePath = configFile.FilePath;
                    string firstColumnText = referencedFileCount > 0 ? $"{mainFilePath} (+{referencedFileCount})" : mainFilePath;
                    
                    // 添加到ListView
                    ListViewItem item = new ListViewItem(new string[] 
                    {
                        firstColumnText,
                        portsText,
                        configFile.Status
                    });
                    listView1.Items.Add(item);
                }
                
                // 自动调整列宽
                AutoResizeColumns();
                
                // 更新按钮状态
                UpdateButtonsEnabledState();
            }
        }
        
        // 编辑按钮点击事件
        private void btnEdit_Click(object sender, EventArgs e)
        {
            // 获取选中项信息（UI操作，必须在主线程执行）
            int index = -1;
            NginxConfigFile configFile = null;
            
            if (listView1.SelectedItems.Count == 1)
            {
                ListViewItem selectedItem = listView1.SelectedItems[0];
                index = selectedItem.Index;
                
                if (Context != null && index >= 0 && index < Context.NginxConfigFiles.Count)
                {
                    configFile = Context.NginxConfigFiles[index];
                }
            }
            
            // 将后续操作放在后台线程执行
            Task.Run(() =>
            {
                if (configFile != null)
                {
                    // 使用notepad一次性打开当前主配置文件及其关联的配置文件
                    try
                    {
                        // 确保有配置文件可以打开
                        if (configFile.ReferencedFiles.Count == 0)
                        {
                            // MessageBox.Show是UI操作，必须在主线程执行
                            if (this.InvokeRequired)
                            {
                                this.Invoke(new Action(() => MessageBox.Show("没有找到配置文件")));
                            }
                            else
                            {
                                MessageBox.Show("没有找到配置文件");
                            }
                            return;
                        }
                        
                        // 构建命令行参数，将每个文件路径用引号括起来
                        List<string> quotedFiles = new List<string>();
                        foreach (string filePath in configFile.ReferencedFiles)
                        {
                            if (File.Exists(filePath))
                            {
                                quotedFiles.Add(string.Format("\"{0}\"", filePath));
                            }
                        }
                        
                        if (quotedFiles.Count == 0)
                        {
                            // MessageBox.Show是UI操作，必须在主线程执行
                            if (this.InvokeRequired)
                            {
                                this.Invoke(new Action(() => MessageBox.Show("配置文件不存在")));
                            }
                            else
                            {
                                MessageBox.Show("配置文件不存在");
                            }
                            return;
                        }
                        
                        // 为每个文件单独启动一个notepad实例
                        // 因为notepad不支持一次性打开多个文件
                        foreach (string quotedFile in quotedFiles)
                        {
                            // 移除引号
                            string filePath = quotedFile.Trim('"');
                            System.Diagnostics.Process.Start("notepad.exe", quotedFile);
                        }
                    }
                    catch (Exception ex)
                    {
                        // MessageBox.Show是UI操作，必须在主线程执行
                        if (this.InvokeRequired)
                        {
                            this.Invoke(new Action(() => MessageBox.Show(string.Format("打开配置文件失败：{0}", ex.Message))));
                        }
                        else
                        {
                            MessageBox.Show(string.Format("打开配置文件失败：{0}", ex.Message));
                        }
                    }
                }
            });
        }
        
        // 系统托盘图标双击事件 - 显示主窗口
        private void notifyIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            ShowMainWindow();
        }

        // 右键菜单"打开窗口"点击事件 - 显示主窗口
        private void openWindowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowMainWindow();
        }

        // 右键菜单"退出"点击事件 - 退出应用程序
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        // 窗口关闭事件 - 隐藏窗口而不是退出应用程序
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 如果是用户点击关闭按钮，隐藏窗口而不是退出
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                this.ShowInTaskbar = false;
            }
        }

        // 刷新按钮点击事件 - 扫描nginx配置文件
        private void btnRefresh_Click(object sender, EventArgs e)
        {
            // 将耗时操作放在后台线程执行
            Task.Run(() =>
            {
                // 扫描nginx配置文件（耗时操作）
                ScanNginxConfigFiles();
            });
        }
        
        // 添加配置文件按钮点击事件
        private void btnAddConfig_Click(object sender, EventArgs e)
        {
            // 确保openFileDialog已经初始化
            if (openFileDialog == null)
            {
                openFileDialog = new OpenFileDialog();
            }
            
            // 设置文件选择对话框
            openFileDialog.Filter = "配置文件 (*.conf)|*.conf|所有文件 (*.*)|*.*";
            openFileDialog.FilterIndex = 1; // 优先选择conf后缀文件
            openFileDialog.Title = "选择nginx配置文件";
            openFileDialog.Multiselect = false; // 只能选择单个文件
            
            // 显示文件选择对话框（UI操作，必须在主线程执行）
            DialogResult result = openFileDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                // 获取选中的文件路径
                string selectedFilePath = openFileDialog.FileName;
                
                // 将后续操作放在后台线程执行
                Task.Run(() =>
                {
                    // 检查文件是否已经存在于配置文件列表中
                    ConfigManager.AppConfig config = ConfigManager.ReadConfig();
                    if (!config.Nginx.Configs.Config.Any(c => c.Entry.Equals(selectedFilePath, StringComparison.OrdinalIgnoreCase)))
                    {
                        // 添加到配置文件列表
                        config.Nginx.Configs.Config.Add(new ConfigManager.Config(selectedFilePath));
                        // 写入配置文件
                        ConfigManager.WriteConfig(config);
                        // 重新显示配置文件列表（UI操作）
                        if (this.InvokeRequired)
                        {
                            this.Invoke(new Action(() => DisplayConfigFilesFromConfig()));
                        }
                        else
                        {
                            DisplayConfigFilesFromConfig();
                        }
                    }
                });
            }
        }
        
        // 删除配置文件按钮点击事件
        private void btnDeleteConfig_Click(object sender, EventArgs e)
        {
            // 确保有选中的项（UI操作，必须在主线程执行）
            if (listView1.SelectedItems.Count > 0)
            {
                // 弹出确认对话框（UI操作，必须在主线程执行）
                DialogResult result = MessageBox.Show("确定要删除选中的配置文件吗？删除前会自动停止对应的nginx服务。", "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    // 收集要删除的配置文件路径（UI操作，必须在主线程执行）
                    List<string> filesToDelete = new List<string>();
                    List<NginxConfigFile> configFilesToStop = new List<NginxConfigFile>();
                    List<KeyValuePair<int, ListViewItem>> stopParams = new List<KeyValuePair<int, ListViewItem>>();
                    
                    foreach (ListViewItem item in listView1.SelectedItems)
                    {
                        int index = item.Index;
                        if (Context != null && index >= 0 && index < Context.NginxConfigFiles.Count)
                        {
                            NginxConfigFile configFile = Context.NginxConfigFiles[index];
                            if (configFile.Status == "运行中")
                            {
                                configFilesToStop.Add(configFile);
                                stopParams.Add(new KeyValuePair<int, ListViewItem>(index, item));
                            }
                            filesToDelete.Add(configFile.FilePath);
                        }
                    }
                    
                    // 将后续操作放在后台线程执行
                    Task.Run(() =>
                    {
                        // 读取当前配置
                        ConfigManager.AppConfig config = ConfigManager.ReadConfig();
                        
                        // 遍历要停止的配置文件，停止对应的nginx服务
                        for (int i = 0; i < configFilesToStop.Count; i++)
                        {
                            NginxConfigFile configFile = configFilesToStop[i];
                            int index = stopParams[i].Key;
                            ListViewItem item = stopParams[i].Value;
                            ExecuteNginxCommand("stop", index, item, configFile);
                        }
                        
                        // 从配置中删除选中的配置文件
                        config.Nginx.Configs.Config = config.Nginx.Configs.Config.Where(c => !filesToDelete.Any(f => f.Equals(c.Entry, StringComparison.OrdinalIgnoreCase))).ToList();
                        
                        // 写入配置文件
                        ConfigManager.WriteConfig(config);
                        
                        // 重新显示配置文件列表（UI操作）
                        if (this.InvokeRequired)
                        {
                            this.Invoke(new Action(() => DisplayConfigFilesFromConfig()));
                        }
                        else
                        {
                            DisplayConfigFilesFromConfig();
                        }
                    });
                }
            }
        }
        
        // 确保目录存在，如果不存在则创建
        private void EnsureDirectoryExists(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
                Console.WriteLine("创建目录: " + directoryPath);
            }
        }
        
        // 执行nginx命令的通用方法
        private void ExecuteNginxCommand(string command, int index, ListViewItem item, NginxConfigFile configFile)
        {
            ConfigManager.AppConfig cfg = ConfigManager.ReadConfig();
            
            // 读取配置获取nginx.exe路径
            string nginxExe = cfg.Nginx.Path;
            
            try
            {
                // 验证nginx.exe路径有效性
                if (!ConfigManager.IsNginxPathValid(nginxExe))
                {
                    Console.WriteLine("nginx.exe路径无效: " + nginxExe);
                    MessageBox.Show("nginx.exe路径无效，请先配置正确的nginx路径", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                
                // 检查配置文件是否存在
                if (!File.Exists(configFile.FilePath))
                {
                    Console.WriteLine("配置文件不存在: " + configFile.FilePath);
                    MessageBox.Show("配置文件不存在: " + configFile.FilePath, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                
                // 获取当前配置文件对应的Config对象
                ConfigManager.Config config = cfg.Nginx.Configs.Config.FirstOrDefault(c => c.Entry.Equals(configFile.FilePath, StringComparison.OrdinalIgnoreCase));
                if (config == null)
                {
                    // 如果配置对象不存在，创建一个新的
                    config = new ConfigManager.Config(configFile.FilePath);
                    cfg.Nginx.Configs.Config.Add(config);
                    ConfigManager.WriteConfig(cfg);
                }
                
                // 获取工作目录
                string workDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, config.WorkDir);
                
                // 检查并创建所需目录
                EnsureDirectoryExists(workDir);
                EnsureDirectoryExists(Path.Combine(workDir, "logs"));
                EnsureDirectoryExists(Path.Combine(workDir, "temp"));
                EnsureDirectoryExists(Path.Combine(workDir, "html"));
                
                // 构造完整的启动参数
                string args;
                if (command == "start")
                {
                    args = string.Format("-p \"{0}\" -c \"{1}\"", workDir, configFile.FilePath);
                }
                else
                {
                    args = string.Format("-p \"{0}\" -s stop -c \"{1}\"", workDir, configFile.FilePath);
                }

                Console.WriteLine("启动nginx进程参数: " + nginxExe + " " + args);

                // 启动nginx进程
                ProcessStartInfo psi = new ProcessStartInfo(nginxExe, args)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WorkingDirectory = workDir,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                
                Process process = Process.Start(psi);
                process.WaitForExit(1000); // 等待1秒，看是否立即退出
                
                if (process.HasExited && process.ExitCode != 0)
                {
                    // 读取错误输出
                    string errorOutput = process.StandardError.ReadToEnd();
                    string standardOutput = process.StandardOutput.ReadToEnd();
                    Console.WriteLine("nginx" + (command == "start" ? "启动" : "停止") + "失败，退出码: " + process.ExitCode);
                    Console.WriteLine("错误输出: " + errorOutput);
                    Console.WriteLine("标准输出: " + standardOutput);
                    MessageBox.Show("nginx" + (command == "start" ? "启动" : "停止") + "失败，退出码: " + process.ExitCode + "\n" + errorOutput + "\n" + standardOutput, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    string status = command == "start" ? "运行中" : "已停止";
                    
                    // 通过Context更新配置文件状态，触发事件通知
                    if (Context != null)
                    {
                        Context.UpdateConfigFileStatus(index, status);
                    }
                    
                    Console.WriteLine("nginx" + (command == "start" ? "启动" : "停止") + "成功");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("执行nginx" + (command == "start" ? "启动" : "停止") + "命令失败: " + ex.Message);
                Console.WriteLine(ex.StackTrace);
                MessageBox.Show("执行nginx" + (command == "start" ? "启动" : "停止") + "命令失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        // 启动按钮点击事件
        private void btnStart_Click(object sender, EventArgs e)
        {
            // 收集要启动的配置文件信息（UI操作，必须在主线程执行）
            List<NginxConfigFile> configFilesToStart = new List<NginxConfigFile>();
            List<KeyValuePair<int, ListViewItem>> startParams = new List<KeyValuePair<int, ListViewItem>>();
            
            foreach (ListViewItem item in listView1.SelectedItems)
            {
                int index = item.Index;
                if (Context != null && index >= 0 && index < Context.NginxConfigFiles.Count)
                {
                    NginxConfigFile configFile = Context.NginxConfigFiles[index];
                    configFilesToStart.Add(configFile);
                    startParams.Add(new KeyValuePair<int, ListViewItem>(index, item));
                }
            }
            
            // 将启动操作放在后台线程执行
            Task.Run(() =>
            {
                // 遍历要启动的配置文件，执行启动操作
                for (int i = 0; i < configFilesToStart.Count; i++)
                {
                    NginxConfigFile configFile = configFilesToStart[i];
                    int index = startParams[i].Key;
                    ListViewItem item = startParams[i].Value;
                    ExecuteNginxCommand("start", index, item, configFile);
                }
            });
        }
        
        // 停止按钮点击事件
        private void btnStop_Click(object sender, EventArgs e)
        {
            // 收集要停止的配置文件信息（UI操作，必须在主线程执行）
            List<NginxConfigFile> configFilesToStop = new List<NginxConfigFile>();
            List<KeyValuePair<int, ListViewItem>> stopParams = new List<KeyValuePair<int, ListViewItem>>();
            
            foreach (ListViewItem item in listView1.SelectedItems)
            {
                int index = item.Index;
                if (Context != null && index >= 0 && index < Context.NginxConfigFiles.Count)
                {
                    NginxConfigFile configFile = Context.NginxConfigFiles[index];
                    configFilesToStop.Add(configFile);
                    stopParams.Add(new KeyValuePair<int, ListViewItem>(index, item));
                }
            }
            
            // 将停止操作放在后台线程执行
            Task.Run(() =>
            {
                // 遍历要停止的配置文件，执行停止操作
                for (int i = 0; i < configFilesToStop.Count; i++)
                {
                    NginxConfigFile configFile = configFilesToStop[i];
                    int index = stopParams[i].Key;
                    ListViewItem item = stopParams[i].Value;
                    ExecuteNginxCommand("stop", index, item, configFile);
                }
            });
        }

        // 定时器事件处理程序，每隔5秒扫描一次nginx进程
        private void nginxProcessTimer_Tick(object sender, EventArgs e)
        {
            // 扫描nginx进程，更新配置文件状态
            UpdateConfigFileStatus();
        }

        // 生成带进程数的任务栏图标
        private Icon GenerateTrayIcon(int processCount)
        {
            // 检查缓存中是否已存在该进程数的图标
            if (trayIconCache.ContainsKey(processCount))
            {
                return trayIconCache[processCount];
            }
            
            try
            {
                // 直接创建32x32的Bitmap作为画布
                int iconSize = 32;
                Bitmap iconBitmap = new Bitmap(iconSize, iconSize);
                
                using (Graphics g = Graphics.FromImage(iconBitmap))
                {
                    // 设置高质量绘图
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                    
                    // 清空画布
                    g.Clear(Color.Transparent);
                    
                    // 获取资源文件中的tray图片并转换为Bitmap
                    byte[] trayBytes = Properties.Resources.tray;
                    using (MemoryStream ms = new MemoryStream(trayBytes))
                    {
                        using (Bitmap originalTrayBitmap = new Bitmap(ms))
                        {
                            // 将原始图片绘制到32x32的画布上，保持比例
                            g.DrawImage(originalTrayBitmap, 0, 0, iconSize, iconSize);
                        }
                    }
                    
                    // 准备绘制文字
                    string text = processCount.ToString();
                    if (processCount > 9999)
                    {
                        text = "9999";
                    }
                    
                    // 设置字体和颜色，大小为16px
                    Font font = new Font("Times New Roman", 20, FontStyle.Bold);
                    Brush brush = new SolidBrush(Color.Red);
                    
                    // 计算文字位置，使其右对齐显示在图片下半部分
                    SizeF textSize = g.MeasureString(text, font);
                    float x = iconSize - textSize.Width + 4; // 右对齐，留出2px边距
                    float y = 8;
                    
                    // 绘制文字
                    g.DrawString(text, font, brush, x, y);
                }
                
                // 创建Icon，使用更可靠的方式
                Icon generatedIcon = Icon.FromHandle(iconBitmap.GetHicon());
                
                // 保存到缓存
                trayIconCache[processCount] = generatedIcon;
                
                return generatedIcon;
            }
            catch (Exception ex)
            {
                Console.WriteLine("生成任务栏图标失败: " + ex.Message);
                // 如果生成失败，返回默认图标
                return this.Icon;
            }
        }
        
        // 更新任务栏图标
        public void UpdateTrayIcon()
        {
            try
            {
                // 统计应用内管理的正在运行的配置数
                int runningConfigCount = 0;
                
                // 使用Context.NginxConfigFiles列表来统计，而不是旧的nginxConfigFiles列表
                if (Context != null)
                {
                    runningConfigCount = Context.NginxConfigFiles.Count(configFile => configFile.Status == "运行中");
                }
                
                // 判断是否需要更新图标
                if (runningConfigCount == lastProcessCount)
                {
                    return; // 运行的配置数没有变化，不需要更新
                }
                
                // 生成或从缓存获取图标
                Icon newIcon = GenerateTrayIcon(runningConfigCount);
                
                // 更新任务栏图标
                notifyIcon.Icon = newIcon;
                
                // 更新上次显示的进程数
                lastProcessCount = runningConfigCount;
            }
            catch (Exception ex)
            {
                Console.WriteLine("更新任务栏图标失败: " + ex.Message);
            }
        }
        
        // 扫描nginx进程，更新配置文件状态
        private void UpdateConfigFileStatus()
        {
            // 现在通过Context中的NginxProcessMonitor来监控进程状态，不需要在这里直接更新
            // 这个方法保留用于兼容性，实际功能已由NginxProcessMonitor实现
        }

        // 重写Dispose方法，释放资源
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        // 显示主窗口的辅助方法
        private void ShowMainWindow()
        {
            this.Show();
            this.ShowInTaskbar = true;
            this.Activate();
        }
    }
}

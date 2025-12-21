using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace nginx_switcher
{
    internal static class Program
    {
        // 导入Windows API函数以分配控制台
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllocConsole();

        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            try
            {
                // 分配控制台窗口，允许Console输出
                // AllocConsole();
                
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                
                // 创建主窗口实例，但不显示
                Form1 form = new Form1();
                
                // 创建Context实例，用于管理应用程序状态
                Context context = new Context(form);
                
                // 初始化Context并订阅事件
                form.InitializeContext(context);
                
                // 创建并启动Nginx进程监控器，由Program管理，NginxProcessMonitor依赖Context
                NginxProcessMonitor processMonitor = new NginxProcessMonitor(context);
                
                // 应用启动后，触发一次配置文件加载和内容更新
                context.LoadConfigFiles();
                
                // 运行应用程序消息循环
                Application.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine("应用程序启动失败: " + ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}
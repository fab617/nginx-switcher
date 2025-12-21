using System;
using System.Drawing;

class Program
{
    static void Main()
    {
        try
        {
            // 加载图片
            using (Image img = Image.FromFile("tray.png"))
            {
                // 获取图片尺寸
                int width = img.Width;
                int height = img.Height;
                
                // 输出结果
                Console.WriteLine($"Image dimensions: {width} x {height} pixels");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}
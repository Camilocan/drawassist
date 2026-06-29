using System;
using System.Windows.Forms;

namespace DrawAssist
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);

            using var app = new AppController();
            app.Run();
            Application.Run();
        }
    }
}

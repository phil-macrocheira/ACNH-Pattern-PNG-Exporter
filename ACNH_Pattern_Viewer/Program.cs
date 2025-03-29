using ACNH_Pattern_Viewer;
using System;
using System.Windows.Forms;

namespace BinaryImageViewer
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
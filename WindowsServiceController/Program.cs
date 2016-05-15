using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Zemna.WindowsServiceController
{
    static class Program
    {
        /// <summary>
        /// 해당 응용 프로그램의 주 진입점입니다.
        /// </summary>
        [STAThread]
        static void Main()
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new frmMain());
            }
            catch (Exception e)
            {
                MessageBox.Show("Main() Error!\r\n" + e.ToString());
                throw;
            }
        }
    }
}

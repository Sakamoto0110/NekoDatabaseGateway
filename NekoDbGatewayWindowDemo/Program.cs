using NekoDbGateway;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NekoDbGatewayWindowDemo
{
    internal static class Program
    {
        class Foo<T> where T :  List<int>
        {

        }

        /// <summary>
        /// Ponto de entrada principal para o aplicativo.
        /// </summary>
        [STAThread]
        static async Task Main()
        {
            
            
                        









            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}

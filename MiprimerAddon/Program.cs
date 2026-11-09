using System;
using System.Windows.Forms;

namespace MiprimerAddon
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
                new Addon();
                System.Windows.Forms.Application.Run();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al conectar: " + ex.Message);
            }
        }
    }
}
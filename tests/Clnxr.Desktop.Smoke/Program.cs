using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace Clnxr.Desktop.Smoke
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                string desktopAssembly = args != null && args.Length > 0
                    ? Path.GetFullPath(args[0])
                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CLNXR-Portable.exe");
                if (!File.Exists(desktopAssembly)) throw new FileNotFoundException("Assembly desktop nao foi copiado para o teste.", desktopAssembly);

                Assembly assembly = Assembly.LoadFrom(desktopAssembly);
                Type formType = assembly.GetType("Clnxr.Desktop.MainForm", true);
                using (Form form = (Form)Activator.CreateInstance(formType, true))
                {
                    if (form.Visible) throw new InvalidOperationException("O smoke test nao pode exibir a janela.");
                    if (!string.Equals(form.Text, "CLNXR (nome provisório)", StringComparison.Ordinal))
                        throw new InvalidOperationException("Titulo inesperado da janela desktop.");
                    if (form.Controls.Count == 0) throw new InvalidOperationException("A janela desktop foi criada sem controles.");
                }

                Console.WriteLine("PASS: construcao nao visual da janela desktop concluida.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAIL: " + ex);
                return 1;
            }
        }
    }
}

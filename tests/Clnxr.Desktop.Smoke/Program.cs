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

                    Type pageType = assembly.GetType("Clnxr.Desktop.DesktopPage", true);
                    object resultsPage = Enum.Parse(pageType, "Results");
                    MethodInfo navigate = formType.GetMethod("Navigate", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (navigate == null) throw new InvalidOperationException("A janela nao expoe a navegacao interna esperada.");
                    navigate.Invoke(form, new[] { resultsPage });
                    FieldInfo gridField = formType.GetField("grid", BindingFlags.Instance | BindingFlags.NonPublic);
                    DataGridView resultsGrid = gridField == null ? null : gridField.GetValue(form) as DataGridView;
                    if (resultsGrid == null || !resultsGrid.VirtualMode)
                        throw new InvalidOperationException("A página de resultados precisa usar DataGridView.VirtualMode.");
                }

                Console.WriteLine("PASS: construcao nao visual da janela desktop e contrato de grade virtualizada concluidos.");
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

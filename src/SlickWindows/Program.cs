using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using SlickWindows.Gui;
using SlickWindows.Gui.Components;

namespace SlickWindows
{
    static class Program
    {
        /// <summary>
        /// Assemblies loaded from embedded resources
        /// </summary>
        private static List<Assembly> EmbeddedDlls;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // Setup the embed dll reader
            LoadEmbeddedAssemblies();
            AppDomain.CurrentDomain.AssemblyResolve += TryUsingManifest;

            // Trap errors
            /*AppDomain.CurrentDomain.FirstChanceException += (sender, eventArgs) => { LogError("First chance", eventArgs?.Exception?.ToString()); };
            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) => {
                MessageBox.Show("An unhandled exception occurred. Check  C:\\Temp\\SlickLog.txt  for details", "Slick: Unhandled Error", MessageBoxButtons.OK);
                LogError("Unhandled", eventArgs?.ExceptionObject?.ToString());
            };*/

            // Run the code
            Win32.SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainWindow(args));
        }

        private static void LogError(string kind, string message)
        {
            try {
                Directory.CreateDirectory(@"C:\Temp");
                File.AppendAllText(@"C:\Temp\SlickLog.txt", $"\r\n{kind}  {DateTime.Now}:\r\n{message}");
            } catch {
                // ignore
            }
        }

        /// <summary>
        /// Load any embedded DLLs into a cache list.
        /// If the required DLLs are embedded correctly, the .exe file doesn't need its dependencies as separate files
        /// </summary>
        private static void LoadEmbeddedAssemblies()
        {
            var assm = Assembly.GetExecutingAssembly();

            EmbeddedDlls = new List<Assembly>();
            var embeddedDlls = assm
                .GetManifestResourceNames()
                .Where(name => name.EndsWith(".dll"));

            foreach (var dll in embeddedDlls)
            {
                var stream = assm.GetManifestResourceStream(dll);
                if (stream == null) continue;

                var ms = new MemoryStream();
                stream.CopyTo(ms);
                ms.Seek(0, SeekOrigin.Begin);

                EmbeddedDlls.Add(AppDomain.CurrentDomain.Load(ms.ToArray()));
            }
        }

        /// <summary>
        /// Try to load dependencies from embedded resource cache
        /// </summary>
        private static Assembly TryUsingManifest(object sender, ResolveEventArgs args)
        {
            if (args?.Name?.StartsWith("SlickWindows.resources") != false) return null;
            if (EmbeddedDlls == null) throw new Exception("Embedded dependency list is null. The executable file may be corrupt.");

            foreach (var dll in EmbeddedDlls) { if (dll.FullName == args.Name) return dll; }

            return null;
        }
    }
}

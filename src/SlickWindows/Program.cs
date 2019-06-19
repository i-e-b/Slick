using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using SlickWindows.Gui;

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

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainWindow(args));
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

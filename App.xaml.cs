using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace PortKnocker
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Ensure log folder exists early
            try { Directory.CreateDirectory(Logger.AppFolder); } catch { /* ignore */ }

            // Global exception handlers
            this.DispatcherUnhandledException += (s, ex) =>
            {
                Logger.File($"[UI-EXCEPTION] {ex.Exception}");
                MessageBox.Show("An unexpected error occurred. See the log file for details.", "PortKnocker", MessageBoxButton.OK, MessageBoxImage.Error);
                ex.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                Logger.File($"[DOMAIN-EXCEPTION] {ex.ExceptionObject}");
            };

            TaskScheduler.UnobservedTaskException += (s, ex) =>
            {
                Logger.File($"[TASK-EXCEPTION] {ex.Exception}");
                ex.SetObserved();
            };
        }
    }
}

using System;
using System.IO;

namespace PortKnocker
{
    internal static class Logger
    {
        public static readonly string AppFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PortKnocker");
        public static readonly string LogPath = Path.Combine(AppFolder, "app.log");

        public static void File(string message)
        {
            try
            {
                Directory.CreateDirectory(AppFolder);
                System.IO.File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch { /* ignore logging errors */ }
        }
    }
}

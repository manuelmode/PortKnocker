using System;
using System.IO;
using System.Text.Json;

namespace PortKnocker
{
    public static class ProfileStore
    {
        // Save profiles.json next to the executable
        private static string FilePath =>
            Path.Combine(AppContext.BaseDirectory, "profiles.json");

        public static AppState LoadState()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    var loaded = JsonSerializer.Deserialize<AppState>(
                        json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );
                    return loaded ?? new AppState();
                }
            }
            catch (Exception ex)
            {
                Logger.File($"[ProfileStore.Load] {ex}");
            }
            return new AppState();
        }

        public static void SaveState(AppState state)
        {
            try
            {
                var json = JsonSerializer.Serialize(
                    state,
                    new JsonSerializerOptions { WriteIndented = true }
                );
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                Logger.File($"[ProfileStore.Save] {ex}");
            }
        }
    }
}

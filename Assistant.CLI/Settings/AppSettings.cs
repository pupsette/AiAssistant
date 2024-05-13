using System.Text.Json.Serialization;

namespace Assistant.CLI.Settings
{
    internal class AppSettings
    {
        private const string settingsFile = "appsettings.json";
        private static readonly string[] localSettingsFiles = ["appsettings.local.json", "appsettings.development.json"];
        private static Lazy<AppSettings> appSettings = new Lazy<AppSettings>(LoadFromDisk);

        public static AppSettings Instance { get => appSettings.Value; } 

        public OpenAiSettings OpenAi { get; set; }

        public static AppSettings LoadFromDisk()
        {
#if DEBUG
            foreach (string file in localSettingsFiles)
            {
                if (TryLoadFromFile(file, out AppSettings? tmp))
                    return tmp!;
            }
#endif
            if (TryLoadFromFile(settingsFile, out AppSettings? result))
                return result!;

            return new AppSettings();
        }

        private static bool TryLoadFromFile(string fileName, out AppSettings? result)
        {
            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
            result = default;
            if (!File.Exists(fullPath))
                return false;

            using var file = File.OpenRead(fullPath);
            result = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(file, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            });
            return true;
        }
    }
}

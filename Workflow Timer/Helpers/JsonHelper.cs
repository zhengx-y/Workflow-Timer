using System.Text.Json;
using Workflow_Timer.Converters;
using System.IO;

namespace Workflow_Timer.Helpers
{
    public static class JsonHelper
    {
        private static readonly JsonSerializerOptions Options =
            new()
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                Converters =
                {
                    new TimeSpanJsonConverter()
                }
            };


        public static void Save<T>(string path, T data)
        {
            var json = JsonSerializer.Serialize(
                data,
                Options);

            File.WriteAllText(path, json);
        }


        public static T? Load<T>(string path)
        {
            if (!File.Exists(path))
                return default;


            var json = File.ReadAllText(path);

            return JsonSerializer.Deserialize<T>(
                json,
                Options);
        }
    }
}
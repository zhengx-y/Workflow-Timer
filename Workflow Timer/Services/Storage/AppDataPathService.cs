using System.IO;

namespace Workflow_Timer.Services.Storage
{
    public class AppDataPathService
    {
        private readonly string _root;


        public AppDataPathService()
        {
            _root = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                "Workflow Timer");


            Directory.CreateDirectory(_root);
        }


        public string PresetsPath =>
            CreateFolder("presets")
            + "\\presets.json";


        public string HistoryPath =>
            CreateFolder("history")
            + "\\history.json";


        public string SettingsPath =>
            CreateFolder("settings")
            + "\\settings.json";



        private string CreateFolder(string folder)
        {
            var path = Path.Combine(_root, folder);

            Directory.CreateDirectory(path);

            return path;
        }
    }
}
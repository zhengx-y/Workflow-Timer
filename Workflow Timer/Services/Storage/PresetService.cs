using Workflow_Timer.Models;

namespace Workflow_Timer.Services.Storage
{
    public class PresetService
    {
        private readonly AppDataPathService _paths;
        private readonly JsonStorageService _storage;

        public PresetService(AppDataPathService paths)
        {
            _paths = paths;
            _storage = new JsonStorageService();
        }

        public List<Preset> LoadPresets()
        {
            var presets = _storage.Load<List<Preset>>(
                _paths.PresetsPath);

            return presets ?? new List<Preset>();
        }

        public void SavePresets(List<Preset> presets)
        {
            _storage.Save(_paths.PresetsPath, presets);
        }
    }
}
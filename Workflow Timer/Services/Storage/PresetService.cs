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
            SaveAtomically(_paths.PresetsPath, presets);
        }

        public Guid LoadSelectedPresetId()
        {
            try { return _storage.Load<Guid>(_paths.SelectedPresetPath); }
            catch { return Guid.Empty; }
        }

        public void SaveSelectedPresetId(Guid id)
        {
            SaveAtomically(_paths.SelectedPresetPath, id);
        }

        private void SaveAtomically<T>(string path, T data)
        {
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                _storage.Save(temporary, data);
                global::System.IO.File.Move(temporary, path, overwrite: true);
            }
            finally
            {
                if (global::System.IO.File.Exists(temporary))
                    global::System.IO.File.Delete(temporary);
            }
        }
    }
}

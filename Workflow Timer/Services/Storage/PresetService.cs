using Workflow_Timer.Helpers;
using Workflow_Timer.Models;
using Workflow_Timer.Services.Storage;

public class PresetService
{
    private readonly AppDataPathService _paths;


    public PresetService(AppDataPathService paths)
    {
        _paths = paths;
    }


    public List<Preset> LoadPresets()
    {
        return JsonHelper.Load<List<Preset>>(
            _paths.PresetsPath)
            ?? new();
    }


    public void SavePresets(List<Preset> presets)
    {
        JsonHelper.Save(
            _paths.PresetsPath,
            presets);
    }
}
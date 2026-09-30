using Workflow_Timer.Helpers;

namespace Workflow_Timer.Services.Storage
{
    public class JsonStorageService
    {
        public T? Load<T>(string path)
        {
            return JsonHelper.Load<T>(path);
        }


        public void Save<T>(string path, T data)
        {
            JsonHelper.Save(path, data);
        }
    }
}
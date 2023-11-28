namespace DatasetFileUpload.Models;

using System.Text.Json;


public class RoCrateManifest
{
    private JsonDocument? manifest;

    public RoCrateManifest(string jsonString)
    {
        manifest = JsonDocument.Parse(jsonString);
    }

    public bool IsPublished(){
        return false;
    }
}
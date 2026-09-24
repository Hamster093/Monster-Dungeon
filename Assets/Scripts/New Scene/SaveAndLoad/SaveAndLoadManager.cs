using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

public class SaveAndLoadManager
{

    private static string GetPath(string key)
        => Path.Combine(Application.persistentDataPath, key + ".json");

    public static void Save<T>(string key, T data)
    {
        var json = JsonConvert.SerializeObject(data, Formatting.Indented);
        File.WriteAllText(GetPath(key), json);
    }

    public static T Load<T>(string key) where T : new()
    {
        var path = GetPath(key);
        if (!File.Exists(path)) return new T();
        var json = File.ReadAllText(path);
        return JsonConvert.DeserializeObject<T>(json) ?? new T();
    }
    public static bool Exists(string key)
        => File.Exists(GetPath(key));

    public static void Delete(string key)
    {
        var path = GetPath(key);
        if (File.Exists(path)) File.Delete(path);
    }
}

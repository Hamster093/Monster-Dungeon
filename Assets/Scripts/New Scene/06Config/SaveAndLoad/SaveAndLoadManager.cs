/****************************************************
    文件：GamePassEvent.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-09-25 19:26:04
	功能：存档与读档管理器
*****************************************************/
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

public class SaveAndLoadManager
{
    /// <summary>
    /// 根据存档键名生成对应的本地文件路径
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    private static string GetPath(string key)
        => Path.Combine(Application.persistentDataPath, key + ".json");
    /// <summary>
    /// 将指定的数据对象序列化并保存到本地
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="data"></param>
    public static void Save<T>(string key, T data)
    {
        var json = JsonConvert.SerializeObject(data, Formatting.Indented);
        File.WriteAllText(GetPath(key), json);
    }
    /// <summary>
    /// 从本地读取指定键名的存档数据，并反序列化为目标对象。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <returns></returns>
    public static T Load<T>(string key) where T : new()
    {
        var path = GetPath(key);
        if (!File.Exists(path)) return new T();
        var json = File.ReadAllText(path);
        return JsonConvert.DeserializeObject<T>(json) ?? new T();
    }
    /// <summary>
    /// 检查指定键名的存档文件是否已经存在于本地存储中。
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public static bool Exists(string key)
        => File.Exists(GetPath(key));
    /// <summary>
    /// 从本地删除指定键名的存档文件。
    /// </summary>
    /// <param name="key"></param>
    public static void Delete(string key)
    {
        var path = GetPath(key);
        if (File.Exists(path)) File.Delete(path);
    }
}

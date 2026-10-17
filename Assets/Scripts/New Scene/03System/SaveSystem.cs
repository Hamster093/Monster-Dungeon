/****************************************************
    文件：SaveSystem.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/17 14:40:41
	功能：存档系统（只存魔物实例）
*****************************************************/

using System.Collections.Generic;
using System.IO;
using UnityEngine;

[System.Serializable]
public class SaveData
{
    public List<MonsterInstance> monsters = new List<MonsterInstance>();
}

public static class SaveSystem
{
    private static string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

    public static void Save(MonsterStorageModel storage)
    {
        var data = new SaveData();
        data.monsters = new List<MonsterInstance>(storage.Items);

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);
        Debug.Log($"[SaveSystem] 存档成功，共 {data.monsters.Count} 只魔物 -> {SavePath}");
    }

    public static void Load(MonsterStorageModel storage)
    {
        if (!File.Exists(SavePath))
        {
            Debug.Log("[SaveSystem] 没有找到存档文件，跳过加载");
            return;
        }

        string json = File.ReadAllText(SavePath);
        var data = JsonUtility.FromJson<SaveData>(json);

        storage.Clear();
        foreach (var m in data.monsters)
        {
            storage.Reclaim(m); // 直接用你刚才写的方法塞回去
        }
        Debug.Log($"[SaveSystem] 读档成功，加载了 {data.monsters.Count} 只魔物");
    }
}

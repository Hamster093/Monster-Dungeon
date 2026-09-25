/****************************************************
    文件：ConfigDatabase.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/9/24 16:37:54
	功能：
*****************************************************/

using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;

public class ConfigDatabase
{
    public static ConfigDatabase Instance { get; private set; }

    public Dictionary<int, AdventurerConfig> Adventurers = new();
    public Dictionary<int, ContractConfig> Contracts = new();
    public Dictionary<int, MonsterConfig> Monsters = new();
    public Dictionary<int, ZoneConfig> Zones = new();
    public Dictionary<int, ItemConfig> Items = new();
    public Dictionary<int, DialogueOptionConfig> Dialogues = new();
    public System.Collections.Generic.Dictionary<int, ValidInfoConfig> ValidInfos = new();
    public Dictionary<int, InvestigationConfig> Investigations = new();
    public Dictionary<int, D20CheckConfig> D20Checks = new();
    public List<ScheduleConfig> Schedules = new();
    public Dictionary<int, MemoryConfig> Memories = new();

    /// <summary>
    /// 创建 ConfigDatabase 新实例并调用 LoadAll() 加载所有配置表。
    /// </summary>
    public static void Load()
    {
        Instance = new ConfigDatabase();
        Instance.LoadAll();
    }
    /// <summary>
    /// 批量加载所有配置表。
    /// </summary>
    private void LoadAll()
    {
        Adventurers = LoadDict<AdventurerConfig>("adventurers");
        Contracts = LoadDict<ContractConfig>("contracts");
        Monsters = LoadDict<MonsterConfig>("monsters");
        Zones = LoadDict<ZoneConfig>("zones");
        Items = LoadDict<ItemConfig>("items");
        Dialogues = LoadDict<DialogueOptionConfig>("dialogues");
        ValidInfos = LoadDict<ValidInfoConfig>("validInfos");
        Investigations = LoadDict<InvestigationConfig>("investigations");
        D20Checks = LoadDict<D20CheckConfig>("d20Checks");
        Schedules = LoadList<ScheduleConfig>("schedules");
        Memories = LoadDict<MemoryConfig>("memories");
    }
    /// <summary>
    /// 加载指定指定文件名的 JSON 配置到字典中
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="fileName"></param>
    /// <returns></returns>
    private Dictionary<int, T> LoadDict<T>(string fileName) where T : class
    {
        var list = LoadList<T>(fileName);
        var dict = new Dictionary<int, T>();
        var idField = typeof(T).GetField("id");
        foreach (var item in list)
        {
            int id = (int)idField.GetValue(item);
            dict[id] = item;
        }
        return dict;
    }
    /// <summary>
    /// 加载指定指定文件名的 JSON 返回列表
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="fileName"></param>
    /// <returns></returns>
    private List<T> LoadList<T>(string fileName)
    {
        var textAsset = Resources.Load<TextAsset>($"Config/{fileName}");
        if (textAsset == null)
        {
            Debug.LogError($"配置表缺失: Config/{fileName}.json");
            return new List<T>();
        }

        return JsonConvert.DeserializeObject<List<T>>(textAsset.text) ?? new List<T>();
    }
}

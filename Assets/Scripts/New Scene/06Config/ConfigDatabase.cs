/****************************************************
    文件：ConfigDatabase.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/9/24 16:37:54
	功能：管理配置表的加载和访问
*****************************************************/

using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

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
    public Dictionary<int, VisitOrder> VisitOrders = new();

    public List<MonsterDropConfig> MonsterDrops = new();
    public ShapeDatabase ShapeDb { get; private set; }

    /// <summary>
    /// 创建 ConfigDatabase 新实例并调用 LoadAll() 加载所有配置表。
    /// </summary>
    public static void Load()
    {
        Instance = new ConfigDatabase();
        Instance.LoadAll();

        var handle = Addressables.LoadAssetAsync<ShapeDatabase>("ShapeDatabase");
        Instance.ShapeDb = handle.WaitForCompletion();
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
        VisitOrders = LoadDict<VisitOrder>("visitOrders");

        MonsterDrops = LoadList<MonsterDropConfig>("monsterDrops");

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

        try
        {
            var settings = new JsonSerializerSettings
            {
                Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() }
            };

            return JsonConvert.DeserializeObject<List<T>>(textAsset.text) ?? new List<T>();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"解析配置表 [{fileName}] 失败！错误信息: {e.Message}");
            return new List<T>();
        }
    }

    /// <summary>
    /// 按阶段取配置，找不到返回 null
    /// </summary>
    public ScheduleConfig GetSchedule(int phase)
    {
        foreach (var s in Schedules)
            if (s.phase == phase) return s;
        return null;
    }
    /// <summary>
    /// 根据当前天 + 当前阶段，计算当前阶段剩余天数（不含今天）
    /// </summary>
    public int GetRemainingDays(int currentDay, int currentPhase)
    {
        int phaseStartDay = 1;
        foreach (var s in Schedules)
        {
            if (s.phase == currentPhase)
            {
                int dayInPhase = currentDay - phaseStartDay + 1;   // 阶段内第几天（从 1 开始）
                return s.dayCount - dayInPhase;                    // ← 去掉 +1
            }
            phaseStartDay += s.dayCount;
        }
        return 0;
    }

    /// <summary>
    /// 按阶段取待付分期金额
    /// </summary>
    public int GetInstallmentPayment(int currentPhase)
    {
        var s = GetSchedule(currentPhase);
        return s != null ? s.installmentAmount : 0;
    }

    /// <summary>
    /// 按魔物类型取掉落素材列表，找不到返回空列表
    /// </summary>
    public List<string> GetDrops(MonsterType type)
    {
        foreach (var d in MonsterDrops)
            if (d.monsterType == type) return d.materials;
        return new List<string>();
    }
}

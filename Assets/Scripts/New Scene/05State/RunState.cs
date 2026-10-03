/****************************************************
    文件：RunState.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/9/24 19:08:25
	功能：运行状态
*****************************************************/

using Newtonsoft.Json;
using System;
using System.Collections.Generic;

[Serializable]
public class RunState
{
    public int currentDay = 0;
    public int currentPhase = 1;
    public int cash = 2000;
    public int nutrient = 0;
    public int actionPoint = 5;
    public int remainingDays;
    public int installmentPayment;

    // 每个冒险者的运行状态，key = adventurerId
    public Dictionary<int, AdventurerRuntimeData> adventurers = new();

    // 每只魔物的运行状态，key = monsterId
    public Dictionary<int, MonsterState> monsters = new();

    // 道具库存，key=itemId, value=数量
    public Dictionary<int, int> itemInventory = new Dictionary<int, int>();

    // 当日已处理的来客
    public List<int> todayVisitors = new();
    public List<int> resolvedVisitors = new();
}


[Serializable]
public class MonsterState
{
    public int id;
    public bool alive = true;
    public bool 接待过今日 = false;
    public int 今日损耗 = 0;
}

[Serializable]
public class AdventurerRuntimeData
{
    // 关联静态配置表的 ID
    public int configId;

    // 运行时绑定，不参与序列化。加载存档后需要通过 configId 重新绑定。
    [JsonIgnore]
    public AdventurerConfig Config;

    // --- 原图所需的动态数据 ---
    public bool isDead = false;       // 存活/死亡
    public HealthState health = HealthState.Normal;
    public EquipmentState equipment = EquipmentState.Normal;
    public RelationshipState relationship = RelationshipState.Neutral;
    public RevengeLevel revenge = RevengeLevel.None;
    public bool inRevengeWindow = false; // 是否处于复仇窗口期

    // --- 互动计数 ---
    public int formalTalkCount = 0;     // 本次来访已搭话次数
    public int investigationCount = 0;  // 本次来访已调查次数

    // --- 信息与档案 ---
    public List<string> memoryTimeline = new();      //记忆时间线
    public List<string> historyDialogue = new();     // 历史对话
    public List<string> validInfo = new();           // 有效信息
    public List<string> investigationArchive = new(); // 已调查背景

    // --- 属性 ---
    // 当前七维数值
    public Dictionary<SevenDimension, int> currentSevenStats = new();

    // 无参构造函数（Newtonsoft.Json 反序列化必须要有）
    public AdventurerRuntimeData() { }

    // 游戏初始化时用的构造函数
    public AdventurerRuntimeData(AdventurerConfig config)
    {
        configId = config.id;
        Config = config;

        // 初始化七维属性
        if (config.initialStats != null)
        {
            currentSevenStats = new Dictionary<SevenDimension, int>(config.initialStats);
        }
    }
}

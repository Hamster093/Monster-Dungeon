/****************************************************
    文件：ConfigData.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/9/24 16:30:54
	功能：
*****************************************************/

using System;
using System.Collections.Generic;

[Serializable]
/// <summary>
/// 冒险者/小队固定配置。
/// 每个单位一条，全局固定 7 个，随阶段解锁。
/// </summary>
public class AdventurerConfig
{
    public int id;
    public string name;
    public bool isParty;                           // 否为小队单位
    public Level baseLevel;                        // 标称等级 S/A/B/C/D，表示基础战力</summary>
    public List<PersonalityAxis> personalityAxes;  // 突出的2~3条
    public List<int> visitDays;                    // 固定来访日，如 [3,10,17,24]
    public int contractId;                         // 默认委托 ID
    public List<string> backgrounds;               // 三条固定背景，按顺序
    public string successorType;                   // 接任类型"
    public SuccessorDimension mainDimension;
    public SuccessorDimension secondaryDimension;  // 主要接任维度
    public Dictionary<SuccessorDimension, int> initialStats; // 六维初始值
}

/// <summary>
/// 委托固定配置。每名冒险者对应一条固定委托。
/// 委托内容、目标、酬劳不随机，随机只影响结算结果。
/// </summary>
[Serializable]
public class ContractConfig
{
    public int id;
    public int adventurerId;        // 关联冒险者ID
    public int targetMonsterId;     // 目标魔物 ID
    public int targetZoneId;        // 目标分区 ID
    public int baseReward;          // 基础酬劳
    public string description;      // 委托描述文本
    public string specialConditions; // 特殊条件，可空。如"必须活捉"
}

/// <summary>
/// 魔物固定配置。魔物日常状态稳定，不持续产生健康/装备修正。
/// </summary>
[Serializable]
public class MonsterConfig
{
    public int id;
    public string name;
    public Level level;             // 魔物等级 S/A/B/C/D
    public int zoneId;              // 所属分区 ID
    public int nutrientValue;       // 冒险者死亡后产生养分
    public int materialValue;       // 材料价值
    public int dailyLimit = 1;      // 每日接待上限
}
/// <summary>
/// 地牢分区固定配置。
/// </summary>
[Serializable]
public class ZoneConfig
{
    public int id;
    public string name;
    public int dangerLevel;          // 区域危险等级
    public List<int> monsterIds;     //该分区可出现的魔物 ID
    public string routeDescription;  //路线描述文本
}

/// <summary>
/// 道具固定配置。用于背包、商店、调查、陷阱等。
/// </summary>
[Serializable]
public class ItemConfig
{
    public int id;
    public string name;
    public ItemType type;           // 道具类型：调查 / 检定操纵 / 陷阱 / 额外行动点
    public int price;               // 价格
    public CurrencyType currency;   // 货币类型：现金 / 养分
    public string effect;           // 如 "+1D20"、"Reroll"、"LockMin"
    public int maxPerDay;           // 每日限购数量
    public string usableIn;         // 可用场景："Reception" 接待界面 / "Prepare" 准备窗口
}

// <summary>
/// 对话选项固定配置。前置对话与正式搭话共用。
/// </summary>
[Serializable]
public class DialogueOptionConfig
{
    public int id;
    public int adventurerId;        // 关联冒险者 ID 0 表示通用
    public DialogueStage stage;     // 对话阶段：前置 / 正式
    public int costAP;              // 消耗行动点。前置对话为 0 正式按第 1~5 次递增
    public string text;             // 选项文本，显示给玩家
    public int checkTarget;         // D20 目标值。前置对话为 0，不检定
    public string successText;      // 检定成功时的回答整句
    public string failText;         // 检定失败时的回答整句
    public int validInfoId;         // 成功且有效时关联的有效信息 ID，0 表示不产出有效信息
    /// <summary>
    /// 接任数值变化，如 { 欲望:+5, 信任:-3 }。
    /// 同次来访最多推动一次。JsonUtility 不支持 Dictionary，
    /// 若用 JsonUtility 需改为 List&lt;StatEntry&gt;。
    /// </summary>
    public Dictionary<SuccessorDimension, int> statChanges; // 接任数值变化
}

/// <summary>
/// 有效信息固定配置。搭话成功后提取的完整句子，默认未核实。
/// </summary>
[Serializable]
public class ValidInfoConfig
{
    public int id;
    public int adventurerId;
    public string text;             // 信息原文
    public bool verified = false;   // 默认未核实
}

/// <summary>
/// 调查档案固定配置。每名冒险者三条固定背景，按顺序解锁。
/// </summary>
[Serializable]
public class InvestigationConfig
{
    public int id;
    public int adventurerId;
    public int order;               // 1/2/3，按顺序解锁
    public string text;
    public int costAP = 1;
    public int itemId;              // 需要的调查道具
}

// <summary>
/// D20 检定固定配置。套话、冒险、报复共用一套检定逻辑。
/// </summary>
[Serializable]
public class D20CheckConfig
{
    public int id;
    public CheckType checkType;     // 检定类型：套话 / 冒险 / 报复
    public int baseTarget;          // 基础目标值
    public string levelModifiers;   // 等级差修正，可后续改为结构化
    public string stateModifiers;   // 状态修正（正常/受损/濒危）
    public string routeModifiers;   // 路线修正（真实/编造）
    public string resultBands;      // 如 "1-2:大失败,3-10:失败,11-18:成功,19-20:大成功"
}

/// <summary>
/// 阶段日程固定配置。共 5 阶段，天数 3/4/5/7/8，共 27 天。
/// </summary>
[Serializable]
public class ScheduleConfig
{
    public int phase;               // 1~5
    public int dayCount;            // 3/4/5/7/8
    public List<int> unlockAdventurerIds; //本阶段解锁的冒险者 ID ，形成 3→4→5→6→7
    public int installmentAmount;   // 本阶段分期费用
    public int deadlineDay;         // 本阶段截止日，即最后一天天号
}

[Serializable]
public class MemoryConfig
{
    public int id;
    public MemoryTrigger trigger;     // 触发条件类型
    public string text;               // 如"被主人误导"
    public RevengeLevel revengeLevel; //该记忆对应的报复等级：无 / 轻度 / 重度
}

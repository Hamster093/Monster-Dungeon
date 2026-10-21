/****************************************************
    文件：ConfigData.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/9/24 16:30:54
	功能：游戏的基础数据类
*****************************************************/

using System;
using System.Collections.Generic;
using UnityEngine.TextCore.Text;

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
    public int contractId;                         // 默认委托 ID
    public List<string> backgrounds;               // 三条固定背景，按顺序
    public string successorType;                   // 接任类型"
    public SevenDimension mainDimension;       // 主要接任维度
    public SevenDimension secondaryDimension;  // 次要接任维度
    public Dictionary<SevenDimension, int> initialStats; // 七维初始值

    public string IllustrationName;  //角色立绘名字
    public string briefStory; // 简要的角色背景故事

}

/// <summary>
/// 来访顺序配置，固定游戏流程 程序应判断是否已解锁该角色
/// </summary>
[Serializable]
public class VisitOrder
{
    public int id;                // 天数
    public List<int> visitors;    // 当天固定的来客 id 列表
}
/// <summary>
/// 委托固定配置。每名冒险者对应一条固定委托。
/// 委托内容、目标、酬劳不随机，随机只影响结算结果。
/// </summary>
[Serializable]
public class ContractConfig
{
    public string specialKind; // Gather / Battle / Legacy；普通剧情委托留空。
    public int minPhase = 1;
    public Dictionary<SevenDimension,int> statDeltas = new();
    public int id;                       // 委托 ID
    public string name;                  // 委托名
    public string description;           // 委托描述
    public int targetMonsterId; // 旧档目标及默认展示兼容；接单按类别判定。
    public MonsterType? targetMonsterType;
    public int reward;                   // 委托报酬（100% 完成度时）

    // 影响属性，空 = 普通委托，非空 = 特殊委托
    public List<SevenDimension> influenceStats = new List<SevenDimension>();
}

/// <summary>
/// 魔物固定配置。魔物日常状态稳定，不持续产生健康/装备修正。
/// </summary>
[Serializable]
public class MonsterConfig
{
    public int id;
    public string name;
    public MonsterType monsterType;   // 魔物类型：史莱姆 / 哥布林 / 犬类 / 植物类 / 构造类 / 龙
    public Level level;               // 魔物等级 S/A/B/C/D

    public int materialValue;         // 分解金币（暂定）
    public int buyPrice;              // 购买金币（暂定）
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
    public int maxPerDay;           // 每日限购数量

    // ---------- 商店/背包相关 ----------
    public string description;          // 物品描述（待文案补充时留空即可）
    public string iconAddress;          // 物品图片的 Addressables 地址
    public int stock = -1;              // -1 = 无限库存；>=0 = 初始库存
    public int restockPerPhase = 0;     // 每个经营阶段补充数量，0 = 不补充
    public int maxStack = -1;           // 背包堆叠上限，-1 = 无限

    public int materialValue;   // 拆解价值（只有魔物用得到，物品填 0）
    public string shapeId; //魔物形状 ID，用于背包拖拽和放置
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



/****************************************************
    文件：RunState.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/9/24 19:08:25
	功能：运行状态
*****************************************************/

using System;
using System.Collections.Generic;

[Serializable]
public class RunState
{
    public int currentDay = 1;
    public int currentPhase = 1;
    public int cash = 2000;
    public int nutrient = 0;
    public int actionPoint = 5;

    // 每个冒险者的运行状态，key = adventurerId
    public Dictionary<int, AdventurerState> adventurers = new();

    // 每只魔物的运行状态，key = monsterId
    public Dictionary<int, MonsterState> monsters = new();

    // 道具库存，key=itemId, value=数量
    public Dictionary<int, int> itemInventory = new Dictionary<int, int>();

    // 当日已处理的来客
    public List<int> todayVisitors = new();
    public List<int> resolvedVisitors = new();
}

[Serializable]
public class AdventurerState
{
    public int id;
    public HealthState health = HealthState.Normal;
    public EquipmentState equipment = EquipmentState.Normal;
    public RelationshipState relationship = RelationshipState.Neutral;
    public RevengeLevel revenge = RevengeLevel.None;
    public bool inRevengeWindow = false;
    public int formalTalkCount = 0;     // 本次来访已搭话次数
    public int investigationCount = 0;  // 本次来访已调查次数
    public List<string> memoryTimeline = new();
    public List<string> historyDialogue = new();
    public List<string> validInfo = new();
    public List<string> investigationArchive = new();
    public Dictionary<SuccessorDimension, int> successorStats = new();
}

[Serializable]
public class MonsterState
{
    public int id;
    public bool alive = true;
    public bool 接待过今日 = false;
    public int 今日损耗 = 0;
}
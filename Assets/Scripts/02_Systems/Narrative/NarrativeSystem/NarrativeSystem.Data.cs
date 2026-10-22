/****************************************************
    文件：NarrativeSystem.Data.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/22
	功能：27天剧情接线层·嵌套数据类：场景规则、存档、检定节点、继任与立绘配置
/*****************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using Ink.Runtime;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 27天剧情的游戏接线层。生命周期由 SystemManager 管理；对话、商店、委托、棋盘收入和结算
/// 均复用原系统，不另建 UI。数值留在 Resources/Narrative/rules.json，原稿 action 标签统一接线。
/// 本类以 partial 拆分为多个文件，同功能代码用 #region 分组。
/// </summary>
public sealed partial class NarrativeSystem
{
    #region 剧情场景与规则配置

    public sealed class SceneRule
    {
        public string id, actor, title, commissionPath, commissionName, description, objective, payment;
        public int day, order, targetMonsterId, contractId, reward;
        public MonsterType? targetMonsterType;
    }
    public sealed class Rules
    {
        public int attributeDelta = 3;
        public int[] lensUseLimit = { 2, 2, 4, 4, 4 };
        public Dictionary<string,string> textOverrides = new Dictionary<string,string>();
        public int evolutionDelta = 5;
        public Dictionary<string,List<SuccessionCondition>> successionRules;
        public int successionNearGap=20,successionChanceStart=95,successionChanceStep=3;
        public Dictionary<string,string> endingCG;
        public Dictionary<string,Dictionary<string,string>> portraitVariants;
        public int blackCrystalPayment; // 原稿 money=config 未给金额，0 表示暂不添加这笔金钱。
        public List<SceneRule> scenes = new List<SceneRule>();
        public Dictionary<string, int[]> initialStats = new Dictionary<string, int[]>();
        public Dictionary<string, string> portraits = new Dictionary<string, string>();
        public Dictionary<string, string> introductions = new Dictionary<string, string>();
    }
    [Serializable]
    public sealed class SaveData
    {
        public string ink, speaker, stage = "playing", ending = "";
        public int uiVersion;
        public int endingCandidate=-1;
        public string endingExpression;
        public bool successionFailed;
        public string portraitAddress;
        public string endingPresentationPhase;
        public string audioContext = "reception";
        public string checkId; // 原稿中实际掷骰调用的稳定地址。
        public int lensDay = -1, lensDailyUses;
        public List<DialogueLine> history = new List<DialogueLine>();
        public List<MonsterInstance> storedInstances;
        public DialogueData frame;
        public SettlementData settlement;
        public HashSet<int> settledDays = new HashSet<int>(), paidPhases = new HashSet<int>();
        public Dictionary<int, int> lensUses = new Dictionary<int, int>();
        public List<CommissionEntry> normal = new List<CommissionEntry>(), special = new List<CommissionEntry>();
        public Dictionary<int, int> returnStates = new Dictionary<int, int>();
        public int activeRevengeActor=-1;
        public Dictionary<int, int> revengeDue = new Dictionary<int, int>();
        public List<InventorySlot> inventory = new List<InventorySlot>();
        public List<BoardSlot> board = new List<BoardSlot>();
        public List<int> storedMonsters = new List<int>();
        public int departingVisitor = -1;
        public int currentVisitor = -1;
        public int specialDetailId;
        public string panel;
        public RouteSelectionPanel.RouteState pendingRoute;
        public string routedScene;
        public bool routedWrong;
        public string topLeftPanel;
        public int displaySerial, archiveActor = -1, archiveTab;
        public bool archiveOpen;
        public bool returningFromLens;
        public HashSet<string> displayed = new HashSet<string>();
        public HashSet<string> registeredInfo = new HashSet<string>();
        public Dictionary<string, string> backgroundLevels = new Dictionary<string, string>();
    }
    [Serializable] public sealed class InventorySlot { public int id, count; }
    [Serializable] public sealed class BoardSlot { public string shape, uid; public bool evolved; public string evolutionKind; public int usedDay = -1; public int x, y, rotation; }


    #endregion

    #region 玩家检定节点配置


    [Serializable] public sealed class PlayerChoiceRule
    {
        public string choiceId,attribute,kind,label,topic,checkId;
        public bool credit;
    }
    [Serializable] public sealed class PlayerCheckRule
    {
        public string checkId,kind,attribute,actor;
        public bool player;
    }
    [Serializable] public sealed class PlayerNodeConfig
    {
        public List<PlayerChoiceRule> choices;
        public List<PlayerCheckRule> checks;
    }
    public sealed class PlayerCheckContext
    {
        public string kind,attribute,thoughtNote;
        public int target,modifier,attributeBonus,thoughtBonus;
        public int thought=-1;
        public bool critical=true,player,hadSwitchOpportunity;
    }

    #endregion

    #region 继任与立绘数据

    [Serializable] public sealed class SuccessionCondition { public SevenDimension stat; public int value; public bool maximum; }
    public sealed class SuccessionAssessment
    {
        public int actorId,gap,target,modifier,chance;
        public string actor,name,tendency,progress,attribute;
        public bool dead;
        public string Annotation => "（检定—"+PlayerBuildState.AttributeName(attribute)+"　难度："+Difficulty(chance)+"）";
    }
    [Serializable] public sealed class PortraitCue { public string actor,pose; public bool injury,hide; }

    #endregion
}

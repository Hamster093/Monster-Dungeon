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
using System.Diagnostics;
using System.Linq;

[Serializable]
public class RunState
{
    public long savedUtc; // UTC 时间戳，界面按本地时间显示。
    public int saveVersion = 9;
    public PlayerBuildState player = new PlayerBuildState();
    public int tradeDay = -1, tradePhase;
    public Dictionary<int,int> shopStock = new();
    public List<int> monsterOffers = new();
    public string pendingEvolutionUid;
    public string pendingEvolutionKind = ""; // 尚未提交的进化方向，与实例选择一并保存。
    public D100Service.Presentation pendingDice;
    public Dictionary<int,int> injuryAppliedDay = new();

    // 读档恢复前只认快照，恢复完成后只认活世界，不能混合两份库存。
    [JsonIgnore] public bool BusinessWorldReady { get; private set; }
    public void MarkBusinessWorldReady() => BusinessWorldReady = true;
    private sealed class ShopDemandRules { public List<NarrativeSystem.SceneRule> scenes = new(); }
    private sealed class ShopOwnershipSnapshot
    {
        public List<NarrativeSystem.BoardSlot> board;
        public List<MonsterInstance> storedInstances;
        public List<int> storedMonsters;
    }
    private static List<NarrativeSystem.SceneRule> _shopDemandScenes;

    /// <summary>按实际经营日刷新，结果写入存档；重开面板或同日读档绝不刷新库存。</summary>
    public void EnsureBusinessDay()
    {
        var cfg=ConfigDatabase.Instance;
        if(cfg==null)return;
        if(tradeDay==currentDay)return; // 包括旧档：当天已有货单与剩余库存全部保留。
        if(tradePhase!=currentPhase)
        {
            tradePhase=currentPhase;
            shopStock[1]=new[]{3,3,4,5,6}[UnityEngine.Mathf.Clamp(currentPhase-1,0,4)];
            shopStock[7]=3;
        }
        tradeDay=currentDay;monsterOffers.Clear(); // 特殊委托已撤除，只刷新魔物商店。
        var rng=new System.Random(Guid.NewGuid().GetHashCode());
        int maxLevel=UnityEngine.Mathf.Clamp(currentPhase,1,4);
        var monsters=new List<int>();
        foreach(var m in cfg.Monsters.Values)if((int)m.level<=maxLevel)monsters.Add(m.id);
        var guaranteed=SelectGuaranteedMonsterIds(ShopDemandScenes,currentDay,maxLevel,
            LivingShopActors(),AvailableMonsterTypes(cfg),cfg.Monsters.Values,cfg.Items);
        // 最多两个货位保障缺少的类别；其余仍随机，既不预留实例，也不补够每名访客。
        monsterOffers.AddRange(guaranteed);
        monsters.RemoveAll(id=>monsterOffers.Contains(id));
        Shuffle(monsters,rng);
        monsterOffers.AddRange(monsters.GetRange(0,Math.Min(4-monsterOffers.Count,monsters.Count)));
        foreach(int id in monsterOffers)shopStock[id]=1;
    }

    private static List<NarrativeSystem.SceneRule> ShopDemandScenes
    {
        get
        {
            if(_shopDemandScenes==null)
            {
                // 直接缓存现有配置，不依赖剧情模块已初始化或已进入当天接待。
                var asset=UnityEngine.Resources.Load<UnityEngine.TextAsset>("Narrative/rules");
                _shopDemandScenes=asset?JsonConvert.DeserializeObject<ShopDemandRules>(asset.text).scenes:new List<NarrativeSystem.SceneRule>();
            }
            return _shopDemandScenes;
        }
    }

    private HashSet<string> LivingShopActors()
    {
        string[] codes={"MY","FE","DW","SA","AU","DA","UN"};
        var living=new HashSet<string>();
        for(int i=0;i<codes.Length;i++)
            if(adventurers.TryGetValue(i+1,out var actor)&&!actor.isDead)living.Add(codes[i]);
        return living;
    }

    private HashSet<MonsterType> AvailableMonsterTypes(ConfigDatabase cfg)
    {
        var types=new HashSet<MonsterType>();
        void Add(int id,int usedDay)
        {
            if(usedDay!=currentDay&&cfg.Monsters.TryGetValue(id,out var monster))types.Add(monster.monsterType);
        }
        if(BusinessWorldReady)
        {
            if(BackpackBoard.Instance!=null)
                foreach(var view in BackpackBoard.Instance.Items)
                    if(view!=null&&!view.IsGhost&&view.Item?.Def!=null&&int.TryParse(view.Item.Def.Id,out int id))Add(id,view.Item.UsedDay);
            foreach(var instance in MonsterStorageModel.Instance.Items)Add(instance.monsterId,instance.usedDay);
        }
        else if(!string.IsNullOrEmpty(narrativeSave))
        {
            var snapshot=JsonConvert.DeserializeObject<ShopOwnershipSnapshot>(narrativeSave);
            if(snapshot.board!=null)
                foreach(var slot in snapshot.board)if(int.TryParse(slot.shape,out int id))Add(id,slot.usedDay);
            if(snapshot.storedInstances!=null)
                foreach(var instance in snapshot.storedInstances)Add(instance.monsterId,instance.usedDay);
            else if(snapshot.storedMonsters!=null)
                foreach(int id in snapshot.storedMonsters)Add(id,-1); // 旧档没有实例使用日，沿用原迁移口径。
        }
        return types;
    }

    /// <summary>纯候选计算：按存活访客首次需求顺序，选至多两个缺失类别的最低等级、低价、低ID商品。</summary>
    public static List<int> SelectGuaranteedMonsterIds(IEnumerable<NarrativeSystem.SceneRule> scenes,int day,int maxLevel,
        ISet<string> livingActors,ISet<MonsterType> availableTypes,IEnumerable<MonsterConfig> monsters,
        IReadOnlyDictionary<int,ItemConfig> items)
    {
        var all=monsters.ToDictionary(m=>m.id);
        var candidates=all.Values.Where(m=>(int)m.level<=maxLevel&&items.ContainsKey(m.id))
            .OrderBy(m=>(int)m.level).ThenBy(m=>items[m.id].price).ThenBy(m=>m.id).ToList();
        var seen=new HashSet<MonsterType>();
        var selected=new List<int>();
        foreach(var scene in scenes.Where(s=>s.day==day&&livingActors.Contains(s.actor)).OrderBy(s=>s.order))
        {
            MonsterType type;
            if(scene.targetMonsterType.HasValue)type=scene.targetMonsterType.Value;
            else if(all.TryGetValue(scene.targetMonsterId,out var target))type=target.monsterType;
            else continue;
            if(!seen.Add(type)||availableTypes.Contains(type))continue;
            var candidate=candidates.FirstOrDefault(m=>m.monsterType==type);
            if(candidate==null)continue;
            selected.Add(candidate.id);
            if(selected.Count==2)break;
        }
        return selected;
    }
    private static void Shuffle<T>(List<T> list,Random rng)
    {for(int i=list.Count-1;i>0;i--){int j=rng.Next(i+1);(list[i],list[j])=(list[j],list[i]);}}

    public int currentDay = 0;
    public int currentPhase = 1;
    public int cash = 2000;
    public int nutrient = 0;
    public int actionPoint = 5;
    public int remainingDays;
    public int installmentPayment;
    public int correctAnswerCount;// 本次来访已答话正确次数
    // Ink 进度、当前显示节点、当日委托及棋盘快照；旧存档没有此字段时按新剧情开局。
    public string narrativeSave;

    /// <summary>
    /// 累计答对次数
    /// </summary>
    public void AddCorrectAnswer()
    {
        correctAnswerCount++; 
    }

    public int GetCorrectAnswerCount()=> correctAnswerCount;
    

    // 每个冒险者的运行状态，key = adventurerId
    public Dictionary<int, AdventurerRuntimeData> adventurers = new();
    /// <summary>
    /// 为当前访客增加指定属性值
    /// </summary>
    public void AddAdventurerStat(SevenDimension stat, int value)
    {
        ModifyCurrentVisitorStat(stat, value);
    }
    /// <summary>
    /// 为当前访客减少指定属性值
    /// </summary>
    public void ReduceAdventurerStat(SevenDimension stat, int value)
    {
        ModifyCurrentVisitorStat(stat, -value);
    }
    /// <summary>
    /// 内部统一修改逻辑，避免重复代码
    /// </summary>
    private void ModifyCurrentVisitorStat(SevenDimension stat, int delta)
    {
        int id = SystemManager.Instance.VisitSystem.GetCurrentVisitorId();

        if (!adventurers.ContainsKey(id))
        {
            return;
        }

        var data = adventurers[id];
        int before = data.currentSevenStats[stat];
        data.currentSevenStats[stat] = UnityEngine.Mathf.Clamp(before + delta, 0, 100);
        int actual = data.currentSevenStats[stat] - before;
        if (actual != 0) ToastManager.Show(data.Config.name + "的" + SevenDimensionDisplay.GetName(stat) + (actual > 0 ? "上升了。" : "下降了。"), Guid.NewGuid().ToString());
        TopBarRefreshEvent.Trigger();
    }

    public int GetAdventurerRuntime( SevenDimension seven)=>adventurers[SystemManager.Instance.VisitSystem.GetCurrentVisitorId()].currentSevenStats[seven];
    



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
    public bool isReception = false;
}

[Serializable]
public class AdventurerRuntimeData
{
    // 关联静态配置表的 ID
    public int configId;
    public int trust = 50; // 旧档缺字段也以50初始化，范围0～100。

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

    // --- 伤势 ---
    public bool isSeverelyInjured = false;   // 是否处于重伤状态
    public int severeInjuryDelay = 0;        // 重伤恢复倒计时，0 = 已恢复

    // --- 属性 ---
    // 当前七维数值
    public Dictionary<SevenDimension, int> currentSevenStats = new();

    // 无参构造函数（Newtonsoft.Json 反序列化必须要有）
    public AdventurerRuntimeData() { }

    // 游戏初始化时用的构造函数
    public AdventurerRuntimeData(AdventurerConfig config)
    {
        configId = config.id;
        trust=D100Service.Balance.initialTrust;
        Config = config;

        // 初始化七维属性
        if (config.initialStats != null)
        {
            currentSevenStats = new Dictionary<SevenDimension, int>(config.initialStats);
        }
    }
}

/// <summary>玩家自身状态与冒险者七维分开；说明6的思维按阶段确认，不再随熟练度增强。</summary>
[Serializable]
public sealed class PlayerBuildState
{
    public bool created;
    public int aptitude = -1, thought = -1;
    public int perception = 35, knowledge = 35, willpower = 35;
    // 搭话培养只记录本版本新增的幅度；旧属性不追补、不回退。
    public Dictionary<string,int> talkGrowth = new Dictionary<string,int>();
    public HashSet<string> trainedTalkEvents = new HashSet<string>();
    public HashSet<string> trainedReceptionAttributes = new HashSet<string>();
    public bool TrainInformation(string eventId,string reception,string attribute,bool success)
    {
        if (!success || (attribute!="perception" && attribute!="knowledge" && attribute!="willpower")) return false;
        if (talkGrowth==null) talkGrowth=new Dictionary<string,int>();
        if (trainedTalkEvents==null) trainedTalkEvents=new HashSet<string>();
        if (trainedReceptionAttributes==null) trainedReceptionAttributes=new HashSet<string>();
        string once=reception+":"+attribute;
        if (trainedTalkEvents.Contains(eventId)||trainedReceptionAttributes.Contains(once)) return false;
        talkGrowth.TryGetValue(attribute,out int grown);if(grown>=8)return false;
        trainedTalkEvents.Add(eventId);trainedReceptionAttributes.Add(once);talkGrowth[attribute]=grown+1;
        if(attribute=="perception")perception++;else if(attribute=="knowledge")knowledge++;else willpower++;
        return true;
    }
    public int proficiency, failureStreak;
    public HashSet<string> creditedChoices = new HashSet<string>();
    public HashSet<string> resolvedChecks = new HashSet<string>();
    public string pendingChoiceId, pendingAttribute;
    public bool pendingChoiceCredit;
    public string influenceKey;
    public int influenceBonus, influenceActor;
    public bool influenceCakeDefault;
    public int thoughtConfirmedPhase;
    public bool switchOpportunity;
    public int switchOpportunityActor = -1, switchOpportunityDay = -1;
    public string switchOpportunityScene, switchOpportunityAttribute;
    // 已建立的玩家检定单独冻结思维上下文，读档、重投或预览不能重新消费机会。
    public ThoughtCheckSnapshot pendingThoughtCheck;
    [Serializable] public sealed class ThoughtCheckSnapshot
    {
        public string key, kind, attribute, scene;
        public int actorId, day, thought = -1;
        public bool hadSwitchOpportunity;
    }
    public int Level => proficiency >= 25 ? 3 : proficiency >= 10 ? 2 : 1;
    public static readonly string[] AttributeNames = { "感知", "学识", "精神力" };
    public static readonly string[] ThoughtNames = { "哈耳摩尼亚史官", "戚风蛋糕谈判术", "地牢精神病人" };
    public static string AttributeName(string key) => key == "perception" ? "感知" : key == "knowledge" ? "学识" : "精神力";
    public int Attribute(string key) => key == "perception" ? perception : key == "knowledge" ? knowledge : willpower;
    public string PreferredAttribute => aptitude == 0 ? "perception" : aptitude == 1 ? "knowledge" : "willpower";
    public bool Active(int day) => created && day >= 1 && day <= 27;
    public bool ThoughtActive(int day, int phase) => Active(day) && day >= 2 && thought >= 0 && thoughtConfirmedPhase == phase;
    public bool Create(int type, int mode = -1)
    {
        if (created || type < 0 || type > 2 || mode < -1 || mode > 2) return false;
        aptitude = type; thought = mode; perception = knowledge = willpower = 35;
        if (type == 0) perception = 55; else if (type == 1) knowledge = 55; else willpower = 55;
        created = true; return true;
    }
    public bool Credit(string id)
    {
        if (string.IsNullOrEmpty(id) || !creditedChoices.Add(id)) return false;
        return true; // 只保留旧事件去重接口，不再通过剧情选择累积等级。
    }
    // 兼容旧的事件登记调用；不再增长熟练度，预览、取消、重显与重投仍不能调用。
    public void Resolve(string key, bool success)
    {
        if (!resolvedChecks.Add(key)) return;
        if (pendingChoiceCredit) Credit(pendingChoiceId);
        pendingChoiceCredit = false;
        failureStreak = 0; // 旧存档字段保留，但不再提供旧版失败累计加成。
    }
    public int ThoughtModifier(string kind, bool hasInformation)
    {
        return 0; // 新思维需要人物/接待/属性上下文，由NarrativeSystem统一计算。
    }
    public bool HasSwitchOpportunity(int actorId, int day, string scene) => switchOpportunity &&
        switchOpportunityActor == actorId && switchOpportunityDay == day && switchOpportunityScene == scene;
    public void ClearSwitchOpportunity()
    {
        switchOpportunity = false; switchOpportunityActor = switchOpportunityDay = -1;
        switchOpportunityScene = switchOpportunityAttribute = null;
    }
    public void EndReceptionThoughts()
    {
        ClearSwitchOpportunity(); influenceKey = null; influenceBonus = 0; influenceCakeDefault = false;
        pendingThoughtCheck = null;
    }
    /// <summary>只在本次检定最终确认时结束上次机会；再次失败只建立一个新机会，不累计。</summary>
    public bool ResolveThoughtCheck(ThoughtCheckSnapshot context, bool success)
    {
        if (!resolvedChecks.Add(context.key)) return false;
        pendingChoiceCredit = false;
        if (context.kind == "information" || context.kind == "influence")
        {
            ClearSwitchOpportunity();
            if (context.thought == 2 && !success)
            {
                switchOpportunity = true; switchOpportunityActor = context.actorId; switchOpportunityDay = context.day;
                switchOpportunityScene = context.scene; switchOpportunityAttribute = context.attribute;
            }
        }
        pendingThoughtCheck = null;
        return true;
    }
    public string EffectDescription()
    {
        if (thought == 0) return "该人物已有至少2条有效信息时，普通影响检定可改用你的擅长属性，自动采用较高值。";
        if (thought == 1) return "普通影响检定修正＋10。检定成功后，原定为±3的七维变化改为±2；单独标明的其他变化照常。";
        if (thought == 2) return "信息或影响检定失败后，本次接待的下一次信息或影响检定，若改用另一属性，修正＋10。效果保留到下一次检定。";
        return "当前思维：尚未采用";
    }
}

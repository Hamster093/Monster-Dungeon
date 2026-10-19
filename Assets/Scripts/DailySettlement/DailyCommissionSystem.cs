/****************************************************
    文件：DailyCommissionSystem.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/15 17:15:54
	功能：每日委托收益系统。普通委托队列，跨天清空；特殊委托已撤除
*****************************************************/

using System.Collections.Generic;
using System;
using System.Linq;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;

public class DailyCommissionSystem
{
    private readonly List<CommissionEntry> _normalCommissions = new List<CommissionEntry>();

    public IReadOnlyList<CommissionEntry> NormalCommissions => _normalCommissions;
    public IReadOnlyList<CommissionEntry> SpecialCommissions => Array.Empty<CommissionEntry>(); // 旧工具兼容，只读空集合。

    private ConfigDatabase Config => ConfigDatabase.Instance;

    public bool IsEmpty => _normalCommissions.Count == 0;


    // ============================================================
    //  Add 接口（接委托时调用）
    // ============================================================

    public void AddNormal(CommissionEntry entry)
    {
        if (entry == null) return;
        if (!_normalCommissions.Exists(e => e.contractId == entry.contractId && e.adventurerId == entry.adventurerId)) _normalCommissions.Add(entry);
    }

    public void RemoveNormal(int contractId, int adventurerId) => _normalCommissions.RemoveAll(e => e.contractId == contractId && e.adventurerId == adventurerId);

    public void RemoveNormal(int adventurerId) => _normalCommissions.RemoveAll(e => e.adventurerId == adventurerId);

    public void AddNormal(int contractId, int adventurerId, float completionRate = 1f)
    {
        AddNormal(new CommissionEntry(contractId, adventurerId, completionRate));
    }

    // 旧工具兼容入口，正式游戏已没有特殊委托派发路径。
    public void AddSpecial(CommissionEntry entry) { }
    public void AddSpecial(int contractId, int adventurerId, float completionRate = 1f) { }

    /// <summary>
    /// 外部（检定系统）回调里用它来设置完成度
    /// isSpecial = true 走特殊队列
    /// </summary>
    public void SetCompletionRate(bool isSpecial, int contractId, float rate)
    {
        if (isSpecial) return;
        var list = _normalCommissions;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].contractId == contractId)
            {
                list[i].completionRate = Mathf.Clamp(rate,0,D20Service.Balance.criticalIncome);
                return;
            }
        }
        Debug.LogWarning($"[DailyCommissionSystem] 找不到委托 {contractId} 来设置完成度");
    }

    // ============================================================
    //  结算
    // ============================================================

    /// <summary>
    /// 普通委托只检定战斗伤势，报酬使用确定公式。
    /// </summary>
    public bool CanOfferSpecial(int contractId, int actor) => false;
    public int SpecialChance(int contractId, int actor, PlacedItem item = null) => 0;
    private IEnumerable<CommissionEntry> All() => _normalCommissions;
    /// <summary>同步接口留给旧流程/规则测试；正式界面使用同一数据的异步结果确认。</summary>
    public void ResolveAllChecks()
    {
        foreach(var e in All().Where(e=>!e.checksResolved))
        {
            var cfg=Config.Contracts[e.contractId];
            e.completionChecked=true;
            if(!e.combatChecked){e.injury=cfg.targetMonsterId>0?CombatResolver.Resolve(e.adventurerId,e.targetMonsterId>0?e.targetMonsterId:cfg.targetMonsterId):InjuryResult.None;e.combatChecked=true;}
        }
        CommitResults();
    }
    private bool _checking;
    public void ResolveAllChecksAsync(Action completed)
    {
        if(_checking||DiceCheckPanel.IsShowing)return;_checking=true;NextCheck(completed);
    }
    private void NextCheck(Action completed)
    {
        var run=RunStateManager.Instance.run;
        foreach(var e in All().Where(e=>!e.checksResolved))
        {
            var cfg=Config.Contracts[e.contractId];
            e.completionChecked=true;
            if(!e.combatChecked)
            {
                if(cfg.targetMonsterId<=0){e.injury=InjuryResult.None;e.combatChecked=true;}
                else
                {
                    int target=e.targetMonsterId>0?e.targetMonsterId:cfg.targetMonsterId;
                    int modifier=CombatResolver.Modifier(e.adventurerId,target);
                    Present(e,"遭遇 · "+Config.Monsters[target].name,D20Service.Balance.lightMax+1,modifier,"battle",true,r=>{e.injury=CombatResolver.Outcome(r,modifier);e.combatChecked=true;NextCheck(completed);});return;
                }
            }
        }
        CommitResults();_checking=false;completed?.Invoke();
    }
    private void Present(CommissionEntry entry,string title,int target,int modifier,string step,bool critical,Action<CheckResult> completed)
    {
        var run=RunStateManager.Instance.run;string key="settle:"+run.currentDay+":"+entry.adventurerId+":"+entry.contractId+":"+step;
        var request=run.pendingDice?.context=="settlement"&&run.pendingDice.key==key?run.pendingDice:D20Service.CreatePresentation(key,Config.Adventurers[entry.adventurerId].name+" · "+title,target,modifier,"settlement",critical);
        run.pendingDice=request;D20Service.Show(request,completed);
    }
    private void CommitResults()
    {
        var run=RunStateManager.Instance.run;
        // 两份任务只读取同一出发伤势，全部掷骰结束后取最重结果；不能在同一天重伤叠死。
        foreach(var group in All().Where(e=>!e.checksResolved).GroupBy(e=>e.adventurerId))
        {
            int actor=group.Key;var adv=run.adventurers[actor];
            if(!run.injuryAppliedDay.TryGetValue(actor,out int day)||day!=run.currentDay)
            {
                var worst=group.Min(e=>(int)e.injury);ApplyInjury(actor,(InjuryResult)worst);run.injuryAppliedDay[actor]=run.currentDay;
            }
        }
        foreach(var e in All().Where(e=>!e.checksResolved))
        {
            var adv=run.adventurers[e.adventurerId];var cfg=Config.Contracts[e.contractId];
            e.completionRate=RewardRate(e,cfg);
            var previous=new Dictionary<SevenDimension,int>(adv.currentSevenStats);
            if(!e.statsApplied)
            {
                int i=EvolutionPanel.Index(e.evolutionKind);
                if(i>=0&&!adv.isDead&&cfg.targetMonsterId>0){int n=SystemManager.Instance.Narrative?.EvolutionDelta??5;ChangeStat(adv,EvolutionPanel.Down[i],-n);ChangeStat(adv,EvolutionPanel.Up[i],n);}
                e.statsApplied=true;
            }
            e.checksResolved=true;
            AudioManager.Instance?.PlayCue(adv.isDead?"death":e.wrongRoute?"adventure_failure":"adventure_success");
            if(adv.currentSevenStats.Any(v=>previous.TryGetValue(v.Key,out int old)&&v.Value!=old))AudioManager.Instance?.PlayCue("stat_up");
            string who=adv.Config.name;
            e.outcomeText=adv.isDead?who+"没有回来。":e.wrongRoute?who+"带回的材料类别不符。":e.injury==InjuryResult.Severe?who+"重伤返回。":e.injury==InjuryResult.Light?who+"轻伤返回。":who+"无伤返回。";
            e.outcomeText += "\n" + (adv.isDead ? "报酬：0金币（未生还）" : e.wrongRoute ? "报酬：0金币（类别不符）" : "等级系数：" + Mathf.RoundToInt(e.completionRate*100) + "% · 报酬：" + Mathf.RoundToInt(cfg.reward*e.completionRate) + "金币");
            // 保存真实净变化；读档及重复打开账目只读这份结果，不再次应用数值。
            var changes=new List<string>();
            foreach(var stat in adv.currentSevenStats){previous.TryGetValue(stat.Key,out int old);int delta=stat.Value-old;if(delta!=0)changes.Add(GetStatName(stat.Key)+(delta>0?" ＋":" −")+Math.Abs(delta));}
            // 与剧情浮条共用前后评估，只追加本次结果；已应用属性的旧队列不会追补变化或再次培养。
            string progress=changes.Count>0?SystemManager.Instance.Narrative?.DescribeSuccessionChange(e.adventurerId,previous):"";
            if(!string.IsNullOrEmpty(progress))changes.Add(progress);
            e.outcomeText="普通委托"+" · "+cfg.name+"\n"+e.outcomeText+(changes.Count>0?"\n"+string.Join("，",changes):"");
        }
        TopBarRefreshEvent.Trigger();UIManager.Instance?.Get<CharacterArchivePanel>()?.RefreshRecords();
    }
    private void ChangeStat(AdventurerRuntimeData adv,SevenDimension stat,int amount)
    {adv.currentSevenStats.TryGetValue(stat,out int old);adv.currentSevenStats[stat]=Mathf.Clamp(old+amount,0,100);}
    private void ApplyInjury(int actor,InjuryResult injury)
    {
        var adv=RunStateManager.Instance.run.adventurers[actor];if(adv.isDead)return;
        if(injury==InjuryResult.Severe)
        {
            if(adv.isSeverelyInjured)
            {
                adv.isDead=true;adv.isSeverelyInjured=false;adv.severeInjuryDelay=0;
                if(Config.Items.TryGetValue(7,out var cfg))InventoryModel.Instance.AddItem(new ItemStack{data=ItemData.FromConfig(cfg),quantity=1});
            }
            else {adv.isSeverelyInjured=true;adv.severeInjuryDelay=2;adv.health=HealthState.Critical;}
        }
        else if(injury==InjuryResult.Light){if(adv.isSeverelyInjured)adv.severeInjuryDelay=2;else adv.health=HealthState.Damaged;}
    }
    public List<string> ResultNotes()=>All().Select(e=>e.outcomeText).Where(s=>!string.IsNullOrEmpty(s)).ToList();

    public CommissionIncomeResult ComputeAll()
    {
        var result = new CommissionIncomeResult();

        var contracts = ConfigDatabase.Instance?.Contracts;
        if (contracts == null)
        {
            Debug.LogError("[DailyCommissionSystem] ConfigDatabase.Contracts 为空");
            return result;
        }

        // 普通委托
        foreach (var entry in _normalCommissions)
        {
            var detail = CalcOne(entry, contracts, isSpecial: false);
            if (detail == null) continue;
            result.normalIncome += detail.actualIncome;
            result.details.Add(detail);
        }

        return result;
    }

    public static MonsterType TargetType(ContractConfig cfg) => cfg.targetMonsterType ?? ConfigDatabase.Instance.Monsters[cfg.targetMonsterId].monsterType;
    public static string TypeName(MonsterType type)
    {
        switch(type) {
            case MonsterType.Slime:return "史莱姆类"; case MonsterType.Goblin:return "哥布林类";
            case MonsterType.Canine:return "犬类"; case MonsterType.Plant:return "植物类";
            case MonsterType.Construct:return "构造类"; default:return "龙类";
        }
    }
    public static float RankFactor(Level actor, Level monster) => new[]{1f,.8f,.5f,.25f}[Math.Abs((int)actor-(int)monster)];
    /// <summary>唯一报酬公式。旧完成度/答对次数不再改变委托收入。</summary>
    public static float RewardRate(CommissionEntry entry, ContractConfig cfg)
    {
        var db=ConfigDatabase.Instance;
        int target=entry.targetMonsterId>0?entry.targetMonsterId:cfg.targetMonsterId;
        var monster=db.Monsters[target];
        var type=entry.requestedMonsterType??TargetType(cfg);
        if(RunStateManager.Instance.run.adventurers[entry.adventurerId].isDead || monster.monsterType!=type || entry.wrongRoute) return 0;
        return entry.rankFactor>=0?entry.rankFactor:RankFactor(db.Adventurers[entry.adventurerId].baseLevel,monster.level);
    }

    private CommissionDetail CalcOne(
        CommissionEntry entry,
        IReadOnlyDictionary<int, ContractConfig> contracts,
        bool isSpecial)
    {
        if (entry == null) return null;

        if (!contracts.TryGetValue(entry.contractId, out var cfg) || cfg == null)
        {
            Debug.LogWarning($"[DailyCommissionSystem] 找不到委托配置 id = {entry.contractId}");
            return null;
        }

        // 结算规则：正确类别且存活，基础报酬 × 等级系数。
        float rate=RewardRate(entry,cfg);
        int income = Mathf.RoundToInt(cfg.reward * rate);

        return new CommissionDetail
        {
            contractId = cfg.id,
            name = cfg.name,
            reward = cfg.reward,
            completionRate = rate,
            actualIncome = income,
            isSpecial = isSpecial
        };
    }

    // ============================================================
    //  跨天清空
    // ============================================================

    public void Clear()
    {
        _normalCommissions.Clear();
    }
    private static string GetStatName(SevenDimension d)
    {
        switch (d)
        {
            case SevenDimension.Reason: return "理智";
            case SevenDimension.Courage: return "勇气";
            case SevenDimension.Piety: return "虔诚";
            case SevenDimension.Tolerance: return "宽容";
            case SevenDimension.Benevolence: return "仁爱";
            case SevenDimension.Resolve: return "坚定";
            case SevenDimension.Diligence: return "勤勉";
            default: return d.ToString();
        }
    }
}

// ============================================================
//  结果载体
// ============================================================

public class CommissionIncomeResult
{
    public int normalIncome;    // 普通委托总收益
    public int specialIncome;   // 特殊委托总收益
    public int Total => normalIncome + specialIncome;

    /// <summary>每条委托的明细，调试 / UI 展示用</summary>
    public List<CommissionDetail> details = new List<CommissionDetail>();
}

public class CommissionDetail
{
    public int contractId;
    public string name;
    public int reward;
    public float completionRate;
    public int actualIncome;
    public bool isSpecial;
}


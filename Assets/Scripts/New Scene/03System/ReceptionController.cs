/****************************************************
    文件：ReceptionController.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/9/25 0:19:36
	功能：接待控制器
*****************************************************/

using System.Linq;
using UnityEngine;

public class ReceptionController
{
    private readonly RunStateManager _runState;
    private readonly ConfigDatabase _config;
    private readonly AdventureResolver _resolver;
    private readonly EconomySystem _economy;
    private readonly ActionPointSystem _actionPoint;
    private readonly MemorySystem _memory;
    private readonly D20Service _d20;

    public ReceptionController(RunStateManager runState,ConfigDatabase config,AdventureResolver resolver,
        EconomySystem economy,ActionPointSystem actionPoint,MemorySystem memory, D20Service d20)
    {
        _runState = runState;
        _config = config;
        _resolver = resolver;
        _economy = economy;
        _actionPoint = actionPoint;
        _memory = memory;
        _d20 = d20;
    }

    public void Init() { }
    public void Dispose() { }

    public void StartVisitor(int adventurerId)
    {
        Debug.Log($"接待冒险者 {adventurerId}");
    }


    // ============================================================
    //  搭话
    // ============================================================
    public ReceptionResult Talk(int adventurerId, int optionId)
    {
        if (!_runState.run.adventurers.TryGetValue(adventurerId, out var state))
            return ReceptionResult.Fail("找不到该冒险者");

        if (!_config.Dialogues.TryGetValue(optionId, out var option))
            return ReceptionResult.Fail("找不到该对话选项");

        if (option.adventurerId != 0 && option.adventurerId != adventurerId)
            return ReceptionResult.Fail("该选项不属于当前冒险者");

        // 搭话消耗
        int cost = _actionPoint.GetTalkCost(state.formalTalkCount);
        if (!_actionPoint.Spend(cost))
            return ReceptionResult.Fail($"行动点不足，需要 {cost} 点");

        state.formalTalkCount++;

        // D20 检定
        var check = _d20.Check(option.checkTarget);
        string answer = check.Success ? option.successText : option.failText;

        // 写入历史对话
        state.historyDialogue.Add($"[搭话] {option.text} → {answer}");

        // 成功且有效信息
        if (check.Success && option.validInfoId != 0 &&
            _config.ValidInfos.TryGetValue(option.validInfoId, out var info))
        {
            if (!state.validInfo.Contains(info.text))
                state.validInfo.Add(info.text);
        }

        string log = check.Success
            ? $"检定成功（{check.Roll}/{option.checkTarget}）：{answer}"
            : $"检定失败（{check.Roll}/{option.checkTarget}）：{answer}";

        return ReceptionResult.Ok(log, cost);
    }

    /// <summary>获取当前冒险者可用的正式搭话选项</summary>
    public System.Collections.Generic.List<DialogueOptionConfig> GetTalkOptions(int adventurerId)
    {
        return _config.Dialogues.Values
            .Where(d => d.stage == DialogueStage.Formal)
            .Where(d => d.adventurerId == 0 || d.adventurerId == adventurerId)
            .ToList();
    }

    // ============================================================
    //  调查
    // ============================================================
    public ReceptionResult Investigate(int adventurerId)
    {
        if (!_runState.run.adventurers.TryGetValue(adventurerId, out var state))
            return ReceptionResult.Fail("找不到该冒险者");

        if (state.investigationCount >= 3)
            return ReceptionResult.Fail("本次来访已调查三次，无法继续");

        // 找需要的道具
        var invList = _config.Investigations.Values
            .Where(i => i.adventurerId == adventurerId)
            .OrderBy(i => i.order)
            .ToList();

        if (invList.Count == 0)
            return ReceptionResult.Fail("该冒险者没有调查配置");

        int nextOrder = state.investigationCount + 1;
        var inv = invList.FirstOrDefault(i => i.order == nextOrder);
        if (inv == null)
            return ReceptionResult.Fail($"缺少 order={nextOrder} 的调查配置");

        // 检查道具
        if (!HasItem(inv.itemId))
            return ReceptionResult.Fail($"缺少调查道具（id={inv.itemId}）");

        // 消耗 1 AP + 1 道具
        if (!_actionPoint.Spend(1))
            return ReceptionResult.Fail("行动点不足");

        ConsumeItem(inv.itemId);
        state.investigationCount++;
        state.investigationArchive.Add(inv.text);

        return ReceptionResult.Ok($"[调查 {nextOrder}/3] {inv.text}", 1);
    }

    // ============================================================
    //  拒绝
    // ============================================================
    public ReceptionResult Refuse(int adventurerId)
    {
        if (!_runState.run.adventurers.ContainsKey(adventurerId))
            return ReceptionResult.Fail("找不到该冒险者");

        // 不消耗 AP，不产生记忆，不进入报复
        _runState.run.resolvedVisitors.Add(adventurerId);
        return ReceptionResult.Ok("已拒绝接待，冒险者离开。", 0, endVisit: true);
    }


    // ============================================================
    //  指路
    // ============================================================
    public ReceptionResult GiveRoute(int adventurerId, bool isTruth)
    {
        if (!_runState.run.adventurers.TryGetValue(adventurerId, out var state))
            return ReceptionResult.Fail("找不到该冒险者");

        if (!_config.Adventurers.TryGetValue(adventurerId, out var adv))
            return ReceptionResult.Fail("找不到冒险者配置");

        if (!_config.Contracts.TryGetValue(adv.contractId, out var contract))
            return ReceptionResult.Fail("找不到委托配置");

        if (!_config.Monsters.TryGetValue(contract.targetMonsterId, out var monster))
            return ReceptionResult.Fail("找不到目标魔物");

        // 检查目标魔物当天是否已接待
        if (_runState.run.monsters.TryGetValue(monster.id, out var monState) && monState.接待过今日)
        {
            return ReceptionResult.Fail("目标魔物今日已被接待，只能拒绝或编造另一条路线。");
        }

        // 执行结算
        var ctx = new AdventureContext
        {
            adventurerId = adventurerId,
            adventurerLevel = adv.baseLevel,
            health = state.health,
            equipment = state.equipment,
            isParty = adv.isParty,
            monsterLevel = monster.level,
            zoneDanger = 0,
            isTruthRoute = isTruth,
            wasDetected = !isTruth && Random.value < 0.3f
        };

        var result = _resolver.Resolve(ctx);
        monState.接待过今日 = true;

        // 奖励
        if (result == AdventureResult.FlawlessSuccess || result == AdventureResult.InjuredSuccess)
        {
            if (isTruth) _economy.RewardHonest(contract.baseReward);
            else _economy.RewardLied(contract.baseReward);
        }
        else if (result == AdventureResult.Death || result == AdventureResult.HeavyRetreat)
        {
            _economy.AddNutrient(monster.nutrientValue);
        }

        // 记忆
        var trigger = MapResultToMemoryTrigger(result, isTruth);
        _memory.Write(adventurerId, trigger);

        _runState.run.resolvedVisitors.Add(adventurerId);

        string text = ResultToText(result);
        return ReceptionResult.Ok($"[结算] {text}", 0, endVisit: true);
    }

    // ============================================================
    //  工具
    // ============================================================
    /// <summary>
    /// 查询是否持有物品 传入id
    /// </summary>
    /// <param name="itemId"></param>
    /// <returns></returns>
    private bool HasItem(int itemId)
    {
        // TODO: 接背包系统后改成查库存
        return _runState.run.itemInventory.TryGetValue(itemId, out int n) && n > 0;
    }
    /// <summary>
    /// 消耗物品
    /// </summary>
    /// <param name="itemId"></param>
    private void ConsumeItem(int itemId)
    {
        if (_runState.run.itemInventory.TryGetValue(itemId, out int n) && n > 0)
            _runState.run.itemInventory[itemId] = n - 1;
    }
    /// <summary>
    /// 冒险结果转文本
    /// </summary>
    /// <param name="r"></param>
    /// <returns></returns>
    private string ResultToText(AdventureResult r)
    {
        switch (r)
        {
            case AdventureResult.FlawlessSuccess: return "无损成功，冒险者无伤返回。";
            case AdventureResult.InjuredSuccess: return "带伤成功，冒险者完成目标但受伤。";
            case AdventureResult.DetectedRetreat: return "识破并撤退，冒险者发现路线有问题。";
            case AdventureResult.LightRetreat: return "轻伤撤退，冒险者未能完成目标。";
            case AdventureResult.HeavyRetreat: return "重伤撤退，冒险者负伤逃离。";
            case AdventureResult.Death: return "冒险者未能返回，地牢获得养分。";
            default: return r.ToString();
        }
    }

    /// <summary>
    /// 将结果映射到内存触发器
    /// </summary>
    /// <param name="result"></param>
    /// <param name="isTruth"></param>
    /// <returns></returns>
    private MemoryTrigger MapResultToMemoryTrigger(AdventureResult result, bool isTruth)
    {
        switch (result)
        {
            case AdventureResult.FlawlessSuccess:
            case AdventureResult.InjuredSuccess:
                return MemoryTrigger.SuccessGetMaterial;
            case AdventureResult.DetectedRetreat:
                return MemoryTrigger.Suspicion;
            case AdventureResult.HeavyRetreat:
                return isTruth ? MemoryTrigger.InjuredInZone : MemoryTrigger.HeavyInjuryByLie;
            case AdventureResult.Death:
                return MemoryTrigger.Death;
            default:
                return MemoryTrigger.Suspicion;
        }
    }
}

/****************************************************
    文件：ReceptionController.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/9/25 0:19:36
	功能：接待控制器
*****************************************************/

using System.Linq;
using UnityEngine;

/// <summary>
/// 接待控制器。
/// 负责处理冒险者来访时的交互流程：
/// 1. Talk：搭话（消耗行动点，D20 检定，记录信息）；
/// 2. Investigate：调查（消耗行动点和物品，增加调查进度）；
/// 3. Refuse：拒绝接待（结束本次接待，不消耗行动点）；
/// 4. GiveRoute：指路（真话/谎话，结算战斗与奖励，写入记忆）。
/// 注意：本类目前维护了一套旧的物品字典（itemInventory），与 InventoryModel 并存。
/// </summary>
public class ReceptionController
{
    #region 依赖注入

    private readonly RunStateManager _runState;
    private readonly ConfigDatabase _config;
    private readonly AdventureResolver _resolver;
    private readonly EconomySystem _economy;
    private readonly ActionPointSystem _actionPoint;
    private readonly MemorySystem _memory;
    private readonly D20Service _d20;

    public ReceptionController(RunStateManager runState, ConfigDatabase config, AdventureResolver resolver,
        EconomySystem economy, ActionPointSystem actionPoint, MemorySystem memory, D20Service d20)
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

    #endregion

    #region 搭话

    /// <summary>
    /// 搭话流程：
    /// 1. 校验冒险者、对话选项归属；
    /// 2. 扣除行动点；
    /// 3. 进行 D20 检定，写入历史对话；
    /// 4. 成功时记录有效信息。
    /// 风险：D20 检定直接传入 option.checkTarget，目前未看到冒险者属性修正参与。
    /// </summary>
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

    /// <summary>获取当前冒险者可用的正式搭话选项。</summary>
    public System.Collections.Generic.List<DialogueOptionConfig> GetTalkOptions(int adventurerId)
    {
        return _config.Dialogues.Values
            .Where(d => d.stage == DialogueStage.Formal)
            .Where(d => d.adventurerId == 0 || d.adventurerId == adventurerId)
            .ToList();
    }

    #endregion

    #region 调查

    /// <summary>
    /// 调查流程：
    /// 1. 限制最多 3 次；
    /// 2. 按 order 顺序查找当前进度需要的调查配置；
    /// 3. 检查并消耗指定物品和 1 点行动点；
    /// 4. 记录调查档案。
    /// 风险：旧物品字典与新的背包模型并存，当前 HasItem/ConsumeItem 操作的是 run.itemInventory。
    /// </summary>
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

    #endregion

    #region 拒绝

    /// <summary>拒绝接待：标记为已解决，结束本次接待，不扣行动点，不产生记忆。</summary>
    public ReceptionResult Refuse(int adventurerId)
    {
        if (!_runState.run.adventurers.ContainsKey(adventurerId))
            return ReceptionResult.Fail("找不到该冒险者");

        // 不消耗 AP，不产生记忆，不进入报复
        _runState.run.resolvedVisitors.Add(adventurerId);
        return ReceptionResult.Ok("已拒绝接待，冒险者离开。", 0, endVisit: true);
    }

    #endregion

    #region 指路

    /// <summary>
    /// 指路流程：
    /// 1. 校验冒险者、委托、目标魔物配置；
    /// 2. 检查目标魔物今日是否已被接待；
    /// 3. 构造 AdventureContext，执行结算；
    /// 4. 发放经济奖励，写入记忆，标记为已解决。
    /// 风险（高）：
    /// 若 run.monsters 中不存在该 monster.id，TryGetValue 会返回 false 且 monState 为 null，
    /// 下一行 monState.isReception = true 会直接触发 NRE（空引用异常）。
    /// </summary>
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
        if (_runState.run.monsters.TryGetValue(monster.id, out var monState) && monState.isReception)
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
            // 风险：0.3f 检测率硬编码。
            wasDetected = !isTruth && Random.value < 0.3f
        };

        var result = _resolver.Resolve(ctx);

        // 风险：这里如果 monState 没查出来是 null，会直接报空引用。
        monState.isReception = true;

        // 奖励
        if (result == AdventureResult.FlawlessSuccess || result == AdventureResult.InjuredSuccess)
        {
            if (isTruth) _economy.RewardHonest(contract.reward);
            else _economy.RewardLied(contract.reward);
        }

        // 记忆
        var trigger = MapResultToMemoryTrigger(result, isTruth);
        _memory.Write(adventurerId, trigger);

        _runState.run.resolvedVisitors.Add(adventurerId);

        string text = ResultToText(result);
        return ReceptionResult.Ok($"[结算] {text}", 0, endVisit: true);
    }

    #endregion

    #region 物品管理工具

    /// <summary>
    /// 查询是否持有物品。
    /// 风险：这里直接查旧字典 run.itemInventory，没有查 InventoryModel（背包系统）。
    /// TODO 注释也标明了要接背包系统，说明目前存在两套物品体系。
    /// </summary>
    private bool HasItem(int itemId)
    {
        return _runState.run.itemInventory.TryGetValue(itemId, out int n) && n > 0;
    }

    /// <summary>消耗物品（同样只操作旧字典）。</summary>
    private void ConsumeItem(int itemId)
    {
        if (_runState.run.itemInventory.TryGetValue(itemId, out int n) && n > 0)
            _runState.run.itemInventory[itemId] = n - 1;
    }

    #endregion

    #region 结果格式化与映射

    /// <summary>将冒险结果转换为日志文本。</summary>
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

    /// <summary>将冒险结果映射为记忆触发器。</summary>
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

    #endregion
}
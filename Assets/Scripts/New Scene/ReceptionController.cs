/****************************************************
    文件：ReceptionController.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/9/25 0:19:36
	功能：
*****************************************************/

using UnityEngine;

public class ReceptionController
{
    private readonly RunStateManager _runState;
    private readonly ConfigDatabase _config;
    private readonly AdventureResolver _resolver;
    private readonly EconomySystem _economy;
    private readonly ActionPointSystem _actionPoint;
    private readonly MemorySystem _memory;

    public ReceptionController(RunStateManager runState,ConfigDatabase config,AdventureResolver resolver,
        EconomySystem economy,ActionPointSystem actionPoint,MemorySystem memory)
    {
        _runState = runState;
        _config = config;
        _resolver = resolver;
        _economy = economy;
        _actionPoint = actionPoint;
        _memory = memory;
    }

    public void Init() { }
    public void Dispose() { }

    public void StartVisitor(int adventurerId)
    {
        Debug.Log($"接待冒险者 {adventurerId}");
    }

    /// <summary>
    /// 指定路线
    /// </summary>
    /// <param name="adventurerId"></param>
    /// <param name="isTruth"></param>
    /// <returns></returns>
    public bool GiveRoute(int adventurerId, bool isTruth)
    {
        if (!_config.Adventurers.TryGetValue(adventurerId, out var adv)) return false;
        if (!_config.Contracts.TryGetValue(adv.contractId, out var contract)) return false;
        if (!_config.Monsters.TryGetValue(contract.targetMonsterId, out var monster)) return false;

        var state = _runState.run.adventurers[adventurerId];

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
        Debug.Log($"结算结果: {result}");

        if (result == AdventureResult.FlawlessSuccess || result == AdventureResult.InjuredSuccess)
        {
            if (isTruth) _economy.RewardHonest(contract.baseReward);
            else _economy.RewardLied(contract.baseReward);
        }
        else if (result == AdventureResult.Death || result == AdventureResult.HeavyRetreat)
        {
            _economy.AddNutrient(monster.nutrientValue);
        }

        var trigger = MapResultToMemoryTrigger(result, isTruth);
        _memory.Write(adventurerId, trigger);

        _runState.run.resolvedVisitors.Add(adventurerId);
        return true;
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

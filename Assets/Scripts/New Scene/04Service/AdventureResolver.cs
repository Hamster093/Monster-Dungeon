/****************************************************
    文件：AdventureResolver.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/9/25 1:32:00
	功能：冒险解析器
*****************************************************/

public enum AdventureResult
{
    FlawlessSuccess,   // 无损成功
    InjuredSuccess,    // 带伤成功
    DetectedRetreat,   // 识破撤退
    LightRetreat,      // 轻伤撤退
    HeavyRetreat,      // 重伤撤退
    Death              // 死亡
}

public class AdventureContext
{
    public int adventurerId;
    public Level adventurerLevel;
    public HealthState health;
    public EquipmentState equipment;
    public bool isParty;
    public Level monsterLevel;
    public int zoneDanger;
    public bool isTruthRoute;
    public bool wasDetected;
    public int checkId = 1;
}

public class AdventureResolver
{
    private readonly D20Service _d20;

    public AdventureResolver(D20Service d20)
    {
        _d20 = d20;
    }

    public AdventureResult Resolve(AdventureContext ctx)
    {
        var balance=D20Service.Balance;
        int target = balance.adventureTarget;

        int levelDiff = (int)ctx.adventurerLevel - (int)ctx.monsterLevel;
        target -= levelDiff * balance.levelStep;

        if (ctx.health == HealthState.Damaged) target += balance.lightPenalty;
        if (ctx.health == HealthState.Critical) target += balance.severePenalty;
        if (ctx.equipment == EquipmentState.Damaged) target += balance.equipmentDamagedPenalty;
        if (ctx.equipment == EquipmentState.Critical) target += balance.equipmentCriticalPenalty;
        if (!ctx.isTruthRoute) target += balance.falseRoutePenalty;

        var check = _d20.Check(target);

        if (check.IsCritical) return AdventureResult.FlawlessSuccess;
        // 与正式委托战斗一致：第一次大失败重伤，已有重伤时再次重伤才死亡。
        if (check.IsFumble) return ctx.health==HealthState.Critical?AdventureResult.Death:AdventureResult.HeavyRetreat;

        if (check.Success)
        {
            if (ctx.health != HealthState.Normal || ctx.equipment != EquipmentState.Normal)
                return AdventureResult.InjuredSuccess;
            return AdventureResult.FlawlessSuccess;
        }

        if (!ctx.isTruthRoute && ctx.wasDetected)
            return AdventureResult.DetectedRetreat;

        if (ctx.health == HealthState.Critical)
            return AdventureResult.HeavyRetreat;

        return AdventureResult.LightRetreat;
    }
}

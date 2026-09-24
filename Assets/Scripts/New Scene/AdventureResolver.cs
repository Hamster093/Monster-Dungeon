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
        int target = 10;

        int levelDiff = (int)ctx.adventurerLevel - (int)ctx.monsterLevel;
        target -= levelDiff * 2;

        if (ctx.health == HealthState.Damaged) target += 3;
        if (ctx.health == HealthState.Critical) target += 6;
        if (ctx.equipment == EquipmentState.Damaged) target += 2;
        if (ctx.equipment == EquipmentState.Critical) target += 4;
        if (!ctx.isTruthRoute) target += 2;

        var check = _d20.Check(target);

        if (check.IsCritical) return AdventureResult.FlawlessSuccess;
        if (check.IsFumble) return AdventureResult.Death;

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
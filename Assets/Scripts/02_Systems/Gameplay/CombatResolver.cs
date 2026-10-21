/****************************************************
    文件：CombatResolver.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/16 19:29:04
	功能：战斗检定 → 伤势结果
    规则：D100。阈值、等级差与伤势修正集中在 Config/d100Rules.json。
*****************************************************/

using UnityEngine;

public enum InjuryResult
{
    Severe,   // 重伤
    Light,    // 轻伤
    None      // 无伤
}

public static class CombatResolver
{
    private static readonly D20Service _d20 = new D20Service();
    public static int Modifier(int adventurerId,int monsterId)
    {
        var db=ConfigDatabase.Instance;var b=D20Service.Balance;
        int modifier=((int)db.Adventurers[adventurerId].baseLevel-(int)db.Monsters[monsterId].level)*b.levelStep;
        var adv=RunStateManager.Instance.run.adventurers[adventurerId];
        return modifier-(adv.isSeverelyInjured?b.severePenalty:adv.health==HealthState.Damaged?b.lightPenalty:0);
    }
    public static InjuryResult Outcome(CheckResult result,int modifier)
    {
        if(result.IsCritical)return InjuryResult.None;
        if(result.IsFumble)return InjuryResult.Severe;
        int sum=result.Roll+modifier;return sum<=D20Service.Balance.severeMax?InjuryResult.Severe:sum<=D20Service.Balance.lightMax?InjuryResult.Light:InjuryResult.None;
    }

    public static int UnhurtPercent(int adventurerId, int monsterId)
    {
        int modifier = Modifier(adventurerId, monsterId), count = 0;
        for (int roll = 1; roll <= 100; roll++)
            if (Outcome(D20Service.Evaluate(roll, D20Service.Balance.lightMax + 1, modifier), modifier) == InjuryResult.None) count++;
        return count;
    }
    public static string Difficulty(int adventurerId, int monsterId) => NarrativeSystem.Difficulty(UnhurtPercent(adventurerId, monsterId));

    public static InjuryResult Resolve(int adventurerId, int monsterId)
    {
        var config = ConfigDatabase.Instance;

        if (!config.Adventurers.TryGetValue(adventurerId, out var advCfg) || advCfg == null)
        {
            Debug.LogWarning($"[CombatResolver] 找不到冒险者 {adventurerId}");
            return InjuryResult.None;
        }

        if (!config.Monsters.TryGetValue(monsterId, out var monCfg) || monCfg == null)
        {
            Debug.LogWarning($"[CombatResolver] 找不到魔物 {monsterId}");
            return InjuryResult.None;
        }

        int advLv = (int)advCfg.baseLevel;   // A=4, B=3, C=2, D=1
        int monLv = (int)monCfg.level;

        int modifier = Modifier(adventurerId,monsterId);
        int roll = _d20.Roll();
        int final = roll + modifier;

        InjuryResult result=Outcome(D20Service.Evaluate(roll,D20Service.Balance.lightMax+1,modifier),modifier);

        Debug.Log($"[战斗检定] 冒险者{adventurerId}({advCfg.baseLevel}) vs 魔物{monsterId}({monCfg.level}) | " +
                  $"骰={roll} + 修正{modifier} = {final} → {result}");

        return result;
    }
}

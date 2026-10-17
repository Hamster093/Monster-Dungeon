/****************************************************
    文件：CombatResolver.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/16 19:29:04
	功能：战斗检定 → 伤势结果
    规则：同级 0-5 重伤，6-10 轻伤，11-20 无伤
          魔物每强 1 级 -5，每弱 1 级 +5
          重伤状态额外 -5
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

    public static InjuryResult Resolve(int adventurerId, MonsterInstance monsterInst)
    {
        if(monsterInst == null)
        {
            Debug.LogWarning("[CombatResolver] 传入的魔物实例为空");
            return InjuryResult.None;
        }

        var config = ConfigDatabase.Instance;

        if (!config.Adventurers.TryGetValue(adventurerId, out var advCfg) || advCfg == null)
        {
            Debug.LogWarning($"[CombatResolver] 找不到冒险者 {adventurerId}");
            return InjuryResult.None;
        }

        if (!config.Monsters.TryGetValue(monsterInst.monsterId, out var monCfg) || monCfg == null)
        {
            Debug.LogWarning($"[CombatResolver] 找不到魔物 {monsterInst.monsterId}");
            return InjuryResult.None;
        }

        int advLv = (int)advCfg.baseLevel;   // A=4, B=3, C=2, D=1
        int monLv = (int)monCfg.level;

        //  如果有进化加成，可以在这里处理（比如进化后等级视为+1）
        if (monsterInst.isEvolved)
        {
            //monLv += 1; // 如进化视为强 1 级
        }

        int modifier = (advLv - monLv) * 5;   // 魔物强 → 负数；魔物弱 → 正数

        var run = RunStateManager.Instance.run;
        if (run.adventurers.TryGetValue(adventurerId, out var advData)
            && advData != null && advData.isSeverelyInjured)
        {
            modifier -= 5;
        }

        int roll = _d20.Roll();
        int final = roll + modifier;

        InjuryResult result;
        if (final <= 5) result = InjuryResult.Severe;
        else if (final <= 10) result = InjuryResult.Light;
        else result = InjuryResult.None;

        Debug.Log($"[战斗检定] 冒险者{adventurerId}({advCfg.baseLevel}) vs 魔物{monsterInst.monsterId}({monCfg.level}) | " +
                  $"骰={roll} + 修正{modifier} = {final} → {result}");

        return result;
    }
}

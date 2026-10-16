/****************************************************
    文件：DailyCommissionSystem.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/15 17:15:54
	功能：每日委托收益系统。两个独立队列：普通 / 特殊 跨天清空
*****************************************************/

using System.Collections.Generic;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;

public class DailyCommissionSystem
{
    private readonly List<CommissionEntry> _normalCommissions = new List<CommissionEntry>();
    private readonly List<CommissionEntry> _specialCommissions = new List<CommissionEntry>();

    public IReadOnlyList<CommissionEntry> NormalCommissions => _normalCommissions;
    public IReadOnlyList<CommissionEntry> SpecialCommissions => _specialCommissions;

    private ConfigDatabase Config => ConfigDatabase.Instance;

    public bool IsEmpty => _normalCommissions.Count == 0 && _specialCommissions.Count == 0;

    private readonly D20Service _d20 = new D20Service();

    // ============================================================
    //  Add 接口（接委托时调用）
    // ============================================================

    public void AddNormal(CommissionEntry entry)
    {
        if (entry == null) return;
        _normalCommissions.Add(entry);
    }

    public void AddNormal(int contractId, int adventurerId, float completionRate = 1f)
    {
        _normalCommissions.Add(new CommissionEntry(contractId, adventurerId, completionRate));
    }

    public void AddSpecial(CommissionEntry entry)
    {
        if (entry == null) return;
        _specialCommissions.Add(entry);
    }

    public void AddSpecial(int contractId, int adventurerId, float completionRate = 1f)
    {
        _specialCommissions.Add(new CommissionEntry(contractId, adventurerId, completionRate));
    }

    /// <summary>
    /// 外部（检定系统）回调里用它来设置完成度
    /// isSpecial = true 走特殊队列
    /// </summary>
    public void SetCompletionRate(bool isSpecial, int contractId, float rate)
    {
        var list = isSpecial ? _specialCommissions : _normalCommissions;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].contractId == contractId)
            {
                list[i].completionRate = Mathf.Clamp01(rate);
                return;
            }
        }
        Debug.LogWarning($"[DailyCommissionSystem] 找不到委托 {contractId} 来设置完成度");
    }

    // ============================================================
    //  结算
    // ============================================================

    /// <summary>
    /// 对队列里所有委托做检定：委托成败 + 战斗伤势 + 特殊委托属性提升
    /// </summary>
    public void ResolveAllChecks()
    {
        int correctCount = RunStateManager.Instance.run.GetCorrectAnswerCount();
        int modifier = correctCount * 2;

        Debug.Log($"[委托检定] ===== 开始，正确回答数={correctCount}，修正=+{modifier} =====");

        ResolveQueue(_normalCommissions, modifier, isSpecial: false);
        ResolveQueue(_specialCommissions, modifier, isSpecial: true);

        Debug.Log("[委托检定] ===== 结束 =====");
    }

    private void ResolveQueue(List<CommissionEntry> list, int modifier, bool isSpecial)
    {
        var config = ConfigDatabase.Instance;

        foreach (var entry in list)
        {
            var check = _d20.Check(11, modifier);
            entry.completionRate = check.Success ? 1f : 0.5f;

            string tag = isSpecial ? "特殊" : "普通";
            string resultStr = check.Success ? "成功 → 100%" : "失败 → 50%";
            string flag = check.IsCritical ? "【暴击】" : check.IsFumble ? "【大失败】" : "";

            Debug.Log($"[委托检定] {tag} 委托{entry.contractId} 冒险者{entry.adventurerId} | " +
                      $"骰={check.Roll} + 修正{modifier} = {check.Roll + modifier} | {flag}{resultStr}");

            // 特殊委托成功 → 提升影响属性
            if (isSpecial && check.Success)
                ApplySpecialStatBonus(entry);

            // 有目标委托 → 战斗伤势检定
            if (config.Contracts.TryGetValue(entry.contractId, out var cfg)
                && cfg != null
                && cfg.targetMonsterId > 0)
            {
                var injury = CombatResolver.Resolve(entry.adventurerId, cfg.targetMonsterId);
                ApplyInjury(entry.adventurerId, injury);
            }
        }
    }

    /// <summary>
    /// 按 adventurerId 直接改属性，不依赖"当前访客"。
    /// influenceStats 里每个属性 +1。
    /// </summary>
    private void ApplySpecialStatBonus(CommissionEntry entry)
    {
        if (entry == null || entry.statsApplied) return;
        entry.statsApplied = true;

        var config = ConfigDatabase.Instance;
        if (config == null || config.Contracts == null) return;
        if (!config.Contracts.TryGetValue(entry.contractId, out var cfg) || cfg == null) return;
        if (cfg.influenceStats == null || cfg.influenceStats.Count == 0) return;

        var run = RunStateManager.Instance?.run;
        if (run == null || run.adventurers == null) return;
        if (!run.adventurers.TryGetValue(entry.adventurerId, out var adv) || adv == null)
        {
            Debug.LogWarning($"[属性提升] 找不到冒险者 {entry.adventurerId}");
            return;
        }

        foreach (var stat in cfg.influenceStats)
        {
            if (!adv.currentSevenStats.ContainsKey(stat))
                adv.currentSevenStats[stat] = 0;

            adv.currentSevenStats[stat] += 1;
            Debug.Log($"[属性提升] 冒险者{entry.adventurerId} {GetStatName(stat)} +1 → 当前 {adv.currentSevenStats[stat]}");
        }
    }

    private void ApplyInjury(int adventurerId, InjuryResult injury)
    {
        var run = RunStateManager.Instance.run;
        if (!run.adventurers.TryGetValue(adventurerId, out var adv) || adv == null) return;

        switch (injury)
        {
            case InjuryResult.Severe:
                if (adv.isSeverelyInjured)
                {
                    adv.isDead = true;
                    adv.isSeverelyInjured = false;
                    adv.severeInjuryDelay = 0;
                    Debug.Log($"[伤势] 冒险者{adventurerId} 重伤叠加 → 死亡");
                }
                else
                {
                    adv.isSeverelyInjured = true;
                    adv.severeInjuryDelay = 2;   // 第 1 次受伤 → 第 3 次来访恢复
                    Debug.Log($"[伤势] 冒险者{adventurerId} 进入重伤，还需 {adv.severeInjuryDelay} 次来访恢复");
                }
                break;

            case InjuryResult.Light:
                if (adv.isSeverelyInjured)
                {
                    adv.severeInjuryDelay = 2;   // 重伤延续
                    Debug.Log($"[伤势] 冒险者{adventurerId} 重伤延续，重置倒计时");
                }
                else
                {
                    Debug.Log($"[伤势] 冒险者{adventurerId} 轻伤，忽略");
                }
                break;

            case InjuryResult.None:
                Debug.Log($"[伤势] 冒险者{adventurerId} 无伤");
                break;
        }
    }

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

        // 特殊委托
        foreach (var entry in _specialCommissions)
        {
            var detail = CalcOne(entry, contracts, isSpecial: true);
            if (detail == null) continue;
            result.specialIncome += detail.actualIncome;
            result.details.Add(detail);
        }

        return result;
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

        // 结算规则：报酬 × 完成度（100% / 75% / 50%）
        int income = Mathf.RoundToInt(cfg.reward * entry.completionRate);

        return new CommissionDetail
        {
            contractId = cfg.id,
            name = cfg.name,
            reward = cfg.reward,
            completionRate = entry.completionRate,
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
        _specialCommissions.Clear();
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

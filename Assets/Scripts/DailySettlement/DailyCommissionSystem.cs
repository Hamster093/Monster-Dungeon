/****************************************************
    文件：DailyCommissionSystem.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/15 17:15:54
	功能：每日委托收益系统。两个独立队列：普通 / 特殊 跨天清空
*****************************************************/

using System.Collections.Generic;
using UnityEngine;

public class DailyCommissionSystem
{
    private readonly List<CommissionEntry> _normalCommissions = new List<CommissionEntry>();
    private readonly List<CommissionEntry> _specialCommissions = new List<CommissionEntry>();

    public IReadOnlyList<CommissionEntry> NormalCommissions => _normalCommissions;
    public IReadOnlyList<CommissionEntry> SpecialCommissions => _specialCommissions;

    public bool IsEmpty => _normalCommissions.Count == 0 && _specialCommissions.Count == 0;

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
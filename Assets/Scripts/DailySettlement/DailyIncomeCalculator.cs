/****************************************************
    文件：DailyIncomeCalculator.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/15 17:16:38
	功能：每日收益统一计算入口= 棋盘收益 + 普通委托收益 + 特殊委托收益 对应 SettlementData 的 5 个字段
*****************************************************/

using UnityEngine;

public static class DailyIncomeCalculator
{
    /// <summary>
    /// 计算当日结算数据。
    /// board / tileConfig / incomeConfig 可以为 null（棋盘不可用时棋盘收益按 0 处理）。
    /// </summary>
    public static SettlementData Compute(
        BackpackBoard board,
        TileAffinityConfig tileConfig,
        MonsterIncomeConfig incomeConfig)
    {
        // 1. 棋盘收益
        int boardIncome = 0;
        if (board != null && tileConfig != null && incomeConfig != null)
            boardIncome = BoardIncomeCalculator.ComputeTotal(board, tileConfig, incomeConfig);

        // 2. 委托收益
        var commission = SystemManager.Instance?.DailyCommission?.ComputeAll()
                         ?? new CommissionIncomeResult();

        // 3. 汇总
        int total = boardIncome + commission.normalIncome + commission.specialIncome;

        // 4. 下阶段应缴纳分期
        int phase = SystemManager.Instance?.RunState?.run?.currentPhase ?? 1;
        int installment = ConfigDatabase.Instance?.GetInstallmentPayment(phase) ?? 0;

        return new SettlementData
        {
            legacyCommissionIncome = commission.normalIncome,
            specialCommissionIncome = commission.specialIncome,
            monsterBusinessIncome = boardIncome,
            totalIncome = total,
            installmentDue = installment
        };
    }

    public static SettlementData Compute()
    {
        var board = BackpackBoard.Instance;
        if (board == null)
        {
            Debug.LogError("[DailyIncomeCalculator] BackpackBoard.Instance 为空，场景里没有棋盘？");
            return new SettlementData();
        }

        return Compute(board, board.TileConfig, board.IncomeConfig);
    }
}
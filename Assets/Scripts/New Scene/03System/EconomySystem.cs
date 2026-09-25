/****************************************************
    文件：EconomySystem.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/9/25 0:28:16
	功能：经济系统
*****************************************************/

using UnityEngine;

public class EconomySystem
{
    private readonly RunStateManager _runState;

    public EconomySystem(RunStateManager runState)
    {
        _runState = runState;
    }

    public void Init()
    {
        PhaseEndedEvent.Register(OnPhaseEnded);
    }

    public void Dispose()
    {
        PhaseEndedEvent.UnRegister(OnPhaseEnded);
    }

    public int Cash => _runState.run.cash;
    public int Nutrient => _runState.run.nutrient;
    /// <summary>
    /// 增加指定数量的现金。
    /// </summary>
    public void AddCash(int amount) => _runState.run.cash += amount;
    /// <summary>
    /// 增加指定数量的养分
    /// </summary>
    public void AddNutrient(int amount) => _runState.run.nutrient += amount;
    /// <summary>
    /// 尝试花费指定数量的现金。
    /// </summary>
    public bool SpendCash(int amount)
    {
        if (_runState.run.cash < amount) return false;
        _runState.run.cash -= amount;
        return true;
    }
    /// <summary>
    /// 发放诚实接待的奖励。
    /// </summary>
    public void RewardHonest(int baseReward) => AddCash(baseReward);
    /// <summary>
    /// 发放欺骗接待的奖励（基础奖励的一半，向下取整）。
    /// </summary>
    public void RewardLied(int baseReward) => AddCash(baseReward / 2);
    /// <summary>
    /// 阶段结束事件回调。根据当前阶段查找对应的日程配置，并自动扣除契约分期金额。
    /// 如果现金不足，则触发游戏失败事件。
    /// </summary>
    private void OnPhaseEnded(int phase)
    {
        var schedule = ConfigDatabase.Instance.Schedules.Find(s => s.phase == phase);
        if (schedule == null) return;

        if (!SpendCash(schedule.installmentAmount))
        {
            Debug.LogError("现金不足，契约逾期");
            GamePassEvent.Trigger();
        }
        else
        {
            Debug.Log($"扣除分期 {schedule.installmentAmount}，剩余现金 {Cash}");
        }
    }
}


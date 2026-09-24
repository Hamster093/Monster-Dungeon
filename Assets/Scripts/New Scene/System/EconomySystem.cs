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

    public void AddCash(int amount) => _runState.run.cash += amount;
    public void AddNutrient(int amount) => _runState.run.nutrient += amount;

    public bool SpendCash(int amount)
    {
        if (_runState.run.cash < amount) return false;
        _runState.run.cash -= amount;
        return true;
    }

    public void RewardHonest(int baseReward) => AddCash(baseReward);
    public void RewardLied(int baseReward) => AddCash(baseReward / 2);

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


/****************************************************
    文件：System.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/9/25 0:11:50
	功能：调度系统
*****************************************************/

using System.Collections.Generic;
using UnityEngine;

public class ScheduleSystem
{
    private readonly RunStateManager _runState;
    private readonly ConfigDatabase _config;

    public int CurrentDay => _runState.run.currentDay;
    public int CurrentPhase => _runState.run.currentPhase;

    public ScheduleSystem(RunStateManager runState, ConfigDatabase config)
    {
        _runState = runState;
        _config = config;
    }

    public void Init()
    {
        NextDayEvent.Register(AdvanceDay);
    }

    public void Dispose()
    {
        NextDayEvent.UnRegister(AdvanceDay);
    }
    /// <summary>
    /// 天数增加
    /// </summary>
    public void AdvanceDay()
    {
        // 完整剧情会在 action:fee 时扣费，不能在次日事件里再扣一次。
        if (SystemManager.Instance.Narrative?.Enabled != true && IsPhaseEndDay(_runState.run.currentDay))
        { 
            EndPhase();
        
        }

        _runState.run.currentDay++;
        _runState.run.currentPhase=_config.Schedules.Find(s=>_runState.run.currentDay<=s.deadlineDay)?.phase??5;
        _runState.run.EnsureBusinessDay();
        if(BackpackBoard.Instance!=null)foreach(var v in BackpackBoard.Instance.Items)if(v!=null)v.Rebuild();
        Debug.Log($"进入第 {_runState.run.currentDay} 天");

        foreach (var m in _runState.run.monsters.Values)
            m.isReception = false;

        _runState.run.todayVisitors.Clear();
        _runState.run.resolvedVisitors.Clear();

        var visitors = GetTodayVisitors();
        _runState.run.todayVisitors.AddRange(visitors);

        TodayVisitorsReadyEvent.Trigger(visitors);

        if (_runState.run.currentDay > 27)
        {
            Debug.Log("27 天结束");
        }
    }

    /// <summary>
    /// 获取今日日来客
    /// </summary>
    public List<int> GetTodayVisitors()
    {
        if (ConfigDatabase.Instance.VisitOrders.TryGetValue(CurrentDay, out var order) && order.visitors != null)
            return order.visitors;

        return new List<int>();
    }
    /// <summary>
    /// 扣除分期
    /// </summary>
    public void EndPhase()
    {
        var schedule = _config.Schedules.Find(s => s.phase == _runState.run.currentPhase);
        if (schedule == null) return;

        Debug.Log($"阶段 {_runState.run.currentPhase} 结束");
        PhaseEndedEvent.Trigger(_runState.run.currentPhase);
        _runState.run.currentPhase++;
    }
    /// <summary>
    /// 是否是阶段结束日
    /// </summary>
    /// <param name="day"></param>
    /// <returns></returns>
    private bool IsPhaseEndDay(int day)
    {
        var schedule = _config.Schedules.Find(s => s.phase == _runState.run.currentPhase);
        return schedule != null && day >= schedule.deadlineDay;
    }

    /// <summary>
    /// 刷新与排期相关的运行时数据（剩余天数、分期金额）
    /// </summary>
    public void RefreshScheduleData()
    {
        var run = SystemManager.Instance.RunStateManager.run;
        var cfg = SystemManager.Instance.Config;

        run.remainingDays = cfg.GetRemainingDays(run.currentDay, run.currentPhase);
        run.installmentPayment = cfg.GetInstallmentPayment(run.currentPhase);
    }
}


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
        _runState.run.currentDay++;
        Debug.Log($"进入第 {_runState.run.currentDay} 天");

        foreach (var m in _runState.run.monsters.Values)
            m.接待过今日 = false;

        _runState.run.todayVisitors.Clear();
        _runState.run.resolvedVisitors.Clear();

        var visitors = GetTodayVisitors();
        _runState.run.todayVisitors.AddRange(visitors);

        TodayVisitorsReadyEvent.Trigger(visitors);

        if (IsPhaseEndDay(_runState.run.currentDay))
            EndPhase();

        if (_runState.run.currentDay > 27)
        {
            Debug.Log("27 天结束");
            GamePassEvent.Trigger();
        }
    }

    /// <summary>
    /// 获取今日日来客
    /// </summary>
    public List<int> GetTodayVisitors()
    {
        var result = new List<int>();
        foreach (var adv in ConfigDatabase.Instance.Adventurers.Values)
        {
            if (adv.visitDays != null && adv.visitDays.Contains(CurrentDay))
                result.Add(adv.id);
        }
        return result;
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

    private bool IsPhaseEndDay(int day)
    {
        var schedule = _config.Schedules.Find(s => s.phase == _runState.run.currentPhase);
        return schedule != null && day >= schedule.deadlineDay;
    }
}


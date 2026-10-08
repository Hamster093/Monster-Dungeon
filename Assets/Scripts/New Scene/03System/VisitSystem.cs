/****************************************************
    文件：VisitSystem.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/8 14:33:08
	功能：访客系统
*****************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 访客队列逻辑。今天有谁、现在到谁、下一位是谁，
/// UI 变化通过 OnCurrentVisitorChanged 事件广播。
/// </summary>
public class VisitSystem : IDisposable
{
    private readonly RunStateManager _runState;
    private readonly ConfigDatabase _config;

    public List<int> TodayVisitors { get; private set; } = new();
    public int VisitorIndex { get; private set; }
    public int CurrentVisitorId { get; private set; } = -1;

    public int CurrentDay => _runState.run.currentDay;

    /// <summary>当前访客变化时触发，参数为 id；-1 表示今日无访客/已接待完</summary>
    public event Action<int> OnCurrentVisitorChanged;
    /// <summary>今日最后一位接待完，即将推进下一天</summary>
    public event Action OnDayVisitsFinished;

    public VisitSystem(RunStateManager runState, ConfigDatabase config)
    {
        _runState = runState;
        _config = config;
    }

    public void Init()
    {
        TodayVisitorsReadyEvent.Register(OnVisitorsReady);

        // 若已存在今日访客（比如中途启动、存档读入），手动灌一次
        if (_runState.run.todayVisitors != null && _runState.run.todayVisitors.Count > 0)
            OnVisitorsReady(_runState.run.todayVisitors);
    }

    public void Dispose()
    {
        TodayVisitorsReadyEvent.UnRegister(OnVisitorsReady);
        OnCurrentVisitorChanged = null;
        OnDayVisitsFinished = null;
    }

    // ---------------- 事件 ----------------

    private void OnVisitorsReady(List<int> ids)
    {
        TodayVisitors = ids ?? new List<int>();
        VisitorIndex = 0;
        NotifyCurrent();
    }

    private void NotifyCurrent()
    {
        if (TodayVisitors.Count == 0 || VisitorIndex >= TodayVisitors.Count)
        {
            CurrentVisitorId = -1;
            OnCurrentVisitorChanged?.Invoke(-1);
            return;
        }
        CurrentVisitorId = TodayVisitors[VisitorIndex];
        OnCurrentVisitorChanged?.Invoke(CurrentVisitorId);
    }

    // ---------------- 查询 ----------------

    public bool HasNextVisitor() => VisitorIndex + 1 < TodayVisitors.Count;

    /// <summary>当前访客的对话 key（角色名 + day + 天数），无访客返回 null</summary>
    public string GetCurrentDialogueKey()
    {
        if (CurrentVisitorId < 0) return null;
        if (!_config.Adventurers.TryGetValue(CurrentVisitorId, out var adv)) return null;
        return $"{adv.name}day{CurrentDay}";
    }

    // ---------------- 推进 ----------------

    /// <summary>
    /// 接待完当前访客。返回是否还有下一位（同一天）。
    /// 当天全部结束会自动触发 NextDayEvent 推进下一天。
    /// </summary>
    public bool OnVisitorFinished()
    {
        // 防重入
        if (TodayVisitors.Count == 0 || VisitorIndex >= TodayVisitors.Count)
            return false;

        VisitorIndex++;

        if (VisitorIndex < TodayVisitors.Count)
        {
            NotifyCurrent();
            return true;
        }

        // 当天结束
        CurrentVisitorId = -1;
        OnCurrentVisitorChanged?.Invoke(-1);
        OnDayVisitsFinished?.Invoke();

        NextDayEvent.Trigger();  
        return false;
    }
}
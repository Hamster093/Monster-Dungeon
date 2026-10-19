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

    public int GetCurrentVisitorId() => CurrentVisitorId;

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
        //  过滤掉已死亡的冒险者
        var filtered = new List<int>();
        if (ids != null)
        {
            foreach (var id in ids)
            {
                if (_runState.run.adventurers.TryGetValue(id, out var adv)
                    && adv != null && adv.isDead)
                {
                    Debug.Log($"[访客] 冒险者{id} 已死亡，跳过来访");
                    continue;
                }
                filtered.Add(id);
            }
        }

        TodayVisitors = filtered;
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
        return $"{adv.name}{"_"}day{CurrentDay}";
    }

    // ---------------- 推进 ----------------

    /// <summary>完整剧情已决定当前人物；仅同步原访客模型和 UI，不重复结束接待。</summary>
    public void SelectNarrativeVisitor(int id)
    {
        CurrentVisitorId = id;
        int index = TodayVisitors.IndexOf(id);
        if (index >= 0) VisitorIndex = index;
        OnCurrentVisitorChanged?.Invoke(id);
    }

    public void FinishNarrativeVisitor(int id) => TickInjuryOnVisit(id);

    /// <summary>
    /// 接待完当前访客。返回是否还有下一位（同一天）。
    /// 当天全部结束会自动触发 NextDayEvent 推进下一天。
    /// </summary>
    public bool OnVisitorFinished()
    {
        // 防重入
        if (TodayVisitors.Count == 0 || VisitorIndex >= TodayVisitors.Count)
            return false;
        //  当前访客离开前，递减重伤倒计时
        TickInjuryOnVisit(TodayVisitors[VisitorIndex]);

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
 
        return false;
    }
    /// <summary>访客离开时递减重伤倒计时。0 → 恢复</summary>
    private void TickInjuryOnVisit(int adventurerId)
    {
        var run = _runState.run;
        if (!run.adventurers.TryGetValue(adventurerId, out var adv) || adv == null) return;
        if (!adv.isSeverelyInjured) {adv.health=HealthState.Normal;return;}

        adv.severeInjuryDelay -= 1;
        if (adv.severeInjuryDelay <= 0)
        {
            adv.isSeverelyInjured = false;
            adv.health=HealthState.Normal;
            adv.severeInjuryDelay = 0;
            Debug.Log($"[伤势] 冒险者{adventurerId} 重伤恢复");
        }
        else
        {
            Debug.Log($"[伤势] 冒险者{adventurerId} 仍重伤，还需 {adv.severeInjuryDelay} 次来访");
        }
    }
    // ---------------- 拒绝 ----------------

    /// <summary>玩家点"拒绝"时调用：重伤直接恢复</summary>
    public void OnRefuseCurrentVisitor()
    {
        if (CurrentVisitorId < 0) return;

        var run = _runState.run;
        if (!run.adventurers.TryGetValue(CurrentVisitorId, out var adv) || adv == null) return;

        if (adv.isSeverelyInjured)
        {
            adv.isSeverelyInjured = false;
            adv.severeInjuryDelay = 0;
            Debug.Log($"[拒绝] 冒险者{CurrentVisitorId} 因拒绝接待而恢复伤势");
        }
    }
}

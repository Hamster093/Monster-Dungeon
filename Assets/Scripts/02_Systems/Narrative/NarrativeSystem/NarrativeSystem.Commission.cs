/****************************************************
    文件：NarrativeSystem.Commission.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/22
	功能：27天剧情接线层·委托与指路：委托详情、棋盘指路、日结算入账
/*****************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using Ink.Runtime;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 27天剧情的游戏接线层。生命周期由 SystemManager 管理；对话、商店、委托、棋盘收入和结算
/// 均复用原系统，不另建 UI。数值留在 Resources/Narrative/rules.json，原稿 action 标签统一接线。
/// 本类以 partial 拆分为多个文件，同功能代码用 #region 分组。
/// </summary>
public sealed partial class NarrativeSystem
{
    #region 委托详情与确认


    private void OpenCommission()
    {
        var scene = CurrentScene;
        var panel = UIManager.Instance.Open<CommissionPanel>(scene.contractId);
        panel.SetTextContext(scene.objective, scene.payment);
        panel.SetOnAccepted(() => CompleteCommission(true));
        panel.SetOnCancelled(() => CompleteCommission(false));
    }

    private void CompleteCommission(bool accepted)
    {
        _save.stage = "playing";
        // 原稿在正文后已有确认/返回分支；先走到该选择，绝不硬编码旧 JSON 节点号。
        int guard = 0;
        while (_runtime.Story.canContinue && guard++ < 100) _runtime.Read();
        if (_runtime.Story.currentChoices.Count != 2) throw new InvalidOperationException("委托确认分支结构与原稿不符。");
        // 迁移旧档会重新经过委托停靠标签，选择之前恢复运行态，避免再次打开同一详情。
        _save.stage = "playing";
        _runtime.Choose(accepted ? 0 : 1);
        _save.frame = null;
        Save();
        UIManager.Instance.Get<TalkPanel>()?.Dialogue.ResumeCurrentNode();
    }

    #endregion

    #region 指路流程


    /// <summary>普通委托接取按钮只建立待指路上下文，不入队、不推进原稿。</summary>
    public void RequestNormalRoute()
    {
        if (_save.pendingRoute != null || _save.stage != "commission") return;
        _save.pendingRoute = new RouteSelectionPanel.RouteState {
            contractId = CurrentScene.contractId, adventurerId = ActorIds[Actor]
        };
        _save.stage = "routing";
        OpenPendingRoute(); Save();
    }

    private void OpenPendingRoute()
    {
        var pending = _save.pendingRoute;
        if (pending == null) return;
        if (UIManager.Instance.Get<CommissionPanel>()?.IsOpen != true) OpenCommission();
        UIManager.Instance.Open<RouteSelectionPanel>(new RouteSelectionPanel.OpenData {
            state = pending, confirmed = CommitRoute, cancelled = CancelRoute
        });
    }

    private void CancelRoute()
    {
        var pending = _save.pendingRoute;
        if (pending == null) return;
        _save.pendingRoute = null;
        _save.stage = "commission";
        // 来源详情保持打开；取消不等于拒绝委托，也不会切到下一位。
        Save();
    }

    private void CommitRoute(PlacedItem item)
    {
        var pending = _save.pendingRoute;
        if (pending == null || item == null || item.IsResting || !int.TryParse(item.Def.Id, out int target)) return;
        var entry = new CommissionEntry(pending.contractId, pending.adventurerId) {
            targetMonsterId = target, targetUid=item.Uid,evolutionKind=item.EvolutionKind,wrongRoute=_systems.Config.Monsters[target].monsterType!=DailyCommissionSystem.TargetType(_systems.Config.Contracts[pending.contractId]),
            requestedMonsterType=DailyCommissionSystem.TargetType(_systems.Config.Contracts[pending.contractId]),
            rankFactor=DailyCommissionSystem.RankFactor(_systems.Config.Adventurers[pending.adventurerId].baseLevel,_systems.Config.Monsters[target].level),
            correctAnswerCount = Run.correctAnswerCount + Convert.ToInt32(_runtime.Get("story_correct"))
        };
        _save.pendingRoute = null;
        item.UsedDay=Run.currentDay;item.View.Rebuild();
        bool existed = _systems.DailyCommission.NormalCommissions.Any(e => e.contractId == entry.contractId && e.adventurerId == entry.adventurerId);
        _systems.DailyCommission.AddNormal(entry);
        _save.routedScene = CurrentScene.id;
        _save.routedWrong = entry.wrongRoute;
        _runtime.Set("preview_wrong_route", _save.routedWrong);
        _save.stage = "commission";
        UIManager.Instance.Get<CommissionPanel>().CompleteRouteAcceptance();
        if (!existed) {AudioManager.Instance?.PlayCue("confirm");ToastManager.Show("委托接取成功");}
        Save();
    }

    private void PrepareLegacyRoute()
    {
        var scene = CurrentScene;
        int actor = ActorIds[Actor];
        _save.normal.RemoveAll(e => e.contractId == scene.contractId && e.adventurerId == actor);
        _systems.DailyCommission.RemoveNormal(scene.contractId, actor);
        _runtime.SetMember("accepted", Actor, false);
        // 委托正文节点只包含说明与确认选项；回到该节点不重放接单/道具/奖励动作。
        _runtime.Story.ChoosePathString(scene.commissionPath);
        _save.frame = null;
        _save.pendingRoute = new RouteSelectionPanel.RouteState { contractId = scene.contractId, adventurerId = actor };
        _save.stage = "routing";
    }

    #endregion

    #region 日结算


    public void CompleteSettlement(SettlementData data)
    {
        if (_save.stage != "settlement" || !_save.settledDays.Add(Run.currentDay)) return;
        _systems.Economy.AddCash(data.totalIncome);
        _systems.DailyCommission.Clear();
        _save.stage = "playing";
        _save.frame = null;
        Save();
        // 回访、阶段扣费、下一天、终局由原稿继续推进，不在按钮里额外 NextDay。
        StartReception();
    }

    #endregion
}

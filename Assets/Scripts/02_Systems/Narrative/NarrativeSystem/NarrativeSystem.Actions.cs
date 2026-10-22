/****************************************************
    文件：NarrativeSystem.Actions.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/22
	功能：27天剧情接线层·动作接线：HandleAction分发、属性反馈、报复、音频、访客同步
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
    #region 属性变化与反馈

    private readonly Dictionary<int, Dictionary<SevenDimension, int>> _attributeBefore = new Dictionary<int, Dictionary<SevenDimension, int>>();

    private void FlushAttributeFeedback()
    {
        foreach (var pair in _attributeBefore)
        {
            var actor = Run.adventurers[pair.Key];
            var parts = new List<string>();
            // 缓存只包含本段改过的属性；补回完整旧快照后只读评估，不临时改动人物数据。
            var previous = new Dictionary<SevenDimension, int>(actor.currentSevenStats);
            foreach (var stat in pair.Value)
            {
                previous[stat.Key] = stat.Value;
                int delta = actor.currentSevenStats[stat.Key] - stat.Value;
                if (delta != 0) parts.Add(SevenDimensionDisplay.GetName(stat.Key) + (delta > 0 ? "＋" : "－") + Math.Abs(delta));
            }
            if (parts.Count > 0)
            {
                if(!_restoring)AudioManager.Instance?.PlayCue(actor.currentSevenStats.Any(v=>previous.TryGetValue(v.Key,out int old)&&v.Value>old)?"stat_up":"stat_down");
                string progress = DescribeSuccessionChange(pair.Key, previous);
                ToastManager.Show(actor.Config.name + "：" + string.Join("，", parts) + (string.IsNullOrEmpty(progress) ? "" : "；" + progress), Guid.NewGuid().ToString());
            }
        }
        _attributeBefore.Clear();
        TopBarRefreshEvent.Trigger();
    }

    #endregion

    #region 报复与档案查询

    public string RevengeWarning
    {
        get
        {
            if(_runtime==null||_rules==null)return "";var warnings=new List<string>();
            foreach(var pair in ActorIds)if(Run.adventurers.TryGetValue(pair.Value,out var a)&&!a.isDead&&_runtime.HasMember("pending_revenge",pair.Key))
            {
                if(_save.activeRevengeActor==pair.Value)continue;
                int due=_save.revengeDue.TryGetValue(pair.Value,out int d)?d:Run.currentDay;
                warnings.Add(a.Config.name+(due>Run.currentDay?"可能在"+(due-Run.currentDay)+"日后回来":"将在今日回访"));
            }
            return warnings.Count==0?"":"报复提醒："+string.Join("；",warnings.Take(2))+(warnings.Count>2?" 等"+warnings.Count+"人":"");
        }
    }
    public bool HasFullBackground(int adventurerId)
    {
        var actor = ActorIds.FirstOrDefault(p => p.Value == adventurerId).Key;
        return actor != null && _save.backgroundLevels.TryGetValue(actor, out var level) && level == "full";
    }
    public string RevealedSuccessionFocus(int adventurerId)
    {
        if(!HasFullBackground(adventurerId))return "";
        // 正式继任条件才是培养方向的来源；旧静态配置中的默认维度不参与显示。
        var conditions=_rules.successionRules[ActorCode(adventurerId)];
        return "主属性："+StatChinese(conditions[0].stat)+"　副属性："+StatChinese(conditions[1].stat);
    }

    #endregion

    #region 音频氛围


    /// <summary>稳定剧情地址选择音乐；与台词文字、玩家属性和随机数完全分离。</summary>
    public void ApplyAudioLine(string source)
    {
        var audio=AudioManager.Instance;if(audio==null||string.IsNullOrEmpty(source))return;
        string node=source.Split('.')[0];
        if(audio.Mapping.nodes.TryGetValue(node,out var context))
        {
            if(context=="revenge")context=(string)_runtime.Get("revenge_severity")=="heavy"?"danger":"uneasy";
            _save.audioContext=context;
        }
        RefreshAudioContext();
    }
    public void RefreshAudioContext()
    {
        string context=_save.stage=="preparation"?"preparation":_save.stage=="settlement"||_save.stage=="settlement_checks"?"preparation":_save.stage=="ending_cg"||_save.stage=="ending_complete"?"ending":_save.audioContext??"reception";
        AudioManager.Instance?.SetContext(context);
    }

    #endregion

    #region 剧情动作接线 HandleAction


    private void HandleAction(string tag)
    {
        var parts = tag.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        string kind = parts[0].Substring("action:".Length);
        var values = new Dictionary<string, string>();
        for (int i = 1; i < parts.Length; i++)
        {
            int eq = parts[i].IndexOf('=');
            if (eq > 0) values[parts[i].Substring(0, eq)] = parts[i].Substring(eq + 1);
        }
        string V(string key, string fallback = "") => values.TryGetValue(key, out var v) ? v : fallback;
        int N(string key, int fallback = 0) => int.TryParse(V(key), out int n) ? n : fallback;
        string actor = V("actor", Actor);
        switch (kind)
        {
            case "panel": _save.stage = "commission"; break;
            case "day":
                if (V("phase") == "start")
                {
                    Run.player.EndReceptionThoughts();
                    int day = N("number");
                    int previousBusinessDay=Run.currentDay,previousPhase=Run.currentPhase;
                    if (Run.currentDay != day)
                    {
                        if (Run.currentDay + 1 != day) throw new InvalidOperationException("剧情日期与游戏日期不一致。");
                        NextDayEvent.Trigger();
                    }
                    Run.currentPhase = _systems.Config.Schedules.Find(s => day <= s.deadlineDay)?.phase ?? 5;
                    _systems.Schedule.RefreshScheduleData();
                    _save.returnStates.Clear();
                    _save.stage = "preparation";
                    _save.audioContext="preparation";
                    if(!_restoring&&Run.currentDay!=previousBusinessDay)
                    {
                        _dayTransitionPending=true;
                        PresentationEffects.PlayDay(Run.currentDay,Run.currentPhase,previousPhase!=Run.currentPhase&&previousBusinessDay>0,()=>{
                            UIManager.Instance.Get<TalkPanel>()?.PrepareBusinessDay();
                            AudioManager.Instance?.PlayCue("day",Run.currentDay.ToString());
                            _dayTransitionPending=false;PresentPause();TopBarRefreshEvent.Trigger();
                        });
                    }
                }
                else
                {
                    FinishVisitor();
                    _save.stage = "settlement_checks"; // 所有结果确认后再统一提交伤势与收益。
                }
                break;
            case "scene":
                _save.activeRevengeActor=-1;
                _save.audioContext="reception";
                if(!_restoring)_visitorTransitionKey=Run.currentDay+":"+(CurrentScene?.id??V("id"))+":"+actor;
                FinishVisitor();
                _save.routedScene = null;
                _save.checkId = null; Run.player.influenceKey = null;
                _save.departingVisitor = ActorIds[actor];
                _systems.VisitSystem.SelectNarrativeVisitor(ActorIds[actor]);
                Run.correctAnswerCount = 0;
                if (TryActor(actor, out var visitor)) { visitor.formalTalkCount = 0; visitor.investigationCount = 0; }
                _runtime.Set("preview_wrong_route", false);
                SyncLists();
                break;
            case "attribute":
                if (TryActor(actor, out var target))
                {
                    var stat = Stat(V("stat"));
                    target.currentSevenStats.TryGetValue(stat, out int old);
                    if (!_attributeBefore.TryGetValue(target.configId, out var before))
                    { before = new Dictionary<SevenDimension, int>(); _attributeBefore[target.configId] = before; }
                    if (!before.ContainsKey(stat)) before[stat] = old;
                    int amount = N("amount", _rules.attributeDelta);
                    // 蛋糕只缩小本次成功普通影响的默认±3；明确写出的其他数值与进化不受影响。
                    var player = Run.player;
                    if (player.influenceActor == target.configId && !string.IsNullOrEmpty(player.influenceKey))
                    {
                        if (player.influenceCakeDefault && (V("amount") == "config" || string.IsNullOrEmpty(V("amount"))) && amount == 3) amount = 2;
                        else if (!player.influenceCakeDefault) amount += player.influenceBonus; // 只续完旧档已建立的结果，不用于新检定。
                    }
                    target.currentSevenStats[stat] = Mathf.Clamp(old + (V("direction") == "down" ? -1 : 1) * amount, 0, 100);
                }
                break;
            case "info":
                if (TryActor(actor, out var informed) && _save.registeredInfo.Add(actor + ":" + V("id", V("key"))))
                {
                    string original = informed.historyDialogue.LastOrDefault(s => s.Contains(V("key"))) ?? informed.historyDialogue.LastOrDefault() ?? "";
                    informed.validInfo.Add(V("key") + "\n" + original);
                    CharacterArchiveModel.Instance?.Add(ActorIds[actor].ToString(), new DialogueLine { speakerName = "有效信息", content = V("key") });
                    // 与首次归档共用已存档的事件标识，查档、重显和读取存档都不会再次提示。
                    if(!_restoring)AudioManager.Instance?.PlayCue("info");
                    ToastManager.Show("有效信息已归档", "info:" + actor + ":" + V("id", V("key")));
                }
                break;
            case "background":
                if (TryActor(actor, out var investigated))
                {
                    string level = V("level");
                    if (_runtime.BackgroundTexts.TryGetValue(actor + ":" + level, out var record))
                    {
                        // 完整揭示替换此前同一人的模糊正文，不向未调查人物预填完整资料。
                        investigated.investigationArchive.Clear();
                        investigated.investigationArchive.Add(record);
                        _save.backgroundLevels[actor] = level;
                        if(!_restoring)AudioManager.Instance?.PlayCue("info");
                        ToastManager.Show("使用成功，你调查了冒险者的一条背景。", Guid.NewGuid().ToString());
                        UIManager.Instance?.Get<CharacterArchivePanel>()?.RefreshRecords();
                    }
                }
                break;
            case "quest":
                if (V("mode") == "accept")
                {
                    var scene = CurrentScene;
                    var entry = _systems.DailyCommission.NormalCommissions.FirstOrDefault(e => e.contractId == scene.contractId && e.adventurerId == ActorIds[actor]);
                    // 接取只由原面板入队；原稿标签同步该次接待的检定信息。
                    if (entry != null) { entry.correctAnswerCount = Run.correctAnswerCount + Convert.ToInt32(_runtime.Get("story_correct")); if (entry.targetMonsterId <= 0) entry.targetMonsterId = scene.targetMonsterId; }
                }
                else _systems.VisitSystem.OnRefuseCurrentVisitor();
                break;
            case "guide":
                if (V("route") == "runtime")
                {
                    if (_save.routedScene == CurrentScene.id) _runtime.Set("preview_wrong_route", _save.routedWrong);
                    else _save.stage = "route";
                }
                break;
            case "return":
                _systems.VisitSystem.SelectNarrativeVisitor(ActorIds[actor]);
                SyncLists();
                _save.returnStates.TryGetValue(ActorIds[actor], out int state);
                _runtime.Set("preview_return_state", state);
                break;
            case "fee":
                int phase = N("phase");
                _save.audioContext="stage";RefreshAudioContext();
                if (_save.paidPhases.Add(phase))
                {
                    _runtime.Set("fee_failed", !_systems.Economy.SpendCash(_systems.Config.GetInstallmentPayment(phase)));
                    if (!(bool)_runtime.Get("fee_failed")) {if(!_restoring)AudioManager.Instance?.PlayCue("phase");if(phase<5)PhaseEndedEvent.Trigger(phase);}
                }
                break;
            case "item":
                int id = ItemId(V("kind"));
                if (!ConsumeItem(id)) throw new InvalidOperationException("道具入口未检查库存：" + tag);
                if(!_restoring)AudioManager.Instance?.PlayCue(id==6?"trap":"item");
                if(id==5||id==6)ToastManager.Show((id==5?"致歉礼盒":"碎骨卷轴")+"－1", "item:"+_save.displaySerial+":"+id);
                if (id == 1)
                {
                    if (_save.lensDay != Run.currentDay) { _save.lensDay = Run.currentDay; _save.lensDailyUses = 0; }
                    _save.lensDailyUses++;
                }
                break;
            case "ap": break; // 原稿自己修改 ap；不能在这里再扣一次。
            case "check_context":
                _save.checkId = V("node");
                Run.player.influenceKey = null;
                break;
            case "check":
                if (V("kind") == "bonus" && V("correct") == "true") Run.correctAnswerCount++;
                break; // 真正的随机数只在 resolve_check 读取一次。
            case "personal":
                if (V("event") == "appraise") _systems.Economy.AddCash(N("profit") / 2);
                else if (V("event") == "black_crystal" && N("choice") != 3) _systems.Economy.AddCash(_rules.blackCrystalPayment);
                break;
            case "revenge":
                if(V("result")=="begin") {
                    _save.activeRevengeActor=ActorIds[actor];
                    _save.audioContext=(string)_runtime.Get("revenge_severity")=="heavy"?"danger":"uneasy";RefreshAudioContext();
                    if(!_restoring){PresentationEffects.PlayRevenge(Run.currentDay+":"+actor);AudioManager.Instance?.PlayCue(_save.audioContext=="danger"?"danger":"warning");}
                }
                else if(V("result")!="scheduled")_save.audioContext="reception";
                if(TryActor(actor,out var revengeActor))
                {
                    if(V("result")=="scheduled")_save.revengeDue[revengeActor.configId]=N("expected_day");
                    else if(V("result")!="begin")_save.revengeDue.Remove(revengeActor.configId);
                }
                if (V("result") == "compensate") _systems.Economy.SpendCash(Run.cash / 2);
                if (V("result") == "target_dead" && TryActor(actor, out var killed))
                {
                    killed.isDead = true;
                    if(!_restoring)AudioManager.Instance?.PlayCue("death");
                    // 原稿 residue_gain 对应商品 7「深渊残质」，并非旧版的养分货币。
                    if (_systems.Config.Items.TryGetValue(7, out var residue))
                    {
                        int gain = N("residue_gain");
                        int leftover = InventoryModel.Instance?.AddItem(new ItemStack { data = ItemData.FromConfig(residue), quantity = gain }) ?? gain;
                        if (leftover > 0) Run.itemInventory[7] = (Run.itemInventory.TryGetValue(7, out int count) ? count : 0) + leftover;
                    }
                }
                break;
            case "cooperate": break; // coop_done 等联合条件已由原稿维护。
            case "ending":
                _save.audioContext=V("kind")=="succession"?"ending":"danger";RefreshAudioContext();
                if(!_restoring&&V("kind")=="succession")AudioManager.Instance?.PlayCue("succession");
                _save.ending = V("kind") + (V("actor") == "" ? "" : ":" + V("actor"));
                break;
            default: throw new InvalidOperationException("未接线的剧情动作：" + tag);
        }
        TopBarRefreshEvent.Trigger();
    }

    private void FinishDayChecks()
    {
        // 普通委托按实际错误指路伤势触发原稿的回访；无伤或死亡不安排报复。
        foreach(var group in _systems.DailyCommission.NormalCommissions.GroupBy(e=>e.adventurerId))
        {
            string actor=ActorIds.First(p=>p.Value==group.Key).Key;var wrong=group.Where(e=>e.wrongRoute).ToArray();
            bool injured=wrong.Any(e=>e.injury!=InjuryResult.None)&&!Run.adventurers[group.Key].isDead;
            _runtime.SetMember("wrong_routes",actor,injured);
            _save.returnStates[group.Key]=injured?(wrong.Any(e=>e.injury==InjuryResult.Severe)?2:1):0;
        }
        foreach(var entry in _systems.DailyCommission.NormalCommissions)
        {
            var adv=Run.adventurers[entry.adventurerId];
            string code=ActorIds.First(p=>p.Value==entry.adventurerId).Key;
            // 剧情报复照常发生，额外报酬减半已撤除。
            if(adv.isDead)entry.completionRate=0;
        }
        SyncLists();_save.settlement=DailyIncomeCalculator.Compute();_save.settlement.notes=_systems.DailyCommission.ResultNotes();
        _save.stage="settlement";Save();UIManager.Instance.Open<DailySettlementPanel>(_save.settlement);
    }

    #endregion

    #region 访客与世界同步


    private void SyncLists()
    {
        foreach (var pair in ActorIds)
            if (Run.adventurers.TryGetValue(pair.Value, out var adv))
            {
                _runtime.SetMember("dead", pair.Key, adv.isDead);
                _runtime.SetMember("injuries", pair.Key, adv.isSeverelyInjured);
            }
    }

    private void FinishVisitor()
    {
        _save.activeRevengeActor=-1;
        Run.player.EndReceptionThoughts();
        if (_save.departingVisitor < 0) return;
        _systems.VisitSystem.FinishNarrativeVisitor(_save.departingVisitor);
        _save.departingVisitor = -1;
    }

    #endregion
}

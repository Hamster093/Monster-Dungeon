/****************************************************
    文件：NarrativeSystem.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/22
	功能：27天剧情接线层·核心骨架：字段/生命周期/对话帧驱动/通用辅助（partial 主文件）
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
    #region 字段与状态

    private static readonly Dictionary<string, int> ActorIds = new Dictionary<string, int>
        { {"MY",1}, {"FE",2}, {"DW",3}, {"SA",4}, {"AU",5}, {"DA",6}, {"UN",7} };
    private readonly SystemManager _systems;
    private NarrativeRuntime _runtime;
    private Rules _rules;
    private SaveData _save = new SaveData { uiVersion = 9 };
    private bool _restoring, _booted;

    #endregion

    #region 公开属性

    public bool IsRestoring => _restoring;
    public bool Enabled => _runtime != null;
    public bool IsTerminal => _save.stage == "ended" || _save.stage=="ending_cg" || _save.stage=="ending_complete";
    public bool IsPreparing => _save.stage == "preparation";
    public bool CanBrowseArchive => !IsPreparing && !IsTerminal && _save.stage != "settlement" && _save.departingVisitor > 0;
    public string Ending => _save.ending;
    public int EvolutionDelta => _rules.evolutionDelta;
    public bool NeedsPlayerCreation => Run.player == null || !Run.player.created;
    // 思维只能在安全整备停点确认；不能从查档或旧档中途闯入待检定、指路或人物台词。
    public bool NeedsThoughtSelection => !NeedsPlayerCreation && Run.currentDay >= 2 && Run.currentDay <= 27 &&
        Run.player.thoughtConfirmedPhase != Run.currentPhase && _save.stage == "preparation" &&
        Run.pendingDice == null && _save.pendingRoute == null && string.IsNullOrEmpty(Run.pendingEvolutionUid);
    public bool CanOfferSpecial => false; // 已撤除，仅保留旧验收工具的只读接口。
    /// <summary>档案只读查询：完整背景来自第二次透镜揭示，沿用存档中的跨天揭示状态。</summary>

    #endregion

    #region 内部访问器与构造

    private RunState Run => _systems.RunStateManager.run;
    private string Actor => (string)_runtime.Get("current_actor");
    private SceneRule CurrentScene => _rules.scenes.Find(s => s.id == (string)_runtime.Get("current_scene"));

    public NarrativeSystem(SystemManager systems) { _systems = systems; }


    #endregion

    #region 生命周期（初始化与启动）

    public void Init()
    {
        var asset = Resources.Load<TextAsset>("Narrative/story27");
        if (asset == null) throw new InvalidOperationException("缺少 Narrative/story27.json，27 天剧情无法启动。");
        var config = Resources.Load<TextAsset>("Narrative/rules");
        if (config == null) throw new InvalidOperationException("缺少 Narrative/rules.json。");
        _rules = JsonConvert.DeserializeObject<Rules>(config.text);
        foreach (var intro in _rules.introductions)
            if (ActorIds.TryGetValue(intro.Key, out var id)) _systems.Config.Adventurers[id].briefStory = intro.Value;
        _runtime = new NarrativeRuntime(asset.text);
        _runtime.OnAction = HandleAction;
        _runtime.OnCheck = Check;
        _runtime.ShouldPause = () => _save.stage != "playing";
        _runtime.OnError = (message, type) =>
        {
            if (type == Ink.ErrorType.Warning) Debug.LogWarning("[27天剧情] " + message);
            else throw new InvalidOperationException("[27天剧情] " + message);
        };
        foreach (var scene in _rules.scenes)
            _systems.Config.Contracts[scene.contractId] = new ContractConfig
            {
                id = scene.contractId, name = scene.commissionName, targetMonsterId = scene.targetMonsterId, targetMonsterType=scene.targetMonsterType,
                description = scene.description, reward = scene.reward
            };
        if (!string.IsNullOrEmpty(Run.narrativeSave))
        {
            _save = JsonConvert.DeserializeObject<SaveData>(Run.narrativeSave);
            _runtime.Story.state.LoadJson(_save.ink);
            _runtime.Speaker = _save.speaker ?? "旁白";
            if (_save.uiVersion < 2) UpgradeArchiveAndUI();
            if (_save.uiVersion < 3)
            {
                // 旧版按阶段累计，无法还原当天消费；只在首次迁移时清零当天额度。
                _save.lensDay = Run.currentDay; _save.lensDailyUses = 0; _save.uiVersion = 3;
            }
            if (_save.uiVersion < 4)
            {
                // 旧版指路前已经入队，迁移时只撤回本次未确认记录；其他已接委托原样保留。
                if (_save.stage == "route" || _save.stage == "myth_route") PrepareLegacyRoute();
                _save.uiVersion = 4;
            }
            DialogueHistoryModel.Instance?.Restore(_save.history);
            _systems.DailyCommission.Clear();
            foreach (var entry in _save.normal) _systems.DailyCommission.AddNormal(entry);
            MigrateRetiredSpecials();
            MigrateGlobalPlayerRules();
            MigrateEndingPresentation();
            MigrateDoc6PlayerRules();
            MigrateTalkAndRevenge();
            _restoring = true;
        }
        else
        {
            DialogueHistoryModel.Instance?.Clear();
            // 旧版测试档没有跨天剧情变量，不能把旧金币/伤势拼进一个新的 OPEN。
            // 先保留原档，再完整开新局；不覆盖已经存在的迁移备份。
            if (Run.currentDay > 0)
            {
                if (!SaveAndLoadManager.Exists("run_state_before_story27"))
                    SaveAndLoadManager.Save("run_state_before_story27", Run);
                _systems.RunStateManager.NewRun();
            }
            Run.currentDay = 0;
            Run.currentPhase = 1;
            PresentationEffects.PrepareOpening();
            // 初始七维以 10.10 更新的属性表为准，覆盖旧代码里的占位角色数值。
            foreach (var pair in _rules.initialStats)
                if (TryActor(pair.Key, out var adv))
                    for (int i = 0; i < 7; i++) adv.currentSevenStats[(SevenDimension)i] = pair.Value[i];
        }
    }

    /// <summary>入口 Start 时调用，此时原来的 UI 和棋盘已完成 Awake。</summary>
    public void Boot()
    {
        if (!Enabled || _booted) return;
        _booted = true;
        AudioManager.Instance?.ClearTransient();
        if(Run.pendingDice!=null)Run.pendingDice.audioPresented=true;
        if (Run.player == null) Run.player = new PlayerBuildState();
        Run.saveVersion = 10;
        if (string.IsNullOrEmpty(Run.pendingEvolutionUid)) Run.pendingEvolutionKind = "";
        // 从背包暂停状态读档时，先恢复正常时间，再由待恢复的面板重新暂停。
        Time.timeScale = 1f;
        if (_restoring) RestoreWorld();
        else
        {
            var inventory = InventoryModel.Instance;
            if (inventory != null)
            {
                for (int i = 0; i < inventory.Capacity; i++) inventory.GetSlot(i).Clear();
                foreach (var pair in Run.itemInventory)
                    if (_systems.Config.Items.TryGetValue(pair.Key, out var cfg))
                        inventory.AddItem(new ItemStack { data = ItemData.FromConfig(cfg), quantity = pair.Value });
                Run.itemInventory.Clear();
            }
        }
        // 活世界与背包恢复完成后才检查供给，避免把尚未恢复的空棋盘误判为缺少魔物。
        Run.MarkBusinessWorldReady();
        Run.EnsureBusinessDay();
        if (_save.stage == "preparation") {UIManager.Instance.Get<TalkPanel>()?.PrepareBusinessDay();UIManager.Instance.Close<TalkPanel>();
            if(!string.IsNullOrEmpty(Run.pendingEvolutionUid))UIManager.Instance.Open<EvolutionPanel>(Run.pendingEvolutionUid);
            else if (NeedsThoughtSelection && _save.topLeftPanel == nameof(PlayerBuildPanel))
                UIManager.Instance.Open<PlayerBuildPanel>(new Action(StartReception));
        }
        else if (_save.stage == "settlement") UIManager.Instance.Open<DailySettlementPanel>(_save.settlement);
        else StartReception();
        TopBarRefreshEvent.Trigger();
        RefreshAudioContext();
        PresentationEffects.RevealOpening();
        _restoring=false;
    }

    public void StartReception()
    {
        if (!Enabled || BackpackBoard.Instance?.IsEditing==true) return;
        if (NeedsThoughtSelection)
        {
            if (!PlayerBuildPanel.IsChoosing) UIManager.Instance.Open<PlayerBuildPanel>(new Action(StartReception));
            return;
        }
        if (_save.stage == "preparation") { _save.stage = "playing"; _save.frame = null; _save.audioContext="reception"; }
        RefreshAudioContext();
        // 新局在原开场前选择；旧档只在安全停点补选，不能中断待确认的检定/指路。
        if (Run.currentDay == 0 && NeedsPlayerCreation && _save.stage == "playing" && Run.pendingDice == null && _save.pendingRoute == null)
        {
            UIManager.Instance.Open<PlayerBuildPanel>(new Action(StartReception)); return;
        }
        UIManager.Instance.ClosePreparationViews();
        // 棋盘属于整备面板，不能关闭其父对象，否则魔物会 OnDisable 并退出收入/指路列表。
        UIManager.Instance.Get<PreparationPanel>()?.SetReceptionAvailable(false);
        var talk = UIManager.Instance.Open<TalkPanel>();
        if (_restoring && _save.archiveOpen)
        {
            UIManager.Instance.Get<TalkPanel>()?.OpenArchive(_save.archiveActor, _save.archiveTab);
        }
        talk.Dialogue.StartNarrative();
        if (_save.stage == "function") OpenWaitingPanel();
        if (_restoring && _save.archiveOpen && _save.topLeftPanel == nameof(CharacterArchivePanel))
            talk.OpenArchive(_save.archiveActor, _save.archiveTab);
        if (_restoring && Run.player.created && _save.topLeftPanel == nameof(PlayerBuildPanel)) UIManager.Instance.Open<PlayerBuildPanel>();
        _restoring = false;
    }

    #endregion

    #region 对话帧驱动


    public DialogueData GetFrame()
    {
        if(_save.stage.StartsWith("ending_",StringComparison.Ordinal))return null;
        // 恢复结算时不重显旧剧情帧，由暂停入口完成尚未提交的结算。
        if (_save.stage == "settlement_checks") return null;
        if (Run.pendingDice!=null&&!Run.pendingDice.confirmed) return null;
        if (_save.pendingRoute != null) return null;
        if (_save.frame != null) { if(InterceptEndingSelection(_save.frame))return _save.stage=="playing"?GetFrame():null; if (OfferPlayerAtOptions(_save.frame)) return null; DecoratePlayerOptions(_save.frame); if(_save.activeRevengeActor>0)foreach(var n in _save.frame.nodes)foreach(var l in n.lines)l.icon=ResolvePortrait(l); return _save.frame; }
        if (_save.stage == "commission") return null;
        _save.displaySerial++;
        if (_save.stage == "route" || _save.stage == "myth_route") { PrepareLegacyRoute(); Save(); return null; }
        if (_save.stage == "ended") { _save.frame = TerminalFrame(); Save(); return _save.frame; }
        int previousAP = Run.actionPoint, previousDay = Run.currentDay;
        var frame = _runtime.Read();
        if(frame==null){Save();return null;}
        FlushAttributeFeedback();
        if (_save.returningFromLens && _runtime.Story.currentChoices.Count > 0 && _runtime.Story.currentChoices.All(c => c.sourcePath.StartsWith("LENS.")))
        {
            _runtime.Choose(_runtime.Story.currentChoices.First(c => c.text.Trim() == "返回").index);
            _save.returningFromLens = false;
            return GetFrame();
        }
        Run.actionPoint = Convert.ToInt32(_runtime.Get("ap"));
        int apDelta = Run.actionPoint - previousAP;
        if (!_restoring && previousDay == Run.currentDay && apDelta != 0)
            ToastManager.Show("行动点" + (apDelta > 0 ? "+" : "") + apDelta, Guid.NewGuid().ToString());
        TopBarRefreshEvent.Trigger();
        foreach (var node in frame.nodes)
            foreach (var line in node.lines)
            {
                line.content = ExpandText(line.content);
                line.icon = ResolvePortrait(line);
            }
        // 动作后的隐藏停靠点不显示给玩家，交给对应的原有面板。
        if (_save.stage == "preparation" || _save.stage == "settlement" || _save.stage == "settlement_checks" || _save.stage == "commission")
        {
            Save();
            return null;
        }
        if (_save.stage == "route") { PrepareLegacyRoute(); Save(); return null; }
        // 第一天 Myth 的原稿有两颗试读指路按钮；实装改用同一张棋盘路线选择，
        // 选完后仍走原稿对应的正确/欺骗分支，不复制或删掉那两段剧情。
        if (frame.nodes[0].options.Any(o => o.text == "正确指路"))
        {
            if (_save.routedScene == CurrentScene.id)
            {
                _runtime.Choose(_save.routedWrong ? 0 : 1);
                _save.frame = null; return GetFrame();
            }
            PrepareLegacyRoute(); Save(); return null;
        }
        if (_runtime.Finished && frame.nodes[0].lines.Count == 0)
        {
            _save.stage = _save.ending.StartsWith("succession:",StringComparison.Ordinal)?"ending_cg":"ended";
            if(_save.stage=="ending_cg"){_save.frame=null;Save();return null;}
            _save.frame = TerminalFrame(); Save(); return _save.frame;
        }
        if(InterceptEndingSelection(frame)) {
            if(_save.stage=="playing")return GetFrame();return null;
        }
        DecoratePlayerOptions(frame);
        _save.frame = frame;
        if (OfferPlayerAtOptions(frame)) { Save(); return null; }
        PresentFrameTransition(frame);
        Save();
        return frame;
    }

    private bool ModalClosedThisFrame => UIManager.Instance != null && UIManager.Instance.LastModalCloseFrame == Time.frameCount;
    // 关闭确认/功能弹窗的同一次输入不能再落到右侧剧情；内部恢复读取不受此输入门禁影响。
    public void Next() { if (!ModalClosedThisFrame) _save.frame = null; }
    public bool Choose(DialogueOption option)
    {
        if (ModalClosedThisFrame) return false;
        if (_save.stage == "ended")
        {
            Save();
            EndingCredits.Show();
            return true;
        }
        if (_save.stage == "route" || _save.stage == "myth_route")
        {
            bool mythRoute = _save.stage == "myth_route";
            var scene = CurrentScene;
            if (option.param < 0)
            {
                _systems.DailyCommission.RemoveNormal(ActorIds[Actor]);
                _runtime.SetMember("accepted", Actor, false);
                _save.stage = "playing";
                _runtime.Story.ChoosePathString(_runtime.Get("menu").ToString());
            }
            else
            {
                bool wrong = _systems.Config.Monsters[option.param].monsterType !=
                             _systems.Config.Monsters[scene.targetMonsterId].monsterType;
                _runtime.Set("preview_wrong_route", wrong);
                foreach (var entry in _systems.DailyCommission.NormalCommissions)
                    if (entry.adventurerId == ActorIds[Actor]) entry.targetMonsterId = option.param;
                _save.stage = "playing";
                if (mythRoute) _runtime.Choose(wrong ? 0 : 1);
            }
            _save.frame = null;
            return true;
        }
        // 原稿试读按钮原先可以无限用道具；实装必须先验库存再进入分支。
        int item = RevengeOptionItem(option.choiceId);
        if(item>0 && (_save.stage!="playing" || _save.frame==null))return false; // 提交后旧按钮不得再次扣物。
        if(item==0)item=RequiredItem(option.text);
        if (item > 0 && ItemCount(item) < 1) { ToastManager.Show("背包里没有这件道具，请先在商店购买。"); return false; }
        int phase = Mathf.Clamp(Run.currentPhase, 1, 5);
        if (item == 1 && !CanUseLens()) return false;
        _runtime.Set("ap", Run.actionPoint);
        CommitPlayerChoice(option);
        _runtime.Choose(option.param);
        _save.frame = null;
        return true;
    }

    /// <summary>剧情暂停时切换原有面板，绝不让 DialogueController 再推进一遍访客/天数。</summary>
    private bool _dayTransitionPending; // 仅为显示锁，读档按已保存的业务阶段重建可见界面。
    public void PresentPause()
    {
        RefreshAudioContext();
        if(_dayTransitionPending)return;
        if (PlayerBuildPanel.IsChoosing) return;
        if(_save.stage.StartsWith("ending_",StringComparison.Ordinal)){OpenEndingPresentation();return;}
        if(Run.pendingDice!=null){RestorePendingDice();return;}
        if(_save.stage=="settlement_checks"){_systems.DailyCommission.ResolveAllChecksAsync(FinishDayChecks);return;}
        if (_save.pendingRoute != null) { OpenPendingRoute(); return; }
        if (_save.stage == "commission") { OpenCommission(); return; }
        UIManager.Instance.Close<TalkPanel>();
        if (_save.stage == "settlement") UIManager.Instance.Open<DailySettlementPanel>(_save.settlement);
        else
        {
            UIManager.Instance.Open<PreparationPanel>().SetReceptionAvailable(true);
        }
    }

    private DialogueData TerminalFrame() => new DialogueData
    {
        dialogueId = "story27", startNodeId = "end", nodes = new List<DialogueNode>
        {
            new DialogueNode { id = "end", speakerName = "系统", contents = new List<string> { "本次故事已结束。" },
                options = new List<DialogueOption> { new DialogueOption { text = "继续" } } }
        }
    };

    #endregion

    #region 通用辅助

    private bool TryActor(string actor, out AdventurerRuntimeData adv)
    {
        adv = null;
        return actor != null && ActorIds.TryGetValue(actor, out int id) && Run.adventurers.TryGetValue(id, out adv);
    }
    private static SevenDimension Stat(string stat)
    {
        switch (stat)
        {
            case "lucidity": return SevenDimension.Reason; case "courage": return SevenDimension.Courage;
            case "reverence": return SevenDimension.Piety; case "tolerance": return SevenDimension.Tolerance;
            case "benevolence": return SevenDimension.Benevolence; case "resolve": return SevenDimension.Resolve;
            case "diligence": return SevenDimension.Diligence;
            default: throw new ArgumentException("未知七维属性：" + stat);
        }
    }
    private static string StatChinese(SevenDimension stat)
    { switch(stat){case SevenDimension.Reason:return "理智";case SevenDimension.Courage:return "勇气";case SevenDimension.Piety:return "虔诚";case SevenDimension.Tolerance:return "宽容";case SevenDimension.Benevolence:return "仁爱";case SevenDimension.Resolve:return "坚定";default:return "勤勉";} }

    #endregion
}

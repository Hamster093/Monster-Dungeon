using System;
using System.Collections.Generic;
using System.Linq;
using Ink.Runtime;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 27 天剧情的游戏接线层。生命周期仍由 SystemManager 管理；
/// 对话、商店、委托、棋盘收入和结算均复用原系统，不另建 UI。
/// 数值留在 Resources/Narrative/rules.json，原稿 action 标签在这里统一接线。
/// </summary>
public sealed class NarrativeSystem
{
    public sealed class SceneRule
    {
        public string id, actor, title, commissionPath, commissionName, description, objective, payment;
        public int day, order, targetMonsterId, contractId, reward;
        public MonsterType? targetMonsterType;
    }
    public sealed class Rules
    {
        public int attributeDelta = 3;
        public int[] lensUseLimit = { 2, 2, 4, 4, 4 };
        public Dictionary<string,string> textOverrides = new Dictionary<string,string>();
        public int evolutionDelta = 5;
        public Dictionary<string,List<SuccessionCondition>> successionRules;
        public int successionNearGap=20,successionChanceStart=95,successionChanceStep=3;
        public Dictionary<string,string> endingCG;
        public Dictionary<string,Dictionary<string,string>> portraitVariants;
        public int blackCrystalPayment; // 原稿 money=config 未给金额，0 表示暂不添加这笔金钱。
        public List<SceneRule> scenes = new List<SceneRule>();
        public Dictionary<string, int[]> initialStats = new Dictionary<string, int[]>();
        public Dictionary<string, string> portraits = new Dictionary<string, string>();
        public Dictionary<string, string> introductions = new Dictionary<string, string>();
    }
    [Serializable]
    public sealed class SaveData
    {
        public string ink, speaker, stage = "playing", ending = "";
        public int uiVersion;
        public int endingCandidate=-1;
        public string endingExpression;
        public bool successionFailed;
        public string portraitAddress;
        public string endingPresentationPhase;
        public string audioContext = "reception";
        public string checkId; // 原稿中实际掷骰调用的稳定地址。
        public int lensDay = -1, lensDailyUses;
        public List<DialogueLine> history = new List<DialogueLine>();
        public List<MonsterInstance> storedInstances;
        public DialogueData frame;
        public SettlementData settlement;
        public HashSet<int> settledDays = new HashSet<int>(), paidPhases = new HashSet<int>();
        public Dictionary<int, int> lensUses = new Dictionary<int, int>();
        public List<CommissionEntry> normal = new List<CommissionEntry>(), special = new List<CommissionEntry>();
        public Dictionary<int, int> returnStates = new Dictionary<int, int>();
        public int activeRevengeActor=-1;
        public Dictionary<int, int> revengeDue = new Dictionary<int, int>();
        public List<InventorySlot> inventory = new List<InventorySlot>();
        public List<BoardSlot> board = new List<BoardSlot>();
        public List<int> storedMonsters = new List<int>();
        public int departingVisitor = -1;
        public int currentVisitor = -1;
        public int specialDetailId;
        public string panel;
        public RouteSelectionPanel.RouteState pendingRoute;
        public string routedScene;
        public bool routedWrong;
        public string topLeftPanel;
        public int displaySerial, archiveActor = -1, archiveTab;
        public bool archiveOpen;
        public bool returningFromLens;
        public HashSet<string> displayed = new HashSet<string>();
        public HashSet<string> registeredInfo = new HashSet<string>();
        public Dictionary<string, string> backgroundLevels = new Dictionary<string, string>();
    }
    [Serializable] public sealed class InventorySlot { public int id, count; }
    [Serializable] public sealed class BoardSlot { public string shape, uid; public bool evolved; public string evolutionKind; public int usedDay = -1; public int x, y, rotation; }

    private static readonly Dictionary<string, int> ActorIds = new Dictionary<string, int>
        { {"MY",1}, {"FE",2}, {"DW",3}, {"SA",4}, {"AU",5}, {"DA",6}, {"UN",7} };
    private readonly SystemManager _systems;
    private readonly D20Service _d20 = new D20Service();
    private NarrativeRuntime _runtime;
    private Rules _rules;
    private SaveData _save = new SaveData { uiVersion = 9 };
    private bool _restoring, _booted;
    public bool IsRestoring => _restoring;
    private readonly Dictionary<int, Dictionary<SevenDimension, int>> _attributeBefore = new Dictionary<int, Dictionary<SevenDimension, int>>();
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
    public bool CanOfferSpecial => false; // 已撤除，仅保留旧验收工具的只读接口。
    /// <summary>档案只读查询：完整背景来自第二次透镜揭示，沿用存档中的跨天揭示状态。</summary>
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
    public string GetPortraitAddress(int adventurerId)
    {
        var actor = ActorIds.FirstOrDefault(p => p.Value == adventurerId).Key;
        if (actor == null) return null;
        string name = _systems.Config.Adventurers[adventurerId].name;
        return _rules.portraits.TryGetValue(name, out var address) ? address : null;
    }

    private RunState Run => _systems.RunState.run;
    private string Actor => (string)_runtime.Get("current_actor");
    private SceneRule CurrentScene => _rules.scenes.Find(s => s.id == (string)_runtime.Get("current_scene"));

    public NarrativeSystem(SystemManager systems) { _systems = systems; }

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
                _systems.RunState.NewRun();
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

    private void UpgradeArchiveAndUI()
    {
        // 上一版存档只保存了调查提示；依据已经揭示的 Ink 状态补回原稿，绝不提前解锁。
        foreach (var pair in ActorIds)
        {
            var adv = Run.adventurers[pair.Value];
            bool Has(string list) => ((InkList)_runtime.Get(list)).Keys.Any(k => k.itemName == "B_" + pair.Key + "3");
            string level = Has("bg_full") ? "full" : Has("bg_partial") ? "partial" : null;
            if (level != null && _runtime.BackgroundTexts.TryGetValue(pair.Key + ":" + level, out var text))
            { adv.investigationArchive.Clear(); adv.investigationArchive.Add(text); _save.backgroundLevels[pair.Key] = level; }
        }
        if (_save.departingVisitor > 0) _save.currentVisitor = ActorIds[Actor];
        if (_save.frame != null)
        {
            _save.displayed.Add(_save.displaySerial + ":line:0");
            if (_runtime.Story.currentChoices.Count > 0) _save.frame.nodes[0].options = _runtime.Read().nodes[0].options;
        }
        string path = _runtime.Story.state.currentPathString ?? "";
        if (path.IndexOf("_commission.", StringComparison.OrdinalIgnoreCase) >= 0 && !path.Contains(".c-"))
        { _save.stage = "commission"; _save.frame = null; }
        _save.uiVersion = 2;
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

    /// <summary>原稿四段幕间的首句稳定标识；只在实际新读时演出，重显存档帧不重播。</summary>
    private string _visitorTransitionKey;
    public string ConsumeVisitorTransition(){var key=_visitorTransitionKey;_visitorTransitionKey=null;return key;}

    private void PresentFrameTransition(DialogueData frame)
    {
        if(_restoring)return;
        foreach(var node in frame.nodes)foreach(var line in node.lines)
            switch(line.sourceId) {
                case "DAY03_END.24": case "DAY07_END.32": case "DAY12_END.32": case "DAY19_END.32":
                    PresentationEffects.PlayInterlude(line.sourceId);return;
            }
    }

    private bool ModalClosedThisFrame => UIManager.Instance != null && UIManager.Instance.LastModalCloseFrame == Time.frameCount;
    // 关闭确认/功能弹窗的同一次输入不能再落到右侧剧情；内部恢复读取不受此输入门禁影响。
    public void Next() { if (!ModalClosedThisFrame) _save.frame = null; }

    /// <summary>只在 TalkPanel 完成实际显示后写入；同一存档帧重显不会重复归档。</summary>
    public void RecordDisplayed(DialogueLine line, string suffix)
    {
        if (!Enabled || _save.departingVisitor < 0 || !TryActor(Actor, out var adv)) return;
        if (_save.displayed.Add(_save.displaySerial + ":" + suffix))
        {
            adv.historyDialogue.Add(line.speakerName + "：" + line.content);
            UIManager.Instance?.Get<CharacterArchivePanel>()?.RefreshRecords();
        }
    }

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

    public DialogueOptionResult OpenFunction(DialogueOption option)
    {
        if (ModalClosedThisFrame) return new DialogueOptionResult { cancelJump = true };
        if (option.callbackKey == "open_special_commission") return new DialogueOptionResult { cancelJump = true };
        if (option.callbackKey == "open_commission_panel")
        {
            // 先走原稿入口，保留委托前的角色事件，到明确的委托正文边界再打开面板。
            Choose(option);
            return new DialogueOptionResult();
        }
        _save.panel = "backpack";
        _save.specialDetailId = 0;
        _save.stage = "function";
        OpenWaitingPanel();
        Save();
        return new DialogueOptionResult { waitForExternal = true };
    }

    private void OpenWaitingPanel()
    {
        // 关闭回调由原 UIManager 在恢复此前左侧窗口后调用，不触发访客或日期推进。
        if (_save.panel == "backpack")
            UIManager.Instance.Open<BackpackPanel>(new Action(ReturnFromFunction));
    }

    public void ReturnFromFunction()
    {
        if (_save.panel == null) return;
        _save.stage = "playing"; _save.panel = null;
        Save();
        UIManager.Instance.Get<TalkPanel>()?.Dialogue.ResumeCurrentNode();
    }

    /// <summary>由原背包的“使用”按钮调用，扣除和效果仍只执行原稿中的 item 动作一次。</summary>
    public bool UseBagItem(int id)
    {
        if (_save.stage != "function" || _save.panel != "backpack") return false;
        if (id != 1 && id != 4) { ToastManager.Show("该道具尚无可用的接待操作。"); return false; }
        if (ItemCount(id) < 1) return false;
        int phase = Mathf.Clamp(Run.currentPhase, 1, 5);
        if (id == 1 && !CanUseLens()) return false;
        _runtime.Set("ap", Run.actionPoint);
        if (id == 1)
        {
            _runtime.Story.ChoosePathString("LENS_" + Actor);
            _save.returningFromLens = true;
        }
        else
        {
            _runtime.Story.ChoosePathString("ITEMS");
            _save.stage = "playing";
            var items = _runtime.Read();
            _runtime.Choose(items.nodes[0].options.First(o => o.text.Contains("醒神药剂")).param);
        }
        _save.frame = null;
        return true;
    }

    public int LensDailyUses => _save.lensDay == Run.currentDay ? _save.lensDailyUses : 0;
    public bool CanUseLens()
    {
        if (_save.backgroundLevels.TryGetValue(Actor, out var level) && level == "full")
        { ToastManager.Show("这名冒险者的灵魂宫殿貌似已经干干净净了", Guid.NewGuid().ToString()); return false; }
        if (LensDailyUses >= _rules.lensUseLimit[Mathf.Clamp(Run.currentPhase, 1, 5) - 1])
        { ToastManager.Show("灵魂透镜使用达到当前经营日上限", Guid.NewGuid().ToString()); return false; }
        return true;
    }

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
    private void RestorePendingDice()
    {
        var request=Run.pendingDice;if(request==null||DiceCheckPanel.IsShowing)return;
        if(request.context=="narrative")D20Service.Show(request,result=>{
            Run.pendingDice=request;request.confirmed=true;_save.stage="playing";_save.frame=null;Save();
            UIManager.Instance.Get<TalkPanel>().Dialogue.StartNarrative();
        });
        else if(request.context=="settlement")_systems.DailyCommission.ResolveAllChecksAsync(FinishDayChecks);
    }
    private bool Check(string kind, string actor, int bonus)
    {
        // 共享合作节点指定了说服对象，不能借用进入该节点前的访客。
        if(CheckRules.TryGetValue(_save.checkId??"",out var caller)&&!string.IsNullOrEmpty(caller.actor))actor=caller.actor;
        if (!TryActor(actor, out var adv) || adv.isDead) return false;
        if (kind == "succession") return ResolveSuccession(actor);
        if (Run.pendingDice?.context == "narrative" && Run.pendingDice.confirmed)
        {
            var request=Run.pendingDice; var result=request.Result();
            if (request.playerDay>=1 && request.playerDay<=27 && Run.player.created && kind!="personal")
            {
                var player = Run.player;
                var snapshot = player.pendingThoughtCheck;
                if (!player.resolvedChecks.Contains(request.key))
                {
                    if(request.trainingEligible&&result.Success&&player.TrainInformation(request.checkId+":"+request.trainingReception,request.trainingReception,request.playerAttribute,true))
                    {ToastManager.Show(PlayerBuildState.AttributeName(request.playerAttribute)+"＋1","player-growth:"+request.key);if(!_restoring)AudioManager.Instance?.PlayCue("stat_up");}
                    player.influenceKey=request.playerKind=="influence" && result.Success ? request.key : null;
                    player.influenceActor=adv.configId;
                    if (snapshot != null && snapshot.key == request.key)
                    {
                        player.influenceBonus=0;
                        player.influenceCakeDefault=snapshot.kind=="influence" && snapshot.thought==1 && result.Success;
                        player.ResolveThoughtCheck(snapshot,result.Success);
                        if (snapshot.thought==2 && !result.Success && (snapshot.kind=="information" || snapshot.kind=="influence"))
                            ToastManager.Show("思维“地牢精神病人”生效", "thought-opportunity:"+request.key);
                    }
                    else
                    {
                        // 迁移前已掷出的检定原样落定，保留当时上下文，不套新思维或重新掷骰。
                        player.influenceCakeDefault=false;
                        player.influenceBonus=request.playerThought==1?request.playerLevel:0;
                        player.Resolve(request.key,result.Success);
                    }
                }
                player.pendingThoughtCheck=null;
                TopBarRefreshEvent.Trigger();
            }
            Run.pendingDice=null; _save.checkId=null; return result.Success;
        }
        if (Run.pendingDice==null)
        {
            if (!CheckRules.TryGetValue(_save.checkId??"",out var rule) || rule.kind!=kind)
                throw new InvalidOperationException("剧情检定没有对应节点配置："+_save.checkId+" / "+kind);
            var context=BuildCheckContext(kind,actor,rule.attribute,bonus,rule.player);
            var request=D20Service.CreatePresentation("story:"+_save.displaySerial,adv.Config.name+" · "+(context.player?PlayerBuildState.AttributeName(context.attribute)+"检定":"角色决定"),context.target,context.modifier,"narrative",context.critical);
            request.checkId=rule.checkId;
            request.trainingEligible=context.player&&kind=="information"&&
                (rule.checkId.StartsWith("MYD1_TOPICS.",StringComparison.Ordinal)||rule.checkId.Contains("_TOPICS."));
            request.trainingReception=Run.currentDay+":"+adv.configId+":"+CurrentScene?.id;
            if (context.player)
            {
                request.playerDay=Run.currentDay; request.playerKind=kind; request.playerAttribute=context.attribute;
                request.playerThought=context.thought; request.playerLevel=0;
                Run.player.pendingThoughtCheck=new PlayerBuildState.ThoughtCheckSnapshot {
                    key=request.key,kind=kind,attribute=context.attribute,scene=CurrentScene?.id,
                    actorId=adv.configId,day=Run.currentDay,thought=context.thought,hadSwitchOpportunity=context.hadSwitchOpportunity
                };
                request.details=PlayerBuildState.AttributeName(context.attribute)+Run.player.Attribute(context.attribute)+"（修正"+Signed(context.attributeBonus)+"） · 回答/情境"+Signed(context.modifier-context.attributeBonus-context.thoughtBonus)
                    +(string.IsNullOrEmpty(context.thoughtNote)?"":"\n"+context.thoughtNote)
                    +"\n单次成功率 "+D20Service.SuccessPercent(context.target,context.modifier,context.critical)+"%";
                request.annotation=CheckLabel(context);
                var line=UIManager.Instance.Get<TalkPanel>()?.CurrentDisplayedLine;
                request.storyText=line?.content; request.storySpeaker=line?.speakerName;
            }
            Run.pendingDice=request;
        }
        _save.stage="dice"; throw new NarrativeRuntime.PendingCheckException();
    }

    [Serializable] public sealed class PlayerChoiceRule
    {
        public string choiceId,attribute,kind,label,topic,checkId;
        public bool credit;
    }
    [Serializable] public sealed class PlayerCheckRule
    {
        public string checkId,kind,attribute,actor;
        public bool player;
    }
    [Serializable] public sealed class PlayerNodeConfig
    {
        public List<PlayerChoiceRule> choices;
        public List<PlayerCheckRule> checks;
    }
    private Dictionary<string,PlayerChoiceRule> _playerChoices;
    private Dictionary<string,PlayerCheckRule> _checkRules;
    private void LoadPlayerNodes()
    {
        if(_playerChoices!=null)return;
        var data=JsonConvert.DeserializeObject<PlayerNodeConfig>(Resources.Load<TextAsset>("Narrative/playerChecks").text);
        _playerChoices=data.choices.ToDictionary(r=>r.choiceId);
        _checkRules=data.checks.ToDictionary(r=>r.checkId);
    }
    private Dictionary<string,PlayerChoiceRule> PlayerChoices { get { LoadPlayerNodes(); return _playerChoices; } }
    private Dictionary<string,PlayerCheckRule> CheckRules { get { LoadPlayerNodes(); return _checkRules; } }
    public sealed class PlayerCheckContext
    {
        public string kind,attribute,thoughtNote;
        public int target,modifier,attributeBonus,thoughtBonus;
        public int thought=-1;
        public bool critical=true,player,hadSwitchOpportunity;
    }
    /// <summary>预览与执行都走此公式；预览只传0，不提前计算本次回答的隐藏修正。</summary>
    private PlayerCheckContext BuildCheckContext(string kind,string actor,string attribute,int bonus,bool isPlayer=true)
    {
        var b=D20Service.Balance; TryActor(actor,out var adv);
        var c=new PlayerCheckContext {kind=kind,attribute=attribute,
            target=kind=="information"?b.informationTarget:kind=="influence"?b.influenceTarget:kind=="cooperate"?b.cooperateTarget:b.personalTarget,
            modifier=Mathf.Clamp(bonus*b.dialogueBonus,-b.dialogueBonusCap,b.dialogueBonusCap),player=isPlayer&&Run.player.Active(Run.currentDay)};
        if(kind=="revenge"||kind=="revenge_gift")
        {
            bool heavy=(string)_runtime.Get("revenge_severity")=="heavy";
            int chance=(heavy?b.revengeHeavyChance:b.revengeLightChance)+(kind=="revenge_gift"?b.revengeGiftBonus:0)+bonus*b.dialogueBonus;
            c.target=101-Mathf.Clamp(chance,0,100); c.modifier=0; c.critical=false;
        }
        if(kind=="cooperate")
        {
            SevenDimension x=actor=="FE"?SevenDimension.Courage:actor=="AU"?SevenDimension.Reason:SevenDimension.Resolve;
            SevenDimension y=actor=="AU"?SevenDimension.Benevolence:actor=="FE"?SevenDimension.Resolve:SevenDimension.Reason;
            c.modifier+=Mathf.Clamp((adv.currentSevenStats[x]+adv.currentSevenStats[y]-100)/b.coopStatDivisor,-b.coopStatCap,b.coopStatCap);
        }
        if(c.player)
        {
            var player=Run.player;
            c.thought=player.ThoughtActive(Run.currentDay,Run.currentPhase)?player.thought:-1;
            if(kind=="influence" && c.thought==0 && adv.validInfo.Count>=2)
            {
                string preferred=player.PreferredAttribute;
                if(player.Attribute(preferred)>player.Attribute(c.attribute))c.attribute=preferred;
                c.thoughtNote="你的思维—哈耳摩尼亚史官提醒你，你应该采用“"+PlayerBuildState.AttributeName(c.attribute)+"”来进行判断。";
            }
            else if(kind=="influence" && c.thought==1)
            {
                c.thoughtBonus=10;
                c.thoughtNote="依据你的思维—戚风蛋糕谈判术：骰子修正＋10。";
            }
            if((kind=="information" || kind=="influence") && c.thought==2)
            {
                c.hadSwitchOpportunity=player.HasSwitchOpportunity(adv.configId,Run.currentDay,CurrentScene?.id);
                if(c.hadSwitchOpportunity && c.attribute!=player.switchOpportunityAttribute)
                {
                    c.thoughtBonus=10;
                    c.thoughtNote="依据你的思维—地牢精神病人：骰子修正＋10。";
                }
            }
            c.attributeBonus=player.Attribute(c.attribute)-40;
            c.modifier+=c.attributeBonus+c.thoughtBonus;
        }
        return c;
    }
    public PlayerCheckContext PreviewPlayerCheck(string checkId)
    {
        var rule=CheckRules[checkId]; return BuildCheckContext(rule.kind,string.IsNullOrEmpty(rule.actor)?Actor:rule.actor,rule.attribute,0,rule.player);
    }
    private static string Signed(int value)=>(value>=0?"+":"")+value;
    public static string Difficulty(int chance)=>chance<=24?"极难":chance<=44?"困难":chance<=69?"一般":chance<=84?"轻松":"极易";
    private static string CheckLabel(PlayerCheckContext c)=>"（检定—"+PlayerBuildState.AttributeName(c.attribute)+"　难度："+Difficulty(D20Service.SuccessPercent(c.target,c.modifier,c.critical))+"）";
    /// <summary>只格式化显示，不运行剧情、读随机数或登记成长。</summary>
    private void DecoratePlayerOptions(DialogueData frame)
    {
        foreach(var node in frame.nodes)
        {
            node.options.RemoveAll(o=>o.callbackKey=="open_special_commission");
            foreach(var option in node.options)
            {
                if(string.IsNullOrEmpty(option.choiceId)&&option.param>=0&&option.param<_runtime.Story.currentChoices.Count)option.choiceId=_runtime.Story.currentChoices[option.param].sourcePath;
                option.displayText=option.text; option.checkAttribute=option.checkKind=null;
                option.requiredItemId=RevengeOptionItem(option.choiceId);
                if(option.requiredItemId>0)
                {
                    string name=option.requiredItemId==5?"致歉礼盒":"碎骨卷轴";
                    option.displayText=option.text+"（需要："+name+"）";
                    continue;
                }
                if(string.IsNullOrEmpty(option.choiceId)||!PlayerChoices.TryGetValue(option.choiceId,out var rule))continue;
                if(!string.IsNullOrEmpty(rule.label)){option.text=rule.label;option.displayText=rule.label;}
                if(string.IsNullOrEmpty(rule.checkId)||!Run.player.created)continue;
                var context=PreviewPlayerCheck(rule.checkId);
                option.checkAttribute=context.attribute; option.checkKind=context.kind;
                option.displayText=option.text+"\n<size=22><color=#6A4D27>"+CheckLabel(context)+"</color></size>";
            }
        }
    }
    private void CommitPlayerChoice(DialogueOption option)
    {
        var player=Run.player;
        if(!player.Active(Run.currentDay))return;
        player.influenceKey=null;player.influenceBonus=0;player.influenceCakeDefault=false;
        if(string.IsNullOrEmpty(option.choiceId)&&option.param>=0&&option.param<_runtime.Story.currentChoices.Count)option.choiceId=_runtime.Story.currentChoices[option.param].sourcePath;
        if(player.pendingChoiceCredit){player.Credit(player.pendingChoiceId);player.pendingChoiceCredit=false;}
        if(string.IsNullOrEmpty(option.choiceId)||!PlayerChoices.TryGetValue(option.choiceId,out var rule))return;
        player.pendingChoiceId=option.choiceId; player.pendingAttribute=rule.attribute;
        player.pendingChoiceCredit=rule.credit&&!string.IsNullOrEmpty(rule.checkId);
        if(rule.credit&&string.IsNullOrEmpty(rule.checkId))player.Credit(option.choiceId);
        TopBarRefreshEvent.Trigger();
    }
    // 旧档在一个可选择的停点补选：不在台词中段或尚未建立的检定前插入新状态。
    private bool OfferPlayerAtOptions(DialogueData frame)
    {
        if (Run.player.created || Run.currentDay < 1 || Run.currentDay > 27 || _save.stage != "playing" ||
            Run.pendingDice != null || _save.pendingRoute != null || !frame.nodes.Any(n => n.options.Count > 0)) return false;
        if (!PlayerBuildPanel.IsChoosing) UIManager.Instance.Open<PlayerBuildPanel>(new Action(StartReception));
        return true;
    }
    public bool CreatePlayer(int aptitude, int thought = -1)
    {
        if (Run.currentDay > 27 || !Run.player.Create(aptitude, Run.currentDay < 2 ? -1 : thought)) return false;
        Save(); TopBarRefreshEvent.Trigger(); return true;
    }

    /// <summary>包括“不采用”在内均视为本阶段已确认；关闭预览与返回不能调用本接口。</summary>
    public bool ConfirmThought(int thought)
    {
        if (thought < -1 || thought > 2 || !NeedsThoughtSelection) return false;
        Run.player.thought = thought;
        Run.player.thoughtConfirmedPhase = Run.currentPhase;
        Run.player.EndReceptionThoughts();
        Save(); TopBarRefreshEvent.Trigger(); return true;
    }

    /// <summary>撤除特殊委托的版本迁移：不回滚已发放金币/七维；未支付任务不再结算。</summary>
    private void MigrateRetiredSpecials()
    {
        if (_save.uiVersion >= 5) return;
        _save.special = _save.special ?? new List<CommissionEntry>();
        bool retiredWait = _save.panel == "special" || _save.pendingRoute?.isSpecial == true || Run.pendingDice?.context == "special";
        bool retiredSettlementDice = Run.pendingDice?.context == "settlement" && _save.special.Any(e => Run.pendingDice.key.Contains(":" + e.adventurerId + ":" + e.contractId + ":"));
        if (retiredWait)
        {
            _save.panel = null; _save.pendingRoute = null; _save.stage = "playing";
            if (Run.pendingDice?.context == "special") Run.pendingDice = null;
        }
        if (retiredSettlementDice) { Run.pendingDice = null; _save.stage = "settlement_checks"; }
        if (_save.settlement != null && !_save.settledDays.Contains(Run.currentDay))
        {
            _save.settlement.totalIncome -= _save.settlement.specialCommissionIncome;
            _save.settlement.specialCommissionIncome = 0;
            _save.settlement.notes?.RemoveAll(n => n.StartsWith("特殊委托"));
        }
        foreach (var e in _save.special.Where(e => !e.statsApplied && !string.IsNullOrEmpty(e.targetUid) && !_save.normal.Any(n => n.targetUid == e.targetUid)))
        {
            foreach (var slot in _save.board.Where(b => b.uid == e.targetUid && b.usedDay == Run.currentDay)) slot.usedDay = -1;
            if (_save.storedInstances != null) foreach (var instance in _save.storedInstances.Where(m => m.uid == e.targetUid && m.usedDay == Run.currentDay)) instance.usedDay = -1;
        }
        _save.special.Clear(); _save.specialDetailId = 0;
        if (_save.topLeftPanel == nameof(SpecialCommissionPanel)) _save.topLeftPanel = null;
        if (_save.frame != null) foreach (var node in _save.frame.nodes) node.options.RemoveAll(o => o.callbackKey == "open_special_commission");
        _save.uiVersion = 5;
    }

    private void MigrateGlobalPlayerRules()
    {
        if(_save.uiVersion>=6)return;
        if(!_save.settledDays.Contains(Run.currentDay))
        {
            foreach(var entry in _save.normal)
            {
                var cfg=_systems.Config.Contracts[entry.contractId];
                int target=entry.targetMonsterId>0?entry.targetMonsterId:cfg.targetMonsterId;
                entry.targetMonsterId=target; entry.requestedMonsterType=DailyCommissionSystem.TargetType(cfg);
                entry.wrongRoute=_systems.Config.Monsters[target].monsterType!=entry.requestedMonsterType;
                entry.rankFactor=DailyCommissionSystem.RankFactor(_systems.Config.Adventurers[entry.adventurerId].baseLevel,_systems.Config.Monsters[target].level);
                entry.completionChecked=true; entry.completionRate=DailyCommissionSystem.RewardRate(entry,cfg);
                if(entry.checksResolved)
                {
                    var lines=(entry.outcomeText??"").Split('\n').Where(s=>!s.StartsWith("报酬：")&&!s.StartsWith("等级系数："));
                    entry.outcomeText=string.Join("\n",lines)+"\n等级系数："+Mathf.RoundToInt(entry.completionRate*100)+"% · 报酬："+Mathf.RoundToInt(cfg.reward*entry.completionRate)+"金币";
                }
            }
            if(_save.settlement!=null)
            {
                int amount=_systems.DailyCommission.ComputeAll().normalIncome;
                _save.settlement.totalIncome+=amount-_save.settlement.legacyCommissionIncome;
                _save.settlement.legacyCommissionIncome=amount;
                _save.settlement.notes=_systems.DailyCommission.ResultNotes();
            }
            if(Run.pendingDice?.context=="settlement"&&Run.pendingDice.key.EndsWith(":income",StringComparison.Ordinal))
            { Run.pendingDice=null; _save.stage="settlement_checks"; }
        }
        if(_save.frame!=null)foreach(var node in _save.frame.nodes)foreach(var line in node.lines)line.content=ExpandText(line.content);
        _save.uiVersion=6;
    }

    private void MigrateTalkAndRevenge()
    {
        string path=_runtime.Story.state.currentPathString??"";
        if(_save.uiVersion<9)
        {
            if(path.StartsWith("REVENGE",StringComparison.Ordinal)&&TryActor(Actor,out var current))_save.activeRevengeActor=current.configId;
            // 礼盒在进入旧检定前已经扣除；迁移只完成礼盒结果，不能再次扣物或赔款。
            if(Run.pendingDice?.playerKind=="revenge_gift"||Run.pendingDice?.checkId=="REVENGE_GIFT.14")
            {Run.pendingDice=null;Run.player.pendingThoughtCheck=null;_save.checkId=null;_save.frame=null;_save.stage="playing";_runtime.Story.ChoosePathString("REVENGE_GIFT_RESOLVED");}
            _save.uiVersion=9;
        }
        foreach(var pair in ActorIds)
            if(_runtime.HasMember("pending_revenge",pair.Key)&&!_save.revengeDue.ContainsKey(pair.Value))
                _save.revengeDue[pair.Value]=_rules.scenes.Where(x=>x.actor==pair.Key&&x.day>=Run.currentDay).Select(x=>x.day).DefaultIfEmpty(Run.currentDay).Min();
    }

    private void MigrateDoc6PlayerRules()
    {
        if (_save.uiVersion >= 8) return;
        if (Run.player == null) Run.player = new PlayerBuildState();
        var player=Run.player;
        // 旧0/1/2依次映射新三思维，类型与三属性保持原值；安全整备时再确认本阶段。
        if (player.thought < -1 || player.thought > 2 || Run.currentDay < 2) player.thought=-1;
        player.thoughtConfirmedPhase=0;
        player.ClearSwitchOpportunity();
        player.pendingThoughtCheck=null;
        player.pendingChoiceCredit=false;
        player.failureStreak=0;
        // 已建骰子及它此前绑定的属性结果不重算，旧熟练度只保留为兼容数据。
        if (string.IsNullOrEmpty(Run.pendingEvolutionUid)) Run.pendingEvolutionKind="";
        _save.uiVersion=8;
        Run.saveVersion=9;
    }

    private static bool MeetsSuccession(string actor, AdventurerRuntimeData adv)
    {
        int S(SevenDimension stat) => adv.currentSevenStats.TryGetValue(stat, out int v) ? v : 0;
        // 数值来自最新《冒险者属性配置表 v0.1》，不沿用旧配置的占位主副属性。
        switch (actor)
        {
            case "MY": return S(SevenDimension.Tolerance) >= 90 && S(SevenDimension.Resolve) >= 75;
            case "FE": return S(SevenDimension.Courage) >= 80 && S(SevenDimension.Resolve) <= 40;
            case "DW": return S(SevenDimension.Benevolence) >= 80 && S(SevenDimension.Diligence) >= 75;
            case "SA": return S(SevenDimension.Piety) <= 35 && S(SevenDimension.Courage) >= 65;
            case "AU": return S(SevenDimension.Reason) <= 30 && S(SevenDimension.Benevolence) <= 30;
            case "DA": return S(SevenDimension.Diligence) >= 85 && S(SevenDimension.Piety) <= 30;
            case "UN": return S(SevenDimension.Resolve) <= 10 && S(SevenDimension.Reason) <= 5;
            default: return false;
        }
    }

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

    private DialogueData TerminalFrame() => new DialogueData
    {
        dialogueId = "story27", startNodeId = "end", nodes = new List<DialogueNode>
        {
            new DialogueNode { id = "end", speakerName = "系统", contents = new List<string> { "本次故事已结束。" },
                options = new List<DialogueOption> { new DialogueOption { text = "继续" } } }
        }
    };

    private string ExpandText(string text)
    {
        if(_rules.textOverrides.TryGetValue(text,out var replacement))text=replacement;
        // 只更新玩家可见的骰制说明；D20_V01等是“第20天”的稳定剧情ID，不能全局替换。
        text=text.Replace("D20检定","D100检定").Replace("D20大失败","D100的1～5大失败");
        int due = text.Contains("27天后") ? _systems.Config.Schedules.Sum(s => s.installmentAmount) :
            _systems.Config.GetInstallmentPayment(Run.currentPhase);
        text = text.Replace("【本局配置金额】", due.ToString());
        var scene = CurrentScene;
        if (scene != null)
        {
            if (text.StartsWith("报酬展示：", StringComparison.Ordinal))
                return "基础报酬：" + scene.reward + "金币";
            if (text.StartsWith("目标需求：", StringComparison.Ordinal) && (text.Contains("待主策配置") || text.Contains("由委托配置显示")))
                return "目标类别：" + DailyCommissionSystem.TypeName(DailyCommissionSystem.TargetType(_systems.Config.Contracts[scene.contractId]));
        }
        return text;
    }
    private string Portrait(string speaker)
    {
        if (_rules.portraits.TryGetValue(speaker, out var address)) return address;
        // 接待菜单、玩家回答和旁白不代表人物离开，继续保留当前访客的立绘。
        return _save.departingVisitor > 0 ? GetPortraitAddress(_save.departingVisitor) ?? "Narrative/blank" : "Narrative/blank";
    }
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
    private static int RevengeOptionItem(string id)
    {
        switch(id){case "REVENGE_OPTIONS.0.11":return 5;case "REVENGE_OPTIONS.0.17":case "REVENGE_TRAP_CONFIRM.0.9":return 6;default:return 0;}
    }
    public bool IsOptionAvailable(DialogueOption option)
    {int item=RevengeOptionItem(option.choiceId);return item==0||ItemCount(item)>0;}

    private static int RequiredItem(string text)
    {
        if (text.Contains("调查") && text.Contains("往事")) return 1;
        if (text.Contains("醒神药剂")) return 4;
        if (text.Contains("使用致歉礼盒")) return 5;
        if (text.Contains("碎骨") && !text.Contains("不用") && !text.Contains("不使用")) return 6;
        return 0;
    }
    private static int ItemId(string kind)
    {
        switch (kind) { case "lens": return 1; case "stimulant": return 4; case "apology_gift": return 5; case "bone_trap": return 6; default: throw new ArgumentException(kind); }
    }
    private int ItemCount(int id)
    {
        int count = Run.itemInventory.TryGetValue(id, out int old) ? old : 0;
        var inventory = InventoryModel.Instance;
        if (inventory != null)
            for (int i = 0; i < inventory.Capacity; i++)
            { var slot = inventory.GetSlot(i); if (!slot.IsEmpty && slot.data.id == id) count += slot.quantity; }
        return count;
    }
    private bool ConsumeItem(int id)
    {
        var inventory = InventoryModel.Instance;
        if (inventory != null)
            for (int i = 0; i < inventory.Capacity; i++)
            { var slot = inventory.GetSlot(i); if (!slot.IsEmpty && slot.data.id == id) return inventory.RemoveItem(i); }
        if (Run.itemInventory.TryGetValue(id, out int count) && count > 0) { Run.itemInventory[id]--; return true; }
        return false;
    }

    public void Capture()
    {
        if (!Enabled) return;
        BackpackBoard.Instance?.CancelPlacement(); // 存档不保存悬空预摆，先安全恢复已固定布局。
        var archive = UIManager.Instance?.Get<CharacterArchivePanel>();
        _save.topLeftPanel = UIManager.Instance?.CurrentLeftPanelName;
        _save.archiveOpen = archive != null && archive.IsOpen;
        if (archive != null) { _save.archiveActor = archive.CurrentActor; _save.archiveTab = archive.CurrentTab; }
        _save.specialDetailId = 0;
        _save.history = DialogueHistoryModel.Instance?.Lines.ToList() ?? new List<DialogueLine>();
        _save.ink = _runtime.Story.state.ToJson();
        _save.currentVisitor = _systems.VisitSystem.CurrentVisitorId;
        _save.speaker = _runtime.Speaker;
        _save.normal = _systems.DailyCommission.NormalCommissions.ToList();
        _save.special.Clear(); // 旧字段只用于迁移，不再写入任务。
        _save.inventory.Clear();
        var inventory = InventoryModel.Instance;
        if (inventory != null)
            for (int i = 0; i < inventory.Capacity; i++)
            { var slot = inventory.GetSlot(i); if (!slot.IsEmpty) _save.inventory.Add(new InventorySlot { id = slot.data.id, count = slot.quantity }); }
        _save.board.Clear();
        if (BackpackBoard.Instance != null)
            foreach (var view in BackpackBoard.Instance.Items)
                if (view != null && view.Item != null) _save.board.Add(new BoardSlot
                { shape = view.Def.Id, uid = view.Item.Uid, evolved = view.Item.Evolved, usedDay=view.Item.UsedDay, evolutionKind=view.Item.EvolutionKind, x = view.Item.Origin.x, y = view.Item.Origin.y, rotation = view.Item.Rotation });
        _save.storedMonsters = MonsterStorageModel.Instance.Items.Select(m => m.monsterId).ToList();
        _save.storedInstances = MonsterStorageModel.Instance.Items.Select(m => new MonsterInstance(m.monsterId) { uid = m.uid, evolved = m.evolved, usedDay=m.usedDay, evolutionKind=m.evolutionKind }).ToList();
        // 地狱框中的魔物尚未确认分解，不能因本体暂时隐藏而漏存。
        // 读档时取消这次未确认操作，将同一实例退回仓库，不发放分解奖励。
        var pending = UIManager.Instance?.Get<DecomposePanel>()?.PendingItem;
        if (pending != null && int.TryParse(pending.Def.Id, out int pendingId) &&
            !_save.storedInstances.Any(m => m.uid == pending.Uid) && !_save.board.Any(b => b.uid == pending.Uid))
            _save.storedInstances.Add(new MonsterInstance(pendingId) { uid = pending.Uid, evolved = pending.Evolved, usedDay=pending.UsedDay, evolutionKind=pending.EvolutionKind });
        Run.narrativeSave = JsonConvert.SerializeObject(_save);
    }
    public void Save() { _systems.RunState.Save(); }

    private void RestoreWorld()
    {
        var inventory = InventoryModel.Instance;
        if (inventory != null)
        {
            for (int i = 0; i < inventory.Capacity; i++) inventory.GetSlot(i).Clear();
            foreach (var slot in _save.inventory)
                if (_systems.Config.Items.TryGetValue(slot.id, out var cfg))
                    inventory.AddItem(new ItemStack { data = ItemData.FromConfig(cfg), quantity = slot.count });
        }
        var board = BackpackBoard.Instance;
        if (board != null)
        {
            foreach (var stage in UnityEngine.Object.FindObjectsOfType<StageController>()) stage.ApplyStage(Run.currentPhase);
            foreach (var view in board.Items.ToArray())
                if (view != null) { if (view.Item != null) board.RemoveItem(view.Item); UnityEngine.Object.Destroy(view.gameObject); }
            foreach (var slot in _save.board)
            {
                var def = _systems.Config.ShapeDb.Get(slot.shape);
                if (def != null && board.CanPlace(def, new Vector2Int(slot.x, slot.y), slot.rotation))
                {
                    var go = new GameObject("RestoredMonster", typeof(RectTransform));
                    go.transform.SetParent(board.ItemsRoot, false);
                    var restored = go.AddComponent<ItemView>();
                    restored.Init(def, board, slot.rotation, new Vector2Int(slot.x, slot.y));
                    restored.SetInstanceState(slot.uid, slot.evolved,slot.usedDay,slot.evolutionKind);
                }
            }
        }
        MonsterStorageModel.Instance.Clear();
        if (_save.storedInstances != null)
            foreach (var inst in _save.storedInstances) MonsterStorageModel.Instance.Add(inst.monsterId, inst.uid, inst.evolved,inst.usedDay,inst.evolutionKind);
        else foreach (int id in _save.storedMonsters) MonsterStorageModel.Instance.Add(id);
        BackpackChanged.Trigger();
        if (_save.currentVisitor > 0) _systems.VisitSystem.SelectNarrativeVisitor(_save.currentVisitor);
    }
    [Serializable] public sealed class SuccessionCondition { public SevenDimension stat; public int value; public bool maximum; }
    public sealed class SuccessionAssessment
    {
        public int actorId,gap,target,modifier,chance;
        public string actor,name,tendency,progress,attribute;
        public bool dead;
        public string Annotation => "（检定—"+PlayerBuildState.AttributeName(attribute)+"　难度："+Difficulty(chance)+"）";
    }
    [Serializable] public sealed class PortraitCue { public string actor,pose; public bool injury,hide; }
    private Dictionary<string,PortraitCue> _portraitCues;
    public string EndingStage => _save.stage;
    public string EndingPresentationPhase => _save.endingPresentationPhase;
    // 转场仅登记展示阶段；读取途中档时由面板恢复到可见CG，不再判定或结算。
    public void SetEndingPresentationPhase(string phase){_save.endingPresentationPhase=phase;Save();}
    public int EndingCandidate => _save.endingCandidate;
    public string EndingExpression => string.IsNullOrEmpty(_save.endingExpression) ? Run.player.PreferredAttribute : _save.endingExpression;
    public string EndingActorCode => _save.ending.Split(':').Last();
    public string ActorCode(int id) => ActorIds.First(p=>p.Value==id).Key;
    public string BasePortrait(int id) => _rules.portraitVariants[ActorCode(id)]["neutral"];
    public string EndingCGAddress => _rules.endingCG[EndingActorCode];
    public string EndingActorName => _systems.Config.Adventurers[ActorIds[EndingActorCode]].name;

    // 档案、确认框和正式检定只读取这一份评估，最大不足值不会被其他超标属性抵消。
    public SuccessionAssessment AssessSuccession(int actorId)
        => AssessSuccession(actorId, Run.adventurers[actorId].currentSevenStats, EndingExpression);

    public SuccessionAssessment AssessSuccession(int actorId, string attribute)
        => AssessSuccession(actorId, Run.adventurers[actorId].currentSevenStats, attribute);

    /// <summary>读取指定七维快照，供变化前后比较；不会修改人物、掷骰或登记成长。</summary>
    public SuccessionAssessment AssessSuccession(int actorId, IReadOnlyDictionary<SevenDimension, int> stats)
        => AssessSuccession(actorId, stats, EndingExpression);

    private SuccessionAssessment AssessSuccession(int actorId, IReadOnlyDictionary<SevenDimension, int> stats, string attribute)
    {
        if (!IsPlayerAttribute(attribute)) attribute=Run.player.PreferredAttribute;
        string code=ActorCode(actorId);var adv=Run.adventurers[actorId];int gap=0;var parts=new List<string>();
        foreach(var c in _rules.successionRules[code]) {
            int v=stats[c.stat],deficit=Math.Max(0,c.maximum?v-c.value:c.value-v);gap=Math.Max(gap,deficit);
            parts.Add(StatChinese(c.stat)+" "+v+" / "+(c.maximum?"≤":"≥")+c.value+"　"+(deficit==0?"已满足":"尚差"+deficit));
        }
        // 接任只用当前所选表达对应的玩家属性，不套普通信息/影响思维。
        int modifier=Run.player.Active(Run.currentDay)?Run.player.Attribute(attribute)-40:0;
        int target=101-(_rules.successionChanceStart-_rules.successionChanceStep*gap);
        return new SuccessionAssessment{actorId=actorId,actor=code,name=adv.Config.name,gap=gap,dead=adv.isDead,attribute=attribute,progress=string.Join("\n",parts),
            tendency=gap==0?"容易接受":gap<=_rules.successionNearGap?"不感兴趣":"非常厌恶",target=target,modifier=modifier,
            chance=gap==0?100:gap>_rules.successionNearGap?0:D20Service.SuccessPercent(target,modifier)};
    }

    private static bool IsPlayerAttribute(string attribute) => attribute=="perception" || attribute=="knowledge" || attribute=="willpower";
    /// <summary>这里只保存表达选择；预览、切换、返回都不会掷骰、消耗道具或变更人物。</summary>
    public void SelectEndingExpression(string attribute)
    {
        if (_save.stage!="ending_confirm" || !IsPlayerAttribute(attribute) || Run.pendingDice!=null) return;
        var assessment=AssessSuccession(_save.endingCandidate,attribute);
        if (assessment.gap==0 || assessment.gap>_rules.successionNearGap || assessment.dead) return;
        _save.endingExpression=attribute;
        Save();
    }

    /// <summary>只显示文字倾向变化；培养差距属于隐藏规则，不反馈具体数值。</summary>
    public string DescribeSuccessionChange(int actorId, IReadOnlyDictionary<SevenDimension, int> beforeStats)
    {
        var after = AssessSuccession(actorId);
        if (after.dead) return "";
        var before = AssessSuccession(actorId, beforeStats);
        if (before.tendency != after.tendency) return "接任倾向：" + before.tendency + "→" + after.tendency;
        return "";
    }
    /// <summary>兼容旧档展示：只移除旧系统生成的接任差距，保留同一行的属性、报酬和原稿。</summary>
    public static string HidePrivateSuccessionProgress(string text)
    {
        if(string.IsNullOrEmpty(text))return text??"";
        if(!text.Contains("接任差距"))return text;
        text=System.Text.RegularExpressions.Regex.Replace(text,@"接任差距\s*\d+\s*(?:→|->)\s*\d+", "");
        return string.Join("\n",System.Linq.Enumerable.Where(text.Split('\n'),line=>!string.IsNullOrWhiteSpace(line)));
    }
    private static string StatChinese(SevenDimension stat)
    { switch(stat){case SevenDimension.Reason:return "理智";case SevenDimension.Courage:return "勇气";case SevenDimension.Piety:return "虔诚";case SevenDimension.Tolerance:return "宽容";case SevenDimension.Benevolence:return "仁爱";case SevenDimension.Resolve:return "坚定";default:return "勤勉";} }
    private bool ResolveSuccession(string actor)
    {
        var a=AssessSuccession(ActorIds[actor]);if(a.dead)return false;
        if(Run.pendingDice?.context=="narrative"&&Run.pendingDice.confirmed) {
            var p=Run.pendingDice;bool success=p.Result().Success;
            if(!p.FixedOutcome&&Run.player.created)Run.player.Resolve(p.key,success);
            Run.pendingDice=null;_save.checkId=null;_save.successionFailed=!success;return success;
        }

        if(Run.pendingDice==null) {
            bool forced=a.gap>_rules.successionNearGap,accepted=a.gap==0;
            // 必败不是抽签机会，不启动护符或幸运硬币，也不登记成长。
            var p=forced||accepted?new D20Service.Presentation{sides=100,key="succession:"+actor,title=a.name+" · 接任说服",context="narrative",target=accepted?1:101,roll=UnityEngine.Random.Range(1,101),criticalRules=false,guaranteedFailure=forced,guaranteedSuccess=accepted}
                :D20Service.CreatePresentation("succession:"+actor,a.name+" · 接任说服",a.target,a.modifier,"narrative");
            p.rawRoll=p.roll;p.playerDay=Run.currentDay;p.playerKind="succession_persuade";p.playerAttribute=a.attribute;p.checkId=_save.checkId;
            p.annotation=a.Annotation;p.details=accepted?"容易接受":forced?"非常厌恶":PlayerBuildState.AttributeName(a.attribute)+Run.player.Attribute(a.attribute)+" · 属性修正"+Signed(a.modifier);
            p.storyText="你向"+a.name+"提出接任。";p.storySpeaker="旁白";Run.pendingDice=p;
        }
        _save.stage="dice";throw new NarrativeRuntime.PendingCheckException();
    }
    private bool InterceptEndingSelection(DialogueData frame)
    {
        if(!frame.nodes.Any(n=>n.options.Any(o=>(o.choiceId??"").StartsWith("END_SELECTION.",StringComparison.Ordinal))))return false;
        if(_save.successionFailed) {
            _save.successionFailed=false;_save.frame=null;_save.stage="playing";_runtime.Story.ChoosePathString("END_SWALLOWED");return true;
        }
        _save.frame=frame;_save.stage="ending_selection";Save();return true;
    }
    public void PreviewEndingCandidate(int id)
    {
        if(_save.stage!="ending_selection"||Run.adventurers[id].isDead)return;
        _save.endingCandidate=id;_save.endingExpression=Run.player.PreferredAttribute;_save.stage="ending_confirm";Save();UIManager.Instance.Open<EndingPresentationPanel>().OnRefresh();
    }
    public void CancelEndingCandidate()
    { if(_save.stage!="ending_confirm")return;_save.stage="ending_selection";_save.endingCandidate=-1;_save.endingExpression=null;Save();UIManager.Instance.Get<EndingPresentationPanel>().OnRefresh(); }
    public bool ConfirmSuccessor(int actorId, string attribute)
    {
        if (_save.stage!="ending_confirm" || _save.endingCandidate!=actorId || !IsPlayerAttribute(attribute) || Run.adventurers[actorId].isDead || Run.pendingDice!=null) return false;
        SelectEndingExpression(attribute);
        ConfirmEndingCandidate();
        return true;
    }
    public void ConfirmEndingCandidate(bool stay=false)
    {
        if(!stay&&_save.stage!="ending_confirm" || stay&&_save.stage!="ending_selection")return;
        string name=stay?null:_systems.Config.Adventurers[_save.endingCandidate].name;
        if(!stay&&Run.adventurers[_save.endingCandidate].isDead){CancelEndingCandidate();return;}
        var choice=_runtime.Story.currentChoices.First(c=>stay?c.text.Contains("不再将经营权"):c.text.Contains(name));
        _save.stage="playing";_save.frame=null;_runtime.Choose(choice.index);
        UIManager.Instance.Close<EndingPresentationPanel>();Save();StartReception();
    }
    public void CompleteEndingCG()
    { if(_save.stage!="ending_cg")return;_save.stage="ending_complete";_save.endingPresentationPhase="complete";Save(); }
    private void OpenEndingPresentation()
    {
        UIManager.Instance.Close<CharacterArchivePanel>();
        if(_save.stage!="ending_cg")UIManager.Instance.Close<TalkPanel>();
        UIManager.Instance.Open<EndingPresentationPanel>();
    }
    private string ResolvePortrait(DialogueLineData line)
    {
        if(_save.activeRevengeActor>0&&ActorIds.TryGetValue(Actor,out int id)&&id==_save.activeRevengeActor)
        {
            _save.portraitAddress=_rules.portraitVariants[Actor]["hurt"];
            return _save.portraitAddress;
        }
        if(_portraitCues==null)_portraitCues=JsonConvert.DeserializeObject<Dictionary<string,PortraitCue>>(Resources.Load<TextAsset>("Narrative/portraitCues").text);
        if(!string.IsNullOrEmpty(line.sourceId)&&_portraitCues.TryGetValue(line.sourceId,out var cue)) {
            if(cue.hide){_save.portraitAddress="Narrative/blank";return _save.portraitAddress;}
            string actor=string.IsNullOrEmpty(cue.actor)?Actor:cue.actor;
            if(actor!=null&&_rules.portraitVariants.TryGetValue(actor,out var variants)) {
                foreach(var path in variants.Values)SpriteLoader.LoadAsync(path);
                string pose=cue.pose=="hurt"&&!cue.injury?"serious":cue.pose;
                if(!variants.TryGetValue(pose??"neutral",out var address))address=variants["neutral"];
                _save.portraitAddress=address;return address;
            }
        }
        var original=Portrait(line.speaker);_save.portraitAddress=original;return original;
    }
    private void MigrateEndingPresentation()
    {
        if(_save.uiVersion>=7)return;
        // 已完成的旧接任档只显示完成文字，不重新说服、播放剧情或消耗物品。
        if(_save.stage=="ended"&&_save.ending.StartsWith("succession:",StringComparison.Ordinal))_save.stage="ending_complete";
        string path=_runtime.Story.state.currentPathString??"";
        if(new[]{"HIDDEN_BATTLE","HIDDEN_TRANSFER","HIDDEN_SAMPLE","HIDDEN_ABANDON"}.Any(k=>path.StartsWith(k+".",StringComparison.Ordinal))) {
            _runtime.Story.ChoosePathString("HIDDEN_CANCEL");_save.stage="playing";_save.frame=null;
        }
        if(_save.frame!=null)foreach(var n in _save.frame.nodes)foreach(var line in n.lines)line.icon=ResolvePortrait(line);
        _save.uiVersion=7;
    }

}

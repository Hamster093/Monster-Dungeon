#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>决策闭环快验：仅在隔离工程启动真实新局，随后使用明确的规则夹具与界面截图，不冒充自然通关。</summary>
[InitializeOnLoad]
public static class DecisionLoopQuickChecks
{
    const string Key = "MonsterDungeon.DecisionLoopQuickChecks";
    const string Output = "../DecisionLoop";
    static IEnumerator routine;
    static double started, next;
    static bool capturing, finished;
    static readonly List<string> checks = new List<string>();
    static readonly List<string> errors = new List<string>();
    static readonly List<string> shots = new List<string>();
    static NarrativeSystem Campaign => SystemManager.Instance.Narrative;
    static RunState Run => RunStateManager.Instance.run;
    static NarrativeSystem.SaveData State => Field<NarrativeSystem.SaveData>(Campaign, "_save");
    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(owner);
    static object Invoke(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(owner, args);
    static void Check(bool value, string name) { if (!value) throw new Exception(name); checks.Add(name); }
    static string InventorySnapshot() => JsonConvert.SerializeObject(new {
        slots = Enumerable.Range(0, InventoryModel.Instance.Capacity).Select(index => {
            var slot = InventoryModel.Instance.GetSlot(index);
            return new { id = slot.IsEmpty ? 0 : slot.data.id, quantity = slot.quantity };
        }).ToArray(), pending = Run.itemInventory
    });

    static DecisionLoopQuickChecks()
    {
        if (SessionState.GetBool(Key, false)) Install();
    }

    public static void RunBatch()
    {
        if (!Application.productName.EndsWith("_NarrativeValidation", StringComparison.Ordinal))
            throw new Exception("快验只允许在隔离验证工程运行，禁止操作正式存档。");
        // 每次快验都先应用本轮编辑器布局，避免旧预制体同步把确认框末句裁掉。
        DecisionLoopIntegration.Configure();
        Directory.CreateDirectory(Output);
        string autosave = Path.Combine(Application.persistentDataPath, "run_state.json");
        if (File.Exists(autosave)) File.Copy(autosave, Path.Combine(Output, "PreviousAutosave.json"), true);
        SaveAndLoadManager.Delete("run_state");
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene("Assets/Scenes/PlayerGame.unity");
        Size(1920, 1080);
        Install();
        EditorApplication.EnterPlaymode();
    }

    static void Install()
    {
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Application.logMessageReceived -= Log;
        Application.logMessageReceived += Log;
    }

    static void Log(string text, string stack, LogType kind)
    {
        if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) errors.Add(text);
    }

    static void Tick()
    {
        if (finished || !EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + .04;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if (EditorApplication.timeSinceStartup - started > 180) throw new Exception("决策闭环快验超时");
            if (errors.Count > 0) throw new Exception(errors[0]);
            if (capturing || !SystemManager.Instance.IsInitialized || !Field<bool>(Campaign, "_booted")) return;
            if (routine == null) routine = Scenario();
            if (!routine.MoveNext()) Finish(true, "");
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }

    static void Finish(bool passed, string failure)
    {
        finished = true;
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "QuickAcceptance.json"), JsonConvert.SerializeObject(new {
            passed, failure, checks, errors, screenshots = shots,
            method = "真实新局初始化；其后为隔离夹具规则检查、程序化界面打开与渲染截图，非自然27天通关，也不等同真实鼠标验收。"
        }, Formatting.Indented));
        SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= Log;
        EditorApplication.isPlaying = false;
        EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
    }

    static IEnumerator Scenario()
    {
        for (int i = 0; i < 8; i++) yield return null;
        Check(Run.currentDay == 0 && Run.adventurers.Count == 7 && !Run.player.created, "真实新局保留七人且尚未选类型");
        Check(InventoryModel.Instance.CountId(1) == 2, "新局两枚透镜已从运行状态恢复至真实背包");
        Check(Run.BusinessWorldReady, "Boot恢复活世界后才标记供给检查就绪");
        var rules = Field<NarrativeSystem.Rules>(Campaign, "_rules");
        Check(rules.attributeDelta == 3 && Campaign.EvolutionDelta == 5, "剧情基础幅度3与进化幅度5独立");
        Check(EvolutionPanel.Effect("慎重").Contains("5"), "进化详情使用新的5点幅度");
        Check(Campaign.CreatePlayer(2, 1), "测试夹具建立精神力与共情玩家");
        UIManager.Instance.Close<PlayerBuildPanel>();
        SupplyChecks();
        SnapshotChecks();

        var db = ConfigDatabase.Instance;
        const int contractId = 99001;
        db.Contracts[contractId] = new ContractConfig {
            id = contractId, name = "快验委托", description = "隔离夹具", reward = 100,
            targetMonsterId = 101, targetMonsterType = MonsterType.Slime
        };
        var actor = Run.adventurers[1];
        actor.isDead = false; actor.isSeverelyInjured = false; actor.health = HealthState.Normal;
        var inventoryBefore = InventorySnapshot();
        var queuesBefore = JsonConvert.SerializeObject(SystemManager.Instance.DailyCommission.NormalCommissions);
        var diceBefore = JsonConvert.SerializeObject(Run.pendingDice);
        var randomBefore = UnityEngine.Random.state;
        string correct = DescriptionPanel.RouteSummary(1, contractId, 101);
        string wrong = DescriptionPanel.RouteSummary(1, contractId, 104);
        Check(correct.Contains("类别：符合") && correct.Contains("80金币"), "同类D级对C级冒险者摘要显示80金币");
        Check(wrong.Contains("类别：不符") && wrong.Contains("0金币"), "异类指路摘要明示0报酬");
        actor.isSeverelyInjured = true;
        Check(DescriptionPanel.RouteSummary(1, contractId, 101).Contains("再次重伤会死亡"), "重伤指路摘要显示死亡风险");
        actor.isSeverelyInjured = false;
        Check(inventoryBefore == InventorySnapshot() &&
              queuesBefore == JsonConvert.SerializeObject(SystemManager.Instance.DailyCommission.NormalCommissions) &&
              diceBefore == JsonConvert.SerializeObject(Run.pendingDice) && randomBefore.Equals(UnityEngine.Random.state),
              "指路摘要预览不改库存、委托、骰子及随机状态");

        SettlementChecks(contractId);
        InfoChecks();
        foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) })
        {
            Size(size.x, size.y);
            for (int i = 0; i < 5; i++) yield return null;
            UIManager.Instance.Close<EndingPresentationPanel>();
            UIManager.Instance.Close<CharacterArchivePanel>();
            UIManager.Instance.Close<TalkPanel>();
            var instance = new MonsterInstance(101) { evolved = true, evolutionKind = "慎重" };
            var detail = UIManager.Instance.Open<DescriptionPanel>(new DescriptionPanel.OpenContext {
                item = ItemData.FromMonster(instance), combatAdventurerId = 1, contractId = contractId,
                onConfirm = () => { }, onReturn = () => { }
            });
            for (int i = 0; i < 5; i++) yield return null;
            Check(Field<Text>(detail, "_itemDescriptionText").text.Contains("80金币") &&
                  Field<Text>(detail, "_itemDescriptionText").text.Contains("宽容 ＋5"), "指路详情摘要及进化效果实际绑定 " + size.x);
            Shot("指路摘要_" + size.x); yield return null;
            var descriptionScroll = Field<ScrollRect>(detail, "_descriptionScroll");
            descriptionScroll.verticalNormalizedPosition = 0;
            for (int i = 0; i < 4; i++) yield return null;
            Check(descriptionScroll.content.rect.height >= Field<Text>(detail, "_itemDescriptionText").preferredHeight + 25,
                "详情底部保留字形留白，进化说明可滚入视口 " + size.x);
            Shot("进化说明底部_" + size.x); yield return null;
            UIManager.Instance.Close<DescriptionPanel>();

            actor.currentSevenStats[SevenDimension.Tolerance] = 80;
            actor.currentSevenStats[SevenDimension.Resolve] = 75;
            State.stage = "ending_confirm"; State.endingCandidate = 1; State.ending = "";
            var ending = UIManager.Instance.Open<EndingPresentationPanel>();
            for (int i = 0; i < 6; i++) yield return null;
            string confirmation = Field<Text>(ending, "_confirmText").text;
            Check(confirmation.Contains("不感兴趣") && confirmation.Contains("吞噬"), "接任确认展示倾向与不可逆失败风险 " + size.x);
            var confirmationText = Field<Text>(ending, "_confirmText");
            Check(ending.IsOpen && ending.gameObject.activeInHierarchy && confirmationText.preferredHeight <= confirmationText.rectTransform.rect.height + 2,
                "确认框实际可见且包括风险末句的全文高度可容纳 " + size.x);
            Shot("终局确认_" + size.x); yield return null;
            UIManager.Instance.Close<EndingPresentationPanel>();
            State.stage = "playing";
            // 档案嵌在TalkPanel中。先建立有效接待和可见父面板，不能只刷新隐藏预制体的文字字段。
            var runtime = Field<NarrativeRuntime>(Campaign, "_runtime");
            runtime.Set("current_actor", "MY"); runtime.Set("current_scene", "D01_V01");
            State.currentVisitor = State.departingVisitor = 1;
            SystemManager.Instance.VisitSystem.SelectNarrativeVisitor(1);
            UIManager.Instance.ClosePreparationViews();
            UIManager.Instance.Get<PreparationPanel>()?.SetReceptionAvailable(false);
            var talk = UIManager.Instance.Open<TalkPanel>();
            talk.SetAutoCompleteAtEnd(false); talk.SetInstantText(true);
            talk.UpdateBackgroundImage(Campaign.BasePortrait(1));
            talk.OpenArchive(1, 0);
            var archive = UIManager.Instance.Get<CharacterArchivePanel>();
            for (int i = 0; i < 4; i++) yield return null;
            Check(Campaign.CanBrowseArchive && talk.IsOpen && talk.gameObject.activeInHierarchy &&
                  archive.IsOpen && archive.gameObject.activeInHierarchy && Field<Text>(archive, "_succession").gameObject.activeInHierarchy,
                "有效MY接待下Talk及嵌套档案均实际可见 " + size.x);
            Check(Field<Text>(archive, "_succession").text.Contains(Campaign.AssessSuccession(1).progress), "档案使用相同实时接任评估 " + size.x);
            Shot("档案进度_" + size.x); yield return null;
            UIManager.Instance.Close<CharacterArchivePanel>();
        }
        db.Contracts.Remove(contractId);
    }

    static void SupplyChecks()
    {
        var db = ConfigDatabase.Instance;
        var scenes = new[] {
            new NarrativeSystem.SceneRule { day = 6, order = 1, actor = "MY", targetMonsterType = MonsterType.Slime },
            new NarrativeSystem.SceneRule { day = 6, order = 2, actor = "FE", targetMonsterType = MonsterType.Goblin },
            new NarrativeSystem.SceneRule { day = 6, order = 3, actor = "DW", targetMonsterType = MonsterType.Canine }
        };
        var living = new HashSet<string> { "MY", "FE", "DW" };
        List<int> Select(ISet<MonsterType> owned) => RunState.SelectGuaranteedMonsterIds(scenes, 6, 1, living, owned, db.Monsters.Values, db.Items);
        var selected = Select(new HashSet<MonsterType>());
        Check(selected.SequenceEqual(new[] { 101, 104 }), "缺失类别按访客顺序保障最低等级商品且最多两种");
        Check(Select(new HashSet<MonsterType> { MonsterType.Slime }).SequenceEqual(new[] { 104, 107 }), "已拥有类别不占保障货位");
        living.Remove("MY");
        Check(Select(new HashSet<MonsterType>()).SequenceEqual(new[] { 104, 107 }), "死亡或不在存活集合的人物不产生需求");
        living.Clear();
        Check(Select(new HashSet<MonsterType>()).Count == 0, "无存活访客不补保障商品");
        var trade = new RunState { currentDay = 6, currentPhase = 2, tradeDay = 6, tradePhase = 2 };
        trade.monsterOffers.Add(108); trade.shopStock[108] = 0; trade.shopStock[1] = 1;
        string before = JsonConvert.SerializeObject(trade);
        trade.EnsureBusinessDay();
        Check(JsonConvert.SerializeObject(trade) == before, "同日刷新不回补已售魔物或道具库存");
        trade.tradePhase = 1;
        string saved = JsonConvert.SerializeObject(trade);
        trade = JsonConvert.DeserializeObject<RunState>(saved);
        trade.EnsureBusinessDay();
        Check(JsonConvert.SerializeObject(trade) == saved && trade.shopStock[108] == 0,
            "同日存档往返保留售罄0库存，即使阶段字段不同也不补货");
        trade.currentDay++;
        trade.EnsureBusinessDay();
        Check(trade.monsterOffers.Count == 4 && trade.monsterOffers.Distinct().Count() == 4 && trade.monsterOffers.All(id => trade.shopStock[id] == 1),
            "跨经营日刷新四个不重复货位且各库存1");
        var legacy = new RunState(); legacy.itemInventory[1] = 5;
        legacy = JsonConvert.DeserializeObject<RunState>(JsonConvert.SerializeObject(legacy));
        Check(legacy.itemInventory[1] == 5, "旧状态存档往返保留5枚透镜，不套用新局赠送2");
    }

    static void SnapshotChecks()
    {
        var actor = Run.adventurers[1]; var stats = actor.currentSevenStats;
        stats[SevenDimension.Resolve] = 75; stats[SevenDimension.Tolerance] = 70;
        var before = new Dictionary<SevenDimension, int>(stats);
        stats[SevenDimension.Tolerance] = 73;
        Check(string.IsNullOrEmpty(Campaign.DescribeSuccessionChange(1, before)), "同档不反馈接任数值差距");
        stats[SevenDimension.Tolerance] = 69; before = new Dictionary<SevenDimension, int>(stats);
        stats[SevenDimension.Tolerance] = 72;
        Check(Campaign.DescribeSuccessionChange(1, before) == "接任倾向：非常厌恶→不感兴趣", "21到18跨档优先反馈倾向");
        stats[SevenDimension.Tolerance] = 90; before = new Dictionary<SevenDimension, int>(stats);
        stats[SevenDimension.Tolerance] = 87;
        Check(Campaign.DescribeSuccessionChange(1, before) == "接任倾向：容易接受→不感兴趣", "0到3退档反馈正确");
        before = new Dictionary<SevenDimension, int>(stats);
        Check(Campaign.DescribeSuccessionChange(1, before) == "", "无属性变化不生成接任提示");
        stats[SevenDimension.Courage] -= 1;
        Check(Campaign.DescribeSuccessionChange(1, before) == "", "只改变非接任条件不产生虚假进度");
        string state = JsonConvert.SerializeObject(stats);
        var assessment = Campaign.AssessSuccession(1, before);
        Check(assessment.gap == 3 && JsonConvert.SerializeObject(stats) == state, "快照评估不临时修改运行中七维");
    }

    static void SettlementChecks(int contractId)
    {
        var daily = SystemManager.Instance.DailyCommission;
        var actor = Run.adventurers[1]; var stats = actor.currentSevenStats;
        stats[SevenDimension.Tolerance] = 70; stats[SevenDimension.Resolve] = 75; stats[SevenDimension.Courage] = 55;
        actor.isDead = actor.isSeverelyInjured = false; actor.health = HealthState.Normal;
        var entry = new CommissionEntry(contractId, 1) {
            targetMonsterId = 101, requestedMonsterType = MonsterType.Slime, evolutionKind = "慎重",
            combatChecked = true, completionChecked = true, injury = InjuryResult.None
        };
        daily.Clear(); daily.AddNormal(entry); daily.ResolveAllChecks();
        Check(stats[SevenDimension.Tolerance] == 75 && stats[SevenDimension.Courage] == 50, "生还结算只施加一次进化正负5点");
        Check(!entry.outcomeText.Contains("接任差距") && entry.outcomeText.Contains("80金币"), "同一委托结果合并培养进度与实际报酬");
        string previous = JsonConvert.SerializeObject(stats), note = entry.outcomeText;
        daily.ResolveAllChecks();
        Check(JsonConvert.SerializeObject(stats) == previous && entry.outcomeText == note, "重复结算不重复改变属性或重写进度");
        daily.Clear();
        var migrated = new CommissionEntry(contractId, 1) {
            targetMonsterId = 101, requestedMonsterType = MonsterType.Slime, evolutionKind = "慎重",
            combatChecked = true, statsApplied = true, injury = InjuryResult.None
        };
        daily.AddNormal(migrated); daily.ResolveAllChecks();
        Check(JsonConvert.SerializeObject(stats) == previous && !migrated.outcomeText.Contains("接任"), "已应用属性的旧队列不追补进化或进度");
        daily.Clear();
    }

    static void InfoChecks()
    {
        int count = Run.adventurers[1].validInfo.Count;
        Invoke(Campaign, "HandleAction", "action:info actor=MY id=DECISION_LOOP_QA key=快验信息");
        Invoke(Campaign, "HandleAction", "action:info actor=MY id=DECISION_LOOP_QA key=快验信息");
        Check(Run.adventurers[1].validInfo.Count == count + 1, "首次信息归档且重复事件不重复登记");
        ToastManager.Instance?.Clear();
    }

    static void Size(int width, int height)
    {
        var view = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
        view.Show();
        typeof(NarrativeIntegrationChecks).GetMethod("SetGameViewSize", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { view, width, height });
    }

    static void Shot(string name)
    {
        capturing = true;
        UIManager.Instance.StartCoroutine(Capture(name));
    }

    static IEnumerator Capture(string name)
    {
        yield return new WaitForSecondsRealtime(.2f);
        Canvas.ForceUpdateCanvases();
        int width = Screen.width, height = Screen.height;
        var camera = Camera.main;
        var target = new RenderTexture(width, height, 24);
        var previous = camera.targetTexture; var active = RenderTexture.active;
        var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas).ToArray();
        var modes = canvases.Select(c => c.renderMode).ToArray();
        var cameras = canvases.Select(c => c.worldCamera).ToArray();
        camera.targetTexture = target;
        foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; }
        Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
        Directory.CreateDirectory(Path.Combine(Output, "Shots"));
        string path = Path.Combine(Output, "Shots", name + ".png");
        File.WriteAllBytes(path, texture.EncodeToPNG()); shots.Add(path);
        for (int i = 0; i < canvases.Length; i++) { canvases[i].renderMode = modes[i]; canvases[i].worldCamera = cameras[i]; }
        camera.targetTexture = previous; RenderTexture.active = active;
        UnityEngine.Object.Destroy(texture); UnityEngine.Object.Destroy(target);
        capturing = false;
    }
}
#endif

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
using UnityEngine.SceneManagement;

/// <summary>规则、事件系统指针与渲染验收分开记录；只允许隔离工程写档。</summary>
[InitializeOnLoad]
public static class Doc4AcceptanceChecks
{
    const string Key = "MonsterDungeon.Doc4QA";
    static readonly List<string> checks = new List<string>(), errors = new List<string>(), shots = new List<string>();
    static readonly HashSet<string> visited = new HashSet<string>(), topics = new HashSet<string>(), enteredTopics = new HashSet<string>(), commissions = new HashSet<string>();
    static IEnumerator routine; static double started, next;
    static bool busy, profileChecked, archiveChecked, diceReloaded;
    static int delay, diceCount, annotationCount;
    static string firstDayMenuJson;
    static T F<T>(object obj, string name) => (T)obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(obj);
    static void Set(object obj, string name, object value) => obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(obj, value);
    static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(obj, args);
    static void Check(bool value, string name) { if (!value) throw new Exception(name); checks.Add(name); }
    static void Click(Component component) => ArtAcceptanceChecks.Pointer(component);
    static NarrativeSystem Campaign => SystemManager.Instance.Narrative;
    static NarrativeSystem.SaveData Save => F<NarrativeSystem.SaveData>(Campaign, "_save");
    static NarrativeRuntime Runtime => F<NarrativeRuntime>(Campaign, "_runtime");
    static RunState Run => RunStateManager.Instance.run;
    static T Copy<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
    static Doc4AcceptanceChecks()
    {
        if (SessionState.GetBool(Key, false)) { checks.AddRange(JsonConvert.DeserializeObject<List<string>>(SessionState.GetString("Doc4Rules", "[]"))); EditorApplication.update += Tick; Application.logMessageReceived += Log; started = EditorApplication.timeSinceStartup; }
    }
    static void Log(string message, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }
    static void Isolated() { if (!Application.productName.EndsWith("_NarrativeValidation")) throw new Exception("禁止在正式用户工程运行写档测试"); }
    public static void RunBatch()
    {
        Isolated(); Rules();
        SaveAndLoadManager.Delete("run_state"); SessionState.SetString("Doc4Rules", JsonConvert.SerializeObject(checks)); SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene("Assets/Scenes/PlayerGame.unity");
        var view = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")); view.Show();
        typeof(NarrativeIntegrationChecks).GetMethod("SetGameViewSize", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { view, 1920, 1080 });
        EditorApplication.update += Tick; Application.logMessageReceived += Log; started = EditorApplication.timeSinceStartup; EditorApplication.EnterPlaymode();
    }
    public static void ConfigureAndRun() { Doc4IntegrationEditor.Configure(); RunBatch(); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + .035; EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if (EditorApplication.timeSinceStartup - started > 420) throw new Exception("说明4验收超时，当前日=" + Run?.currentDay + "，状态=" + Save?.stage);
            if (errors.Count > 0) throw new Exception(errors[0]);
            if (busy || !SystemManager.Instance.IsInitialized || !F<bool>(Campaign, "_booted")) return;
            if (delay > 0) { delay--; return; }
            if (routine == null) routine = Scenario();
            if (!routine.MoveNext()) Finish(true, "");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Finish(bool passed, string failure)
    {
        File.WriteAllText("Doc4Acceptance.json", JsonConvert.SerializeObject(new { passed, failure, checks, errors, shots, visited, diceCount, annotationCount }, Formatting.Indented));
        SessionState.SetBool(Key, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        EditorApplication.isPlaying = false; EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
    }
    static void Rules()
    {
        for (int type = 0; type < 3; type++) for (int thought = 0; thought < 3; thought++)
        {
            var p = new PlayerBuildState(); Check(p.Create(type, thought), "开局组合 " + type + "/" + thought);
            Check(p.perception + p.knowledge + p.willpower == 125 && p.Attribute(new[] { "perception", "knowledge", "willpower" }[type]) == 55, "三属性独立起点 " + type + "/" + thought);
            Check(!p.Create((type + 1) % 3, (thought + 1) % 3), "本局类型/思维不能重选 " + type + "/" + thought);
        }
        var state = new PlayerBuildState(); state.Create(0, 0);
        Check(state.ThoughtModifier("information", false) == 0 && state.ThoughtModifier("information", true) == 5, "求证只在已有本人物信息时生效");
        state.proficiency = 10; Check(state.Level == 2 && state.ThoughtModifier("information", true) == 10, "求证二阶");
        state.proficiency = 25; Check(state.Level == 3 && state.ThoughtModifier("information", true) == 15, "求证三阶");
        Check(state.ThoughtModifier("influence", true) == 0, "求证不加成影响检定");
        state = new PlayerBuildState(); state.Create(2, 2);
        for (int i = 0; i < 8; i++) state.Resolve("fail" + i, false);
        Check(state.ThoughtModifier("influence", false) == 10, "坚韧一阶失败加成封顶10");
        state.proficiency = 10; Check(state.ThoughtModifier("information", false) == 15, "坚韧二阶封顶15");
        state.proficiency = 25; Check(state.ThoughtModifier("information", false) == 20, "坚韧三阶封顶20");
        state.Resolve("success", true); Check(state.failureStreak == 0, "坚韧成功后清零");
        state.pendingChoiceId = "once"; state.pendingChoiceCredit = true; int xp = state.proficiency;
        state.Resolve("same-check", false); state.pendingChoiceCredit = true; state.Resolve("same-check", false);
        Check(state.proficiency == xp + 1 && !state.Credit("once"), "选择和检定结果按稳定ID去重");
        var restored = Copy(state); Check(restored.proficiency == state.proficiency && !restored.Credit("once") && restored.failureStreak == state.failureStreak, "读档保留熟练度与失败累计且不刷成长");
        Check(!state.Active(0) && state.Active(1) && state.Active(3) && !state.Active(4), "第4天起关闭新玩法加成和成长");
        Check(D20Service.SuccessPercent(65, 15) == 51 && D20Service.SuccessPercent(65, -5) == 31, "D100优势/短板基础成功率51%/31%");
        Check(D20Service.SuccessPercent(65, 999) == 95 && D20Service.SuccessPercent(65, -999) == 5, "自然极值保持5%/95%边界");
        Check(NarrativeSystem.Difficulty(24) == "极难" && NarrativeSystem.Difficulty(25) == "困难" && NarrativeSystem.Difficulty(45) == "一般" && NarrativeSystem.Difficulty(70) == "轻松" && NarrativeSystem.Difficulty(85) == "极易", "五档难度分界");
        NarrativeIntegrationChecks.ValidateScript(); Check(true, "原稿27天和全部终局入口仍可解析，不等于终局养成可达性验证");
    }
    static void Shot(string name)
    {
        busy = true; UIManager.Instance.StartCoroutine(Capture(name));
    }
    static IEnumerator Capture(string name)
    {
        yield return new WaitForSecondsRealtime(.4f); yield return null; Canvas.ForceUpdateCanvases();
        string folder = Path.GetFullPath("../qa/Doc4"); Directory.CreateDirectory(folder);
        var camera = Camera.main; var target = new RenderTexture(1920, 1080, 24); var previous = camera.targetTexture; var active = RenderTexture.active;
        var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas).ToArray(); var modes = canvases.Select(c => c.renderMode).ToArray(); var cameras = canvases.Select(c => c.worldCamera).ToArray();
        camera.targetTexture = target; for (int i = 0; i < canvases.Length; i++) { canvases[i].renderMode = RenderMode.ScreenSpaceCamera; canvases[i].worldCamera = camera; }
        Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
        string path = Path.Combine(folder, name + ".png"); File.WriteAllBytes(path, image.EncodeToPNG());
        for (int i = 0; i < canvases.Length; i++) { canvases[i].renderMode = modes[i]; canvases[i].worldCamera = cameras[i]; }
        camera.targetTexture = previous; RenderTexture.active = active; UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(target); shots.Add(path); busy = false;
    }
    static ItemView Spawn(int id)
    {
        var board = BackpackBoard.Instance; var definition = ConfigDatabase.Instance.ShapeDb.Get(id.ToString());
        for (int y = 0; y < 7; y++) for (int x = 0; x < 7; x++) if (board.CanPlace(definition, new Vector2Int(x, y), 0))
        { var go = new GameObject("Doc4Fixture" + id, typeof(RectTransform)); go.transform.SetParent(board.ItemsRoot, false); var view = go.AddComponent<ItemView>(); view.Init(definition, board, 0, new Vector2Int(x, y)); return view; }
        throw new Exception("测试棋盘无位置 " + id);
    }
    static void Option(DialogueOption option)
    {
        var candidates = UnityEngine.Object.FindObjectsOfType<DialogueOptionItem>().Where(v => F<Button>(v, "_button").interactable).OrderBy(v => v.transform.GetSiblingIndex()).ToArray();
        int index = Save.frame.nodes[0].options.IndexOf(option);
        if (index < 0 || index >= candidates.Length) throw new Exception("显示选项和剧情帧不对应");
        Click(F<Button>(candidates[index], "_button"));
    }
    static IEnumerator Scenario()
    {
        while (!PlayerBuildPanel.IsChoosing) yield return null;
        Shot("01_开局类型与固定思维"); yield return null;
        var creation = UIManager.Instance.Get<PlayerBuildPanel>(); int ap = Run.actionPoint;
        Check(!F<Button>(creation, "_confirm").interactable, "未完成两个选择不能提交");
        Click(F<Button[]>(creation, "_types")[0]); Click(F<Button[]>(creation, "_thoughts")[0]);
        Click(F<Button>(creation, "_confirm"));
        Check(Run.player.created && Run.player.perception == 55 && Run.actionPoint == ap, "实际指针完成开局选择且不消耗行动点");
        yield return null;
        while (Run.currentDay < 4)
        {
            var ui = UIManager.Instance; var talk = ui.Get<TalkPanel>(); var save = Save;
            if (DiceCheckPanel.IsShowing)
            {
                var panel = ui.Get<DiceCheckPanel>(); if (!F<bool>(panel, "_ready")) { yield return null; continue; }
                var request = Run.pendingDice; diceCount++;
                if (request.playerDay > 0) Check(request.details.Contains("修正") && request.details.Contains("单次成功率"), "玩家检定上下文显示实际修正 " + request.key);
                if (!diceReloaded && request.playerDay > 0)
                {
                    diceReloaded = true; request.roll = 85; Campaign.Save(); RunStateManager.Instance.SaveSlot(1);
                    int beforeXp = Run.player.proficiency, beforeAP = Run.actionPoint; string pending = JsonConvert.SerializeObject(request);
                    SceneManager.LoadScene("PlayerGame"); for (int i = 0; i < 20; i++) yield return null;
                    Check(DiceCheckPanel.IsShowing && JsonConvert.SerializeObject(Run.pendingDice) == pending && Run.player.proficiency == beforeXp && Run.actionPoint == beforeAP, "待确认检定读档不重掷/不成长/不扣点");
                    continue;
                }
                request.roll = 85; panel.Complete(); yield return null; continue;
            }
            if (save.stage == "settlement")
            {
                var panel = ui.Get<DailySettlementPanel>(); var data = F<SettlementData>(panel, "_data");
                Check(data.specialCommissionIncome == 0 && ui.Configs.All(c => c.panelName != nameof(SpecialCommissionPanel)), "结算无特殊收入且场景无特殊委托注册 日" + Run.currentDay);
                Shot("结算_第" + Run.currentDay + "天"); yield return null;
                var close = F<Button>(panel, "btn_Close"); Click(close); int cash = Run.cash; close.onClick.Invoke();
                Check(Run.cash == cash, "结算重复确认不重复发放 日" + Run.currentDay); yield return null; continue;
            }
            if (Campaign.IsPreparing)
            {
                if (Run.currentDay == 1 && BackpackBoard.Instance.Items.Count == 0) Spawn(101);
                Click(F<Button>(ui.Get<PreparationPanel>(), "_startReceptionButton")); yield return null; continue;
            }
            if (RouteSelectionPanel.IsShowing)
            {
                var route = ui.Get<RouteSelectionPanel>();
                if (ui.Get<DescriptionPanel>()?.IsRouteContext == true)
                {
                    var button = F<Button>(ui.Get<DescriptionPanel>(), "_routeConfirmButton"); Click(button); int count = SystemManager.Instance.DailyCommission.NormalCommissions.Count; button.onClick.Invoke();
                    Check(SystemManager.Instance.DailyCommission.NormalCommissions.Count == count, "普通委托重复指路确认只入队一次");
                }
                else
                {
                    int target = ConfigDatabase.Instance.Contracts[route.State.contractId].targetMonsterId;
                    var view = BackpackBoard.Instance.Items.FirstOrDefault(v => v != null && v.Item != null && v.Def.Id == target.ToString() && !v.Item.IsResting) ?? Spawn(target);
                    Check(SystemManager.Instance.DailyCommission.NormalCommissions.All(e => e.contractId != route.State.contractId), "选择魔物前不提前接单");
                    Click(view.GetComponentsInChildren<Image>().First(i => i.gameObject.activeInHierarchy));
                }
                yield return null; continue;
            }
            if (save.stage == "commission")
            {
                var panel = ui.Get<CommissionPanel>(); if (panel == null || !panel.IsOpen) { talk.MoveNext(); yield return null; continue; }
                string sceneId = (string)Runtime.Get("current_scene");
                Check(!F<Text>(panel, "txt_Objectives").text.Contains("配置") && !F<Text>(panel, "txt_Payment").text.Contains("配置"), "普通委托无占位目标/报酬 " + sceneId);
                if (!commissions.Add(sceneId)) { Click(F<Button>(panel, "btn_AcceptCommission")); }
                else
                {
                    Shot("普通委托_" + sceneId); yield return null;
                    int count = SystemManager.Instance.DailyCommission.NormalCommissions.Count; Click(F<Button>(panel, "CloseButton"));
                    Check(SystemManager.Instance.DailyCommission.NormalCommissions.Count == count, "查看后返回不接单 " + sceneId);
                }
                yield return null; continue;
            }
            if (talk == null || !talk.IsOpen || save.frame == null) { yield return null; continue; }
            talk.SetInstantText(true); Set(talk, "_clickCooldown", 0f);
            var options = save.frame.nodes[0].options;
            if (options.Count == 0) { talk.MoveNext(); yield return null; continue; }
            var renderedOptions = UnityEngine.Object.FindObjectsOfType<DialogueOptionItem>().Count(o => F<Button>(o, "_button").interactable);
            if (renderedOptions != options.Count) { talk.MoveNext(); yield return null; continue; }
            string scene = (string)Runtime.Get("current_scene"); visited.Add(scene);
            Check(options.All(o => o.callbackKey != "open_special_commission" && !o.text.Contains("特殊委托")), "剧情菜单没有特殊委托入口 " + scene);
            foreach (var o in options.Where(o => !string.IsNullOrEmpty(o.checkKind)))
            { annotationCount++; Check(o.displayText.Contains("（检定—") && o.displayText.Contains("难度：") && !o.text.Contains("难度："), "检定标注仅用于显示 " + o.choiceId); }
            bool menu = options.Any(o => o.callbackKey == "open_commission_panel");
            if (menu)
            {
                if (!profileChecked)
                {
                    profileChecked = true;
                    var hud = ui.Get<HUDPanel>(); var entry = F<Button>(hud, "_playerButton");
                    File.WriteAllText("Doc4HUD.json", JsonConvert.SerializeObject(new { has=entry!=null, active=entry?.gameObject.activeInHierarchy, text=F<Text>(hud,"_playerText")?.text, position=entry?.transform.position.ToString(), scale=entry?.transform.lossyScale.ToString(), layer=entry?.gameObject.layer }));
                    var entryText = F<Text>(hud, "_playerText"); Check(entryText.preferredHeight <= entryText.rectTransform.rect.height, "顶部自身状态入口文字不被裁掉");
                    Click(entry);
                    Check(ui.IsOpen<PlayerBuildPanel>() && !ui.HasLeftOverlay, "自身状态入口可点击、非模态且可继续右侧剧情");
                    Shot("02_自身状态与右侧接待并行"); yield return null;
                }
                if (!archiveChecked && Run.adventurers[1].validInfo.Count > 0)
                {
                    archiveChecked = true; ui.Close<PlayerBuildPanel>(); talk.OpenArchive(1, 0); var archive = ui.Get<CharacterArchivePanel>(); string ink = Runtime.Story.state.ToJson();
                    foreach (var button in new[] { F<Button>(archive, "_basicInfo"), F<Button>(archive, "_dialogueLog"), F<Button>(archive, "_validInfo"), F<Button>(archive, "_investigationLog") }) { Click(button); yield return null; }
                    Check(Runtime.Story.state.ToJson() == ink && archive.CurrentTab == 3, "四档案页签实际指针切换，不推进剧情"); ui.Close<CharacterArchivePanel>();
                }
                if (scene == "D01_V01" && topics.Contains(scene) && firstDayMenuJson == null)
                { Campaign.Capture(); firstDayMenuJson = JsonConvert.SerializeObject(Run); }
                if (!enteredTopics.Contains(scene) && Run.actionPoint > 0)
                { enteredTopics.Add(scene); Option(options.First(o => o.text.Contains("搭话"))); }
                else Option(options.First(o => o.callbackKey == "open_commission_panel"));
            }
            else if (options.Any(o => o.text.Contains("返回正式接待")))
            {
                if (topics.Add(scene)) Option(options.First(o => !o.text.Contains("返回")));
                else Option(options.First(o => o.text.Contains("返回正式接待")));
            }
            else
            {
                var chosen = options.FirstOrDefault(o => o.checkKind == "influence") ?? options.LastOrDefault(o => o.checkKind == "information") ?? options[0];
                if (!string.IsNullOrEmpty(chosen.checkKind)) { Shot("剧情检定_" + scene); yield return null; }
                Option(chosen);
            }
            yield return null;
        }
        Check(visited.Count == 6 && commissions.Count == 6 && topics.Count == 6, "前三天六次接待、普通委托与搭话均走通");
        Check(Run.currentDay == 4 && !Run.player.Active(Run.currentDay), "第3天正常衔接第4天，保留新状态并停止新加成");
        Check(Run.player.proficiency > 0 && Run.player.creditedChoices.Count == Run.player.proficiency, "熟练度只来自有效且唯一的选择");
        Shot("03_第四天衔接"); yield return null;
        for (int slot = 1; slot <= 4; slot++)
        {
            var copy = Copy(Run); copy.cash = 1000 + slot; copy.player.proficiency = slot;
            SaveAndLoadManager.Save(SaveAndLoadManager.SlotKey(slot), copy);
        }
        for (int slot = 1; slot <= 4; slot++) { var read = SaveAndLoadManager.Load<RunState>(SaveAndLoadManager.SlotKey(slot)); Check(read.cash == 1000 + slot && read.player.proficiency == slot, "手动槽独立保存玩家状态 槽" + slot); }
        SaveSlotsPanel.Show(false); Shot("04_四槽读取界面"); yield return null;
        var slotsPanel = (SaveSlotsPanel)typeof(SaveSlotsPanel).GetField("current", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null); slotsPanel.OnClose();
        for (int slot = 1; slot <= 4; slot++)
        {
            Check(SaveAndLoadManager.ActivateSlot(slot), "激活手动槽 " + slot); SceneManager.LoadScene("PlayerGame");
            for (int i = 0; i < 20; i++) yield return null;
            Check(Run.cash == 1000 + slot && Run.player.proficiency == slot && Run.currentDay == 4, "实际场景读档恢复独立槽 " + slot);
        }
        var migration = Migrations(); while (migration.MoveNext()) yield return migration.Current;
        // 验证共情真实属性动作，避免只有数据类测试而业务未应用。
        while (SaveSlotsPanel.IsShowing) { yield return null; }
        var originalPlayer = Run.player; int originalDay = Run.currentDay; Run.currentDay = 1;
        for (int rank = 1; rank <= 3; rank++)
        {
            var p = new PlayerBuildState(); p.Create(2, 1); p.proficiency = rank == 1 ? 0 : rank == 2 ? 10 : 25; Run.player = p;
            Run.pendingDice = new D20Service.Presentation { sides = 100, roll = 85, target = 60, confirmed = true, context = "narrative", key = "empathy" + rank, playerDay = 1, playerKind = "influence", playerThought = 1, playerLevel = rank };
            Check((bool)Call(Campaign, "Check", "influence", "MY", 0), "共情先读取实际成功检定 " + rank);
            var actor = Run.adventurers[1]; actor.currentSevenStats[SevenDimension.Tolerance] = 50;
            Call(Campaign, "HandleAction", "action:attribute actor=MY stat=tolerance direction=down amount=config");
            Check(actor.currentSevenStats[SevenDimension.Tolerance] == 47 - rank, "共情真实下降幅度三档 " + rank);
            actor.currentSevenStats[SevenDimension.Tolerance] = 50;
            Call(Campaign, "HandleAction", "action:attribute actor=MY stat=tolerance direction=up amount=config");
            Check(actor.currentSevenStats[SevenDimension.Tolerance] == 53 + rank, "共情真实上升幅度三档 " + rank);
            Run.pendingDice = new D20Service.Presentation { sides = 100, roll = 15, target = 60, confirmed = true, context = "narrative", key = "empathy-fail" + rank, playerDay = 1, playerKind = "influence", playerThought = 1, playerLevel = rank };
            Check(!(bool)Call(Campaign, "Check", "influence", "MY", 0), "共情失败检定 " + rank);
            actor.currentSevenStats[SevenDimension.Tolerance] = 50;
            Call(Campaign, "HandleAction", "action:attribute actor=MY stat=tolerance direction=up amount=config");
            Check(actor.currentSevenStats[SevenDimension.Tolerance] == 53, "失败不强化七维变化 " + rank);
        }
        Run.player = originalPlayer; Run.currentDay = originalDay;
    }
    static IEnumerator Migrations()
    {
        // 使用当前真实第4天进度造旧档，仅更改兼容字段，绝不执行任务或奖励。
        if (SaveSlotsPanel.IsShowing) { var type = typeof(SaveSlotsPanel); var current = (SaveSlotsPanel)type.GetField("current", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null); current.OnClose(); }
        var baseline = Copy(Run); Campaign.Capture(); baseline.narrativeSave = Run.narrativeSave;
        for (int mode = 0; mode < 3; mode++)
        {
            var legacy = Copy(baseline); legacy.cash = 777; var data = JsonConvert.DeserializeObject<NarrativeSystem.SaveData>(legacy.narrativeSave);
            data.uiVersion = 4; data.special.Add(new CommissionEntry(201, 1)); data.normal.Add(new CommissionEntry(1011, 1)); data.panel = "special"; data.stage = "function";
            if (mode == 1) { data.pendingRoute = new RouteSelectionPanel.RouteState { isSpecial = true, contractId = 211, adventurerId = 1 }; data.stage = "routing"; }
            if (mode == 2) { legacy.pendingDice = new D20Service.Presentation { sides = 100, context = "special", key = "special:201:1", roll = 42 }; data.stage = "dice"; }
            legacy.narrativeSave = JsonConvert.SerializeObject(data); SaveAndLoadManager.Save("run_state", legacy); SceneManager.LoadScene("PlayerGame");
            for (int i = 0; i < 25; i++) yield return null;
            Check(Save.uiVersion == 5 && Save.special.Count == 0 && Save.panel == null && Save.pendingRoute == null && Run.pendingDice?.context != "special", "旧特殊委托等待安全退出 类型" + mode);
            Check(Run.cash == 777 && SystemManager.Instance.DailyCommission.SpecialCommissions.Count == 0, "迁移不回滚已发金币、不增加特殊队列 类型" + mode);
            Check(SystemManager.Instance.DailyCommission.NormalCommissions.Any(e => e.contractId == 1011), "特殊迁移保留普通委托 类型" + mode);
            Check(!PlayerBuildPanel.IsChoosing, "第4天旧档不强制补选 类型" + mode);
        }
        var unpaid = Copy(baseline); var unpaidData = JsonConvert.DeserializeObject<NarrativeSystem.SaveData>(unpaid.narrativeSave);
        unpaid.cash = 888; unpaidData.uiVersion = 4; unpaidData.stage = "settlement";
        unpaidData.settlement = new SettlementData(100, 20, 10, 130, 150); unpaidData.special.Add(new CommissionEntry(201, 1));
        unpaid.narrativeSave = JsonConvert.SerializeObject(unpaidData); SaveAndLoadManager.Save("run_state", unpaid); SceneManager.LoadScene("PlayerGame");
        for (int i = 0; i < 20; i++) yield return null;
        Check(Run.cash == 888 && Save.settlement.totalIncome == 110 && Save.settlement.specialCommissionIncome == 0, "旧待支付结算取消特殊收入，不追扣已发金币");
        var oldMenu = JsonConvert.DeserializeObject<RunState>(firstDayMenuJson); oldMenu.player = null;
        int oldAP = oldMenu.actionPoint, oldCash = oldMenu.cash, oldRecords = oldMenu.adventurers[1].historyDialogue.Count;
        SaveAndLoadManager.Save("run_state", oldMenu); SceneManager.LoadScene("PlayerGame");
        for (int i = 0; i < 20; i++) yield return null;
        Check(PlayerBuildPanel.IsChoosing, "前三天旧档在真实选项停点补选");
        var creation = UIManager.Instance.Get<PlayerBuildPanel>(); Click(F<Button[]>(creation, "_types")[1]); Click(F<Button[]>(creation, "_thoughts")[2]); Click(F<Button>(creation, "_confirm"));
        yield return null;
        Check(Run.player.created && Run.player.knowledge == 55 && Run.player.proficiency == 0 && Run.actionPoint == oldAP && Run.cash == oldCash, "旧档补选保留剧情/资源且不追补经验");
        Check(Run.adventurers[1].historyDialogue.Count == oldRecords && Run.adventurers[1].validInfo.Count > 0, "旧档补选不重复记录，保留已知信息");
        // 恢复已建立的普通剧情检定时，不能应用新属性或重新掷骰。
        var pending = Copy(Run); var pendingData = JsonConvert.DeserializeObject<NarrativeSystem.SaveData>(pending.narrativeSave);
        pending.player = null; pendingData.stage = "dice"; pending.pendingDice = new D20Service.Presentation { sides = 100, context = "narrative", key = "old-narrative", roll = 42, rawRoll = 42, target = 68, modifier = 6 };
        pending.narrativeSave = JsonConvert.SerializeObject(pendingData); SaveAndLoadManager.Save("run_state", pending); SceneManager.LoadScene("PlayerGame");
        for (int i = 0; i < 20; i++) yield return null;
        Check(DiceCheckPanel.IsShowing && !PlayerBuildPanel.IsChoosing && Run.pendingDice.roll == 42 && Run.pendingDice.target == 68 && Run.pendingDice.modifier == 6, "旧普通待确认检定完整保留原参数");
        UIManager.Instance.Close<DiceCheckPanel>(); Run.pendingDice = null;
    }
}
#endif

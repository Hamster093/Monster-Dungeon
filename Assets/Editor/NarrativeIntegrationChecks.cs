#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 开发者验收入口。批处理测试只能在独立验证项目中运行，防止覆盖正式存档。
/// 点击真实界面按钮跑 27 天，并在第 5 天重新加载场景验证存档。
/// </summary>
[InitializeOnLoad]
public static class NarrativeIntegrationChecks
{
    const string Key = "MonsterDungeon.NarrativeCheck";
    static int ticks, reloadWait;
    static bool didReload, finishing;
    static string expectedFrame;
    static int expectedCash, expectedAP;
    static readonly HashSet<string> scenes = new HashSet<string>();
    static readonly HashSet<int> days = new HashSet<int>();
    static readonly HashSet<string> inspectedCommissions = new HashSet<string>();
    static readonly HashSet<string> requestedCommissions = new HashSet<string>();
    static readonly List<string> errors = new List<string>();
    static double started;
    static NarrativeRuntime observedRuntime;
    static bool seededBoard, choseTalk, choseTopic, checkedAP;
    static int totalIncome, settlementClicks;
    static bool branchChecks;
    static int mainCash;
    static string mainEnding;
    static bool archiveChecked;
    static int uiStep, uiWait, uiAP, uiRecords;
    static string uiInk;
    static bool capturePending;
    static bool infoChecked;
    static bool bagReloaded, commissionReloaded;
    static readonly List<string> screenshots = new List<string>();

    static NarrativeIntegrationChecks()
    {
        if (SessionState.GetBool(Key, false))
        {
            Application.logMessageReceived += Log;
            EditorApplication.update += Tick;
            started = EditorApplication.timeSinceStartup;
        }
    }

    [MenuItem("Tools/27天剧情/检查原稿与全部终局")]
    public static void ValidateScript()
    {
        string raw = Resources.Load<TextAsset>("Narrative/story27").text;
        var archiveSource = new NarrativeRuntime(raw);
        foreach (var actor in new[] { "MY", "FE", "DW", "SA", "AU", "DA", "UN" })
        {
            if (!archiveSource.BackgroundTexts.TryGetValue(actor + ":partial", out var partial) || !partial.Contains("□") ||
                !archiveSource.BackgroundTexts.TryGetValue(actor + ":full", out var full) || full.Contains("□"))
                throw new Exception("调查原稿部分/完整正文不齐：" + actor);
        }
        int endings = 0;
        foreach (var entry in new[] { "END_STAY", "END_SWALLOWED", "END_KILLED", "HIDDEN_SAMPLE", "HIDDEN_ABANDON",
            "SUCCESSION_MY_01", "SUCCESSION_FE_01", "SUCCESSION_DW_01", "SUCCESSION_SA_01",
            "SUCCESSION_AU_01", "SUCCESSION_DA_01", "SUCCESSION_UN_01" })
        {
            var r = new NarrativeRuntime(raw);
            r.OnCheck = (kind, actor, bonus) => true;
            r.Story.ChoosePathString(entry);
            int guard = 0;
            while (!r.Finished && guard++ < 1500)
            {
                var frame = r.Read();
                var options = frame.nodes[0].options;
                if (options.Count > 0) r.Choose(options[0].param);
            }
            if (!r.Finished) throw new Exception("终局未结束：" + entry);
            endings++;
        }
        // 每个日程入口都应存在；不是只检查 JSON 语法或文件名。
        var source = Newtonsoft.Json.Linq.JObject.Parse(raw);
        var root = (Newtonsoft.Json.Linq.JArray)source["root"];
        var named = (Newtonsoft.Json.Linq.JObject)root.Last;
        for (int day = 1; day <= 27; day++)
            if (named[$"DAY{day:00}_START"] == null || named[$"DAY{day:00}_END"] == null)
                throw new Exception("缺少日期入口：" + day);
        File.WriteAllText("NarrativeScriptCheck.json", JsonConvert.SerializeObject(new { endings, days = 27, result = "passed" }, Formatting.Indented));
        Debug.Log($"[27天验收] 27 个日期入口、{endings} 个终局入口通过。");
    }

    public static void RunBatch()
    {
        if (!Application.productName.EndsWith("_NarrativeValidation", StringComparison.Ordinal))
            throw new Exception("请在独立验证副本运行 RunBatch，避免更改正式存档。");
        ValidateScript();
        for (int i = 1; i <= 4; i++) SaveAndLoadManager.Delete(SaveAndLoadManager.SlotKey(i));
        SaveAndLoadManager.Delete("run_state");
        SaveAndLoadManager.Delete("run_state_before_story27");
        // 同时验收无剧情变量的旧档迁移：原档须留备份，完整剧情须从新局开始。
        var legacy = new RunState { currentDay = 9, cash = 321 };
        legacy.adventurers[1] = new AdventurerRuntimeData { configId = 1 };
        SaveAndLoadManager.Save("run_state", legacy);
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene("Assets/Scenes/PlayerGame.unity");
        var gameView = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
        gameView.Show(); gameView.Focus();
        SetGameViewSize(gameView, 1920, 1080);
        Application.logMessageReceived += Log;
        EditorApplication.update += Tick;
        started = EditorApplication.timeSinceStartup;
        EditorApplication.EnterPlaymode();
    }

    static void Log(string text, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            errors.Add(text + "\n" + trace);
    }

    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    static void Click(Button button)
    {
        if (button == null || !button.interactable) throw new Exception("验收按钮缺失或不可交互。");
        ArtAcceptanceChecks.Pointer(button);
    }

    static double nextInputTick;
    static void Tick()
    {
        if (finishing || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        // 批处理验收也要让原游戏循环处理协程，不能在编辑器更新中连续挤入鼠标事件。
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.timeSinceStartup < nextInputTick) return;
        nextInputTick = EditorApplication.timeSinceStartup + 0.01;
        try
        {
            if (++ticks > 60000 || EditorApplication.timeSinceStartup - started > 700) throw new Exception("流程超时，可能卡在界面或循环。");
            if (errors.Count > 0) throw new Exception(errors[0]);
            if (!SystemManager.Instance.IsInitialized || UIManager.Instance == null) return;
            var campaign = SystemManager.Instance.Narrative;
            if (!campaign.Enabled) throw new Exception("完整剧情没有启用。");
            var runtime = Field<NarrativeRuntime>(campaign, "_runtime");
            if (observedRuntime != runtime)
            {
                observedRuntime = runtime;
                // 覆盖全部委托时固定个人事件的“继续接待”结果，避免原稿的随机掷骰直接离店。仅作用于验收。
                var coverageCheck = runtime.OnCheck;
                runtime.OnCheck = (kind, actor, bonus) => kind == "personal" ? true : coverageCheck(kind, actor, bonus);
                runtime.OnAction += tag =>
                {
                    if (tag.StartsWith("action:scene ")) scenes.Add(tag.Split(' ')[1]);
                    if (tag.StartsWith("action:day phase=start")) days.Add(Convert.ToInt32(runtime.Get("day")));
                };
            }
            if (reloadWait > 0) { reloadWait--; return; }
            if (capturePending) return;
            if (uiWait > 0) { uiWait--; return; }
            var save = Field<NarrativeSystem.SaveData>(campaign, "_save");
            var run = SystemManager.Instance.RunState.run;
            var talk = UIManager.Instance.Get<TalkPanel>();
            if (talk != null) { typeof(TalkPanel).GetField("_clickCooldown", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(talk, 0f); talk.SetInstantText(true); }
            if (CheckReceptionUI(campaign, runtime, save, talk)) return;
            if (talk != null && Field<bool>(talk.Dialogue, "_narrativeAdvancing")) return;
            if (expectedFrame != null)
            {
                if (talk == null || !talk.IsOpen) return;
                if (JsonConvert.SerializeObject(save.frame) != expectedFrame || run.cash != expectedCash || run.actionPoint != expectedAP)
                    throw new Exception("读档后当前台词/选项/金币/行动点发生变化。");
                expectedFrame = null;
                Debug.Log("[27天验收] 第 5 天中途读档通过。");
            }
            if (!didReload && run.currentDay == 5 && save.stage == "playing" && save.frame?.nodes[0].options.Count > 0)
            {
                didReload = true;
                campaign.Save();
                expectedFrame = JsonConvert.SerializeObject(save.frame);
                expectedCash = run.cash; expectedAP = run.actionPoint;
                reloadWait = 10;
                SceneManager.LoadScene("PlayerGame");
                return;
            }
            var settlement = UIManager.Instance.Get<DailySettlementPanel>();
            if (settlement != null && settlement.IsOpen)
            {
                int before = run.cash;
                var paidBefore = new HashSet<int>(save.paidPhases);
                var data = Field<SettlementData>(settlement, "_data");
                totalIncome += data.totalIncome;
                settlementClicks++;
                var button = Field<Button>(settlement, "btn_Close");
                Click(button);
                int justPaid = save.paidPhases.Where(p => !paidBefore.Contains(p)).Sum(p => ConfigDatabase.Instance.GetInstallmentPayment(p));
                int after = before + data.totalIncome - justPaid;
                if (run.cash != after) throw new Exception("结算入账金额不正确。");
                button.onClick.Invoke(); // 模拟重复点击，必须只入账一次。
                if (run.cash != after) throw new Exception("结算重复入账。");
                return;
            }
            if (campaign.IsPreparing)
            {
                if (ArtAcceptanceChecks.Preparation()) return;
                if (Doc2AcceptanceChecks.Preparation()) return;
                if (!seededBoard && run.currentDay == 1)
                {
                    seededBoard = true;
                    var def = ConfigDatabase.Instance.ShapeDb.Get("101");
                    var go = new GameObject("ValidationMonster", typeof(RectTransform));
                    go.transform.SetParent(BackpackBoard.Instance.ItemsRoot, false);
                    go.AddComponent<ItemView>().Init(def, BackpackBoard.Instance, 0, new Vector2Int(0,0));
                }
                var preparation = UIManager.Instance.Get<PreparationPanel>();
                if (preparation != null && preparation.IsOpen) Click(Field<Button>(preparation, "_startReceptionButton"));
                return;
            }
            if (Doc2AcceptanceChecks.Reception(campaign, runtime, save, talk)) return;
            if (campaign.IsTerminal)
            {
                if (!ArtAcceptanceChecks.ExtraUI(campaign, talk)) return;
                if (run.currentDay != 27 || save.settledDays.Count != 27 || save.paidPhases.Count != 5 || scenes.Count != 78 || inspectedCommissions.Count != 78 || !didReload || !checkedAP || !infoChecked)
                    throw new Exception($"覆盖不完整：day={run.currentDay}, 结算={save.settledDays.Count}, 费用={save.paidPhases.Count}, 来访={scenes.Count}");
                int fees = ConfigDatabase.Instance.Schedules.Sum(s => s.installmentAmount);
                var legacyBackup = SaveAndLoadManager.Load<RunState>("run_state_before_story27");
                if (legacyBackup.currentDay != 9 || legacyBackup.cash != 321) throw new Exception("旧存档迁移备份不正确。");
                if (run.cash != RunStateManager.InitialCash + totalIncome - fees)
                    throw new Exception("最终资金不等于初始资金 + 每日收入 - 五次分期，可能有重复扣款。");
                mainCash = run.cash;
                mainEnding = save.ending;
                CheckGameBranches(campaign, runtime, save);
                branchChecks = true;
                Finish(true, "");
                return;
            }
            var commission = UIManager.Instance.Get<CommissionPanel>();
            if (commission != null && commission.IsOpen)
            {
                int count = SystemManager.Instance.DailyCommission.NormalCommissions.Count;
                string sceneId = runtime.Get("current_scene").ToString();
                var rule = Field<NarrativeSystem.Rules>(campaign, "_rules").scenes.Find(s => s.id == sceneId);
                if (Field<Text>(commission, "txt_name").text != rule.commissionName || Field<Text>(commission, "txt_Description").text != rule.description)
                    throw new Exception("委托名称或正文与来访节点不一致：" + sceneId);
                inspectedCommissions.Add(sceneId);
                if (sceneId != "D01_V01")
                {
                    Click(Field<Button>(commission, "CloseButton"));
                    if (SystemManager.Instance.DailyCommission.NormalCommissions.Count != count) throw new Exception("取消委托仍然入队。");
                    return;
                }
                var accept = Field<Button>(commission, "btn_AcceptCommission");
                Click(accept); accept.onClick.Invoke();
                if (!RouteSelectionPanel.IsShowing || SystemManager.Instance.DailyCommission.NormalCommissions.Count != count) throw new Exception("指路确认前错误入队。");
                Doc2AcceptanceChecks.ConfirmRoute();
                if (SystemManager.Instance.DailyCommission.NormalCommissions.Count != count + 1) throw new Exception("普通委托重复入队或未入队。");
                return;
            }
            if (talk == null || !talk.IsOpen || save.frame == null) return;
            if (archiveChecked && choseTopic && !infoChecked && run.currentDay == 1 && run.adventurers[1].validInfo.Count > 0)
            {
                var info = run.adventurers[1].validInfo[0];
                if (!info.Contains("只为自己工作") || !info.Contains("钱还是多多益善")) throw new Exception("有效信息没有保留原句与关键词。");
                talk.OpenArchive(1, 2); Shot(talk, "13_valid_information_original_quote");
                infoChecked = true; return;
            }
            if (infoChecked && UIManager.Instance.Get<CharacterArchivePanel>()?.IsOpen == true)
            { UIManager.Instance.HandleEscape(); return; }
            // 测试快速点击；实际玩家仍保留打字机和正常点击冷却。
            typeof(TalkPanel).GetField("_clickCooldown", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(talk, 0f);
            talk.SetInstantText(true);
            var options = UnityEngine.Object.FindObjectsOfType<DialogueOptionItem>();
            if (options.Length > 0)
            {
                var usable = options.Where(o => Field<Button>(o, "_button").interactable).OrderBy(o => o.transform.GetSiblingIndex()).ToArray();
                if (usable.Length == 0) return;
                DialogueOptionItem chosen = null;
                bool mythFirst = run.currentDay == 1 && SystemManager.Instance.VisitSystem.CurrentVisitorId == 1;
                string currentScene = runtime.Get("current_scene").ToString();
                if (MenuReady(save) && !inspectedCommissions.Contains(currentScene) && !(mythFirst && !choseTopic))
                {
                    chosen = usable.First(o => Field<Text>(o, "_text").text.Contains("查看委托"));
                    requestedCommissions.Add(currentScene);
                }
                if (chosen == null && mythFirst && !choseTalk)
                {
                    chosen = usable.FirstOrDefault(o => Field<Text>(o, "_text").text.Contains("选择搭话"));
                    if (chosen != null) choseTalk = true;
                }
                else if (chosen == null && mythFirst && choseTalk && !choseTopic)
                {
                    chosen = usable.FirstOrDefault(o => Field<Text>(o, "_text").text.Contains("堂堂"));
                    if (chosen != null)
                    {
                        choseTopic = true;
                        var ordinaryCheck = runtime.OnCheck;
                        runtime.OnCheck = (kind, actor, bonus) => kind == "information" && actor == "MY" ? true : ordinaryCheck(kind, actor, bonus);
                    }
                }
                string[] words = mythFirst ? new[] { "返回正式接待", "指向：普通史莱姆", "确认委托", "查看委托", "返回" } :
                    new[] { "拒绝", "不再将经营权", "先收起", "返回", "确认委托", "查看委托" };
                foreach (string word in words)
                {
                    if (chosen != null) break;
                    chosen = usable.FirstOrDefault(o => Field<Text>(o, "_text").text.Contains(word));
                    if (chosen != null) break;
                }
                Debug.Log("[QA_CHOICE] " + currentScene + " inspected=" + inspectedCommissions.Contains(currentScene) + " menu=" + MenuReady(save) + " " + Field<Text>(chosen ?? usable[0], "_text").text);
                Click(Field<Button>(chosen ?? usable[0], "_button"));
            }
            else talk.MoveNext();
            if (choseTopic && !checkedAP && run.currentDay == 1 && SystemManager.Instance.VisitSystem.CurrentVisitorId == 2)
            {
                if (run.actionPoint != 5) throw new Exception("UI 药剂增加 1 点后搭话应剩 5，可能重复扣除。");
                checkedAP = true;
            }
        }
        catch (Exception ex) { Finish(false, ex.ToString()); }
    }

    static bool MenuReady(NarrativeSystem.SaveData save) => save.stage == "playing" &&
        save.frame?.nodes[0].options.Any(o => o.callbackKey == "open_commission_panel") == true &&
        UnityEngine.Object.FindObjectsOfType<DialogueOptionItem>().Any(o => Field<Button>(o, "_button").interactable && Field<Text>(o, "_text").text.Contains("查看委托"));

    static void Option(string text)
    {
        var item = UnityEngine.Object.FindObjectsOfType<DialogueOptionItem>().First(o => Field<Text>(o, "_text").text.Contains(text) && Field<Button>(o, "_button").interactable);
        Click(Field<Button>(item, "_button"));
    }

    static void Shot(TalkPanel talk, string name)
    {
        capturePending = true;
        talk.StartCoroutine(Capture(name));
    }
    static void SetGameViewSize(EditorWindow view, int width, int height)
    {
        var assembly = typeof(EditorWindow).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var instance = sizesType.BaseType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
        var group = sizesType.GetMethod("GetGroup").Invoke(instance, new[] { Enum.Parse(groupType, "Standalone") });
        var sizeType = assembly.GetType("UnityEditor.GameViewSize");
        var modeType = assembly.GetType("UnityEditor.GameViewSizeType");
        var size = Activator.CreateInstance(sizeType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
            new object[] { Enum.Parse(modeType, "FixedResolution"), width, height, "UI acceptance" }, null);
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
        int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;
        view.GetType().GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(view, index);
    }
    static System.Collections.IEnumerator Capture(string name)
    {
        // 必须在有图形的 Editor Play 模式里取真正的游戏帧，不能用统计计数代替界面。
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (Application.isBatchMode) yield return null;
        else yield return new WaitForEndOfFrame();
        string folder = Path.GetFullPath("../qa/UI");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, name + ".png");
        // 批处理的活动目标可能是 SceneView，直接 ScreenCapture 会截到编辑器窗口。
        // 使用运行中的同一套 Canvas/UI，以 Unity 图形相机渲染完整 1920x1080 游戏画面。
        var camera = Camera.main;
        var target = new RenderTexture(1920, 1080, 24);
        var oldTarget = camera.targetTexture;
        var oldActive = RenderTexture.active;
        var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas).ToArray();
        var modes = canvases.Select(c => c.renderMode).ToArray();
        var cameras = canvases.Select(c => c.worldCamera).ToArray();
        var distances = canvases.Select(c => c.planeDistance).ToArray();
        camera.targetTexture = target;
        for (int i = 0; i < canvases.Length; i++)
        { canvases[i].renderMode = RenderMode.ScreenSpaceCamera; canvases[i].worldCamera = camera; canvases[i].planeDistance = 1; }
        Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
        var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        for (int i = 0; i < canvases.Length; i++)
        { canvases[i].renderMode = modes[i]; canvases[i].worldCamera = cameras[i]; canvases[i].planeDistance = distances[i]; }
        camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
        UnityEngine.Object.Destroy(texture); UnityEngine.Object.Destroy(target);
        screenshots.Add(path);
        capturePending = false;
    }

    static void BagSlot(int itemId)
    {
        var bag = UIManager.Instance.Get<BackpackPanel>();
        int index = Enumerable.Range(0, InventoryModel.Instance.Capacity).First(i => !InventoryModel.Instance.GetSlot(i).IsEmpty && InventoryModel.Instance.GetSlot(i).data.id == itemId);
        ArtAcceptanceChecks.Pointer(bag._itemSlots[index]);
    }

    static bool CheckReceptionUI(NarrativeSystem campaign, NarrativeRuntime runtime, NarrativeSystem.SaveData save, TalkPanel talk)
    {
        if (uiStep >= 30) return false;
        if (uiStep == 0 && !(SystemManager.Instance.RunState.run.currentDay == 1 && SystemManager.Instance.VisitSystem.CurrentVisitorId == 1 && MenuReady(save))) return false;
        if (talk == null || !talk.IsOpen) return true;
        var run = SystemManager.Instance.RunState.run;
        var archive = talk.leftDisplayArea.GetComponentInChildren<CharacterArchivePanel>(true);
        var panel = UIManager.Instance.Get<CommissionPanel>();
        var special = UIManager.Instance.Get<SpecialCommissionPanel>();
        var bag = UIManager.Instance.Get<BackpackPanel>();
        string[] tabs = { "_basicInfo", "_dialogueLog", "_validInfo", "_investigationLog" };
        switch (uiStep)
        {
            case 0:
                ArtAcceptanceChecks.Reception(campaign, talk);
                if (!save.frame.nodes[0].options.Any(o => o.callbackKey == "open_special_commission")) throw new Exception("原接待菜单没有补回特殊委托。");
                uiInk = runtime.Story.state.ToJson(); uiRecords = run.adventurers[1].historyDialogue.Count;
                Click(UIManager.Instance.Get<HUDPanel>().ButtonRoot.Find("CharacterArchive/CharacterArchiveButton").GetComponent<Button>()); uiStep++; break;
            case 1: case 2: case 3: case 4:
                if (runtime.Story.state.ToJson() != uiInk) throw new Exception("查档推进了剧情。");
                Click(Field<Button>(archive, tabs[uiStep - 1]));
                if (archive.CurrentTab != uiStep - 1) throw new Exception("档案页签按钮未绑定。");
                if (uiStep == 1 && Field<Text>(archive, "_Introduction").text.Contains("骑士")) throw new Exception("基本信息仍为旧占位人物。");
                if (uiStep == 2 && !Field<Text>(archive, "_Introduction").text.Contains(run.adventurers[1].historyDialogue.Last())) throw new Exception("对话未单独显示在档案页。");
                Shot(talk, "0" + uiStep + "_archive_tab"); uiStep++; break;
            case 5:
                Click(Field<Button>(archive, "_dialogueLog"));
                archive.Refresh(2);
                if (Field<Text>(archive, "_Introduction").text != "尚无对话记录。") throw new Exception("切换人物串档。");
                archive.Refresh(1);
                campaign.Save();
                uiInk = runtime.Story.state.ToJson(); uiStep++; uiWait = 15;
                SceneManager.LoadScene("PlayerGame"); break;
            case 6:
                if (!archive.IsOpen || archive.CurrentActor != 1 || archive.CurrentTab != 1 || run.adventurers[1].historyDialogue.Count != uiRecords)
                    throw new Exception("读档没有恢复档案人物、页签，或重显重复登记对话。");
                UIManager.Instance.HandleEscape();
                if (runtime.Story.state.ToJson() != uiInk) throw new Exception("关闭档案推进剧情。");
                uiStep++; break;
            case 7:
                if (!MenuReady(save)) { talk.MoveNext(); break; }
                Option("查看委托"); uiStep++; break;
            case 8:
                if (panel == null || !panel.IsOpen) { talk.MoveNext(); break; }
                inspectedCommissions.Add(runtime.Get("current_scene").ToString());
                if (!Field<Text>(panel, "txt_name").text.Contains("维护灾区") || !Field<Text>(panel, "txt_Description").text.Contains("魔力污染")) throw new Exception("委托正文未绑定原稿。");
                if (SystemManager.Instance.DailyCommission.NormalCommissions.Count != 0) throw new Exception("打开委托就接单。");
                if (!commissionReloaded)
                {
                    commissionReloaded = true; campaign.Save(); uiWait = 15;
                    SceneManager.LoadScene("PlayerGame"); break;
                }
                uiInk = runtime.Story.state.ToJson(); Shot(talk, "05_commission_original_text"); uiStep++; break;
            case 9:
                talk.MoveNext(); talk.MoveNext();
                if (runtime.Story.state.ToJson() != uiInk) throw new Exception("功能面板打开时右侧仍推进。");
                UIManager.Instance.HandleEscape(); uiStep++; break;
            case 10:
                if (!MenuReady(save)) { talk.MoveNext(); break; }
                Option("查看委托"); uiStep++; break;
            case 11:
                if (panel == null || !panel.IsOpen) { talk.MoveNext(); break; }
                Click(Field<Button>(panel, "CloseButton"));
                if (SystemManager.Instance.DailyCommission.NormalCommissions.Count != 0) throw new Exception("返回委托导致接单。");
                uiStep++; break;
            case 12:
                if (!MenuReady(save)) { talk.MoveNext(); break; }
                Option("特殊委托"); uiStep++; break;
            case 13:
                if (special == null || !special.IsOpen) break;
                uiInk = runtime.Story.state.ToJson();
                Shot(talk, "06_special_list"); uiStep++; break;
            case 14:
                Click(Field<Button>(special.GetComponentInChildren<SpecialCommissionItem>(), "_viewButton"));
                if (!Field<SpecialCommissionDetailPanel>(special, "_detailPanel").IsShowing) throw new Exception("首次查看特殊委托详情未打开。");
                Shot(talk, "07_special_detail"); uiStep++; break;
            case 15:
                UIManager.Instance.HandleEscape();
                if (!special.IsOpen || Field<SpecialCommissionDetailPanel>(special, "_detailPanel").IsShowing) throw new Exception("特殊委托详情 ESC 未返回列表。");
                // 再次查看并点击原有接取按钮。
                Click(Field<Button>(special.GetComponentInChildren<SpecialCommissionItem>(), "_viewButton"));
                Click(Field<Button>(Field<SpecialCommissionDetailPanel>(special, "_detailPanel"), "_acceptButton"));
                if (SystemManager.Instance.DailyCommission.SpecialCommissions.Count != 0 || !RouteSelectionPanel.IsShowing) throw new Exception("特殊委托未指路却接取。");
                Doc2AcceptanceChecks.ConfirmRoute();
                if (SystemManager.Instance.DailyCommission.SpecialCommissions.Count != 1) throw new Exception("特殊委托未进入原队列。");
                if (runtime.Story.state.ToJson() != uiInk) throw new Exception("特殊委托操作推进了原剧情。");
                uiStep++; break;
            case 16:
                if (!MenuReady(save)) { talk.MoveNext(); break; }
                foreach (var pair in new[] { new[] {1,2}, new[] {4,1} }) InventoryModel.Instance.AddItem(new ItemStack { data = ItemData.FromConfig(ConfigDatabase.Instance.Items[pair[0]]), quantity = pair[1] });
                uiAP = run.actionPoint;
                Option("使用道具"); uiStep++; break;
            case 17:
                if (bag == null || !bag.IsOpen) break;
                if (!bagReloaded)
                {
                    bagReloaded = true; campaign.Save(); uiWait = 15;
                    SceneManager.LoadScene("PlayerGame"); break;
                }
                if (Time.timeScale != 1 || !bag.IsFunctionContext || !UIManager.Instance.HasLeftOverlay) throw new Exception("功能背包必须阻塞右侧，但不能冻结全局时间。");
                Shot(talk, "08_existing_backpack"); BagSlot(1); uiStep++; break;
            case 18:
                var actionRect=(RectTransform)UIManager.Instance.Get<ItemActionPanel>().transform;
                if(actionRect.rect.width<=0||actionRect.rect.height<=0)throw new Exception("道具操作面板尺寸无效。");
                var actionCanvas=actionRect.GetComponentInParent<Canvas>().rootCanvas;
                if(RectTransformUtility.WorldToScreenPoint(actionCanvas.renderMode==RenderMode.ScreenSpaceOverlay?null:actionCanvas.worldCamera,actionRect.TransformPoint(actionRect.rect.center)).x>900)throw new Exception("道具操作面板偏离背包区域。");
                Click(Field<Button>(UIManager.Instance.Get<ItemActionPanel>(), "_useButton")); uiStep++; break;
            case 19:
                if (!MenuReady(save)) { talk.MoveNext(); break; }
                if (Time.timeScale != 1) throw new Exception("关闭读档恢复的背包后游戏仍暂停。");
                if (run.adventurers[1].investigationArchive.Count != 1 || !run.adventurers[1].investigationArchive[0].Contains("□□□□") || !run.adventurers[1].investigationArchive[0].Contains("部分缺失")) throw new Exception("透镜未登记原稿的模糊正文。");
                if (run.adventurers[1].historyDialogue.Any(s => s.Contains("Myth的父母"))) throw new Exception("调查正文仍泄露到右侧对话。");
                Click(UIManager.Instance.Get<HUDPanel>().ButtonRoot.Find("CharacterArchive/CharacterArchiveButton").GetComponent<Button>()); Click(Field<Button>(archive, "_investigationLog"));
                Shot(talk, "09_investigation_partial"); uiStep++; break;
            case 20:
                UIManager.Instance.HandleEscape();
                Option("使用道具"); uiStep++; break;
            case 21: BagSlot(1); Click(Field<Button>(UIManager.Instance.Get<ItemActionPanel>(), "_useButton")); uiStep++; break;
            case 22:
                if (!MenuReady(save)) { talk.MoveNext(); break; }
                if (run.adventurers[1].investigationArchive[0].Contains("□□□□") || !run.adventurers[1].investigationArchive[0].Contains("战后安置的街区")) throw new Exception("完整调查没有替换模糊记录。");
                talk.OpenArchive(1, 3); Shot(talk, "10_investigation_full"); uiStep++; break;
            case 23:
                var scroll = Field<ScrollRect>(archive, "_recordScroll");
                if (scroll.content.rect.height < scroll.viewport.rect.height) throw new Exception("记录正文布局高度无效。");
                scroll.verticalNormalizedPosition = 0;
                Shot(talk, "11_investigation_scrolled"); uiStep++; break;
            case 24: UIManager.Instance.HandleEscape(); Option("使用道具"); uiStep++; break;
            case 25: BagSlot(4); Click(Field<Button>(UIManager.Instance.Get<ItemActionPanel>(), "_useButton")); uiStep++; break;
            case 26:
                if (!MenuReady(save)) { talk.MoveNext(); break; }
                if (run.actionPoint != uiAP + 1) throw new Exception("背包使用药剂没有增加一次行动点。");
                Option("特殊委托"); uiStep++; break;
            case 27:
                Click(Field<Button>(special.GetComponentInChildren<SpecialCommissionItem>(), "_viewButton"));
                campaign.Save(); uiInk = runtime.Story.state.ToJson(); uiRecords = run.adventurers[1].historyDialogue.Count;
                uiStep++; uiWait = 15; SceneManager.LoadScene("PlayerGame"); break;
            case 28:
                if (special == null || !special.IsOpen) break;
                if (!Field<SpecialCommissionDetailPanel>(special, "_detailPanel").IsShowing) throw new Exception("读档没有恢复特殊委托详情。");
                if (runtime.Story.state.ToJson() != uiInk || SystemManager.Instance.DailyCommission.SpecialCommissions.Count != 1 || run.actionPoint != uiAP + 1 || run.adventurers[1].historyDialogue.Count != uiRecords)
                    throw new Exception("功能面板读档重放了接单、道具或记录。");
                Shot(talk, "12_restore_special_context"); uiStep++; break;
            case 29:
                UIManager.Instance.HandleEscape(); UIManager.Instance.HandleEscape(); archiveChecked = true; uiStep++;
                Debug.Log("[UI验收] 四页签、人物隔离、委托取消、特殊列表详情接取、背包道具、部分/完整调查、滚动、两次面板读档通过。"); break;
        }
        return true;
    }

    static void Finish(bool passed, string failure)
    {
        finishing = true;
        SessionState.SetBool(Key, false);
        var run = SystemManager.Instance.RunState.run;
        var save = Field<NarrativeSystem.SaveData>(SystemManager.Instance.Narrative, "_save");
        Doc2AcceptanceChecks.Write(passed);
        var result = new
        {
            passed, failure, ticks, day = run.currentDay, cash = mainCash, visits = scenes.Count,
            settledDays = save.settledDays.Count, paidPhases = save.paidPhases.Count,
            ending = mainEnding, branchEnding = save.ending, midDialogueReload = didReload, oneTalkCostsOneAP = checkedAP,
            totalIncome, settlementClicks, branchChecks, archiveChecked, infoChecked, bagReloaded, commissionReloaded, inspectedCommissions = inspectedCommissions.Count,
            missingCommissions = Field<NarrativeSystem.Rules>(SystemManager.Instance.Narrative, "_rules").scenes.Select(s => s.id).Where(id => !inspectedCommissions.Contains(id)).ToArray(), uiStep, screenshots, errors
        };
        File.WriteAllText("NarrativePlayCheck.json", JsonConvert.SerializeObject(result, Formatting.Indented));
        Debug.Log("[NARRATIVE_CHECK_RESULT] " + JsonConvert.SerializeObject(result));
        EditorApplication.update -= Tick;
        EditorApplication.isPlaying = false;
        EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
    }

    static void CheckGameBranches(NarrativeSystem campaign, NarrativeRuntime runtime, NarrativeSystem.SaveData save)
    {
        // 以下只在隔离测试局中执行。先验证背包守卫，再实际消费一次药剂。
        var run = SystemManager.Instance.RunState.run;
        var inventory = InventoryModel.Instance;
        for (int i = 0; i < inventory.Capacity; i++) inventory.GetSlot(i).Clear();
        run.itemInventory.Clear();
        save.stage = "playing";
        runtime.Story.ChoosePathString("ITEMS");
        campaign.Next();
        DialogueData frame = campaign.GetFrame();
        int guard = 0;
        while (frame.nodes[0].options.Count == 0 && guard++ < 20) { campaign.Next(); frame = campaign.GetFrame(); }
        var stimulant = frame.nodes[0].options.First(o => o.text.Contains("醒神药剂"));
        if (campaign.Choose(stimulant)) throw new Exception("没有药剂时仍可使用。");
        inventory.AddItem(new ItemStack { data = ItemData.FromConfig(ConfigDatabase.Instance.Items[4]), quantity = 1 });
        int ap = run.actionPoint;
        if (!campaign.Choose(stimulant)) throw new Exception("有库存时无法使用药剂。");
        frame = campaign.GetFrame();
        int remaining = Enumerable.Range(0, inventory.Capacity).Sum(i => inventory.GetSlot(i).IsEmpty ? 0 : inventory.GetSlot(i).data.id == 4 ? inventory.GetSlot(i).quantity : 0);
        if (run.actionPoint != ap + 1 || remaining != 0) throw new Exception("药剂没有正确扣库存/增加 AP。");

        var check = typeof(NarrativeSystem).GetMethod("Check", BindingFlags.Instance | BindingFlags.NonPublic);
        string[] actors = { "MY", "FE", "DW", "SA", "AU", "DA", "UN" };
        foreach (string actor in actors)
        {
            int id = Array.IndexOf(actors, actor) + 1;
            var adv = run.adventurers[id];
            adv.isDead = false;
            foreach (SevenDimension stat in Enum.GetValues(typeof(SevenDimension))) adv.currentSevenStats[stat] = 50;
            if ((bool)check.Invoke(campaign, new object[] { "succession", actor, 0 })) throw new Exception("未达门槛却允许继任：" + actor);
            adv.currentSevenStats[SevenDimension.Tolerance] = 100;
            adv.currentSevenStats[SevenDimension.Courage] = 100;
            adv.currentSevenStats[SevenDimension.Diligence] = 100;
            adv.currentSevenStats[SevenDimension.Reason] = actor == "MY" ? 100 : 0;
            adv.currentSevenStats[SevenDimension.Resolve] = actor == "MY" ? 100 : 0;
            adv.currentSevenStats[SevenDimension.Benevolence] = actor == "AU" ? 0 : 100;
            adv.currentSevenStats[SevenDimension.Piety] = 0;
            if (!(bool)check.Invoke(campaign, new object[] { "succession", actor, 0 })) throw new Exception("达门槛无法继任：" + actor);
        }
        var action = typeof(NarrativeSystem).GetMethod("HandleAction", BindingFlags.Instance | BindingFlags.NonPublic);
        run.cash = 0;
        save.paidPhases.Remove(1);
        action.Invoke(campaign, new object[] { "action:fee phase=1 amount=config" });
        if (!(bool)runtime.Get("fee_failed")) throw new Exception("欠费没有写入失败条件。");
        runtime.Story.ChoosePathString("END_SWALLOWED");
        campaign.Next();
        guard = 0;
        while (!campaign.IsTerminal && guard++ < 80) { campaign.GetFrame(); campaign.Next(); }
        if (!campaign.IsTerminal || campaign.Ending != "swallowed") throw new Exception("失败终局无法结束。");
        Debug.Log("[27天验收] 道具库存/药剂效果、七名继任条件正反例、欠费与吞噬终局通过。");
    }
}
#endif

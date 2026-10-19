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
public static class GlobalGameplayChecks
{
    const string Key = "MonsterDungeon.GlobalGameplayQA";
    static readonly List<string> checks = new List<string>(), errors = new List<string>(), shots = new List<string>();
    static readonly HashSet<string> visited = new HashSet<string>(), topics = new HashSet<string>(), enteredTopics = new HashSet<string>(), commissions = new HashSet<string>();
    static IEnumerator routine; static double started, next;
    static bool busy, profileChecked, archiveChecked, diceReloaded;
    static int delay, diceCount, annotationCount;
    static string firstDayMenuJson, day4MenuJson;
    static readonly List<object> cashflow = new List<object>();
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
    static GlobalGameplayChecks()
    {
        if (SessionState.GetBool(Key, false)) { checks.AddRange(JsonConvert.DeserializeObject<List<string>>(SessionState.GetString("GlobalRules", "[]"))); EditorApplication.update += Tick; Application.logMessageReceived += Log; started = EditorApplication.timeSinceStartup; }
    }
    static void Log(string message, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }
    static void Isolated() { if (!Application.productName.EndsWith("_NarrativeValidation")) throw new Exception("禁止在正式用户工程运行写档测试"); }
    public static void RunBatch()
    {
        Isolated(); Rules();
        SaveAndLoadManager.Delete("run_state"); SessionState.SetString("GlobalRules", JsonConvert.SerializeObject(checks)); SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene("Assets/Scenes/PlayerGame.unity");
        var view = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")); view.Show();
        typeof(NarrativeIntegrationChecks).GetMethod("SetGameViewSize", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { view, 1920, 1080 });
        EditorApplication.update += Tick; Application.logMessageReceived += Log; started = EditorApplication.timeSinceStartup; EditorApplication.EnterPlaymode();
    }
    public static void ResumeBatch()
    {
        Isolated();Rules(); SessionState.SetString("GlobalRules",JsonConvert.SerializeObject(checks));SessionState.SetBool("MonsterDungeon.GlobalResume",true);
        SaveAndLoadManager.Save("run_state",JsonConvert.DeserializeObject<RunState>(File.ReadAllText("../Global27Checkpoint.json")));
        SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/PlayerGame.unity");
        var view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));view.Show();
        typeof(NarrativeIntegrationChecks).GetMethod("SetGameViewSize",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{view,1920,1080});
        EditorApplication.update+=Tick;Application.logMessageReceived+=Log;started=EditorApplication.timeSinceStartup;EditorApplication.EnterPlaymode();
    }
    public static void ConfigureAndRun() { Doc4IntegrationEditor.Configure(); RunBatch(); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + .035; EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if (EditorApplication.timeSinceStartup - started > 1100) throw new Exception("全局验收超时，当前日=" + Run?.currentDay + "，状态=" + Save?.stage);
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
        File.WriteAllText("GlobalGameplayAcceptance.json", JsonConvert.SerializeObject(new { passed, failure, checks, errors, shots, visited, diceCount, annotationCount, cashflow }, Formatting.Indented));
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
        Check(!state.Active(0) && state.Active(1) && state.Active(4) && state.Active(27) && !state.Active(28), "轻量玩法贯穿27天");
        Check(D20Service.SuccessPercent(65, 15) == 51 && D20Service.SuccessPercent(65, -5) == 31, "D100优势/短板基础成功率51%/31%");
        Check(D20Service.SuccessPercent(65, 999) == 95 && D20Service.SuccessPercent(65, -999) == 5, "自然极值保持5%/95%边界");
        Check(NarrativeSystem.Difficulty(24) == "极难" && NarrativeSystem.Difficulty(25) == "困难" && NarrativeSystem.Difficulty(45) == "一般" && NarrativeSystem.Difficulty(70) == "轻松" && NarrativeSystem.Difficulty(85) == "极易", "五档难度分界");
        NarrativeIntegrationChecks.ValidateScript(); Check(true, "原稿27天和全部终局入口仍可解析，不等于终局养成可达性验证");
        var balance=JsonConvert.DeserializeObject<D20Service.BalanceConfig>(Resources.Load<TextAsset>("Config/d100Rules").text);
        Check(balance.informationTarget==65&&balance.influenceTarget==60,"最终配置与执行门槛统一65/60");
        var rules=JsonConvert.DeserializeObject<NarrativeSystem.Rules>(Resources.Load<TextAsset>("Narrative/rules").text);
        Check(rules.scenes.Count==78&&rules.scenes.All(s=>s.targetMonsterType.HasValue&&!s.description.Contains("类魔物类")&&!s.description.Contains("部位会影响报酬")),"最终78委托材料与报酬文案一致");

    }
    static void Shot(string name)
    {
        busy = true; UIManager.Instance.StartCoroutine(Capture(name));
    }
    static IEnumerator Capture(string name)
    {
        yield return new WaitForSecondsRealtime(.4f); yield return null; Canvas.ForceUpdateCanvases();
        string folder = Path.GetFullPath("../qa/GlobalGameplay"); Directory.CreateDirectory(folder);
        int width=Screen.width,height=Screen.height; var camera = Camera.main; var target = new RenderTexture(width, height, 24); var previous = camera.targetTexture; var active = RenderTexture.active;
        var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas).ToArray(); var modes = canvases.Select(c => c.renderMode).ToArray(); var cameras = canvases.Select(c => c.worldCamera).ToArray();
        camera.targetTexture = target; for (int i = 0; i < canvases.Length; i++) { canvases[i].renderMode = RenderMode.ScreenSpaceCamera; canvases[i].worldCamera = camera; }
        Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
        string path = Path.Combine(folder, name + ".png"); File.WriteAllBytes(path, image.EncodeToPNG());
        for (int i = 0; i < canvases.Length; i++) { canvases[i].renderMode = modes[i]; canvases[i].worldCamera = cameras[i]; }
        camera.targetTexture = previous; RenderTexture.active = active; UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(target); shots.Add(path); busy = false;
    }
    static ItemView Spawn(int id)
    {
        var board = BackpackBoard.Instance; var definition = ConfigDatabase.Instance.ShapeDb.Get(id.ToString());
        for (int y = 0; y < 7; y++) for (int x = 0; x < 7; x++) if (board.CanPlace(definition, new Vector2Int(x, y), 0))
        { var go = new GameObject("Doc4Fixture" + id, typeof(RectTransform)); go.transform.SetParent(board.ItemsRoot, false); var view = go.AddComponent<ItemView>(); view.Init(definition, board, 0, new Vector2Int(x, y)); return view; }
        var used=board.Items.Where(v=>v.Item!=null && v.Item.IsResting).ToArray();
        if(used.Length>0){foreach(var v in used){board.RemoveItem(v.Item);UnityEngine.Object.Destroy(v.gameObject);}return Spawn(id);}
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
        if(!SessionState.GetBool("MonsterDungeon.GlobalResume",false))
        {
        while (!PlayerBuildPanel.IsChoosing) yield return null;
        Shot("01_开局类型与固定思维"); yield return null;
        var creation = UIManager.Instance.Get<PlayerBuildPanel>(); int ap = Run.actionPoint;
        Check(!F<Button>(creation, "_confirm").interactable, "未完成两个选择不能提交");
        Click(F<Button[]>(creation, "_types")[0]); Click(F<Button[]>(creation, "_thoughts")[0]);
        Click(F<Button>(creation, "_confirm"));
        Check(Run.player.created && Run.player.perception == 55 && Run.actionPoint == ap, "实际指针完成开局选择且不消耗行动点");
        yield return null;
        }
        else
        {
            SessionState.SetBool("MonsterDungeon.GlobalResume",false);
            var counts=Newtonsoft.Json.Linq.JObject.Parse(Save.ink)["visitCounts"];
            foreach(var scene in F<NarrativeSystem.Rules>(Campaign,"_rules").scenes)
            {
                Check((int?)counts[scene.commissionPath]>0 && (int?)counts[scene.commissionPath+".0.c-0"]>0,"原存档证明已确认委托 "+scene.id);
                visited.Add(scene.id);commissions.Add(scene.id);topics.Add(scene.id);
            }
            Check(Save.settledDays.Count==27&&Save.paidPhases.Count==5,"同一回归存档已完成27日结算和5次缴费");
            var fixture=JsonConvert.DeserializeObject<RunState>(File.ReadAllText("GlobalNativeFixture.json"));
            var data=JsonConvert.DeserializeObject<NarrativeSystem.SaveData>(fixture.narrativeSave);
            var r=new NarrativeRuntime(Resources.Load<TextAsset>("Narrative/story27").text);r.Story.state.LoadJson(data.ink);r.Story.ChoosePathString("VIS_D04_V01_RECEPTION");
            DialogueData frame=null;for(int i=0;i<20;i++){frame=r.Read();if(frame?.nodes[0].options.Count>0)break;}
            data.stage="playing";data.pendingRoute=null;data.panel=null;data.frame=frame;data.ink=r.Story.state.ToJson();fixture.pendingDice=null;
            fixture.narrativeSave=JsonConvert.SerializeObject(data);day4MenuJson=JsonConvert.SerializeObject(fixture);
        }
        while (!Campaign.IsTerminal)
        {
            var ui = UIManager.Instance; var talk = ui.Get<TalkPanel>(); var save = Save;
            if (DiceCheckPanel.IsShowing)
            {
                var panel = ui.Get<DiceCheckPanel>(); if (!F<bool>(panel, "_ready")) { yield return null; continue; }
                var request = Run.pendingDice; diceCount++;
                Check(!request.key.EndsWith(":income")&&!request.title.Contains("报酬检定"),"运行中没有报酬掷骰 "+request.key);
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
                foreach(var entry in SystemManager.Instance.DailyCommission.NormalCommissions) {
                    var cfg=ConfigDatabase.Instance.Contracts[entry.contractId];
                    Check(entry.completionChecked && entry.combatChecked && entry.checksResolved,"当天战斗已落定 "+Run.currentDay+"/"+entry.adventurerId);
                    Check(entry.completionRate==DailyCommissionSystem.RewardRate(entry,cfg),"报酬只按类别/生还/等级计算 "+Run.currentDay+"/"+entry.adventurerId);
                }
                cashflow.Add(new {day=Run.currentDay,cash=Run.cash,commission=data.legacyCommissionIncome,board=data.monsterBusinessIncome,total=data.totalIncome,feesPaid=Save.paidPhases.Count});
                if(new[]{1,3,4,7,12,19,27}.Contains(Run.currentDay)){Shot("结算_第"+Run.currentDay+"天");yield return null;}
                var close = F<Button>(panel, "btn_Close"); Click(close); int cash = Run.cash; close.onClick.Invoke();
                Check(Run.cash == cash, "结算重复确认不重复发放 日" + Run.currentDay); yield return null; continue;
            }
            if (Campaign.IsPreparing)
            {
                // 覆盖夹具每天清空并按本次目标重新放置；不是自然经营收入模型。
                foreach(var view in BackpackBoard.Instance.Items.ToArray()) { BackpackBoard.Instance.RemoveItem(view.Item); UnityEngine.Object.Destroy(view.gameObject); }
                yield return null;
                Click(F<Button>(ui.Get<PreparationPanel>(), "_startReceptionButton")); yield return null; continue;
            }
            if (RouteSelectionPanel.IsShowing)
            {
                var route = ui.Get<RouteSelectionPanel>();
                if (ui.Get<DescriptionPanel>()?.IsRouteContext == true)
                {
                    var detail=ui.Get<DescriptionPanel>();
                    int monster=int.Parse(BackpackBoard.Instance.Items.First(v=>v.Item.Uid==route.State.selectedUid).Def.Id);
                    Check(F<GameObject>(detail,"_combatArea").activeSelf && F<Text>(detail,"_combatDifficulty").text=="战斗难度："+CombatResolver.Difficulty(route.State.adventurerId,monster),"指路详情显示真实无伤难度 "+Run.currentDay);
                    if(Run.currentDay==4){Shot("指路战斗难度_1920");yield return null;}
                    var button = F<Button>(detail, "_routeConfirmButton"); Click(button); int count = SystemManager.Instance.DailyCommission.NormalCommissions.Count; button.onClick.Invoke();
                    Check(SystemManager.Instance.DailyCommission.NormalCommissions.Count == count, "普通委托重复指路确认只入队一次");
                }
                else
                {
                    var cfg=ConfigDatabase.Instance.Contracts[route.State.contractId];
                    int target=ConfigDatabase.Instance.Monsters.Values.Where(m=>m.monsterType==DailyCommissionSystem.TargetType(cfg)).OrderBy(m=>Math.Abs((int)m.level-(int)ConfigDatabase.Instance.Adventurers[route.State.adventurerId].baseLevel)).First().id;
                    var view = BackpackBoard.Instance.Items.FirstOrDefault(v => v != null && v.Item != null && v.Def.Id == target.ToString() && !v.Item.IsResting) ?? Spawn(target);
                    Check(SystemManager.Instance.DailyCommission.NormalCommissions.All(e => e.contractId != route.State.contractId), "选择魔物前不提前接单");
                    if(Run.currentDay==4 && !File.Exists("GlobalNativeFixture.json")){ Campaign.Save();File.WriteAllText("GlobalNativeFixture.json",JsonConvert.SerializeObject(Run)); }
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
                    if(new[]{1,4,27}.Contains(Run.currentDay)){Shot("普通委托_"+sceneId);yield return null;}
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
                if(Run.currentDay==4 && day4MenuJson==null){Campaign.Capture();day4MenuJson=JsonConvert.SerializeObject(Run);}
                if (!enteredTopics.Contains(scene) && Run.actionPoint > 0)
                { enteredTopics.Add(scene); Option(options.First(o => o.text.Contains("搭话"))); }
                else Option(options.First(o => o.callbackKey == "open_commission_panel"));
            }
            else if (options.Any(o => o.text.Contains("返回正式接待")))
            {
                if (topics.Add(scene)) Option(options.First(o => !o.text.Contains("返回")));
                else Option(options.First(o => o.text.Contains("返回正式接待")));
            }
            else if(options.Any(o=>o.text=="不再将经营权转给别人。"))Option(options.First(o=>o.text=="不再将经营权转给别人。"));
            else
            {
                var chosen = options.FirstOrDefault(o => o.checkKind == "influence") ?? options.LastOrDefault(o => o.checkKind == "information") ?? options[0];
                if(!string.IsNullOrEmpty(chosen.checkKind)&&new[]{1,4,14,27}.Contains(Run.currentDay)){Shot("剧情检定_"+scene);yield return null;}
                Option(chosen);
            }
            yield return null;
        }
        Check(visited.Count==78 && commissions.Count==78 && topics.Count==78,"27天78次接待/委托/搭话均走通");
        Check(Run.currentDay==27 && Campaign.IsTerminal && Save.settledDays.Count==27 && Save.paidPhases.Count==5,"27天结算、五次缴费与终局走通");
        Check(Run.player.proficiency>25 && Run.player.Level==3 && Run.player.creditedChoices.Count==Run.player.proficiency,"全局持续成长且三级封顶，不重复记选择");
        Shot("27天终局");yield return null;
        var rules=ExtendedRules();while(rules.MoveNext())yield return rules.Current;
        ExtraOutcomes();
        var migration = Migrations(); while (migration.MoveNext()) yield return migration.Current;
        // 验证共情真实属性动作，避免只有数据类测试而业务未应用。
        while (SaveSlotsPanel.IsShowing) { yield return null; }
        var originalPlayer = Run.player; int originalDay = Run.currentDay; Run.currentDay = 27;
        for (int rank = 1; rank <= 3; rank++)
        {
            var p = new PlayerBuildState(); p.Create(2, 1); p.proficiency = rank == 1 ? 0 : rank == 2 ? 10 : 25; Run.player = p;
            Run.pendingDice = new D20Service.Presentation { sides = 100, roll = 85, target = 60, confirmed = true, context = "narrative", key = "empathy" + rank, playerDay = 27, playerKind = "influence", playerThought = 1, playerLevel = rank };
            Check((bool)Call(Campaign, "Check", "influence", "MY", 0), "共情先读取实际成功检定 " + rank);
            var actor = Run.adventurers[1]; actor.currentSevenStats[SevenDimension.Tolerance] = 50;
            Call(Campaign, "HandleAction", "action:attribute actor=MY stat=tolerance direction=down amount=config");
            Check(actor.currentSevenStats[SevenDimension.Tolerance] == 47 - rank, "共情真实下降幅度三档 " + rank);
            actor.currentSevenStats[SevenDimension.Tolerance] = 50;
            Call(Campaign, "HandleAction", "action:attribute actor=MY stat=tolerance direction=up amount=config");
            Check(actor.currentSevenStats[SevenDimension.Tolerance] == 53 + rank, "共情真实上升幅度三档 " + rank);
            Run.pendingDice = new D20Service.Presentation { sides = 100, roll = 15, target = 60, confirmed = true, context = "narrative", key = "empathy-fail" + rank, playerDay = 27, playerKind = "influence", playerThought = 1, playerLevel = rank };
            Check(!(bool)Call(Campaign, "Check", "influence", "MY", 0), "共情失败检定 " + rank);
            actor.currentSevenStats[SevenDimension.Tolerance] = 50;
            Call(Campaign, "HandleAction", "action:attribute actor=MY stat=tolerance direction=up amount=config");
            Check(actor.currentSevenStats[SevenDimension.Tolerance] == 53, "失败不强化七维变化 " + rank);
        }
        Run.player = originalPlayer; Run.currentDay = originalDay;
    }
    static IEnumerator ExtendedRules()
    {
        var db=ConfigDatabase.Instance;var actor=Run.adventurers[1];var oldLevel=db.Adventurers[1].baseLevel;
        var oldHealth=actor.health;bool oldSevere=actor.isSeverelyInjured,oldDead=actor.isDead;
        for(int rank=1;rank<=4;rank++) for(int monsterRank=1;monsterRank<=4;monsterRank++) {
            int diff=Math.Abs(rank-monsterRank);Check(DailyCommissionSystem.RankFactor((Level)rank,(Level)monsterRank)==new[]{1f,.8f,.5f,.25f}[diff],"等级差报酬 "+rank+"/"+monsterRank);
        }
        foreach(var monster in db.Monsters.Values) foreach(MonsterType type in Enum.GetValues(typeof(MonsterType))) {
            var cfg=new ContractConfig{targetMonsterId=monster.id,targetMonsterType=type,reward=100};
            var e=new CommissionEntry{adventurerId=1,targetMonsterId=monster.id,requestedMonsterType=type};
            actor.isDead=false;
            Check(DailyCommissionSystem.RewardRate(e,cfg)==(type==monster.monsterType?DailyCommissionSystem.RankFactor(db.Adventurers[1].baseLevel,monster.level):0),"六类目标与17魔物匹配 "+type+"/"+monster.id);
        }
        foreach(var monster in db.Monsters.Values) for(int rank=1;rank<=4;rank++) for(int health=0;health<3;health++) {
            db.Adventurers[1].baseLevel=(Level)rank; actor.health=(HealthState)health; actor.isSeverelyInjured=health==2;
            int modifier=CombatResolver.Modifier(1,monster.id), expected=5+Mathf.Clamp(96-Math.Max(6,36-modifier),0,90);
            Check(CombatResolver.UnhurtPercent(1,monster.id)==expected,"战斗预览枚举与解析无伤率一致 "+monster.id+"/"+rank+"/"+health);
        }
        db.Adventurers[1].baseLevel=oldLevel;actor.health=oldHealth;actor.isSeverelyInjured=oldSevere;actor.isDead=oldDead;
        int oldDay=Run.currentDay;var oldPlayer=Run.player;Run.currentDay=27;Run.player=new PlayerBuildState();Run.player.Create(0,0);
        var nodes=JsonConvert.DeserializeObject<NarrativeSystem.PlayerNodeConfig>(Resources.Load<TextAsset>("Narrative/playerChecks").text);
        Check(nodes.checks.Count==314 && nodes.checks.Count(c=>c.player)==312,"314个检定调用分类，312个玩家检定");
        var prepared=NarrativeRuntime.Prepare(Resources.Load<TextAsset>("Narrative/story27").text);
        foreach(var rule in nodes.checks) {
            Check(prepared.Contains("check_context node="+rule.checkId+"\""),"原编译调用有稳定上下文 "+rule.checkId);
            if(rule.player) {
                var rng=JsonUtility.ToJson(UnityEngine.Random.state);var c=Campaign.PreviewPlayerCheck(rule.checkId);
                Check(c.attribute==rule.attribute && c.kind==rule.kind && rng==JsonUtility.ToJson(UnityEngine.Random.state),"全局节点预览无随机/分类一致 "+rule.checkId);
            }
        }
        Check(nodes.choices.Count(c=>!string.IsNullOrEmpty(c.checkId))>=470,"嵌套选项完整标注，不遗漏元数据容器地址");
        var fe=Run.adventurers[2];var prevC=fe.currentSevenStats[SevenDimension.Courage];var prevR=fe.currentSevenStats[SevenDimension.Resolve];
        fe.currentSevenStats[SevenDimension.Courage]=0;fe.currentSevenStats[SevenDimension.Resolve]=0;
        var coop=Campaign.PreviewPlayerCheck(nodes.checks.First(c=>c.kind=="cooperate"&&c.actor=="FE").checkId);
        Check(coop.modifier==-25,"共享合作使用Ferdia七维，不借用上一位访客");
        fe.currentSevenStats[SevenDimension.Courage]=prevC;fe.currentSevenStats[SevenDimension.Resolve]=prevR;
        Run.player=oldPlayer;Run.currentDay=oldDay;
        // 两种分辨率重新打开真实面板，保留截图而不改变正式剧情。
        foreach(int width in new[]{1920,1280}) {
            var view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
            typeof(NarrativeIntegrationChecks).GetMethod("SetGameViewSize",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{view,width,width==1920?1080:720});
            UIManager.Instance.Open<CharacterArchivePanel>();UIManager.Instance.Get<CharacterArchivePanel>().Refresh(2);UIManager.Instance.Get<CharacterArchivePanel>().SelectTab(0);yield return null;
            Check(F<Text>(UIManager.Instance.Get<CharacterArchivePanel>(),"_level").text=="等级：A","基本信息等级栏 "+width);
            Shot("档案等级_"+width);yield return null;UIManager.Instance.Close<CharacterArchivePanel>();
            var d=UIManager.Instance.Open<DescriptionPanel>(ItemData.FromConfig(db.Items[101]));yield return null;
            Check(!F<GameObject>(d,"_combatArea").activeSelf,"普通查看详情不显示战斗难度 "+width);UIManager.Instance.Close<DescriptionPanel>();
            d=UIManager.Instance.Open<DescriptionPanel>(new DescriptionPanel.OpenContext{item=ItemData.FromConfig(db.Items[101]),combatAdventurerId=2,onConfirm=()=>{}});yield return null;
            Check(F<GameObject>(d,"_combatArea").activeSelf && F<Text>(d,"_combatDifficulty").text=="战斗难度："+CombatResolver.Difficulty(2,101),"指路详情显示统一战斗难度 "+width);
            Shot("战斗难度_"+width);yield return null;UIManager.Instance.Close<DescriptionPanel>();
            Runtime.Set("current_actor","FE");Runtime.Set("current_scene","D04_V01");Runtime.Story.ChoosePathString("VIS_D04_V01_TOPICS");Save.stage="playing";Save.frame=null;
            var talk=UIManager.Instance.Open<TalkPanel>();talk.SetInstantText(true);talk.Dialogue.StartNarrative();for(int i=0;i<8;i++)yield return null;
            Check(Save.frame.nodes[0].options.Any(o=>!string.IsNullOrEmpty(o.checkKind)),"两分辨率长话题实际绑定检定 "+width);
            Shot("长选项_"+width);yield return null;

        }
    }
    static void ExtraOutcomes()
    {
        var backup=Run.adventurers;var entries=Save.normal.ToList();var system=SystemManager.Instance.DailyCommission;
        int contract=F<NarrativeSystem.Rules>(Campaign,"_rules").scenes[0].contractId;
        var cfg=ConfigDatabase.Instance.Contracts[contract];int target=cfg.targetMonsterId;
        foreach(bool wrong in new[]{false,true})foreach(bool preSevere in new[]{false,true})foreach(InjuryResult injury in Enum.GetValues(typeof(InjuryResult))) {
            Run.adventurers=Copy(backup);foreach(var pair in Run.adventurers)pair.Value.Config=ConfigDatabase.Instance.Adventurers[pair.Key];Run.injuryAppliedDay.Clear();var adv=Run.adventurers[1];adv.isDead=false;adv.isSeverelyInjured=preSevere;
            system.Clear();var e=new CommissionEntry(contract,1){targetMonsterId=target,requestedMonsterType=DailyCommissionSystem.TargetType(cfg),wrongRoute=wrong,injury=injury,combatChecked=true};system.AddNormal(e);
            system.ResolveAllChecks();Call(Campaign,"FinishDayChecks");
            bool dead=preSevere&&injury==InjuryResult.Severe;
            int income=system.ComputeAll().normalIncome;float rate=DailyCommissionSystem.RankFactor(ConfigDatabase.Instance.Adventurers[1].baseLevel,ConfigDatabase.Instance.Monsters[target].level);
            Check(adv.isDead==dead && income==(wrong||dead?0:Mathf.RoundToInt(cfg.reward*rate)),"正确/错误与原伤势死亡报酬 "+wrong+"/"+preSevere+"/"+injury);
            Check(Save.returnStates[1]==(wrong&&!dead&&injury!=InjuryResult.None?(injury==InjuryResult.Severe?2:1):0),"报复仅错误指路且受伤存活 "+wrong+"/"+preSevere+"/"+injury);
            int cash=Run.cash;system.ResolveAllChecks();Check(Run.cash==cash&&adv.isDead==dead,"重复提交不发钱不重复伤势 "+wrong+"/"+preSevere+"/"+injury);
            UIManager.Instance.Close<DailySettlementPanel>();
        }
        Run.adventurers=backup;system.Clear();foreach(var e in entries)system.AddNormal(e);
        Save.checkId=null;
        string[] actors={"MY","FE","DW","SA","AU","DA","UN"};
        for(int i=0;i<actors.Length;i++) {
            var adv=Run.adventurers[i+1];adv.isDead=false;foreach(SevenDimension stat in Enum.GetValues(typeof(SevenDimension)))adv.currentSevenStats[stat]=50;
            Check(!(bool)Call(Campaign,"Check","succession",actors[i],0),"继任未达标不掷骰 "+actors[i]);
            adv.currentSevenStats[SevenDimension.Tolerance]=100;adv.currentSevenStats[SevenDimension.Courage]=100;adv.currentSevenStats[SevenDimension.Diligence]=100;
            adv.currentSevenStats[SevenDimension.Reason]=actors[i]=="MY"?100:0;adv.currentSevenStats[SevenDimension.Resolve]=actors[i]=="MY"?100:0;
            adv.currentSevenStats[SevenDimension.Benevolence]=actors[i]=="AU"?0:100;adv.currentSevenStats[SevenDimension.Piety]=0;
            Check((bool)Call(Campaign,"Check","succession",actors[i],0),"继任达标仍按条件放行 "+actors[i]);
        }
    }
    static IEnumerator Migrations()
    {
        var baseline=JsonConvert.DeserializeObject<RunState>(day4MenuJson);var saved=JsonConvert.DeserializeObject<NarrativeSystem.SaveData>(baseline.narrativeSave);
        int actor=2,contract=F<NarrativeSystem.Rules>(Campaign,"_rules").scenes.First(s=>s.id=="D04_V01").contractId;
        var cfg=ConfigDatabase.Instance.Contracts[contract];int target=cfg.targetMonsterId;
        for(int mode=0;mode<4;mode++) {
            var legacy=Copy(baseline);legacy.cash=777;var data=Copy(saved);data.uiVersion=5;data.normal.Clear();
            var e=new CommissionEntry(contract,actor){targetMonsterId=target,completionRate=.25f,completionChecked=true,combatChecked=mode!=2,checksResolved=mode!=2,statsApplied=true};data.normal.Add(e);
            if(mode==0) {
                data.stage="settlement_checks";legacy.pendingDice=new D20Service.Presentation{sides=100,key="settle:4:"+actor+":"+contract+":income",context="settlement",target=52,roll=42,rawRoll=42,charmUsed=true};
            } else if(mode==1) {
                data.settledDays.Add(4);data.stage="settlement";data.settlement=new SettlementData(13,0,17,30,150);
            } else if(mode==2) {
                data.stage="settlement_checks";legacy.pendingDice=new D20Service.Presentation{sides=100,key="settle:4:"+actor+":"+contract+":battle",context="settlement",target=36,modifier=-18,roll=42,rawRoll=42};
            } else {
                data.stage="dice";legacy.pendingDice=new D20Service.Presentation{sides=100,key="old-story",context="narrative",target=68,modifier=6,roll=42,rawRoll=42};
            }
            legacy.narrativeSave=JsonConvert.SerializeObject(data);SaveAndLoadManager.Save("run_state",legacy);SceneManager.LoadScene("PlayerGame");for(int i=0;i<25;i++)yield return null;
            Check(Save.uiVersion==6 && Run.cash==777,"迁移升级且不回滚/重复发金币 "+mode);
            if(mode==0)Check(Run.pendingDice==null && Save.stage=="settlement" && Save.settlement.legacyCommissionIncome==Mathf.RoundToInt(cfg.reward*DailyCommissionSystem.RankFactor(ConfigDatabase.Instance.Adventurers[actor].baseLevel,ConfigDatabase.Instance.Monsters[target].level)),"旧报酬检定取消，未付款按新公式 stage="+Save.stage+" pending="+(Run.pendingDice!=null)+" actual="+Save.settlement?.legacyCommissionIncome);
            if(mode==1)Check(Save.settlement.legacyCommissionIncome==13 && Save.settlement.totalIncome==30,"已付款旧账不重算");
            if(mode==2||mode==3)Check(Run.pendingDice.roll==42&&Run.pendingDice.rawRoll==42&&Run.pendingDice.modifier==(mode==2?-18:6)&&Run.pendingDice.target==(mode==2?36:68),"已建立战斗/剧情检定保持原骰与修正 "+mode);
            if(DiceCheckPanel.IsShowing)UIManager.Instance.Close<DiceCheckPanel>();
        }
        var old=Copy(baseline);old.player=null;old.cash=888;var oldData=Copy(saved);oldData.uiVersion=5;old.narrativeSave=JsonConvert.SerializeObject(oldData);
        SaveAndLoadManager.Save("run_state",old);SceneManager.LoadScene("PlayerGame");for(int i=0;i<25;i++)yield return null;
        Check(PlayerBuildPanel.IsChoosing,"第4天未建玩家的旧档也在安全停点补选");
        var creation=UIManager.Instance.Get<PlayerBuildPanel>();Click(F<Button[]>(creation,"_types")[1]);Click(F<Button[]>(creation,"_thoughts")[2]);Click(F<Button>(creation,"_confirm"));yield return null;
        Check(Run.player.created&&Run.player.proficiency==0&&Run.player.knowledge==55&&Run.cash==888,"全局补选不补经验、不改资源");
        Campaign.Capture();
        for(int slot=1;slot<=4;slot++){var copy=Copy(Run);copy.cash=1000+slot;copy.player.proficiency=slot;SaveAndLoadManager.Save(SaveAndLoadManager.SlotKey(slot),copy);}
        for(int slot=1;slot<=4;slot++){
            Check(SaveAndLoadManager.ActivateSlot(slot),"四槽可独立激活 "+slot);SceneManager.LoadScene("PlayerGame");for(int i=0;i<20;i++)yield return null;
            Check(Run.cash==1000+slot&&Run.player.proficiency==slot,"四槽独立恢复 "+slot);
        }
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>隔离工程中的布局、射线命中与真实渲染检查；不冒充操作系统鼠标验收。</summary>
[InitializeOnLoad]
public static class UIPolishChecks
{
    const string Key = "MonsterDungeon.UIPolishQA";
    static readonly List<string> checks = new List<string>(), errors = new List<string>(), shots = new List<string>();
    static IEnumerator sequence; static double started, next; static bool capturing;
    static int width = 1920, height = 1080;
    static T F<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(owner);
    static void Check(bool value, string name) { if (!value) throw new Exception(name); checks.Add(name); }
    static void Click(Component c) => ArtAcceptanceChecks.Pointer(c);
    static UIManager UI => UIManager.Instance;
    static UIPolishChecks() { if (SessionState.GetBool(Key, false)) { EditorApplication.update += Tick; Application.logMessageReceived += Log; started = EditorApplication.timeSinceStartup; } }
    static void Log(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }
    public static void RunBatch()
    {
        if (!Application.productName.EndsWith("_NarrativeValidation")) throw new Exception("禁止在用户工程写入验收档");
        if (!SaveAndLoadManager.ActivateSlot(1)) throw new Exception("需要隔离的第四天整备测试槽");
        SessionState.SetBool(Key, true); EditorSceneManager.OpenScene("Assets/Scenes/PlayerGame.unity"); SetResolution(1920,1080);
        EditorApplication.update += Tick; Application.logMessageReceived += Log; started = EditorApplication.timeSinceStartup; EditorApplication.EnterPlaymode();
    }
    public static void PrepareNativeSlot()
    {
        if (!Application.productName.EndsWith("_NarrativeValidation")) throw new Exception("仅限隔离副本");
        var fixture = JsonConvert.DeserializeObject<RunState>(File.ReadAllText("UIPolishNativeFixture.json"));
        SaveAndLoadManager.Save(SaveAndLoadManager.SlotKey(4), fixture);
    }
    public static void BuildNative() { UIPolishEditor.Configure(); PrepareNativeSlot(); UIPolishEditor.BuildInteractive(); }
    static void SetResolution(int w, int h)
    {
        width = w; height = h; var view = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")); view.Show();
        typeof(NarrativeIntegrationChecks).GetMethod("SetGameViewSize", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { view, w, h });
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + .06; EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if (EditorApplication.timeSinceStartup - started > 230) throw new Exception("UI修复验收超时");
            if (errors.Count > 0) throw new Exception(errors[0]);
            if (capturing || SystemManager.Instance?.IsInitialized != true || !F<bool>(SystemManager.Instance.Narrative, "_booted")) return;
            if (sequence == null) sequence = Scenario(); if (!sequence.MoveNext()) Finish(true, "");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Finish(bool passed, string failure)
    {
        File.WriteAllText("UIPolishAcceptance.json", JsonConvert.SerializeObject(new { passed, failure, checks, errors, shots }, Formatting.Indented));
        SessionState.SetBool(Key, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        EditorApplication.isPlaying = false; EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
    }
    static void Shot(string name) { capturing = true; UI.StartCoroutine(Capture(name)); }
    static IEnumerator Capture(string name)
    {
        yield return new WaitForSecondsRealtime(.25f); Canvas.ForceUpdateCanvases();
        string folder = Path.GetFullPath("../qa/UIPolish/" + width); Directory.CreateDirectory(folder);
        var camera = Camera.main; var target = new RenderTexture(width, height, 24); var previous = camera.targetTexture; var active = RenderTexture.active;
        var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas).ToArray(); var modes = canvases.Select(c => c.renderMode).ToArray(); var cameras = canvases.Select(c => c.worldCamera).ToArray();
        camera.targetTexture = target; foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; }
        Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply();
        string path = Path.Combine(folder, name + ".png"); File.WriteAllBytes(path, image.EncodeToPNG()); shots.Add(path);
        for (int i=0;i<canvases.Length;i++) { canvases[i].renderMode=modes[i]; canvases[i].worldCamera=cameras[i]; }
        camera.targetTexture=previous; RenderTexture.active=active; UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(target); capturing=false;
    }
    static IEnumerator Scenario()
    {
        var prep = UI.Get<PreparationPanel>(); var run = RunStateManager.Instance.run;
        Check(SystemManager.Instance.Narrative.IsPreparing, "从隔离槽恢复第四天整备");
        for(int resolution = 0; resolution < 2; resolution++)
        {
            SetResolution(resolution == 0 ? 1920 : 1280, resolution == 0 ? 1080 : 720); yield return null;
            Shot("01_整备"); yield return null;
            Click(F<Button>(prep,"_abyssButton")); yield return null;
            var altar=UI.Get<AbyssalAltarPanel>(); Click(F<Button>(altar,"_returnButton")); yield return null;
            var hell=UI.Get<DecomposePanel>(); Shot("02_祭坛与地狱"); yield return null;
            Click(F<Button>(altar,"_newMonsterButton")); yield return null;
            var shop=UI.Get<ShopPanel>(); Check(hell.IsOpen && !altar.IsOpen,"二级商店保留地狱 " + width);
            Shot("03_魔物商店与地狱"); yield return null;
            Click(F<Button>(shop,"_closeButton")); yield return null;
            Check(altar.IsOpen && hell.IsOpen,"商店返回恢复祭坛且地狱保留 " + width);
            Click(F<Button>(hell,"_closeButton")); Click(F<Button>(altar,"_closelButton")); yield return null;
            ShopPanel.OpenMonsterStorage(); yield return null; Shot("04_魔物仓库"); yield return null; Click(F<Button>(UI.Get<ShopPanel>(),"_closeButton"));
            UI.Open<PlayerBuildPanel>(); Shot("05_自身状态"); yield return null; Click(F<Button>(UI.Get<PlayerBuildPanel>(),"_back"));
            UI.Open<SettingsPanel>(); yield return null; var settings=UI.Get<SettingsPanel>(); Shot("06_设置"); yield return null;
            float volume=F<Slider>(settings,"sld_sfx").value; Click(F<Button>(settings,"btn_read_save")); yield return null;
            Check(!settings.gameObject.activeSelf && settings.IsOpen && SaveSlotsPanel.IsShowing,"四槽隐藏来源设置且保留打开上下文 " + width);
            Shot("07_四槽独立显示"); yield return null; var slots=UnityEngine.Object.FindObjectOfType<SaveSlotsPanel>(); Click(F<Button>(slots,"back")); yield return null;
            Check(settings.gameObject.activeSelf && F<Slider>(settings,"sld_sfx").value==volume,"返回设置保留滑块值 " + width); Click(F<Button>(settings,"btn_close"));
        }
        SetResolution(1920,1080); Click(F<Button>(prep,"_startReceptionButton")); yield return null;
        var talk=UI.Get<TalkPanel>();
        var liveWriter=(TypewriterTextLegacy)F<MonoBehaviour>(talk,"_typewriterComponent");
        var reading=liveWriter.text.GetComponentInParent<ScrollRect>();
        Check(liveWriter.text.transform.IsChildOf(talk.RightDisplayArea.transform),"当前台词位于右侧纸张而非旧底部框");
        Check(reading!=null && reading.viewport.rect.height>0,"当前台词长文有有效滚动视口");
        var oldLine=liveWriter.text.text; liveWriter.Play(string.Concat(Enumerable.Repeat("长台词排版检查，确认文字可滚动且不会落出纸张。",24))); yield return null;
        Canvas.ForceUpdateCanvases(); Check(reading.content.rect.height>reading.viewport.rect.height,"长台词内容高度可扩展");
        reading.verticalNormalizedPosition=0; liveWriter.Play(oldLine); Check(reading.content.anchoredPosition==Vector2.zero,"换句重置当前台词滚动位置");
        Shot("右侧当前台词布局"); yield return null;
        var campaign=SystemManager.Instance.Narrative; var save=F<NarrativeSystem.SaveData>(campaign,"_save");
        var archive=UI.Get<CharacterArchivePanel>();
        for (int actor=1;actor<=7;actor++)
        {
            UI.Open<CharacterArchivePanel>(); archive.Refresh(actor); archive.SelectTab(0); yield return null;
            var icon=F<Image>(archive,"_iconImage"); Check(icon.enabled==(actor!=3 && actor!=7),"人物独立头像或明确待补 " + actor);
            if(icon.enabled)Check(icon.sprite==Resources.Load<Sprite>("Art/Portraits/Actor"+actor),"头像未被中央立绘覆盖 " + actor);
            Shot("档案_人物"+actor); yield return null;
        }
        string position=save.ink; int serial=save.displaySerial;
        archive.Refresh(2);
        for(int tab=0;tab<4;tab++) { archive.SelectTab(tab); Check(F<GameObject>(archive,"_statsRoot").activeSelf==(tab==0),"档案七维仅基本页显示 " + tab); Shot("档案_页签"+tab); yield return null; }
        Check(save.ink==position && save.displaySerial==serial,"查档切页不推进右侧剧情"); Click(F<Button>(archive,"_closeButton"));
        var item=ItemData.FromConfig(ConfigDatabase.Instance.Items[1]);
        // 菜单测试使用局部回调计数，不消耗正式道具；射线命中成功才发送事件。
        for(int mode=0;mode<2;mode++)
        {
            int used=0,dropped=0; var data=new ItemActionData{data=item,onDrop=()=>dropped++,onUse=mode==1?(Action)(()=>used++):null};
            UI.Open<BackpackPanel>(); UI.Open<ItemActionPanel>(data); yield return null;
            var menu=UI.Get<ItemActionPanel>(); Check(F<Button>(menu,"_useButton").gameObject.activeSelf==(mode==1),"两行/三行菜单显示 " + mode);
            Shot("道具菜单_"+mode); yield return null; Click(F<Button>(menu,"_examineButton")); yield return null;
            Check(UI.Get<DescriptionPanel>().IsOpen && !menu.IsOpen,"点击查看详情实际打开并退出菜单 " + mode); Shot("道具详情_"+mode); yield return null;
            Click(F<Button>(UI.Get<DescriptionPanel>(),"_confirmButton")); UI.Open<ItemActionPanel>(data); yield return null;
            if(mode==1)Click(F<Button>(menu,"_useButton"));else Click(F<Button>(menu,"_dropButton"));
            Check((mode==1?used:dropped)==1,"点击使用/丢弃只调用一次 " + mode); UI.Close<BackpackPanel>();
        }
        SetResolution(1280,720); UI.Open<CharacterArchivePanel>(); archive.Refresh(1); archive.SelectTab(0); Shot("档案_1280基本信息"); yield return null;
        archive.SelectTab(1); Shot("档案_1280记录"); yield return null; UI.Close<CharacterArchivePanel>();
        // 独立夹具只走原指路面板，检查同一棋盘搬运和详情逐层返回，不产生委托。
        var board=BackpackBoard.Instance; var originalParent=board.transform.parent.parent;
        int queue=SystemManager.Instance.DailyCommission.NormalCommissions.Count;
        var route=UI.Open<RouteSelectionPanel>(new RouteSelectionPanel.OpenData{state=new RouteSelectionPanel.RouteState{contractId=ConfigDatabase.Instance.Contracts.Keys.First(),adventurerId=2},cancelled=()=>{}});
        Shot("指路_原棋盘"); yield return null;
        Click(F<Button>(route,"_returnButton")); yield return null;
        Check(board.transform.parent.parent==originalParent && SystemManager.Instance.DailyCommission.NormalCommissions.Count==queue,"指路返回原棋盘完整归位且不接单");
        var commission=UI.Open<CommissionPanel>(ConfigDatabase.Instance.Contracts.Keys.First()); yield return null;
        var heading=commission.transform.Find("UIRepairCommissionTitle"); var strip=commission.transform.Find("UIRepairTitleStrip");
        Check(!commission.transform.Find("img_BG/img_Title").gameObject.activeSelf,"普通委托旧标题隐藏");
        Check(heading.GetSiblingIndex()>strip.GetSiblingIndex(),"普通委托文字在标题条之上");
        Shot("普通委托_标题与返回"); yield return null; UI.Close<CommissionPanel>();
    }
}
#endif

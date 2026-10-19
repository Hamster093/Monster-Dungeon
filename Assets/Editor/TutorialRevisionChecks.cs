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
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>在隔离产品读取导出的原格式存档，抽查教程及真实剧情接续，不覆盖用户存档槽。</summary>
[InitializeOnLoad]
public static class TutorialRevisionChecks
{
    const string Key="MonsterDungeon.TutorialRevisionChecks";
    const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    static bool ended,capturing;static double start,next;static Stack<IEnumerator> steps=new Stack<IEnumerator>();
    static List<string> checks=new List<string>(),shots=new List<string>(),errors=new List<string>();
    static string Out=>TutorialTestSaves.Out;
    static NarrativeSystem C=>SystemManager.Instance.Narrative;
    static NarrativeSystem.SaveData S=>F<NarrativeSystem.SaveData>(C,"_save");
    static NarrativeRuntime Story=>F<NarrativeRuntime>(C,"_runtime");
    static RunState Run=>RunStateManager.Instance.run;
    static T F<T>(object o,string field)=>(T)o.GetType().GetField(field,Flags).GetValue(o);
    static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Flags).Invoke(o,args);
    static GuideHelpOverlay Guide=>(GuideHelpOverlay)typeof(GuideHelpOverlay).GetField("current",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add(message);}
    static TutorialRevisionChecks(){if(SessionState.GetBool(Key,false))Install();}
    public static void RunBatch()
    {
        if(!Application.productName.EndsWith("_NarrativeValidation"))throw new Exception("仅允许隔离产品运行");
        string path=Path.Combine(Application.persistentDataPath,"run_state.json");SessionState.SetString(Key+".before",File.Exists(path)?Convert.ToBase64String(File.ReadAllBytes(path)):"<absent>");
        File.WriteAllText(path,File.ReadAllText(Path.Combine(Out,"测试存档/终局测试.json")));
        SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/PlayerGame.unity");Size(1920,1080);Install();EditorApplication.EnterPlaymode();
    }
    static void Install(){start=EditorApplication.timeSinceStartup;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
    static void Log(string text,string stack,LogType kind){if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)errors.Add(text);}
    static void Tick()
    {
        GuideHelpOverlay.SuppressAutomatic=true;
        if(ended||!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.timeSinceStartup<next)return;
        next=EditorApplication.timeSinceStartup+.04;EditorApplication.QueuePlayerLoopUpdate();
        try{if(EditorApplication.timeSinceStartup-start>200)throw new Exception("快验超时，"+C?.EndingStage);if(errors.Count>0)throw new Exception(errors[0]);if(capturing)return;if(steps.Count==0)steps.Push(Scenario());var step=steps.Peek();if(!step.MoveNext()){steps.Pop();if(steps.Count==0)Finish(true,"");}else if(step.Current is IEnumerator nested)steps.Push(nested);}
        catch(Exception e){Finish(false,e.ToString());}
    }
    static void Finish(bool ok,string failure)
    {
        if(ended)return;ended=true;Directory.CreateDirectory(Out);File.WriteAllText(Path.Combine(Out,"必要验收.json"),JsonConvert.SerializeObject(new{passed=ok,failure,checks,errors,shots,method="独立Unity实际读入导出档、构造界面与程序化控件调用；未覆盖全部人物终局和报复分支。"},Formatting.Indented));
        SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;EditorApplication.isPlaying=false;
        EditorApplication.delayCall+=()=>{string before=SessionState.GetString(Key+".before","<absent>"),file=Path.Combine(Application.persistentDataPath,"run_state.json");if(before=="<absent>"){if(File.Exists(file))File.Delete(file);}else File.WriteAllBytes(file,Convert.FromBase64String(before));EditorApplication.Exit(ok?0:1);};
    }
    static IEnumerator Boot(){while(!SystemManager.Instance.IsInitialized||!F<bool>(C,"_booted"))yield return null;yield return Wait(.2f);PresentationEffects.CancelAll();CloseGuide();}
    static IEnumerator Scenario()
    {
        yield return Boot();var ui=UIManager.Instance;ui.Close<TalkPanel>();
        for(int n=0;n<10&&S.stage!="ending_selection";n++){C.Next();C.GetFrame();yield return null;}
        C.PresentPause();yield return Wait(.2f);
        Check(S.stage=="ending_selection"&&Run.currentDay==27&&C.EndingCandidate==-1&&Run.pendingDice==null,"终局档读入停在选人前，无残留检定");
        for(int id=1;id<=7;id++)Check(!Run.adventurers[id].isDead&&C.AssessSuccession(id).gap==0,"七人存活且接任条件满足 "+id);
        var end=ui.Get<EndingPresentationPanel>();Check(F<Button[]>(end,"_actorButtons").All(b=>b.interactable),"七个人物可选择");Shot("终局测试档_1920");yield return null;
        C.PreviewEndingCandidate(1);C.ConfirmEndingCandidate();yield return Wait(.3f);
        Check(Run.pendingDice==null&&S.frame?.nodes.Any(n=>n.lines.Any(l=>(l.sourceId??"").StartsWith("SUCCESSION_MY_")))==true,"实际确认进入Myth终局开头，不预先播放CG");
        ui.Close<TalkPanel>();ui.Close<EndingPresentationPanel>();
        S.stage="preparation";ui.Open<PreparationPanel>();
        foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720)})
        {
            Size(size.x,size.y);yield return Wait(.15f);Check(((RectTransform)F<Button>(ui.Get<HUDPanel>(),"_playerButton").transform).anchoredPosition==new Vector2(530,-68),"自身状态入口居于前两版之间 "+size.x);Shot("顶部入口_"+size.x);yield return null;
        }
        var prep=ui.Get<PreparationPanel>().GetComponentInChildren<GuideTopicAnchor>(true);
        Check(!prep.CanAutoShow&&GuideHelpOverlay.Find("preparation").intro.Length==0,"删除的整备首次教程不被说明正文替代");
        GuideHelpOverlay.ShowHelp(prep);yield return Wait(.1f);Check(GuideHelpOverlay.IsShowing&&!F<Button>(Guide,"_replay").gameObject.activeSelf,"无首次教程时隐藏重新查看");CloseGuide();
        var altar=ui.Open<AbyssalAltarPanel>();var aa=altar.GetComponentInChildren<GuideTopicAnchor>(true);GuideHelpOverlay.ShowHelp(aa);yield return Wait(.1f);
        Check(!F<Button>(Guide,"_detailsButton").gameObject.activeSelf&&!F<Button>(Guide,"_replay").gameObject.activeSelf&&((RectTransform)F<Button>(Guide,"_close").transform).anchoredPosition.x==0,"空详情和空教程按钮隐藏，返回居中");Shot("简要说明无空按钮_1280");yield return null;CloseGuide();ui.Close(altar);
        S.stage="playing";ui.Open<TalkPanel>();var talk=ui.Get<TalkPanel>();var reception=talk.GetComponentsInChildren<GuideTopicAnchor>(true).First(a=>a.Owner==talk);
        Check(!reception.help&&reception.IsVisible&&reception.CanAutoShow,"来访者问号撤除但首次引导可用");
        Call(Guide,"Open",reception,false);yield return Wait(.1f);Check(GuideHelpOverlay.IsTutorial&&F<Text>(Guide,"_tutorialText").text==GuideHelpOverlay.Find("reception").intro[0],"无问号页面仍显示保留的原教程");CloseGuide();
        var archive=ui.Open<CharacterArchivePanel>();archive.Refresh(3);archive.SelectTab(2);
        var anchor=archive.GetComponent<GuideTopicAnchor>();
        var helpRect=(RectTransform)anchor.help.transform;Check(helpRect.anchoredPosition.y==(helpRect.anchorMax.y>.9f?-38:6),"仅档案问号上移6像素");
        foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080)}){Size(size.x,size.y);yield return Wait(.1f);Shot("档案问号_"+size.x);yield return null;}
        Call(Guide,"Open",anchor,false);yield return Wait(.1f);
        for(int step=0;step<5;step++)
        {Check(archive.CurrentTab==(step<2?0:step-1),"档案高亮保持原步骤归属 "+step);if(step<4){yield return Wait(.22f);Call(Guide,"Advance");yield return null;}}
        CloseGuide();Check(archive.CurrentTab==2,"引导结束恢复原页签");ui.Close(archive);
        int contract=ConfigDatabase.Instance.Contracts.Keys.First();var commission=ui.Open<CommissionPanel>(contract);var ca=commission.GetComponent<GuideTopicAnchor>();
        Call(Guide,"Open",ca,false);yield return Wait(.1f);var scroll=F<ScrollRect>(Guide,"_tutorialScroll");
        Check(scroll&&scroll.content&&scroll.viewport&&F<Text>(Guide,"_tutorialText").text==GuideHelpOverlay.Find("commission").intro[0],"长委托教程原文完整，滚动容器绑定");
        scroll.verticalNormalizedPosition=0;int before=F<int>(Guide,"_step");Guide.OnPointerClick(new PointerEventData(EventSystem.current){dragging=true});Check(F<int>(Guide,"_step")==before,"滚动拖动不推进教程");Shot("长委托教程_1920");yield return null;
        Size(1280,720);yield return Wait(.15f);Shot("长委托教程_1280");yield return null;CloseGuide();ui.Close(commission);
        foreach(string topic in new[]{"player_type","saves","gallery"})Check(!GuideHelpOverlay.Find(topic).HasHelp&&!GuideHelpOverlay.Find(topic).HasIntro,"删除的主题停用 "+topic);
        Check(GuideHelpOverlay.Find("archive").introIds.SequenceEqual(new[]{"archive.intro.01","archive.intro.02","archive.intro.03","archive.intro.04","archive.intro.05"}),"保留原固定编号");
        yield return LoadFixture("报复测试.json");ui=UIManager.Instance;ui.Close<TalkPanel>();
        Check(Run.currentDay==2&&Run.cash==2000&&InventoryModel.Instance.CountId(5)==3&&InventoryModel.Instance.CountId(6)==3&&InventoryModel.Instance.CountId(2)==2,"报复档资源准确且未提前消耗");
        yield return DriveTo(o=>(o.choiceId??"").StartsWith("REVENGE_OPTIONS."));
        Check((string)Story.Get("current_actor")=="DW"&&(string)Story.Get("revenge_severity")=="light","第一位为矮人小队轻度报复");
        C.StartReception();talk=ui.Get<TalkPanel>();talk.PrepareVisitorPortrait(S.portraitAddress);yield return Wait(1.1f);Shot("轻度报复测试档_1280");yield return null;ui.Close<TalkPanel>();
        Check(C.Choose(S.frame.nodes.SelectMany(n=>n.options).First(o=>o.text.Contains("致歉礼盒"))),"实际提交轻度报复礼盒选项");
        yield return DriveTo(o=>(o.choiceId??"").StartsWith("REVENGE_OPTIONS."));
        Check(InventoryModel.Instance.CountId(5)==2&&InventoryModel.Instance.CountId(6)==3&&Run.cash==2000,"轻度报复礼盒只扣一次，按原剧情直接接续下一人");
        Check((string)Story.Get("current_actor")=="FE"&&(string)Story.Get("revenge_severity")=="heavy"&&Run.pendingDice==null,"结束首位接待后接上Ferdia重度报复，未预掷骰");
        C.StartReception();talk=ui.Get<TalkPanel>();talk.PrepareVisitorPortrait(S.portraitAddress);yield return Wait(1.1f);Shot("重度报复接续_1280");yield return null;
        yield return LoadFixture("报复测试.json");ui=UIManager.Instance;ui.Close<TalkPanel>();yield return DriveTo(o=>(o.choiceId??"").StartsWith("REVENGE_OPTIONS."));
        Check((string)Story.Get("current_actor")=="DW"&&InventoryModel.Instance.CountId(5)==3&&Run.cash==2000,"重新读取导出报复档恢复起点与道具");
    }
    static IEnumerator DriveTo(Func<DialogueOption,bool> match)
    {
        for(int i=0;i<350;i++)
        {
            var frame=C.GetFrame();
            if(frame!=null&&frame.nodes.SelectMany(n=>n.options).Any(match))yield break;
            if(Run.pendingDice!=null)throw new Exception("接续途中意外进入检定："+Run.pendingDice.key);
            if(frame!=null&&frame.nodes.Any(n=>n.options.Count>0))throw new Exception("接续途中出现其他选项："+string.Join("/",frame.nodes.SelectMany(n=>n.options).Select(o=>o.text)));
            C.Next();yield return null;
        }
        throw new Exception("接续未到预期停点："+Story.Story.state.currentPathString);
    }
    static IEnumerator LoadFixture(string name){CloseGuide();File.WriteAllText(Path.Combine(Application.persistentDataPath,"run_state.json"),File.ReadAllText(Path.Combine(Out,"测试存档",name)));SceneManager.LoadScene("PlayerGame");yield return null;yield return Boot();}
    static void CloseGuide(){if(GuideHelpOverlay.IsShowing)Call(Guide,"Close",false);}
    static IEnumerator Wait(float seconds){float until=Time.unscaledTime+seconds;while(Time.unscaledTime<until)yield return null;}
    static void Size(int w,int h){var view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));view.Show();typeof(NarrativeIntegrationChecks).GetMethod("SetGameViewSize",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{view,w,h});}
    static void Shot(string name){capturing=true;UIManager.Instance.StartCoroutine(Capture(name));}
    static IEnumerator Capture(string name)
    {
        yield return new WaitForSecondsRealtime(.12f);Canvas.ForceUpdateCanvases();int w=Screen.width,h=Screen.height;var camera=Camera.main;var rt=new RenderTexture(w,h,24);var old=camera.targetTexture;var active=RenderTexture.active;var canvases=UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas).ToArray();var modes=canvases.Select(c=>c.renderMode).ToArray();var cameras=canvases.Select(c=>c.worldCamera).ToArray();Texture2D texture=null;
        try{camera.targetTexture=rt;foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;}Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;texture=new Texture2D(w,h,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,w,h),0,0);texture.Apply();Directory.CreateDirectory(Path.Combine(Out,"截图"));string path=Path.Combine(Out,"截图",name+".png");File.WriteAllBytes(path,texture.EncodeToPNG());shots.Add(path);}finally{for(int i=0;i<canvases.Length;i++)if(canvases[i]){canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];}camera.targetTexture=old;RenderTexture.active=active;if(texture)UnityEngine.Object.Destroy(texture);UnityEngine.Object.Destroy(rt);capturing=false;}
    }
}
#endif

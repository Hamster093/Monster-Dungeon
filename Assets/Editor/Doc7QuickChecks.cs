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

[InitializeOnLoad]
public static class Doc7QuickChecks
{
    const string Key="MonsterDungeon.Doc7Checks";
    static readonly List<string> checks=new List<string>(),errors=new List<string>(),shots=new List<string>();
    static IEnumerator routine;static bool finished,capturing;static double started,next;
    static string Output=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../outputs/说明7_引导与演出"));
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Flags).GetValue(o);
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,Flags).SetValue(o,v);
    static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,Flags).Invoke(o,a);
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(label);}
    static Doc7QuickChecks(){if(SessionState.GetBool(Key,false))Install();}
    public static void RunBatch()
    {
        if(!Application.productName.EndsWith("_NarrativeValidation"))throw new Exception("仅允许隔离产品");
        SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/PlayerGame.unity");Size(1920,1080);Install();EditorApplication.EnterPlaymode();
    }
    static void Install(){started=EditorApplication.timeSinceStartup;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
    static void Log(string t,string s,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(t);}
    static void Tick()
    {
        GuideHelpOverlay.SuppressAutomatic=true;
        if(finished||!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.timeSinceStartup<next)return;
        next=EditorApplication.timeSinceStartup+.04;EditorApplication.QueuePlayerLoopUpdate();
        try {
            if(EditorApplication.timeSinceStartup-started>150)throw new Exception("说明7快验超时");
            if(errors.Count>0)throw new Exception(errors[0]);
            if(capturing||!SystemManager.Instance.IsInitialized||!Field<bool>(SystemManager.Instance.Narrative,"_booted"))return;
            if(routine==null)routine=Scenario();if(!routine.MoveNext())Finish(true,"");
        }catch(Exception ex){Finish(false,ex.ToString());}
    }
    static void Finish(bool pass,string failure)
    {
        if(finished)return;finished=true;Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output,"说明7自动检查.json"),JsonConvert.SerializeObject(new{passed=pass,failure,checks,errors,screenshots=shots,method="隔离产品构造状态与程序化交互；原生鼠标记录另列。"},Formatting.Indented));
        SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;EditorApplication.isPlaying=false;EditorApplication.delayCall+=()=>EditorApplication.Exit(pass?0:1);
    }
    static GuideTopicAnchor Anchor(string topic)=>UnityEngine.Object.FindObjectsOfType<GuideTopicAnchor>(true).First(a=>a.IsVisible&&a.Topic==topic);
    static GuideHelpOverlay Overlay=>UnityEngine.Object.FindObjectOfType<GuideHelpOverlay>();
    static IEnumerator Scenario()
    {
        for(int i=0;i<12;i++)yield return null;
        foreach(string id in new[]{"preparation","shop","storage","altar","decompose","backpack","commission","route","archive","dice","evolution","player_type","thought","player_status","ending","settlement","reception","saves","gallery"}) {
            var e=GuideHelpOverlay.Find(id);Check(e!=null&&e.main.Length==3&&e.details.Length>=3&&e.details.Length<=5,"说明结构 "+id);
            Check(!string.Join(" ",e.main.Concat(e.details)).Contains("特殊委托"),"说明不恢复撤除系统 "+id);
        }
        UIManager.Instance.Close<PlayerBuildPanel>();
        var campaign=SystemManager.Instance.Narrative;var state=Field<NarrativeSystem.SaveData>(campaign,"_save");state.stage="preparation";
        RunStateManager.Instance.run.player=new PlayerBuildState();RunStateManager.Instance.run.player.Create(0);UIManager.Instance.Open<PreparationPanel>();
        for(int i=0;i<5;i++)yield return null;
        foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720)}) {
            Size(size.x,size.y);for(int i=0;i<6;i++)yield return null;
            var anchor=Anchor("preparation");string before=JsonConvert.SerializeObject(RunStateManager.Instance.run);
            Check(anchor.highlights.Length>0&&anchor.highlights[0].name=="Board"&&anchor.highlights[0].rect.width<1200,"整备只高亮实际棋盘 "+size.x);
            GuideHelpOverlay.ShowHelp(anchor);yield return null;
            Check(GuideHelpOverlay.IsShowing&&UIManager.Instance.HasLeftOverlay,"说明阻止底层剧情 "+size.x);
            Shot("机制说明_折叠_"+size.x);yield return null;
            Field<Button>(Overlay,"_detailsButton").onClick.Invoke();yield return null;
            Check(Field<ScrollRect>(Overlay,"_detailsScroll").gameObject.activeSelf,"展开详情 "+size.x);
            Shot("机制说明_展开_"+size.x);yield return null;
            GuideHelpOverlay.HandleEscape();yield return null;yield return null;
            Check(!GuideHelpOverlay.IsShowing&&JsonConvert.SerializeObject(RunStateManager.Instance.run)==before,"返回不改经营状态 "+size.x);
            Call(Overlay,"Open",anchor,false);yield return null;yield return null;
            Shot("整备高亮引导_"+size.x);yield return null;
            Check(GuideHelpOverlay.IsTutorial,"独立引导层 "+size.x);
            GuideHelpOverlay.HandleEscape();yield return null;yield return null;
            Check(GuideHelpOverlay.HasSeen("preparation"),"略过记忆且允许重看 "+size.x);
            var bag=UIManager.Instance.Open<BackpackPanel>();for(int i=0;i<4;i++)yield return null;
            GuideHelpOverlay.ShowHelp(Anchor("backpack"));yield return null;
            Check(GuideHelpOverlay.ActiveTopic=="backpack","帮助匹配来源页面 "+size.x);
            UIManager.Instance.Close(bag);yield return null;Check(!GuideHelpOverlay.IsShowing,"来源关闭不遗留遮罩 "+size.x);
        }
        // 解锁状态只写隔离产品；调用前后验证单局经济与剧情没有变化。
        string runBefore=JsonConvert.SerializeObject(RunStateManager.Instance.run);
        foreach(var e in CGGalleryProgress.Catalog)CGGalleryProgress.RecordShown(e.address);
        while(GuideHelpOverlay.BlocksInput)yield return null;
        CGGalleryPanel.Show();for(int i=0;i<20;i++)yield return null;
        var gallery=UnityEngine.Object.FindObjectOfType<CGGalleryPanel>();
        Check(gallery.VisibleCount==7,"七张既有CG目录");Shot("CG鉴赏_七张已解锁");yield return null;
        gallery.OpenCG(0);for(int i=0;i<20;i++)yield return null;
        Check(gallery.IsViewing,"鉴赏放大");Shot("CG鉴赏_原比例查看");yield return null;
        CGGalleryPanel.HandleEscape();yield return null;Check(!gallery.IsViewing&&CGGalleryPanel.IsShowing,"鉴赏ESC只退一层");
        CGGalleryPanel.HandleEscape();yield return null;Check(!CGGalleryPanel.IsShowing,"鉴赏返回来源");
        Check(JsonConvert.SerializeObject(RunStateManager.Instance.run)==runBefore,"鉴赏不改经营档");
        Check(!CGGalleryProgress.RecordShown("Art/Endings/MY")&&!CGGalleryProgress.RecordShown("invalid"),"CG重复与非目录解锁被忽略");
        CGGalleryProgress.Reload();Check(CGGalleryProgress.GetUnlocked().Length==7,"CG独立记录落盘重读");
        var legacy=JsonConvert.DeserializeObject<RunState>(runBefore);
        var ending=JsonConvert.DeserializeObject<NarrativeSystem.SaveData>(JsonConvert.SerializeObject(state));ending.ending="succession:MY";ending.successionFailed=false;
        ending.stage="ending_cg";legacy.narrativeSave=JsonConvert.SerializeObject(ending);Check(CGGalleryProgress.CompletedAddress(legacy)==null,"未完成旧终局不提前解锁");
        ending.stage="ending_complete";legacy.narrativeSave=JsonConvert.SerializeObject(ending);Check(CGGalleryProgress.CompletedAddress(legacy)=="Art/Endings/MY","明确完成旧终局可迁移");
        int starts=0,ends=0;Action<string> started=_=>starts++,ended=_=>ends++;
        PresentationEffects.Started+=started;PresentationEffects.Finished+=ended;
        PresentationEffects.PlayDay(2,1,false);Check(PresentationEffects.IsBusy,"跨日动画锁输入");
        for(int i=0;i<70;i++)yield return null;
        Check(!PresentationEffects.IsBusy&&starts==1&&ends==1,"跨日动画完成释放输入");
        PresentationEffects.PlayDay(2,1,false);for(int i=0;i<5;i++)yield return null;
        Check(starts==1,"同一跨日事件去重");
        var prep=Anchor("preparation");GuideHelpOverlay.ShowHelp(prep);
        PresentationEffects.PlayInterlude("Doc7:test");for(int i=0;i<10;i++)yield return null;
        Check(starts==1&&GuideHelpOverlay.IsShowing,"转场等引导结束，不抢关闭入口");
        GuideHelpOverlay.HandleEscape();for(int i=0;i<70;i++)yield return null;
        Check(starts==2&&ends==2&&!PresentationEffects.IsBusy,"引导关闭后转场继续");
        PresentationEffects.PlayRevenge("Doc7:test");for(int i=0;i<65;i++)yield return null;
        Check(starts==3&&ends==3&&!PresentationEffects.IsBusy,"报复演出完成且输入释放");
        PresentationEffects.Started-=started;PresentationEffects.Finished-=ended;
        var typeGo=new GameObject("Doc7_Typewriter_Probe",typeof(RectTransform),typeof(Text));
        var writer=typeGo.AddComponent<TypewriterTextLegacy>();writer.text=typeGo.GetComponent<Text>();writer.text.font=Resources.Load<Font>("Art/Fonts/FangZhengShuSong");writer.charInterval=.001f;
        const string sample="<color=#FFDDAA>你好，地牢。</color>";
        GuideHelpOverlay.ShowHelp(prep);writer.Play(sample);for(int i=0;i<10;i++)yield return null;
        Check(writer.text.text.Length==0&&writer.IsTyping,"引导期间打字暂停");
        GuideHelpOverlay.HandleEscape();for(int i=0;i<35;i++)yield return null;
        Check(writer.text.text==sample&&!writer.IsTyping,"关闭后原文与富文本标签完整续播");UnityEngine.Object.Destroy(typeGo);

    }
    static void Size(int width,int height)
    {
        var view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));view.Show();
        typeof(NarrativeIntegrationChecks).GetMethod("SetGameViewSize",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{view,width,height});
    }
    static void Shot(string name){capturing=true;UIManager.Instance.StartCoroutine(Capture(name));}
    static IEnumerator Capture(string name)
    {
        yield return new WaitForSecondsRealtime(.15f);Canvas.ForceUpdateCanvases();
        int width=Screen.width,height=Screen.height;var camera=Camera.main;
        var target=new RenderTexture(width,height,24);var previous=camera.targetTexture;var active=RenderTexture.active;
        var canvases=UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas).ToArray();
        var modes=canvases.Select(c=>c.renderMode).ToArray();var cameras=canvases.Select(c=>c.worldCamera).ToArray();
        Texture2D texture=null;
        try
        {
            camera.targetTexture=target;
            foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;}
            Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
            texture=new Texture2D(width,height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();
            Directory.CreateDirectory(Path.Combine(Output,"截图"));string path=Path.Combine(Output,"截图",name+".png");File.WriteAllBytes(path,texture.EncodeToPNG());shots.Add(path);
        }
        finally
        {
            for(int i=0;i<canvases.Length;i++)if(canvases[i]){canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];}
            camera.targetTexture=previous;RenderTexture.active=active;
            if(texture)UnityEngine.Object.Destroy(texture);UnityEngine.Object.Destroy(target);capturing=false;
        }
    }
}
#endif

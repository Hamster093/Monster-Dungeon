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

/// <summary>本轮必要运行快验：只允许隔离产品，不代替共同大验收。</summary>
[InitializeOnLoad]
public static class Round2Checks
{
    const string Key="MonsterDungeon.Round2Checks";const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static IEnumerator routine;static bool ended,capturing;static double start,next;
    static readonly List<string> checks=new List<string>(),shots=new List<string>(),errors=new List<string>();
    static string Out=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../outputs/状态档案与教程文案"));
    static NarrativeSystem Campaign=>SystemManager.Instance.Narrative;
    static RunState Run=>RunStateManager.Instance.run;
    static NarrativeSystem.SaveData Save=>F<NarrativeSystem.SaveData>(Campaign,"_save");
    static T F<T>(object owner,string field)=>(T)owner.GetType().GetField(field,Flags).GetValue(owner);
    static object Call(object owner,string name,params object[] args)=>owner.GetType().GetMethod(name,Flags).Invoke(owner,args);
    static void Check(bool pass,string message){if(!pass)throw new Exception(message);checks.Add(message);}
    static Round2Checks(){if(SessionState.GetBool(Key,false))Install();}
    public static void RunBatch()
    {
        if(!Application.productName.EndsWith("_NarrativeValidation"))throw new Exception("仅隔离产品允许快验");
        Round2UiEditor.Configure();string p=Path.Combine(Application.persistentDataPath,"run_state.json");
        SessionState.SetString(Key+".before",File.Exists(p)?Convert.ToBase64String(File.ReadAllBytes(p)):"<absent>");SaveAndLoadManager.Delete("run_state");
        SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/PlayerGame.unity");Size(1920,1080);Install();EditorApplication.EnterPlaymode();
    }
    static void Install(){start=EditorApplication.timeSinceStartup;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
    static void Log(string message,string stack,LogType kind){if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)errors.Add(message);}
    static void Tick()
    {
        GuideHelpOverlay.SuppressAutomatic=true;
        if(ended||!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.timeSinceStartup<next)return;
        next=EditorApplication.timeSinceStartup+.04;EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if(EditorApplication.timeSinceStartup-start>130)throw new Exception("必要快验超时");
            if(errors.Count>0)throw new Exception(errors[0]);
            if(capturing||SystemManager.Instance==null||!SystemManager.Instance.IsInitialized||!F<bool>(Campaign,"_booted"))return;
            if(routine==null)routine=Scenario();if(!routine.MoveNext())Finish(true,"");
        }
        catch(Exception e){Finish(false,e.ToString());}
    }
    static void Finish(bool success,string failure)
    {
        if(ended)return;ended=true;Directory.CreateDirectory(Out);File.WriteAllText(Path.Combine(Out,"必要运行快验.json"),JsonConvert.SerializeObject(new{passed=success,failure,checks,errors,shots,method="隔离Unity中的构造状态运行、程序化控件与截图；未重跑27天或全部鼠标组合。"},Formatting.Indented));
        SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;EditorApplication.isPlaying=false;
        EditorApplication.delayCall+=()=>{string before=SessionState.GetString(Key+".before","<absent>"),p=Path.Combine(Application.persistentDataPath,"run_state.json");if(before=="<absent>"){if(File.Exists(p))File.Delete(p);}else File.WriteAllBytes(p,Convert.FromBase64String(before));EditorApplication.Exit(success?0:1);};
    }
    static IEnumerator Scenario()
    {
        PresentationEffects.CancelAll();UIManager.Instance.Close<PlayerBuildPanel>();UIManager.Instance.Close<TalkPanel>();
        for(int i=0;i<4;i++)yield return null;
        Run.currentDay=2;Run.currentPhase=1;Run.pendingDice=null;Run.player=new PlayerBuildState();Run.player.Create(0,1);Run.player.thoughtConfirmedPhase=1;
        Save.stage="preparation";Save.frame=null;Save.pendingRoute=null;UIManager.Instance.Open<PreparationPanel>();ShopPanel.OpenMonsterStorage();
        var shop=UIManager.Instance.Get<ShopPanel>();string source=UIManager.Instance.CurrentLeftPanelName;
        foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720)})
        {
            Size(size.x,size.y);for(int i=0;i<4;i++)yield return null;
            var panel=UIManager.Instance.Open<PlayerBuildPanel>();for(int i=0;i<3;i++)yield return null;
            var root=F<GameObject>(panel,"_statusRoot");var rect=(RectTransform)root.transform.Find("CenteredStatus");
            Check(panel.IsModal&&PlayerBuildPanel.IsShowing&&UIManager.Instance.HasLeftOverlay,"居中状态阻挡底层 "+size.x);
            Check(rect.anchoredPosition==Vector2.zero&&!rect.GetComponent<Image>(),"状态内容居中无框 "+size.x);
            Check(shop.IsOpen&&shop.gameObject.activeInHierarchy&&UIManager.Instance.CurrentLeftPanelName==source,"状态页不关闭或覆盖来源仓库 "+size.x);
            Check(F<Text>(panel,"_effect").text==PlayerBuildPanel.Presentation.thoughts[1].effect,"思维效果全文保留 "+size.x);
            var entry=F<Button>(UIManager.Instance.Get<HUDPanel>(),"_playerButton");Check(((RectTransform)entry.transform).anchoredPosition==new Vector2(530,-52),"顶部入口在金币与阶段框之间 "+size.x);
            Shot("自身状态与顶部入口_"+size.x);yield return null;
            panel.OnEscapePressed();yield return null;Check(!PlayerBuildPanel.IsShowing&&shop.IsOpen&&shop.gameObject.activeInHierarchy,"ESC恢复原仓库 "+size.x);
        }
        UIManager.Instance.Close<ShopPanel>();Save.stage="playing";Save.departingVisitor=3;UIManager.Instance.Open<TalkPanel>();
        var archive=UIManager.Instance.Open<CharacterArchivePanel>();
        foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720)})
        {
            Size(size.x,size.y);for(int i=0;i<3;i++)yield return null;
            for(int actor=1;actor<=7;actor++)
            {
                archive.Refresh(actor);var icon=F<Image>(archive,"_iconImage");var frame=archive.transform.Find("Doc2PortraitFrame") as RectTransform;
                Check(icon.sprite&&icon.preserveAspect&&icon.rectTransform.rect.width==96&&((RectTransform)icon.transform.parent).anchoredPosition==frame.anchoredPosition,"头像原比例居中且留白 "+actor+" "+size.x);
                for(int tab=0;tab<4;tab++){archive.SelectTab(tab);Check(F<Text>(archive,"_level").gameObject.activeInHierarchy,"等级常驻四页 "+actor+"/"+tab+" "+size.x);}
            }
            archive.Refresh(3);archive.SelectTab(0);Check(F<Text>(archive,"_name").text=="Clot·Lach·Artos","矮人档案全名 "+size.x);
            Shot("矮人档案_"+size.x);yield return null;
        }
        UIManager.Instance.Close(archive);
        string legacy="宽容+3\n接任差距20→17\n报酬80金币";
        Check(NarrativeSystem.HidePrivateSuccessionProgress(legacy)=="宽容+3\n报酬80金币","旧提示只移除私有差距，保留属性和报酬");
        var data=new SettlementData(80,0,12,92,325);data.notes.Add(legacy);var settlement=UIManager.Instance.Open<DailySettlementPanel>(data);
        Check(!F<Text>(settlement,"_detailsText").text.Contains("接任差距")&&data.notes[0]==legacy,"旧结算显示过滤，不重写旧业务结果");UIManager.Instance.Close(settlement);
        var helpPrefab=Resources.Load<GuideHelpOverlay>("Panel/GuideHelpOverlay");Check(helpPrefab&&GuideHelpOverlay.Find("archive").intro.Length==5,"公共文案与档案五步配置有效");
        foreach(var anchor in Resources.FindObjectsOfTypeAll<GuideTopicAnchor>())if(anchor.help)
        {
            var b=anchor.help;Check(b.transition==UnityEngine.UI.Selectable.Transition.SpriteSwap&&b.GetComponent<Image>().sprite.name=="问号1"&&b.spriteState.highlightedSprite.name=="问号2"&&b.spriteState.selectedSprite.name=="问号2","问号普通悬停聚焦素材 "+anchor.topic);
        }
        var talk=UIManager.Instance.Open<TalkPanel>();talk.SetInstantText(true);talk.SetAutoCompleteAtEnd(false);
        talk.Init(new List<DialogueLine>{new DialogueLine{speakerName="旧来访者",content="上一日最后一句",iconName=Campaign.BasePortrait(1)}});talk.SetAutoCompleteAtEnd(false);talk.MoveNext();
        float until=Time.unscaledTime+5;while(talk.IsPortraitLoading&&Time.unscaledTime<until)yield return null;
        var story=F<NarrativeRuntime>(Campaign,"_runtime").Story;string ink=story.state.ToJson();int history=DialogueHistoryModel.Instance.Lines.Count;
        Call(Campaign,"HandleAction","action:day phase=start number=3");Check(PresentationEffects.IsBusy,"跨天立即锁输入");
        Campaign.PresentPause();Check(talk.IsOpen,"完全黑场前不提前切换旧页面");
        until=Time.unscaledTime+5;bool sawBlack=false;
        while(PresentationEffects.IsBusy)
        {
            if(Time.unscaledTime>until)throw new Exception("跨天转场未释放");
            var effect=UnityEngine.Object.FindObjectOfType<PresentationEffects>();var fade=effect?F<Image>(effect,"fade"):null;
            if(fade&&fade.color.a>=.999f)
            {
                sawBlack=true;Check(!talk.IsOpen&&!F<Image>(talk,"_iconImage").enabled&&F<Text>(talk,"_speakerNameText").text=="","全黑时旧人物与旧对话显示已撤下");
            }
            yield return null;
        }
        Check(sawBlack&&Save.stage=="preparation"&&UIManager.Instance.Get<PreparationPanel>().IsOpen,"全黑后恢复第三日整备");
        Check(story.state.ToJson()==ink&&DialogueHistoryModel.Instance.Lines.Count==history,"转场不额外推进Ink或删除历史模型");
        Shot("跨天后无旧人物_1280");yield return null;
    }
    static void Size(int width,int height){var w=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));w.Show();typeof(NarrativeIntegrationChecks).GetMethod("SetGameViewSize",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{w,width,height});}
    static void Shot(string name){capturing=true;UIManager.Instance.StartCoroutine(Capture(name));}
    static IEnumerator Capture(string name)
    {
        yield return new WaitForSecondsRealtime(.12f);Canvas.ForceUpdateCanvases();int width=Screen.width,height=Screen.height;var camera=Camera.main;
        var target=new RenderTexture(width,height,24);var previous=camera.targetTexture;var active=RenderTexture.active;var canvases=UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas).ToArray();var modes=canvases.Select(c=>c.renderMode).ToArray();var cameras=canvases.Select(c=>c.worldCamera).ToArray();Texture2D texture=null;
        try{camera.targetTexture=target;foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;}Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;texture=new Texture2D(width,height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();Directory.CreateDirectory(Path.Combine(Out,"截图"));string path=Path.Combine(Out,"截图",name+".png");File.WriteAllBytes(path,texture.EncodeToPNG());shots.Add(path);}
        finally{for(int i=0;i<canvases.Length;i++)if(canvases[i]){canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];}camera.targetTexture=previous;RenderTexture.active=active;if(texture)UnityEngine.Object.Destroy(texture);UnityEngine.Object.Destroy(target);capturing=false;}
    }
}
#endif

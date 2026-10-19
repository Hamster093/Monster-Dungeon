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
public static class FinalInteractionChecks
{
    const string Key="MonsterDungeon.FinalInteractionChecks";
    const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    static bool ended,capturing;static double start,next;static Stack<IEnumerator> steps=new Stack<IEnumerator>();
    static List<string> checks=new List<string>(),shots=new List<string>(),errors=new List<string>();
    static string Out=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../outputs/终局报复与搭话修复"));
    static string Fixtures=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../outputs/教程回填与测试存档/测试存档"));
    static NarrativeSystem C=>SystemManager.Instance.Narrative;
    static NarrativeSystem.SaveData S=>F<NarrativeSystem.SaveData>(C,"_save");
    static NarrativeRuntime Story=>F<NarrativeRuntime>(C,"_runtime");
    static RunState Run=>RunStateManager.Instance.run;
    static T F<T>(object o,string field)=>(T)o.GetType().GetField(field,Flags).GetValue(o);
    static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Flags).Invoke(o,args);
    static GuideHelpOverlay Guide=>(GuideHelpOverlay)typeof(GuideHelpOverlay).GetField("current",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add(message);}
    static FinalInteractionChecks(){if(SessionState.GetBool(Key,false))Install();}
    public static void RunBatch()
    {
        if(!Application.productName.EndsWith("_NarrativeValidation"))throw new Exception("仅允许隔离产品运行");
        string path=Path.Combine(Application.persistentDataPath,"run_state.json");SessionState.SetString(Key+".before",File.Exists(path)?Convert.ToBase64String(File.ReadAllBytes(path)):"<absent>");
        File.WriteAllText(path,File.ReadAllText(Path.Combine(Fixtures,"终局测试.json")));
        SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/PlayerGame.unity");Size(1920,1080);Install();EditorApplication.EnterPlaymode();
    }
    static void Install(){start=EditorApplication.timeSinceStartup;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
    static void Log(string text,string stack,LogType kind){if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)errors.Add(text);}
    static void Tick()
    {
        GuideHelpOverlay.SuppressAutomatic=true;
        if(ended||!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.timeSinceStartup<next)return;
        next=EditorApplication.timeSinceStartup+.04;EditorApplication.QueuePlayerLoopUpdate();
        try{if(EditorApplication.timeSinceStartup-start>240)throw new Exception("快验超时，"+C?.EndingStage);if(errors.Count>0)throw new Exception(errors[0]);if(capturing)return;if(steps.Count==0)steps.Push(Scenario());var step=steps.Peek();if(!step.MoveNext()){steps.Pop();if(steps.Count==0)Finish(true,"");}else if(step.Current is IEnumerator nested)steps.Push(nested);}
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
        yield return Boot();var ui=UIManager.Instance;
        yield return SelectEnding(0);
        foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720)})
        {
            Size(size.x,size.y);yield return Wait(.2f);var end=ui.Get<EndingPresentationPanel>();
            var images=F<Image[]>(end,"_portraits");
            for(int i=0;i<7;i++)Check(images[i].preserveAspect&&Mathf.Abs(images[i].rectTransform.rect.width/images[i].rectTransform.rect.height-images[i].sprite.rect.width/images[i].sprite.rect.height)<.001f,"终局人物保持原比例 "+(i+1)+" "+size.x);
            Shot("终局七人比例_"+size.x);yield return Wait(.2f);
        }
        C.PreviewEndingCandidate(1);yield return Wait(.1f);
        var ending=ui.Get<EndingPresentationPanel>();Check(!F<Text>(ending,"_confirmText").text.Contains("%")&&!F<Text>(ending,"_confirmText").text.Contains("成功率"),"终局确认只有文字倾向，无概率");
        Shot("终局倾向确认_1280");yield return Wait(.2f);
        int coins=InventoryModel.Instance.CountId(2),charms=InventoryModel.Instance.CountId(3);
        C.ConfirmEndingCandidate();yield return Wait(.2f);
        Check(Run.pendingDice!=null&&Run.pendingDice.guaranteedSuccess&&Run.pendingDice.Result().Success,"容易接受建立必成演出");
        Check(!Run.pendingDice.charmUsed&&!Run.pendingDice.criticalRules,"必成不启用极值及护符");
        int fixedRoll=Run.pendingDice.roll;C.Save();SceneManager.LoadScene("PlayerGame");yield return null;yield return Boot();
        Check(Run.pendingDice.guaranteedSuccess&&Run.pendingDice.roll==fixedRoll,"必成演出中途读档不重掷");
        yield return FinishDice();
        Check(Run.pendingDice==null&&S.frame.nodes.Any(n=>n.lines.Any(l=>(l.sourceId??"").StartsWith("SUCCESSION_MY_"))),"必成确认进入Myth原终局");
        Check(InventoryModel.Instance.CountId(2)==coins&&InventoryModel.Instance.CountId(3)==charms&&Run.player.talkGrowth.Count==0,"固定演出不消耗道具或成长");
        yield return LoadFixture("终局测试.json");ui=UIManager.Instance;yield return SelectEnding(10);C.PreviewEndingCandidate(1);C.ConfirmEndingCandidate();yield return Wait(.2f);
        Check(Run.pendingDice!=null&&!Run.pendingDice.FixedOutcome,"不感兴趣保留真实D100检定");
        Check(!Run.pendingDice.details.Contains("%"),"终局检定附注不显示百分比");
        yield return LoadFixture("终局测试.json");ui=UIManager.Instance;yield return SelectEnding(30);C.PreviewEndingCandidate(1);C.ConfirmEndingCandidate();yield return Wait(.2f);
        Check(Run.pendingDice.guaranteedFailure&&!Run.pendingDice.Result().Success,"非常厌恶保持必败演出");yield return FinishDice();
        Check(S.successionFailed||S.ending=="swallowed"||S.frame?.nodes.Any(n=>n.lines.Any(l=>(l.sourceId??"").StartsWith("END_SWALLOWED")))==true,"必败接入原吞噬流程");
        // 构造状态验证上限，和下面实际剧情检定分开记录。
        foreach(string attr in new[]{"perception","knowledge","willpower"})
        {
            var p=new PlayerBuildState();p.Create(0);int before=p.Attribute(attr);
            Check(!p.TrainInformation("f","r",attr,false)&&p.Attribute(attr)==before,"失败不成长 "+attr);
            Check(p.TrainInformation("e0","r0",attr,true)&&!p.TrainInformation("e0","r0",attr,true)&&!p.TrainInformation("e1","r0",attr,true),"同事件及同接待属性去重 "+attr);
            for(int i=1;i<12;i++)p.TrainInformation("e"+i,"r"+i,attr,true);
            Check(p.Attribute(attr)==before+8&&p.talkGrowth[attr]==8,"每项新增最多8点 "+attr);
            p=JsonConvert.DeserializeObject<PlayerBuildState>(JsonConvert.SerializeObject(p));Check(!p.TrainInformation("e99","r99",attr,true)&&p.Attribute(attr)==before+8,"读档保留培养上限 "+attr);
        }
        yield return LoadFixture("报复测试.json");ui=UIManager.Instance;ui.Close<TalkPanel>();
        // 创建一项真实信息检定，再用独立测试结果检查最终确认时的成长提交。
        S.checkId="MYD1_TOPICS.0.c-0.28";Story.Set("current_actor","MY");int perception=Run.player.perception;
        try{Call(C,"Check","information","MY",0);}catch(TargetInvocationException e){if(!(e.InnerException is NarrativeRuntime.PendingCheckException))throw;}
        var request=Run.pendingDice;Check(request!=null&&request.trainingEligible&&request.playerAttribute=="perception","新信息检定冻结成长属性");
        request.roll=100;request.confirmed=true;Call(C,"Check","information","MY",0);
        Check(Run.player.perception==perception+1,"信息检定最终成功只加1");
        Run.pendingDice=request;Call(C,"Check","information","MY",0);Check(Run.player.perception==perception+1,"重复提交同一检定不成长");
        C.Save();SceneManager.LoadScene("PlayerGame");yield return null;yield return Boot();Check(Run.player.perception==perception+1,"实际存读档保留已增加属性");
        // 正式报复流程：礼盒轻度→重度，均不检定、不扣赔偿。
        yield return LoadFixture("报复测试.json");ui=UIManager.Instance;ui.Close<TalkPanel>();yield return DriveTo(o=>(o.choiceId??"").StartsWith("REVENGE_OPTIONS."));
        var light=S.frame.nodes.SelectMany(n=>n.options).First(o=>o.requiredItemId==5);
        Check(light.displayText.Contains("（需要：致歉礼盒）")&&!light.displayText.Contains("检定"),"礼盒显示需求而非检定");
        Check(S.frame.nodes.SelectMany(n=>n.lines).All(l=>l.icon==C.BasePortrait(3).Replace("IMG_7602","IMG_3306")),"轻度报复台词和旁白全程血脸");
        C.Choose(light);Check(!C.Choose(light),"同一礼盒选项重复提交被拒绝");yield return DriveTo(o=>(o.choiceId??"").StartsWith("REVENGE_OPTIONS."));
        Check((string)Story.Get("current_actor")=="FE"&&Run.pendingDice==null&&Run.cash==2000&&InventoryModel.Instance.CountId(5)==2,"轻度礼盒扣1且接续重度");
        Check(!C.RevengeWarning.Contains("Ferdia"),"正在处理的报复不显示未来倒计时");
        Check(S.frame.nodes.SelectMany(n=>n.lines).All(l=>l.icon=="Art/CharacterSprites/FE/IMG_7536"),"重度报复台词和旁白全程血脸");
        C.StartReception();yield return Wait(1);Check(F<Image>(ui.Get<TalkPanel>(),"_iconImage").sprite.name=="FE_hurt","实际中央立绘为Ferdia血脸");Shot("报复需求与血脸_1280");yield return Wait(.2f);ui.Close<TalkPanel>();
        var heavy=S.frame.nodes.SelectMany(n=>n.options).First(o=>o.requiredItemId==5);C.Choose(heavy);
        yield return DriveUntil(()=>!Story.HasMember("pending_revenge","FE"));
        Check(Run.pendingDice==null&&Run.cash==2000&&InventoryModel.Instance.CountId(5)==1,"重度礼盒确定平息且无赔偿检定");
        // 同一报复选项，库存变化后置灰，既拦按钮也拦实际提交。
        yield return LoadFixture("报复测试.json");ui=UIManager.Instance;ui.Close<TalkPanel>();yield return DriveTo(o=>(o.choiceId??"").StartsWith("REVENGE_OPTIONS."));
        while(InventoryModel.Instance.ConsumeId(5)){}while(InventoryModel.Instance.ConsumeId(6)){}
        C.StartReception();yield return Wait(1);
        var choices=S.frame.nodes.SelectMany(n=>n.options).ToList();
        Check(choices.Where(o=>o.requiredItemId>0).All(o=>!C.IsOptionAvailable(o)),"无道具时业务禁止提交");
        var giftOption=choices.First(o=>o.requiredItemId==5);Check(!C.Choose(giftOption),"缺货不能直接调用礼盒分支");
        var items=ui.Get<TalkPanel>().GetComponentsInChildren<DialogueOptionItem>();
        Check(items.Where(i=>F<Text>(i,"_text").text.Contains("需要：")).All(i=>!F<Button>(i,"_button").interactable),"无道具选项实际按钮置灰");
        Shot("报复道具不足_1280");yield return Wait(.2f);
        yield return LoadFixture("报复测试.json");ui=UIManager.Instance;ui.Close<TalkPanel>();yield return DriveTo(o=>(o.choiceId??"").StartsWith("REVENGE_OPTIONS."));
        C.Choose(S.frame.nodes.SelectMany(n=>n.options).First(o=>o.requiredItemId==6));yield return DriveTo(o=>(o.choiceId??"").StartsWith("REVENGE_TRAP_CONFIRM."));
        Check(InventoryModel.Instance.CountId(6)==3,"卷轴进入确认不提前扣除");
        C.Choose(S.frame.nodes.SelectMany(n=>n.options).First(o=>o.choiceId=="REVENGE_TRAP_CONFIRM.0.9"));yield return DriveUntil(()=>Run.adventurers[3].isDead);
        Check(InventoryModel.Instance.CountId(6)==2&&Run.pendingDice==null,"卷轴确定扣1并保留击杀效果，无检定");
        // 倒计时由固定经营日计算，不在读显示时寻找下一次普通接待。
        Story.SetMember("pending_revenge","MY",true);S.revengeDue[1]=Run.currentDay+3;S.activeRevengeActor=-1;
        Check(C.RevengeWarning.Contains("3日后"),"预约回访显示3经营日");Run.currentDay++;Check(C.RevengeWarning.Contains("2日后"),"经营日推进后倒计时即时减少");
        S.activeRevengeActor=1;Check(!C.RevengeWarning.Contains("Myth"),"当前报复隐藏旧预约");
        // 旧重度礼盒已扣物、停在检定的迁移，不再次扣物或执行赔偿。
        yield return LoadFixture("报复测试.json");ui=UIManager.Instance;ui.Close<TalkPanel>();Story.Set("current_actor","FE");Story.Set("revenge_severity","heavy");Story.Story.ChoosePathString("REVENGE_GIFT");S.uiVersion=8;S.stage="dice";S.frame=null;
        InventoryModel.Instance.ConsumeId(5);Run.pendingDice=new D20Service.Presentation{key="old-gift",context="narrative",checkId="REVENGE_GIFT.14",playerKind="revenge_gift",sides=100,roll=4,target=46};C.Save();SceneManager.LoadScene("PlayerGame");yield return null;yield return Boot();ui=UIManager.Instance;ui.Close<TalkPanel>();
        yield return DriveUntil(()=>!Story.HasMember("pending_revenge","FE"));Check(Run.pendingDice==null&&Run.cash==2000&&InventoryModel.Instance.CountId(5)==2,"旧礼盒等待档迁移不重扣、不赔款");
        Check(Run.saveVersion==10&&S.uiVersion==9,"新增状态版本及旧档兼容");
    }
    static IEnumerator SelectEnding(int gap)
    {
        var ui=UIManager.Instance;ui.Close<TalkPanel>();
        for(int n=0;n<20&&S.stage!="ending_selection";n++){C.Next();C.GetFrame();yield return null;}
        Run.adventurers[1].currentSevenStats[SevenDimension.Tolerance]=90-gap;
        C.PresentPause();yield return Wait(.2f);
    }
    static IEnumerator FinishDice()
    {
        if(!DiceCheckPanel.IsShowing)C.PresentPause();
        var panel=UIManager.Instance.Get<DiceCheckPanel>();
        for(int i=0;i<140&&!F<bool>(panel,"_ready");i++)yield return Wait(.03f);
        Check(F<bool>(panel,"_ready"),"骰子顺序动画可完成");
        Check(!F<Button>(panel,"_coin").gameObject.activeSelf||!Run.pendingDice.FixedOutcome,"固定演出隐藏重投入口");
        Shot(Run.pendingDice.guaranteedSuccess?"终局必成骰子_1280":"终局必败骰子_1280");yield return Wait(.2f);panel.Complete();yield return Wait(.2f);
    }
    static IEnumerator DriveUntil(Func<bool> done)
    {
        for(int i=0;i<180;i++)
        {
            if(done())yield break;var frame=C.GetFrame();
            if(Run.pendingDice!=null)throw new Exception("意外检定 "+Run.pendingDice.key);
            if(frame!=null&&frame.nodes.Any(n=>n.options.Count>0))throw new Exception("意外选择 "+Story.Story.state.currentPathString);
            C.Next();yield return null;
        }
        throw new Exception("处理未完成 "+Story.Story.state.currentPathString);
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
    static IEnumerator LoadFixture(string name){CloseGuide();File.WriteAllText(Path.Combine(Application.persistentDataPath,"run_state.json"),File.ReadAllText(Path.Combine(Fixtures,name)));SceneManager.LoadScene("PlayerGame");yield return null;yield return Boot();}
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

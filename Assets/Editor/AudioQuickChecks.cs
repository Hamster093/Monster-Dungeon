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

/// <summary>本轮隔离产品快验，构造状态不写正式存档。声音设备输出另行人工听验。</summary>
[InitializeOnLoad]
public static class AudioQuickChecks
{
    const string Key="MonsterDungeon.AudioChecks";const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static IEnumerator routine;static bool ended,capturing;static double start,next;
    static Stack<IEnumerator> steps=new Stack<IEnumerator>();
    static List<string> checks=new List<string>(),errors=new List<string>(),shots=new List<string>();
    static string Out=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../outputs/音乐接入与界面微调"));
    static T F<T>(object owner,string field)=>(T)owner.GetType().GetField(field,Flags).GetValue(owner);
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add(text);}
    static AudioQuickChecks(){if(SessionState.GetBool(Key,false))Install();}
    public static void RunBatch()
    {
        if(!Application.productName.EndsWith("_NarrativeValidation"))throw new Exception("快验仅允许隔离产品");
        if(SessionState.GetBool(Key,false)){string seed=SessionState.GetString(Key+".before","<absent>"),p=Path.Combine(Application.persistentDataPath,"run_state.json");if(seed!="<absent>")File.WriteAllBytes(p,Convert.FromBase64String(seed));SessionState.SetBool(Key,false);}
        string file=Path.Combine(Application.persistentDataPath,"run_state.json");SessionState.SetString(Key+".before",File.Exists(file)?Convert.ToBase64String(File.ReadAllBytes(file)):"<absent>");
        SaveAndLoadManager.Delete("run_state");SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/StartGame.unity");Size(1920,1080);Install();EditorApplication.EnterPlaymode();
    }
    static void Install(){start=EditorApplication.timeSinceStartup;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
    static void Log(string text,string stack,LogType kind){if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)errors.Add(text);}
    static void Tick()
    {
        GuideHelpOverlay.SuppressAutomatic=true;
        if(ended||!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.timeSinceStartup<next)return;
        next=EditorApplication.timeSinceStartup+.04;EditorApplication.QueuePlayerLoopUpdate();
        try{if(EditorApplication.timeSinceStartup-start>150)throw new Exception("快验超时");if(errors.Count>0)throw new Exception(errors[0]);if(capturing)return;if(routine==null){routine=Scenario();steps.Push(routine);}var step=steps.Peek();if(!step.MoveNext()){steps.Pop();if(steps.Count==0)Finish(true,"");}else if(step.Current is IEnumerator nested)steps.Push(nested);}
        catch(Exception e){Finish(false,e.ToString());}
    }
    static void Finish(bool ok,string failure)
    {
        if(ended)return;ended=true;Directory.CreateDirectory(Out);File.WriteAllText(Path.Combine(Out,"必要快验.json"),JsonConvert.SerializeObject(new{passed=ok,failure,checks,errors,shots,method="独立Unity运行，构造状态与程序化控件调用；未进行完整27天或人工听感验收。"},Formatting.Indented));
        SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;EditorApplication.isPlaying=false;
        EditorApplication.delayCall+=()=>{string before=SessionState.GetString(Key+".before","<absent>"),file=Path.Combine(Application.persistentDataPath,"run_state.json");if(before=="<absent>"){if(File.Exists(file))File.Delete(file);}else File.WriteAllBytes(file,Convert.FromBase64String(before));EditorApplication.Exit(ok?0:1);};
    }
    static IEnumerator Scenario()
    {
        while(!AudioManager.Instance)yield return null;
        var audio=AudioManager.Instance;yield return Wait(.9f);
        Check(audio.MusicId=="BGM_001"&&audio.AmbientId==null,"标题001且不叠环境音");
        foreach(var clip in audio.Mapping.cues.Values.Select(v=>v.clip).Concat(audio.Mapping.contexts.Values.Select(v=>v.music)).Concat(audio.Mapping.contexts.Values.Select(v=>v.ambient)).Where(v=>!string.IsNullOrEmpty(v)).Distinct())Check(Resources.Load<AudioClip>("Audio/"+clip)!=null,"音频引用有效 "+clip);
        Check(!UnityEngine.Object.FindObjectsOfType<GuideTopicAnchor>(true).Any(a=>a.Owner is SettingsPanel),"标题设置无教程/问号绑定");
        var cg=Resources.Load<CGGalleryPanel>("UI/CGGalleryPanel");Check(!cg.GetComponentsInChildren<GuideTopicAnchor>(true).Any(),"CG无教程/问号绑定");
        var source=F<AudioSource>(audio,"active");Check(source.loop,"标题循环");
        // 首页会从手动槽准备续玩档；此处明确从隔离产品的新局开始，避免旧终局遮挡截图。
        SaveAndLoadManager.Delete("run_state");SceneManager.LoadScene("PlayerGame");yield return null;
        while(SystemManager.Instance==null||!SystemManager.Instance.IsInitialized||!F<bool>(SystemManager.Instance.Narrative,"_booted"))yield return null;
        yield return Wait(.4f);PresentationEffects.CancelAll();var ui=UIManager.Instance;ui.Close<PlayerBuildPanel>();
        var campaign=SystemManager.Instance.Narrative;var save=F<NarrativeSystem.SaveData>(campaign,"_save");var run=RunStateManager.Instance.run;
        run.currentDay=2;run.currentPhase=1;run.player.Create(0,1);run.player.thoughtConfirmedPhase=1;run.pendingDice=null;save.stage="preparation";save.frame=null;ui.Close<TalkPanel>();ui.Open<PreparationPanel>();campaign.RefreshAudioContext();yield return Wait(.9f);
        Check(audio.MusicId=="BGM_002"&&audio.AmbientId=="AMB_001_Loop","整备002与基础环境音");
        var active=F<AudioSource>(audio,"active");audio.SetContext("preparation");yield return null;Check(ReferenceEquals(active,F<AudioSource>(audio,"active")),"同曲不重新启动音源");
        foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720)})
        {
            Size(size.x,size.y);yield return Wait(.2f);var hud=ui.Get<HUDPanel>();var button=F<Button>(hud,"_playerButton");Check(((RectTransform)button.transform).anchoredPosition==new Vector2(530,-84),"自身状态入口下移并保留原尺寸 "+size.x);
            var prep=ui.Get<PreparationPanel>();var anchor=prep.GetComponentInChildren<GuideTopicAnchor>(true);var rect=(RectTransform)anchor.help.transform;
            Check(rect.anchoredPosition.x>0&&rect.anchorMax.x==1,"整备问号在原标题右侧 "+size.x);CheckOnScreen(rect,size.x,size.y);
            Shot("整备与顶部入口_"+size.x);yield return null;
        }
        save.stage="playing";save.audioContext="reception";ui.Open<TalkPanel>();campaign.RefreshAudioContext();yield return Wait(.9f);
        Check(audio.MusicId=="BGM_003"&&audio.AmbientId=="AMB_002_Loop","接待003与安静环境音");
        var talk=ui.Get<TalkPanel>();var help=talk.GetComponentsInChildren<GuideTopicAnchor>(true).First(a=>a.Owner==talk);Check(((RectTransform)help.help.transform).anchoredPosition.y==-44,"对话日志问号下移对齐");Shot("对话日志问号_1280");yield return null;
        var archive=ui.Open<CharacterArchivePanel>();archive.Refresh(3);yield return Wait(.9f);Check(audio.MusicId=="BGM_001"&&audio.AmbientId=="AMB_002_Loop","档案001且保持来源环境音");ui.Close(archive);yield return Wait(.9f);Check(audio.MusicId=="BGM_003","档案关闭恢复接待音乐");
        foreach(var context in new[]{"uneasy","danger","stage","ending"}){audio.SetContext(context);yield return Wait(.9f);var rule=audio.Mapping.contexts[context];Check(audio.MusicId==rule.music&&audio.AmbientId==rule.ambient,"上下文音乐与环境音 "+context);Check(F<AudioSource>(audio,"active").loop==rule.loop,"循环设置 "+context);}
        audio.SetContext("danger");archive=ui.Open<CharacterArchivePanel>();yield return Wait(.9f);Check(audio.MusicId=="BGM_005","危险期间档案不覆盖音乐");ui.Close(archive);
        audio.SetMusicVolume(.31f);audio.SetSfxVolume(.42f);audio.ToggleMusic(false);audio.ToggleSfx(false);Check(F<AudioSource>(audio,"active").mute&&F<AudioSource>(audio,"ambient").mute&&F<AudioSource>(audio,"sfx").mute,"音乐与音效静音控制实际音源");int count=audio.PlayedCueCount;audio.PlayCue("confirm");yield return Wait(.1f);Check(audio.PlayedCueCount==count,"静音不播放提示音");audio.ToggleMusic(true);audio.ToggleSfx(true);Check(!F<AudioSource>(audio,"active").mute&&!F<AudioSource>(audio,"ambient").mute,"取消静音恢复");
        Check(!ReferenceEquals(F<AudioSource>(audio,"active"),F<AudioSource>(audio,"sfx"))&&!ReferenceEquals(F<AudioSource>(audio,"ambient"),F<AudioSource>(audio,"sfx")),"短音效/环境/音乐音源独立");
        count=audio.PlayedCueCount;audio.PlayCue("click");audio.PlayCue("confirm");yield return Wait(.1f);Check(audio.PlayedCueCount==count+1,"点击和确认同帧只响一次");
        count=audio.PlayedCueCount;audio.PlayCue("info","same");audio.PlayCue("info","same");yield return Wait(.1f);audio.PlayCue("info","same");yield return Wait(.1f);Check(audio.PlayedCueCount==count+1,"一次性提示标识去重");
        audio.SetContext("reception");save.audioContext="reception";count=audio.PlayedCueCount;
        var p=new D20Service.Presentation{key="audio_check",context="narrative",sides=100,roll=60,rawRoll=60,target=50,modifier=0,audioPresented=true};
        ui.Open<DiceCheckPanel>(new DiceCheckPanel.OpenData{request=p,completed=r=>{}});yield return WaitDice(ui.Get<DiceCheckPanel>());Check(audio.PlayedCueCount==count,"读档重显检定不重放音效");ui.Close<DiceCheckPanel>();run.pendingDice=null;
        p=new D20Service.Presentation{key="audio_new",context="narrative",sides=100,roll=60,rawRoll=60,target=50,modifier=0};count=audio.PlayedCueCount;
        ui.Open<DiceCheckPanel>(new DiceCheckPanel.OpenData{request=p,completed=r=>{}});yield return WaitDice(ui.Get<DiceCheckPanel>());Check(audio.PlayedCueCount>=count+2&&p.audioPresented,"新检定配合骰点和结果播放 count="+(audio.PlayedCueCount-count)+" stage="+ui.Get<DiceCheckPanel>().RevealStage);Shot("检定音效与原布局_1280");yield return null;ui.Close<DiceCheckPanel>();run.pendingDice=null;
        int before=run.cash;count=audio.PlayedCueCount;RunStateManager.Instance.AddCash(10);yield return Wait(.1f);Check(run.cash==before+10&&audio.LastCue=="coins_in"&&audio.PlayedCueCount==count+1,"真实金币收入一次音效");
        audio.SetContext("ending");yield return Wait(.9f);Check(!F<AudioSource>(audio,"active").loop&&!F<AudioSource>(audio,"ambient").isPlaying,"终局单次音乐与环境音关闭");
    }
    static IEnumerator Wait(float seconds){float until=Time.unscaledTime+seconds;while(Time.unscaledTime<until)yield return null;}
    static IEnumerator WaitDice(DiceCheckPanel dice){float until=Time.unscaledTime+6;while(dice.IsRevealing&&Time.unscaledTime<until)yield return null;yield return null;}
    static void CheckOnScreen(RectTransform rect,int w,int h){Canvas.ForceUpdateCanvases();var corners=new Vector3[4];rect.GetWorldCorners(corners);var canvas=rect.GetComponentInParent<Canvas>().rootCanvas;var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;var a=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);var b=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);Check(a.x>=0&&b.x<=w&&a.y>=0&&b.y<=h,"问号完整位于屏幕内 "+w);}
    static void Size(int w,int h){var view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));view.Show();typeof(NarrativeIntegrationChecks).GetMethod("SetGameViewSize",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{view,w,h});}
    static void Shot(string name){capturing=true;UIManager.Instance.StartCoroutine(Capture(name));}
    static IEnumerator Capture(string name)
    {
        yield return new WaitForSecondsRealtime(.12f);Canvas.ForceUpdateCanvases();int w=Screen.width,h=Screen.height;var camera=Camera.main;var rt=new RenderTexture(w,h,24);var old=camera.targetTexture;var active=RenderTexture.active;var canvases=UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas).ToArray();var modes=canvases.Select(c=>c.renderMode).ToArray();var cameras=canvases.Select(c=>c.worldCamera).ToArray();Texture2D texture=null;
        try{camera.targetTexture=rt;foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;}Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;texture=new Texture2D(w,h,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,w,h),0,0);texture.Apply();Directory.CreateDirectory(Path.Combine(Out,"截图"));string path=Path.Combine(Out,"截图",name+".png");File.WriteAllBytes(path,texture.EncodeToPNG());shots.Add(path);}finally{for(int i=0;i<canvases.Length;i++)if(canvases[i]){canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];}camera.targetTexture=old;RenderTexture.active=active;if(texture)UnityEngine.Object.Destroy(texture);UnityEngine.Object.Destroy(rt);capturing=false;}
    }
}
#endif

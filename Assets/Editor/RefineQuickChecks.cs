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

/// <summary>交互精修快验。仅隔离产品内构造状态，不代替自然游玩和原生指针验收。</summary>
[InitializeOnLoad]
public static class RefineQuickChecks
{
    const string Key="MonsterDungeon.RefineChecks",BackupKey=Key+".Autosave";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static readonly List<string> checks=new List<string>(),errors=new List<string>(),shots=new List<string>();
    static readonly List<object> stages=new List<object>();
    static IEnumerator routine;static bool finished,capturing;static double started,next;
    static string Output=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../outputs/交互与界面精修"));
    static NarrativeSystem Campaign=>SystemManager.Instance.Narrative;
    static RunState Run=>RunStateManager.Instance.run;
    static NarrativeSystem.SaveData State=>Field<NarrativeSystem.SaveData>(Campaign,"_save");
    static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,Flags).GetValue(owner);
    static object Call(object owner,string name,params object[] args)=>owner.GetType().GetMethod(name,Flags).Invoke(owner,args);
    static void Check(bool pass,string label){if(!pass)throw new Exception(label);checks.Add(label);}
    static void Isolated(){if(!Application.productName.EndsWith("_NarrativeValidation",StringComparison.Ordinal))throw new Exception("精修快验只允许隔离验证产品。");}
    static RefineQuickChecks(){if(SessionState.GetBool(Key,false))Install();}
    public static void RunBatch()
    {
        Isolated();checks.Clear();errors.Clear();shots.Clear();stages.Clear();
        string path=Path.Combine(Application.persistentDataPath,"run_state.json");
        SessionState.SetString(BackupKey,File.Exists(path)?Convert.ToBase64String(File.ReadAllBytes(path)):"<absent>");
        SaveAndLoadManager.Delete("run_state");
        SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/PlayerGame.unity");
        Size(1920,1080);Install();EditorApplication.EnterPlaymode();
    }
    static void Install()
    {
        started=EditorApplication.timeSinceStartup;EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
    }
    static void Log(string text,string stack,LogType kind){if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)errors.Add(text);}
    static void Tick()
    {
        GuideHelpOverlay.SuppressAutomatic=true;
        if(finished||!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.timeSinceStartup<next)return;
        next=EditorApplication.timeSinceStartup+.035;EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if(EditorApplication.timeSinceStartup-started>210)throw new Exception("精修快验超时。");
            if(errors.Count>0)throw new Exception(errors[0]);
            if(capturing||SystemManager.Instance==null||!SystemManager.Instance.IsInitialized||!Field<bool>(Campaign,"_booted"))return;
            if(routine==null)routine=Scenario();if(!routine.MoveNext())Finish(true,"");
        }
        catch(Exception exception){Finish(false,exception.ToString());}
    }
    static void Finish(bool passed,string failure)
    {
        if(finished)return;finished=true;Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output,"自动检查.json"),JsonConvert.SerializeObject(new{passed,failure,checks,errors,screenshots=shots,diceStages=stages,
            nativeFixtures=Directory.Exists(Path.Combine(Output,"构造状态"))?Directory.GetFiles(Path.Combine(Output,"构造状态"),"*.json"):Array.Empty<string>(),
            method="隔离产品中的构造状态、程序化按钮与RenderTexture截图；不代表原生鼠标交互、自然开局或27天通关。"},Formatting.Indented));
        SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
        EditorApplication.isPlaying=false;
        EditorApplication.delayCall+=()=>{
            string before=SessionState.GetString(BackupKey,"<absent>"),path=Path.Combine(Application.persistentDataPath,"run_state.json");
            if(before=="<absent>"){if(File.Exists(path))File.Delete(path);}else File.WriteAllBytes(path,Convert.FromBase64String(before));
            SessionState.EraseString(BackupKey);EditorApplication.Exit(passed?0:1);
        };
    }
    static IEnumerator Scenario()
    {
        for(int i=0;i<10;i++)yield return null;
        UIManager.Instance.Close<PlayerBuildPanel>();
        State.stage="preparation";State.pendingRoute=null;Run.pendingDice=null;Run.currentDay=2;Run.currentPhase=1;
        Run.player=new PlayerBuildState();Run.player.Create(0,0);Run.player.thoughtConfirmedPhase=1;
        UIManager.Instance.Open<PreparationPanel>();
        AddItem(7,3);AddItem(2,3);
        var view=CreateMonster();
        foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720)})
        {
            Size(size.x,size.y);for(int i=0;i<6;i++)yield return null;
            Check(Screen.width==size.x&&Screen.height==size.y,"真实渲染分辨率 "+size.x+"×"+size.y);
            var segment=StatusChecks(size.x);while(segment.MoveNext())yield return null;
            segment=EvolutionChecks(view,size.x);while(segment.MoveNext())yield return null;
            segment=DiceChecks(size.x);while(segment.MoveNext())yield return null;
            segment=SettlementChecks(size.x);while(segment.MoveNext())yield return null;
        }
        State.stage="preparation";Run.pendingDice=null;Run.pendingEvolutionUid=Run.pendingEvolutionKind=null;
        var finalEvolution=UIManager.Instance.Open<EvolutionPanel>(view.Item.Uid);finalEvolution.Choose(2);
        int count=InventoryModel.Instance.CountId(7);yield return null;
        finalEvolution.Confirm();finalEvolution.Confirm();
        Check(view.Item.Evolved&&view.Item.EvolutionKind==EvolutionPanel.Names[2]&&InventoryModel.Instance.CountId(7)==count-1,"进化确认唯一扣料，重复确认不再次提交");
        yield return null;
        foreach(var label in RefineBoardQuickChecks.Run(view))Check(true,label);
        var extra=ArchiveAndDetailChecks();while(extra.MoveNext())yield return null;
        ToastManager.Instance?.Clear();
        var narrativeChecks=RefineNarrativeChecks.Run(Check,Shot);while(narrativeChecks.MoveNext())yield return null;
        var fixtures=PrepareNativeFixtures();while(fixtures.MoveNext())yield return null;
        Check(Run.pendingDice==null&&string.IsNullOrEmpty(Run.pendingEvolutionUid),"所有构造界面结束后无遗留检定或待进化");
    }
    static IEnumerator PrepareNativeFixtures()
    {
        Isolated();
        UIManager.Instance.Close<DiceCheckPanel>();UIManager.Instance.Close<EvolutionPanel>();
        UIManager.Instance.Close<DescriptionPanel>();UIManager.Instance.Close<RouteSelectionPanel>();
        UIManager.Instance.Close<PlayerBuildPanel>();UIManager.Instance.Close<CharacterArchivePanel>();UIManager.Instance.Close<TalkPanel>();
        Run.currentDay=2;Run.currentPhase=1;Run.pendingDice=null;Run.pendingEvolutionUid=Run.pendingEvolutionKind=null;
        State.stage="preparation";State.frame=null;State.panel=null;State.pendingRoute=null;State.checkId=null;
        State.currentVisitor=State.departingVisitor=-1;State.archiveOpen=false;State.topLeftPanel=nameof(PreparationPanel);
        State.ending="";State.endingCandidate=-1;State.endingExpression=State.endingPresentationPhase=null;
        // 使用真实整备起点；不把只有临时测试回调的骰子/指路面板伪装成可续接的剧情存档。
        Field<NarrativeRuntime>(Campaign,"_runtime").Story.ChoosePathString("DAY02_START");
        if(!Run.player.created)Run.player.Create(0,0);Run.player.thoughtConfirmedPhase=1;
        var board=BackpackBoard.Instance;
        var view=board.Items.FirstOrDefault(v=>v?.Item!=null&&!v.IsGhost&&!v.Item.Evolved);
        if(!view)view=CreateMonster();
        view.SetInstanceState(view.Item.Uid,false,-1);
        const string storageUid="Refine-native-storage";
        if(!MonsterStorageModel.Instance.Items.Any(item=>item.uid==storageUid))MonsterStorageModel.Instance.Add(101,storageUid);
        if(InventoryModel.Instance.CountId(7)<2)AddItem(7,2);
        UIManager.Instance.Open<PreparationPanel>();yield return null;
        SaveNativeFixture("整备棋盘与仓库.json");
        Check(State.board.Count>0&&State.storedInstances.Any(item=>item.uid==storageUid),"原生拖放构造档包含真实棋盘与仓库实例");
        int count=InventoryModel.Instance.CountId(7);
        var evolution=UIManager.Instance.Open<EvolutionPanel>(view.Item.Uid);evolution.Choose(1);yield return null;
        SaveNativeFixture("未确认进化.json");
        Check(!view.Item.Evolved&&Run.pendingEvolutionUid==view.Item.Uid&&Run.pendingEvolutionKind==EvolutionPanel.Names[1]&&InventoryModel.Instance.CountId(7)==count,"原生进化构造档保留待选方向，尚未扣料");
        evolution.OnEscapePressed();yield return null;
    }
    static void SaveNativeFixture(string name)
    {
        Isolated();Campaign.Save();
        string directory=Path.Combine(Output,"构造状态");Directory.CreateDirectory(directory);
        // 复制正式Save入口写出的完整RunState，库存、实例与叙事字段均按真实接口捕获。
        File.Copy(Path.Combine(Application.persistentDataPath,"run_state.json"),Path.Combine(directory,name),true);
    }
    static IEnumerator StatusChecks(int width)
    {
        var copy=PlayerBuildPanel.Presentation;
        Check(copy.types.Length==3&&copy.thoughts.Length==3&&copy.types.All(e=>!string.IsNullOrEmpty(e.description)&&!string.IsNullOrEmpty(e.brief)&&!string.IsNullOrEmpty(e.selected)),"三类型完整原文仍保留 "+width);
        for(int thought=0;thought<3;thought++)
        {
            Run.player=new PlayerBuildState();Run.player.Create(0,thought);Run.player.thoughtConfirmedPhase=1;
            var status=UIManager.Instance.Open<PlayerBuildPanel>();for(int i=0;i<3;i++)yield return null;
            Check(Field<Text>(status,"_effect").text==copy.thoughts[thought].effect,"状态思维效果全文未删减 "+thought+" "+width);
            var scroll=Field<ScrollRect>(status,"_effectScroll");var effect=Field<Text>(status,"_effect");Canvas.ForceUpdateCanvases();
            Check(scroll&&scroll.vertical&&scroll.content==effect.rectTransform&&scroll.content.rect.height+2>=effect.preferredHeight,"状态长文滚动与内容高度 "+thought+" "+width);
            Check(Field<Text>(status,"_stats").text.Contains("55")&&Field<Text>(status,"_stats").text.Contains("35"),"状态真实三属性可见 "+thought+" "+width);
            if(thought==1){Shot("自身状态_"+width);yield return null;}
            UIManager.Instance.Close(status);yield return null;
        }
        var old=Run.player;string stage=State.stage;Run.player=new PlayerBuildState();State.stage="playing";
        var selection=UIManager.Instance.Open<PlayerBuildPanel>(new Action(()=>{}));
        for(int i=0;i<3;i++)
        {
            Field<Button[]>(selection,"_types")[i].onClick.Invoke();yield return null;
            string detail=Field<Text>(selection,"_detail").text;
            Check(detail.Contains(copy.types[i].subtitle)&&detail.Contains(copy.types[i].description)&&detail.Contains(copy.types[i].brief)&&detail.Contains(copy.types[i].selected)&&Field<Text>(selection,"_intro").text==copy.typeIntro,"类型选择原文完整展示 "+i+" "+width);
        }
        var anchor=selection.GetComponentsInChildren<GuideTopicAnchor>(true).First(a=>a.Owner==selection&&a.Topic=="player_type");
        Check(!anchor.CanAutoShow,"类型页不再自动遮暗引导 "+width);
        UIManager.Instance.Close(selection);Run.player=old;State.stage=stage;yield return null;
    }
    static IEnumerator EvolutionChecks(ItemView view,int width)
    {
        State.stage="preparation";Run.pendingDice=null;
        var evolution=UIManager.Instance.Open<EvolutionPanel>(view.Item.Uid);
        int before=InventoryModel.Instance.CountId(7);evolution.Choose(1);
        Check(!view.Item.Evolved&&InventoryModel.Instance.CountId(7)==before&&Run.pendingEvolutionKind==EvolutionPanel.Names[1],"进化只预览不扣料 "+width);
        var icon=Field<Image>(evolution,"_monsterIcon");var directions=Field<Button[]>(evolution,"_directions");
        Check(icon.sprite==view.Def.GetIcon(false)&&icon.preserveAspect&&Mathf.Abs(icon.rectTransform.anchoredPosition.x)<1,"中央显示当前魔物原比例图 "+width);
        Check(directions.Length==7&&directions.All(b=>b.GetComponentInChildren<Text>().text.Contains("5")),"七方向展示现有±5变化 "+width);
        Check(directions.Any(b=>b.GetComponent<RectTransform>().anchoredPosition.x< -300)&&directions.Any(b=>b.GetComponent<RectTransform>().anchoredPosition.x>300),"七方向分布于魔物两侧 "+width);
        for(int i=0;i<3;i++)yield return null;Shot("进化七方向_"+width);yield return null;
        evolution.OnEscapePressed();yield return null;
        Check(!view.Item.Evolved&&InventoryModel.Instance.CountId(7)==before&&Run.pendingEvolutionKind==null,"进化返回不扣料 "+width);
    }
    static IEnumerator DiceChecks(int width)
    {
        int callbacks=0;var p=Request("refine:first:"+width);
        var panel=UIManager.Instance.Open<DiceCheckPanel>(new DiceCheckPanel.OpenData{request=p,completed=_=>callbacks++});
        Check(panel.IsRevealing&&!Field<Button>(panel,"_continue").interactable,"检定开场锁确认 "+width);
        var sequence=new List<int>();int last=-1;float deadline=Time.unscaledTime+12;bool paused=false;
        while(panel.IsRevealing)
        {
            if(Time.unscaledTime>deadline)throw new Exception("骰子演出未完成 "+width);
            int stage=panel.RevealStage;
            if(stage!=last){last=stage;if(stage>0)sequence.Add(stage);stages.Add(new{width,attempt="first",stage,time=Time.unscaledTime});}
            int coins=InventoryModel.Instance.CountId(2);panel.Complete();Field<Button>(panel,"_coin").onClick.Invoke();
            Check(callbacks==0&&!p.confirmed&&ReferenceEquals(Run.pendingDice,p)&&InventoryModel.Instance.CountId(2)==coins,"演出中确认/重投不提交阶段 "+stage+" "+width);
            if(!paused&&stage==2)
            {
                paused=true;var anchor=panel.GetComponentsInChildren<GuideTopicAnchor>(true).First(a=>a.Owner==panel);
                GuideHelpOverlay.ShowHelp(anchor);yield return null;
                int frozen=panel.RevealStage;string value=Field<Text>(panel,"_rollText").text;float until=Time.unscaledTime+.35f;
                while(Time.unscaledTime<until)yield return null;
                Check(panel.RevealStage==frozen&&Field<Text>(panel,"_rollText").text==value&&panel.IsRevealing,"帮助期间冻结演出时钟 "+width);
                GuideHelpOverlay.HandleEscape();yield return null;yield return null;
            }
            yield return null;
        }
        Check(sequence.SequenceEqual(new[]{1,2,3,4,5}),"严格依次门槛→基础→加权→总点→结果 "+width);
        Check(Field<Text>(panel,"_rollText").text==p.roll.ToString()&&Field<Text>(panel,"_modifierText").text.Contains("15")&&Field<Text>(panel,"_totalText").text==(p.roll+p.modifier).ToString(),"基础加权总点与冻结结果一致 "+width);
        Check(Field<Text>(panel,"_outcome").text=="成功"&&Field<Button>(panel,"_continue").interactable,"结果出现后才开放确认 "+width);
        Check(panel.transform.Find("Reveal/Frame").GetComponent<Image>()==null,"检定无厚重框体 "+width);
        Shot("检定分层结果_"+width);yield return null;
        panel.Complete();panel.Complete();yield return null;
        Check(callbacks==1&&Run.pendingDice==null&&p.confirmed,"确认仅执行一次回调并清待骰 "+width);
        callbacks=0;p=Request("refine:reroll:"+width);
        panel=UIManager.Instance.Open<DiceCheckPanel>(new DiceCheckPanel.OpenData{request=p,completed=_=>callbacks++});
        float end=Time.unscaledTime+10;while(panel.IsRevealing){if(Time.unscaledTime>end)throw new Exception("重投前演出超时");yield return null;}
        int before=InventoryModel.Instance.CountId(2);Field<Button>(panel,"_coin").onClick.Invoke();
        Check(p.rerolled&&panel.IsRevealing&&InventoryModel.Instance.CountId(2)==before-1,"重投扣一枚并重新完整演出 "+width);
        int result=p.roll;Field<Button>(panel,"_coin").onClick.Invoke();panel.Complete();
        Check(p.roll==result&&callbacks==0&&InventoryModel.Instance.CountId(2)==before-1&&Run.pendingDice!=null,"重投演出中再次点击不重掷或确认 "+width);
        end=Time.unscaledTime+10;while(panel.IsRevealing){if(Time.unscaledTime>end)throw new Exception("重投演出超时");yield return null;}
        Check(Field<Text>(panel,"_totalText").text==(p.roll+p.modifier).ToString()&&!Field<Button>(panel,"_coin").gameObject.activeSelf,"重投结果显示真实总点且不再开放重掷 "+width);
        panel.Complete();panel.Complete();yield return null;Check(callbacks==1&&Run.pendingDice==null,"重投后仅确认一次 "+width);
    }
    static D20Service.Presentation Request(string key)=>new D20Service.Presentation{key=key,sides=100,title="影响检定",target=60,modifier=15,roll=63,rawRoll=63,criticalRules=false,context="narrative",details="精神力修正 −5\n思维：戚风蛋糕谈判术 ＋10\n回答修正 ＋10"};
    static IEnumerator SettlementChecks(int width)
    {
        var data=new SettlementData(160,0,39,199,325);
        for(int i=0;i<9;i++)data.notes.Add("构造条目 "+(i+1)+" · 普通委托\n类别符合，轻伤生还。等级系数80%，实际收入80金币。\n虔诚−5，坚定＋5；接任倾向更新。");
        int cash=Run.cash;var panel=UIManager.Instance.Open<DailySettlementPanel>(data);
        for(int i=0;i<4;i++)yield return null;
        Check(Field<Text>(panel,"txt_Total").text.Contains("199")&&Field<Text>(panel,"txt_LegacyCommission").text.Contains("160")&&Field<Text>(panel,"txt_MonsterBusiness").text.Contains("39"),"结算金额保持传入真实值 "+width);
        Check(Field<Text>(panel,"_detailsText").text==string.Join("\n\n",data.notes),"结算所有结果原文完整保留 "+width);
        var scroll=Field<ScrollRect>(panel,"_detailsScroll");Check(scroll.vertical&&scroll.content.rect.height>scroll.viewport.rect.height&&scroll.content.rect.height>=Field<Text>(panel,"_detailsText").preferredHeight,"长结算结果可完整滚动 "+width);
        Check(Run.cash==cash,"只展示结算不提前重复收款 "+width);
        Shot("每日结算_"+width);yield return null;scroll.verticalNormalizedPosition=0;yield return null;Shot("每日结算底部_"+width);yield return null;
        UIManager.Instance.Close(panel);yield return null;Check(Run.cash==cash,"构造结算关闭不执行收益 "+width);
    }
    static IEnumerator ArchiveAndDetailChecks()
    {
        State.stage="playing";State.departingVisitor=1;UIManager.Instance.Close<PreparationPanel>();
        var actor=Run.adventurers[1];var records=actor.historyDialogue.ToArray();
        for(int i=0;i<15;i++)actor.historyDialogue.Add("【构造长记录"+i+"】道路尽头有脚印，委托还没决定。这里用于检查档案引导结束后的滚动位置。");
        var talk=UIManager.Instance.Open<TalkPanel>();talk.SetInstantText(true);var archive=UIManager.Instance.Open<CharacterArchivePanel>();archive.Refresh(1);archive.SelectTab(1);
        for(int i=0;i<3;i++)yield return null;
        var scroll=Field<ScrollRect>(archive,"_recordScroll");scroll.verticalNormalizedPosition=.37f;yield return null;
        float position=scroll.verticalNormalizedPosition;string ink=Field<NarrativeRuntime>(Campaign,"_runtime").Story.state.ToJson();
        archive.BeginGuide();
        for(int step=0;step<5;step++)
        {
            var target=archive.GuideTarget(step);yield return null;
            Check(target&&target.gameObject.activeInHierarchy&&archive.CurrentTab==(step<2?0:step-1),"档案五步引导定位正确页签 "+step);
        }
        archive.EndGuide();yield return null;
        Check(archive.CurrentActor==1&&archive.CurrentTab==1&&Mathf.Abs(scroll.verticalNormalizedPosition-position)<.03f,"档案引导恢复原人物页签与滚动");
        Check(Field<NarrativeRuntime>(Campaign,"_runtime").Story.state.ToJson()==ink,"档案引导不推进右侧剧情");
        UIManager.Instance.Close(archive);UIManager.Instance.Close(talk);actor.historyDialogue.Clear();actor.historyDialogue.AddRange(records);yield return null;
        State.stage="preparation";UIManager.Instance.Open<PreparationPanel>();
        var contract=ConfigDatabase.Instance.Contracts.Values.First(c=>!string.IsNullOrEmpty(c.name));var shape=ConfigDatabase.Instance.ShapeDb.Get("101");
        var detail=UIManager.Instance.Open<DescriptionPanel>(new DescriptionPanel.OpenContext{item=ItemData.FromConfig(ConfigDatabase.Instance.Items.Values.First()),icon=shape.GetIcon(false)});
        // 普通浏览不应自动弹出指路说明；随后使用真实魔物实例打开指路上下文。
        var ordinaryAnchor=detail.GetComponentsInChildren<GuideTopicAnchor>(true).FirstOrDefault(a=>a.Owner==detail);
        Check(ordinaryAnchor&&!ordinaryAnchor.CanAutoShow&&!ordinaryAnchor.IsVisible,"普通详情不自动显示指路引导");
        UIManager.Instance.Close(detail);yield return null;
        var view=BackpackBoard.Instance.Items.First(v=>v?.Item!=null);
        detail=UIManager.Instance.Open<DescriptionPanel>(new DescriptionPanel.OpenContext{item=ItemData.FromMonster(view.Item),icon=view.Def.GetIcon(view.Item.Evolved),combatAdventurerId=1,contractId=contract.id,onConfirm=()=>{}});
        yield return null;var anchor=detail.GetComponentsInChildren<GuideTopicAnchor>(true).FirstOrDefault(a=>a.Owner==detail);
        Check(anchor&&anchor.IsVisible&&anchor.CanAutoShow&&anchor.highlights.Length>=3,"指路详情具有可见分区引导锚点");
        UIManager.Instance.Close(detail);yield return null;
    }
    static void AddItem(int id,int quantity)=>InventoryModel.Instance.AddItem(new ItemStack{data=ItemData.FromConfig(ConfigDatabase.Instance.Items[id]),quantity=quantity});
    static ItemView CreateMonster()
    {
        var board=BackpackBoard.Instance;var shape=ConfigDatabase.Instance.ShapeDb.Get("101");Vector2Int origin=default;bool found=false;
        for(int y=0;y<board.Height&&!found;y++)for(int x=0;x<board.Width&&!found;x++)if(board.CanPlace(shape,new Vector2Int(x,y),0)){origin=new Vector2Int(x,y);found=true;}
        Check(found,"构造魔物找到合法落点");var go=new GameObject("Refine-构造魔物",typeof(RectTransform));go.transform.SetParent(board.ItemsRoot,false);
        var view=go.AddComponent<ItemView>();view.Init(shape,board,0,origin);view.SetInstanceState("Refine-evolution-"+Guid.NewGuid().ToString("N"),false);return view;
    }
    static void Size(int width,int height)
    {var window=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));window.Show();typeof(NarrativeIntegrationChecks).GetMethod("SetGameViewSize",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{window,width,height});}
    static void Shot(string name){capturing=true;UIManager.Instance.StartCoroutine(Capture(name));}
    static IEnumerator Capture(string name)
    {
        yield return new WaitForSecondsRealtime(.15f);Canvas.ForceUpdateCanvases();int width=Screen.width,height=Screen.height;var camera=Camera.main;
        var target=new RenderTexture(width,height,24);var previous=camera.targetTexture;var active=RenderTexture.active;
        var canvases=UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas).ToArray();var modes=canvases.Select(c=>c.renderMode).ToArray();var cameras=canvases.Select(c=>c.worldCamera).ToArray();Texture2D texture=null;
        try
        {
            camera.targetTexture=target;foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;}
            Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;texture=new Texture2D(width,height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();
            Directory.CreateDirectory(Path.Combine(Output,"截图"));string path=Path.Combine(Output,"截图",name+".png");File.WriteAllBytes(path,texture.EncodeToPNG());shots.Add(path);
        }
        finally
        {
            for(int i=0;i<canvases.Length;i++)if(canvases[i]){canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];}
            camera.targetTexture=previous;RenderTexture.active=active;if(texture)UnityEngine.Object.Destroy(texture);UnityEngine.Object.Destroy(target);capturing=false;
        }
    }
}
#endif

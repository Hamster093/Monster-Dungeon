#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>说明6快验。全部写档、构造状态及截图只允许隔离验证产品，不能当作自然游玩或原生鼠标验收。</summary>
[InitializeOnLoad]
public static class Doc6QuickChecks
{
    const string SessionKey="MonsterDungeon.Doc6QuickChecks";
    const string RulesKey=SessionKey+".Rules";
    static readonly List<string> checks=new List<string>(),errors=new List<string>(),shots=new List<string>();
    static IEnumerator routine;
    static double started,next;
    static bool capturing,finished,slot4Touched;
    static string slot4Before;
    static NarrativeSystem Campaign=>SystemManager.Instance.Narrative;
    static RunState Run=>RunStateManager.Instance.run;
    static NarrativeSystem.SaveData State=>Field<NarrativeSystem.SaveData>(Campaign,"_save");
    static NarrativeRuntime Runtime=>Field<NarrativeRuntime>(Campaign,"_runtime");
    static string Output=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../outputs/说明6与新增UI打磨"));
    const BindingFlags InstanceFlags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,InstanceFlags).GetValue(owner);
    static object Call(object owner,string name,params object[] args)=>owner.GetType().GetMethod(name,InstanceFlags).Invoke(owner,args);
    static T Copy<T>(T value)=>JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
    static void Check(bool pass,string name){if(!pass)throw new Exception(name);checks.Add(name);}
    static void Isolated(){if(!Application.productName.EndsWith("_NarrativeValidation",StringComparison.Ordinal))throw new Exception("说明6快验只允许隔离产品，禁止读写正式存档。");}

    static Doc6QuickChecks()
    {
        if(SessionState.GetBool(SessionKey,false))
        {
            checks.AddRange(JsonConvert.DeserializeObject<List<string>>(SessionState.GetString(RulesKey,"[]")));
            Install();
        }
    }
    /// <summary>编辑态纯状态规则检查；不打开场景、不进入Play，也不写任何存档。</summary>
    public static void Rules()
    {
        Isolated();
        for(int type=0;type<3;type++)
        {
            var p=new PlayerBuildState();
            Check(p.Create(type)&&p.thought==-1&&p.Attribute(p.PreferredAttribute)==55&&p.perception+p.knowledge+p.willpower==125,"三类型起点与开局不采用思维 "+type);
            Check(!p.Create((type+1)%3),"类型不能重复建立 "+type);
            Check(!p.ThoughtActive(1,1)&&!p.ThoughtActive(2,1),"第一日无思维，第二日须先确认 "+type);
            p.thought=type;p.thoughtConfirmedPhase=1;
            Check(p.ThoughtActive(2,1)&&!p.ThoughtActive(2,2),"思维只在已确认阶段生效 "+type);
            int old=p.proficiency;p.Credit("Doc6:choice");p.Resolve("Doc6:old-result",false);
            Check(p.proficiency==old&&p.ThoughtModifier("information",true)==0,"旧熟练度与失败累计不再提供加成 "+type);
        }
        var player=new PlayerBuildState();player.Create(0,2);player.thoughtConfirmedPhase=1;
        var failure=new PlayerBuildState.ThoughtCheckSnapshot{key="Doc6:fail",kind="information",attribute="perception",scene="D03_V01",actorId=1,day=3,thought=2};
        Check(player.ResolveThoughtCheck(failure,false)&&player.HasSwitchOpportunity(1,3,"D03_V01"),"失败建立本人物、本接待机会");
        string snapshot=JsonConvert.SerializeObject(player);
        Check(!player.ResolveThoughtCheck(failure,false)&&JsonConvert.SerializeObject(player)==snapshot,"重复确认同一失败不叠加或重建机会");
        Check(!player.HasSwitchOpportunity(2,3,"D03_V01")&&!player.HasSwitchOpportunity(1,4,"D03_V01")&&!player.HasSwitchOpportunity(1,3,"D03_V02"),"机会不跨人物、日期或接待");
        var sameSuccess=Copy(failure);sameSuccess.key="Doc6:same-success";sameSuccess.hadSwitchOpportunity=true;
        Check(player.ResolveThoughtCheck(sameSuccess,true)&&!player.switchOpportunity,"下一次仍用原属性，确认后也结束旧机会");
        var failAgain=Copy(failure);failAgain.key="Doc6:fail-again";player.ResolveThoughtCheck(failAgain,false);
        var again=Copy(failure);again.key="Doc6:next";again.hadSwitchOpportunity=true;
        Check(player.ResolveThoughtCheck(again,false)&&player.switchOpportunity&&player.switchOpportunityAttribute=="perception","同属性再失败只留下一个新机会");
        var success=Copy(again);success.key="Doc6:success";success.attribute="willpower";
        Check(player.ResolveThoughtCheck(success,true)&&!player.switchOpportunity,"下次检定确认成功后机会结束");
        player.ResolveThoughtCheck(new PlayerBuildState.ThoughtCheckSnapshot{key="Doc6:other",kind="information",attribute="knowledge",scene="D03_V01",actorId=1,day=3,thought=2},false);
        player.EndReceptionThoughts();Check(!player.switchOpportunity&&player.pendingThoughtCheck==null,"结束接待清除临时机会");
        var roundTrip=Copy(new RunState{player=player,pendingEvolutionUid="Doc6-instance",pendingEvolutionKind="慎重"});
        Check(roundTrip.pendingEvolutionKind=="慎重"&&roundTrip.pendingEvolutionUid=="Doc6-instance","待进化方向与实例可以序列化往返");
    }
    /// <summary>由root先配置本轮预制体，再调用此入口；不会调用任何旧版思维验收。</summary>
    public static void RunBatch()
    {
        Isolated();checks.Clear();errors.Clear();shots.Clear();Rules();
        Directory.CreateDirectory(Output);
        string autosave=Path.Combine(Application.persistentDataPath,"run_state.json");
        if(File.Exists(autosave))File.Copy(autosave,Path.Combine(Output,"验收前隔离自动档.json"),true);
        SaveAndLoadManager.Delete("run_state");
        SessionState.SetString(RulesKey,JsonConvert.SerializeObject(checks));SessionState.SetBool(SessionKey,true);
        EditorSceneManager.OpenScene("Assets/Scenes/PlayerGame.unity");Size(1920,1080);Install();EditorApplication.EnterPlaymode();
    }
    static void Install()
    {
        started=EditorApplication.timeSinceStartup;
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
    }
    static void Log(string message,string stack,LogType kind)
    {if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)errors.Add(message);}
    static void Tick()
    {
        GuideHelpOverlay.SuppressAutomatic=true;
        if(finished||!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.timeSinceStartup<next)return;
        next=EditorApplication.timeSinceStartup+.04;EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if(EditorApplication.timeSinceStartup-started>210)throw new Exception("说明6快验超时");
            if(errors.Count>0)throw new Exception(errors[0]);
            if(capturing||!SystemManager.Instance.IsInitialized||!Field<bool>(Campaign,"_booted"))return;
            if(routine==null)routine=Scenario();
            if(!routine.MoveNext())Finish(true,"");
        }
        catch(Exception exception){Finish(false,exception.ToString());}
    }
    static void Finish(bool passed,string failure)
    {
        if(finished)return;finished=true;
        if(slot4Touched)
        {
            string path=Path.Combine(Application.persistentDataPath,"manual_slot_4.json");
            if(slot4Before==null){if(File.Exists(path))File.Delete(path);}else File.WriteAllText(path,slot4Before);
        }
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output,"自动检查.json"),JsonConvert.SerializeObject(new{passed,failure,checks,errors,screenshots=shots,
            method="隔离产品真实新局初始化；后续为明确构造的状态、规则调用、程序化按钮和渲染截图。不是自然流程通关，也不等于原生鼠标验收。",
            nativeFixtures=new[]{"构造状态/第二日思维选择.json","构造状态/终局三表达.json","构造状态/进化未确认.json"}},Formatting.Indented));
        SessionState.SetBool(SessionKey,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
        EditorApplication.isPlaying=false;EditorApplication.delayCall+=()=>EditorApplication.Exit(passed?0:1);
    }
    static IEnumerator Scenario()
    {
        for(int i=0;i<8;i++)yield return null;
        Check(Run.currentDay==0&&!Run.player.created&&Run.adventurers.Count==7,"真实新局未创建玩家，保留七人");
        Check(Run.saveVersion==9&&State.uiVersion==8,"新局存档版本9、剧情界面版本8");
        PresentationChecks();
        UIManager.Instance.Close<PlayerBuildPanel>();
        RuleContexts();MigrationAndNone();
        State.stage="preparation";State.pendingRoute=null;Run.pendingDice=null;Run.currentDay=2;Run.currentPhase=1;
        Run.player=new PlayerBuildState();Run.player.Create(0,0);Run.player.thoughtConfirmedPhase=1;
        var view=CreateMonster();
        InventoryModel.Instance.AddItem(new ItemStack{data=ItemData.FromConfig(ConfigDatabase.Instance.Items[7]),quantity=2});
        var evolution=UIManager.Instance.Open<EvolutionPanel>(view.Item.Uid);
        int material=InventoryModel.Instance.CountId(7);evolution.Choose(1);
        Check(!view.Item.Evolved&&InventoryModel.Instance.CountId(7)==material&&Run.pendingEvolutionKind=="慎重","方向仅预览，不扣材料或提交进化");
        Check(Field<Text>(evolution,"_cost").text.Contains(material.ToString())&&Field<Button>(evolution,"_confirm").interactable,"成本库存和可确认状态已绑定");
        var saved=Copy(Run);evolution.OnEscapePressed();
        Check(!view.Item.Evolved&&InventoryModel.Instance.CountId(7)==material&&string.IsNullOrEmpty(Run.pendingEvolutionKind),"进化取消不扣料且清除待选方向");
        Run.pendingEvolutionUid=saved.pendingEvolutionUid;Run.pendingEvolutionKind=saved.pendingEvolutionKind;
        evolution=UIManager.Instance.Open<EvolutionPanel>(view.Item.Uid);
        Check(Field<int>(evolution,"_selected")==1&&!view.Item.Evolved&&InventoryModel.Instance.CountId(7)==material,"序列化后的待选方向重开时恢复，不自动扣料");
        evolution.Confirm();evolution.Confirm();
        Check(view.Item.Evolved&&view.Item.EvolutionKind=="慎重"&&InventoryModel.Instance.CountId(7)==material-1,"进化确认只扣一份，重复调用无额外提交");
        var untouched=CreateMonster();untouched.SetInstanceState("Doc6-preview",false);
        SlotChecks();

        foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720)})
        {
            Size(size.x,size.y);for(int i=0;i<5;i++)yield return null;
            Check(Screen.width==size.x&&Screen.height==size.y,"游戏实际渲染分辨率 "+size.x+"×"+size.y);
            yield return null;
            Run.player=new PlayerBuildState();Run.currentDay=0;Run.currentPhase=1;State.stage="playing";
            var player=UIManager.Instance.Open<PlayerBuildPanel>(new Action(()=>{}));
            Field<Button[]>(player,"_types")[0].onClick.Invoke();for(int i=0;i<3;i++)yield return null;
            Check(!Run.player.created,"类型卡预览不自动建立玩家 "+size.x);
            Check(Field<ScrollRect>(player,"_detailScroll")!=null&&Field<Text>(player,"_detailTitle").text.Length>0,"类型详情标题和滚动绑定 "+size.x);
            Shot("类型选择_"+size.x);yield return null;
            var scroll=Field<ScrollRect>(player,"_detailScroll");
            Field<Button[]>(player,"_types")[1].onClick.Invoke();for(int i=0;i<2;i++)yield return null;
            ScrollCheck(player,"类型完整叙述 "+size.x);scroll.verticalNormalizedPosition=0;
            for(int i=0;i<3;i++)yield return null;Shot("类型详情底部_"+size.x);yield return null;
            Field<Button>(player,"_confirm").onClick.Invoke();
            Check(Run.player.created&&Run.player.thought==-1,"类型确认后第一日仍不选择思维 "+size.x);
            UIManager.Instance.Close<PlayerBuildPanel>();

            Run.currentDay=2;State.stage="preparation";Run.player.thoughtConfirmedPhase=-1;ToastManager.Instance?.Clear();
            player=UIManager.Instance.Open<PlayerBuildPanel>(new Action(()=>{}));
            Check(Field<Text>(player,"_choiceTitle").text==PlayerBuildPanel.Presentation.thoughtTitle&&!Field<Button>(player,"_keep").gameObject.activeSelf,"第二日首次使用首次文案并隐藏保持不变 "+size.x);
            Field<Button[]>(player,"_thoughts")[2].onClick.Invoke();for(int i=0;i<3;i++)yield return null;
            Check(Run.player.thought==-1&&Campaign.NeedsThoughtSelection,"思维预览与确认分离 "+size.x);
            ScrollCheck(player,"思维完整叙述 "+size.x);Shot("思维选择_"+size.x);yield return null;
            Field<Button>(player,"_none").onClick.Invoke();
            Check(Run.player.thought==-1&&Run.player.thoughtConfirmedPhase==1&&!Campaign.NeedsThoughtSelection,"不采用是有效确认而非未完成 "+size.x);
            UIManager.Instance.Close<PlayerBuildPanel>();
            ToastManager.Instance?.Clear();player=UIManager.Instance.Open<PlayerBuildPanel>();for(int i=0;i<3;i++)yield return null;
            Check(Field<Text>(player,"_mode").text.Length>0&&!Field<Text>(player,"_scope").text.Contains("熟练度"),"自身状态支持None且不显示旧熟练度成长 "+size.x);
            Shot("自身状态_"+size.x);yield return null;UIManager.Instance.Close<PlayerBuildPanel>();

            evolution=UIManager.Instance.Open<EvolutionPanel>(untouched.Item.Uid);evolution.Choose(4);
            for(int i=0;i<3;i++)yield return null;Shot("进化预览_"+size.x);yield return null;evolution.OnEscapePressed();
            foreach(var item in BackpackBoard.Instance.Items.Where(v=>v?.Item!=null))item.SetInstanceState(item.Item.Uid,item.Item.Evolved,Run.currentDay,item.Item.EvolutionKind);
            var route=UIManager.Instance.Open<RouteSelectionPanel>(new RouteSelectionPanel.OpenData{state=new RouteSelectionPanel.RouteState{adventurerId=1,contractId=99006},cancelled=()=>{}});
            Check(Field<Text>(route,"_instruction").text.Contains("均已出战"),"全休整指路说明无可选实例 "+size.x);
            for(int i=0;i<3;i++)yield return null;Shot("指路全休整_"+size.x);yield return null;UIManager.Instance.Close<RouteSelectionPanel>();
            foreach(var item in BackpackBoard.Instance.Items.Where(v=>v?.Item!=null))item.SetInstanceState(item.Item.Uid,item.Item.Evolved,-1,item.Item.EvolutionKind);
            var db=ConfigDatabase.Instance;db.Contracts[99006]=new ContractConfig{id=99006,name="构造快验",reward=100,targetMonsterId=101,targetMonsterType=MonsterType.Slime};
            var detail=UIManager.Instance.Open<DescriptionPanel>(new DescriptionPanel.OpenContext{item=ItemData.FromMonster(view.Item),icon=view.Def.GetIcon(true),combatAdventurerId=1,contractId=99006,onConfirm=()=>{}});
            for(int i=0;i<3;i++)yield return null;
            Check(Field<Text>(detail,"_routeSummary").text.Contains("生还报酬")&&Field<Text>(detail,"_combatDifficulty").text.Contains("战斗难度")&&Field<Text>(detail,"_contextTitle").text=="魔物详情","指路详情关键信息实际绑定 "+size.x);
            Shot("指路详情_"+size.x);yield return null;UIManager.Instance.Close<DescriptionPanel>();
            yield return null;
            var endings=EndingChecks(size.x);
            while(endings.MoveNext())yield return endings.Current;
            yield return null;
            var settings=UIManager.Instance.Open<SettingsPanel>();SaveSlotsPanel.Show(true);
            var slots=UnityEngine.Object.FindObjectOfType<SaveSlotsPanel>(true);
            for(int i=0;i<3;i++)yield return null;Shot("四槽概要_"+size.x);yield return null;
            Field<Button[]>(slots,"slots")[3].onClick.Invoke();
            for(int i=0;i<3;i++)yield return null;
            Check(Field<GameObject>(slots,"_overwriteRoot").activeInHierarchy,"独立覆盖确认层实际可见 "+size.x);
            Shot("存档覆盖_"+size.x);yield return null;
            slots.OnEscapePressed();yield return null;slots.OnEscapePressed();yield return null;
            UIManager.Instance.Close<SettingsPanel>();yield return null;
            var added=ConversationUiScreens(size.x);
            while(added.MoveNext())yield return added.Current;
        }
        State.stage="preparation";Run.currentDay=2;Run.currentPhase=1;
        Run.pendingDice=null;State.pendingRoute=null;
        untouched.SetInstanceState(untouched.Item.Uid,false,-1);
        var pendingEvolution=UIManager.Instance.Open<EvolutionPanel>(untouched.Item.Uid);pendingEvolution.Choose(4);
        SaveFixture("进化未确认.json");
        pendingEvolution.OnEscapePressed();yield return null;
        PrepareNativeFixture();
    }
    // 所有截图仍为隔离产品中的明确构造状态，只用于新增界面的布局与可读性快验。
    static IEnumerator ConversationUiScreens(int width)
    {
        var actor=Run.adventurers[1];
        var dialogue=actor.historyDialogue.ToList();var info=actor.validInfo.ToList();var investigation=actor.investigationArchive.ToList();
        for(int i=0;i<12;i++)actor.historyDialogue.Add("【构造记录"+(i+1)+"】冒险者：道路尽头是废弃矿井，门口有新的脚印。\n你：先看看委托，再决定要往哪里走。记录应能完整滚动，不挤进基本信息。");
        actor.validInfo.Add("【构造有效信息】原句：门口有新的脚印。\n关键词：矿井、足迹。");
        actor.investigationArchive.Add("【构造调查资料·模糊揭示】她似乎仍在回避过去的经历。尚未揭示的正文不应出现在这里。");
        string oldStage=State.stage;int oldVisitor=State.departingVisitor;
        State.stage="playing";State.departingVisitor=1;UIManager.Instance.Close<PreparationPanel>();
        var talk=UIManager.Instance.Open<TalkPanel>();talk.SetInstantText(true);
        var archive=UIManager.Instance.Open<CharacterArchivePanel>();archive.Refresh(1);
        Check(archive.gameObject.activeInHierarchy,"档案截图建立有效接待上下文 "+width);
        bool hadBackground=State.backgroundLevels.TryGetValue("MY",out var oldBackground);
        foreach(var level in new[]{"", "blur", "full"})
        {
            if(level=="")State.backgroundLevels.Remove("MY");else State.backgroundLevels["MY"]=level;
            archive.Refresh(1);archive.SelectTab(0);
            string visible=Field<Text>(archive,"_succession").text;
            Check(visible.Contains("接任倾向")&&!System.Text.RegularExpressions.Regex.IsMatch(visible,"[0-9≤≥]"),"档案倾向不显示阈值、差距数字 "+level+" "+width);
            Check(visible.Contains("主属性")== (level=="full")&&visible.Contains("副属性")== (level=="full"),"档案仅完整调查后显示主副属性 "+level+" "+width);
            for(int i=0;i<3;i++)yield return null;Shot("档案调查_"+(level==""?"未调查":level=="full"?"两次":"一次")+"_"+width);yield return null;
        }
        if(hadBackground)State.backgroundLevels["MY"]=oldBackground;else State.backgroundLevels.Remove("MY");archive.Refresh(1);
        for(int tab=0;tab<4;tab++)
        {archive.SelectTab(tab);for(int i=0;i<4;i++)yield return null;Shot("档案第"+(tab+1)+"页_"+width);yield return null;}
        UIManager.Instance.Close<CharacterArchivePanel>();UIManager.Instance.Close<TalkPanel>();
        State.stage=oldStage;State.departingVisitor=oldVisitor;UIManager.Instance.Open<PreparationPanel>();
        actor.historyDialogue.Clear();actor.historyDialogue.AddRange(dialogue);actor.validInfo.Clear();actor.validInfo.AddRange(info);actor.investigationArchive.Clear();actor.investigationArchive.AddRange(investigation);
        var history=DialogueHistoryModel.Instance;var oldHistory=history.Lines.ToList();
        history.Clear();var historyPanel=UIManager.Instance.Open<DialogueHistoryPanel>();for(int i=0;i<4;i++)yield return null;
        Shot("历史空状态_"+width);yield return null;UIManager.Instance.Close<DialogueHistoryPanel>();
        for(int i=0;i<8;i++)history.Add(new DialogueLine{speakerName=i%2==0?"Myth":"你",content="【构造长记录"+(i+1)+"】这条记录用于检查正文换行与历史阅读区。委托名称、原句和关键词需要完整保留，长文本应通过滚动查看。"});
        historyPanel=UIManager.Instance.Open<DialogueHistoryPanel>();for(int i=0;i<5;i++)yield return null;
        Shot("历史长记录_"+width);yield return null;UIManager.Instance.Close<DialogueHistoryPanel>();history.Restore(oldHistory);
        var contract=ConfigDatabase.Instance.Contracts.Values.First(c=>c.id!=99006&&!string.IsNullOrEmpty(c.name));
        var commission=UIManager.Instance.Open<CommissionPanel>(contract.id);for(int i=0;i<4;i++)yield return null;
        Shot("普通委托_"+width);yield return null;UIManager.Instance.Close<CommissionPanel>();
        var route=UIManager.Instance.Open<RouteSelectionPanel>(new RouteSelectionPanel.OpenData{state=new RouteSelectionPanel.RouteState{adventurerId=1,contractId=contract.id},cancelled=()=>{}});
        for(int i=0;i<4;i++)yield return null;Shot("指路棋盘_"+width);yield return null;UIManager.Instance.Close<RouteSelectionPanel>();
        var request=new D20Service.Presentation{key="conversation-ui:"+width,sides=100,title="影响检定",target=60,modifier=15,roll=63,rawRoll=63,context="narrative",details="精神力修正 −5\n思维：戚风蛋糕谈判术 ＋10\n回答修正 ＋10\n\n检定成功后，原定为±3的七维变化改为±2；单独标明的其他变化照常。"};
        UIManager.Instance.Open<DiceCheckPanel>(new DiceCheckPanel.OpenData{request=request,completed=_=>{}});
        for(int i=0;i<26;i++)yield return null;Shot("检定结果_"+width);yield return null;UIManager.Instance.Close<DiceCheckPanel>();Run.pendingDice=null;
        var settlement=new SettlementData(160,0,39,199,325);
        settlement.notes.Add("Myth · 普通委托\n目标类别符合，轻伤生还。等级系数80%，实际收入80金币。\n接任倾向：非常厌恶→不感兴趣");
        settlement.notes.Add("Sarah · 普通委托\n目标类别符合，无伤生还。等级系数100%，实际收入80金币。\n虔诚−5，坚定＋5；非常厌恶→不感兴趣");
        settlement.notes.Add("经营收入\n按棋盘当前摆放结算39金币。");
        UIManager.Instance.Open<DailySettlementPanel>(settlement);for(int i=0;i<5;i++)yield return null;
        Shot("每日结算_"+width);yield return null;UIManager.Instance.Close<DailySettlementPanel>();
        var shop=UIManager.Instance.Open<ShopPanel>(new ShopOpenData{category=ShopCategory.Monster});for(int i=0;i<12;i++)yield return null;
        Shot("魔物商店_"+width);yield return null;UIManager.Instance.Close<ShopPanel>();
        var instances=new List<MonsterInstance>{new MonsterInstance(101){evolved=true,usedDay=Run.currentDay,evolutionKind="慎重"},new MonsterInstance(101)};
        shop=UIManager.Instance.Open<ShopPanel>(new ShopOpenData{category=ShopCategory.Monster,freeMode=true,customMonsters=instances});for(int i=0;i<10;i++)yield return null;
        Shot("仓库实例状态_"+width);yield return null;UIManager.Instance.Close<ShopPanel>();
        ToastManager.Instance?.Clear();Check(Run.pendingDice==null,"新增界面展示快验未留下待检定 "+width);
    }
    static IEnumerator EndingChecks(int width)
    {
        var previousPlayer=Copy(Run.player);int day=Run.currentDay,phase=Run.currentPhase;
        var my=Run.adventurers[1];var oldStats=new Dictionary<SevenDimension,int>(my.currentSevenStats);
        bool oldMyDead=my.isDead,oldSarahDead=Run.adventurers[4].isDead;
        string oldInk=Runtime.Story.state.ToJson(),oldSpeaker=Runtime.Speaker,oldStage=State.stage,oldEnding=State.ending,oldExpression=State.endingExpression;
        int oldCandidate=State.endingCandidate;var oldFrame=State.frame;
        Run.currentDay=27;Run.currentPhase=5;Run.player=new PlayerBuildState();Run.player.Create(0);Run.player.thoughtConfirmedPhase=5;
        my.isDead=false;my.currentSevenStats[SevenDimension.Tolerance]=80;my.currentSevenStats[SevenDimension.Resolve]=75;
        Run.pendingDice=null;Run.pendingEvolutionUid=Run.pendingEvolutionKind=null;
        State.stage="playing";State.ending="";State.frame=null;State.pendingRoute=null;
        Call(Campaign,"SyncLists");Runtime.Story.ChoosePathString("END_SELECTION");
        int guard=0;while(Runtime.Story.canContinue&&guard++<80)Runtime.Read();
        Check(Runtime.Story.currentChoices.Count>0,"终局构造状态保留原稿选人选择 "+width);
        State.stage="ending_selection";State.endingCandidate=-1;State.endingExpression=null;
        var ending=UIManager.Instance.Open<EndingPresentationPanel>();
        Field<Button[]>(ending,"_actorButtons")[0].onClick.Invoke();
        Check(State.stage=="ending_confirm"&&Campaign.EndingCandidate==1&&Campaign.AssessSuccession(1).gap==10&&Campaign.EndingExpression==Run.player.PreferredAttribute,"gap10默认选中玩家擅长属性表达 "+width);
        string inventory=InventorySnapshot(),player=JsonConvert.SerializeObject(Run.player),stats=JsonConvert.SerializeObject(my.currentSevenStats),ink=Runtime.Story.state.ToJson();
        var random=UnityEngine.Random.state;
        string[] attributes={"perception","knowledge","willpower"};
        for(int i=0;i<3;i++)
        {
            Field<Button[]>(ending,"_expressionButtons")[i].onClick.Invoke();
            var assessment=Campaign.AssessSuccession(1,attributes[i]);
            Check(Campaign.EndingExpression==attributes[i]&&assessment.attribute==attributes[i]&&assessment.modifier==Run.player.Attribute(attributes[i])-40&&
                assessment.chance==D20Service.SuccessPercent(assessment.target,assessment.modifier),"三表达采用对应属性和同一正式概率 "+width+"/"+attributes[i]);
            Check(Field<Text[]>(ending,"_expressionTexts")[i].text.Contains(assessment.Annotation)&&Field<Text>(ending,"_confirmText").text.Contains("成功率："+assessment.chance+"%"),"表达标注与确认概要使用同一预览 "+width+"/"+attributes[i]);
        }
        Check(Run.pendingDice==null&&inventory==InventorySnapshot()&&player==JsonConvert.SerializeObject(Run.player)&&stats==JsonConvert.SerializeObject(my.currentSevenStats)&&ink==Runtime.Story.state.ToJson()&&random.Equals(UnityEngine.Random.state),"终局三表达反复预览不掷骰、不扣物、不成长或推进原稿 "+width);
        Campaign.Save();var restored=SaveAndLoadManager.Load<RunState>("run_state");var restoredUI=JsonConvert.DeserializeObject<NarrativeSystem.SaveData>(restored.narrativeSave);
        Check(restoredUI.stage=="ending_confirm"&&restoredUI.endingCandidate==1&&restoredUI.endingExpression==Campaign.EndingExpression&&restored.pendingDice==null&&restoredUI.ink==State.ink,
            "终局保存后读取同一人选、表达、停点且无新骰，仅验证序列化上下文 "+width);
        Field<Button[]>(ending,"_expressionButtons")[0].onClick.Invoke();
        for(int i=0;i<4;i++)yield return null;
        Shot("终局表达_"+width);yield return null;
        SaveFixture("终局三表达.json");
        my.currentSevenStats[SevenDimension.Tolerance]=90;ending.OnRefresh();
        Check(Campaign.AssessSuccession(1).gap==0&&Campaign.AssessSuccession(1).chance==100&&!Field<GameObject>(ending,"_expressionRoot").activeSelf&&Run.pendingDice==null,"达标倾向为100%且无需表达检定 "+width);
        my.currentSevenStats[SevenDimension.Tolerance]=69;ending.OnRefresh();
        Check(Campaign.AssessSuccession(1).gap==21&&Campaign.AssessSuccession(1).chance==0&&!Field<GameObject>(ending,"_expressionRoot").activeSelf&&Field<Text>(ending,"_confirmText").text.Contains("0%"),"差距21明示0%且不提供三表达翻盘 "+width);
        for(int i=0;i<3;i++)yield return null;
        Shot("终局必败_"+width);yield return null;
        Campaign.CancelEndingCandidate();Run.adventurers[4].isDead=true;ending.OnRefresh();
        Field<Button[]>(ending,"_actorButtons")[3].onClick.Invoke();
        Check(!Field<Button[]>(ending,"_actorButtons")[3].interactable&&State.stage=="ending_selection"&&State.endingCandidate==-1,"死亡人选禁用且程序化重复点击也不进入确认 "+width);
        UIManager.Instance.Close<EndingPresentationPanel>();yield return null;
        Run.player=previousPlayer;Run.currentDay=day;Run.currentPhase=phase;my.currentSevenStats=oldStats;my.isDead=oldMyDead;Run.adventurers[4].isDead=oldSarahDead;
        Runtime.Story.state.LoadJson(oldInk);Runtime.Speaker=oldSpeaker;State.stage=oldStage;State.ending=oldEnding;State.endingExpression=oldExpression;State.endingCandidate=oldCandidate;State.frame=oldFrame;
        ToastManager.Instance?.Clear();
    }
    static void PresentationChecks()
    {
        var asset=Resources.Load<TextAsset>("Narrative/playerPresentation");
        Check(asset!=null,"说明6文案独立资源存在");
        var copy=PlayerBuildPanel.Presentation;
        Check(new[]{copy.typeTitle,copy.typeIntro,copy.typeConfirmed,copy.thoughtTitle,copy.thoughtIntro,copy.phaseTitle,copy.phaseIntro}.All(s=>!string.IsNullOrWhiteSpace(s)),"开局、首次思维、阶段调整的新文案字段齐全");
        Check(copy.types.Length==3&&copy.thoughts.Length==3,"说明6保留三类型与三思维完整条目");
        for(int i=0;i<3;i++)
        {
            var type=copy.types[i];var thought=copy.thoughts[i];
            Check(new[]{type.name,type.subtitle,type.description,type.brief,type.selected}.All(s=>!string.IsNullOrWhiteSpace(s)),"类型名称、副标题、描述、简述、选择反馈齐全 "+i);
            Check(thought.name==PlayerBuildState.ThoughtNames[i]&&new[]{thought.description,thought.effect,thought.selected}.All(s=>!string.IsNullOrWhiteSpace(s)),"思维名称与规则一致，描述、效果、选择反馈齐全 "+i);
        }
    }
    static void MigrationAndNone()
    {
        var previous=Copy(Run.player);var stage=State.stage;int day=Run.currentDay,phase=Run.currentPhase;
        Run.player=new PlayerBuildState();Run.player.Create(1,2);Run.player.proficiency=37;Run.player.failureStreak=4;
        Run.currentDay=3;Run.currentPhase=1;State.uiVersion=7;Run.saveVersion=8;
        Run.pendingDice=new D20Service.Presentation{sides=100,key="Doc6:legacy",context="narrative",target=65,modifier=7,roll=73,rawRoll=73,playerThought=1,playerLevel=3,playerDay=3,playerKind="influence",playerAttribute="willpower"};
        string dice=JsonConvert.SerializeObject(Run.pendingDice);Call(Campaign,"MigrateDoc6PlayerRules");
        Check(State.uiVersion==8&&Run.saveVersion==9&&Run.player.aptitude==1&&Run.player.knowledge==55&&Run.player.thought==2,"旧档升级保留类型、属性与原思维索引");
        Check(Run.player.thoughtConfirmedPhase==0&&Run.player.failureStreak==0&&JsonConvert.SerializeObject(Run.pendingDice)==dice,"旧已掷检定不重算，新阶段待安全确认");
        var legacyRandom=UnityEngine.Random.state;Run.pendingDice.confirmed=true;State.stage="playing";
        Check((bool)Call(Campaign,"Check","influence","MY",0)&&Run.pendingDice==null&&Run.player.influenceBonus==3&&!Run.player.influenceCakeDefault&&legacyRandom.Equals(UnityEngine.Random.state),"旧已建检定确认沿用已掷结果与原绑定，不重掷或套新思维");
        Run.pendingDice=null;Run.pendingEvolutionUid=null;State.pendingRoute=null;State.stage="preparation";Run.currentDay=2;
        Check(Campaign.NeedsThoughtSelection&&Campaign.ConfirmThought(-1)&&!Campaign.NeedsThoughtSelection,"第二日None可正式确认");
        Call(Campaign,"MigrateDoc6PlayerRules");Check(Run.player.thought==-1&&Run.player.thoughtConfirmedPhase==1,"新版None读档迁移不当作未选");
        Run.currentPhase=2;Check(Campaign.NeedsThoughtSelection&&Campaign.ConfirmThought(Run.player.thought)&&Run.player.thought==-1&&Run.player.thoughtConfirmedPhase==2,"新阶段可继续保持None");
        Run.currentDay=1;State.uiVersion=7;Run.player.thought=1;Call(Campaign,"MigrateDoc6PlayerRules");
        Check(Run.player.thought==-1,"第一日旧档不提前启用思维");
        Run.player=previous;Run.currentDay=day;Run.currentPhase=phase;State.stage=stage;
    }
    static void RuleContexts()
    {
        Run.currentDay=3;Run.currentPhase=1;State.stage="playing";State.pendingRoute=null;Run.pendingEvolutionUid=null;Run.pendingDice=null;
        Runtime.Set("current_actor","MY");Runtime.Set("current_scene","D03_V01");
        var node=JsonConvert.DeserializeObject<NarrativeSystem.PlayerNodeConfig>(Resources.Load<TextAsset>("Narrative/playerChecks").text);
        string influence=node.checks.First(r=>r.kind=="influence"&&r.player&&string.IsNullOrEmpty(r.actor)).checkId;
        string information=node.checks.First(r=>r.kind=="information"&&r.player&&r.attribute=="perception"&&string.IsNullOrEmpty(r.actor)).checkId;
        var actor=Run.adventurers[1];var savedInfo=actor.validInfo.ToList();actor.validInfo.Clear();
        Run.player=new PlayerBuildState();Run.player.Create(0,0);Run.player.thoughtConfirmedPhase=1;
        actor.validInfo.Add("Doc6:one");Check(Campaign.PreviewPlayerCheck(influence).attribute=="willpower","史官一条信息不足以替换属性");
        actor.validInfo.Add("Doc6:two");var p=Campaign.PreviewPlayerCheck(influence);
        Check(p.attribute=="perception"&&p.attributeBonus==15&&p.thoughtBonus==0,"史官两条信息后影响检定采用较高的擅长属性");
        Run.player.willpower=65;Check(Campaign.PreviewPlayerCheck(influence).attribute=="willpower","史官不会把更高的原属性替换成更低值");
        Run.player.willpower=35;Run.player.thought=1;p=Campaign.PreviewPlayerCheck(influence);
        Check(p.attribute=="willpower"&&p.thoughtBonus==10&&Campaign.PreviewPlayerCheck(information).thoughtBonus==0,"蛋糕仅给普通影响检定加10");
        foreach(string kind in new[]{"cooperate","revenge"})
        {var c=(NarrativeSystem.PlayerCheckContext)Call(Campaign,"BuildCheckContext",kind,"MY","willpower",0,true);Check(c.thoughtBonus==0,"蛋糕不扩展到 "+kind);}
        Run.player.thought=2;Run.player.ResolveThoughtCheck(new PlayerBuildState.ThoughtCheckSnapshot{key="Doc6:context-fail",kind="information",attribute="perception",actorId=1,day=3,scene="D03_V01",thought=2},false);
        string before=JsonConvert.SerializeObject(Run.player);var random=UnityEngine.Random.state;string inventory=InventorySnapshot();
        var switched=Campaign.PreviewPlayerCheck(influence);
        Check(Campaign.PreviewPlayerCheck(information).thoughtBonus==0&&switched.thoughtBonus==10,"精神病人同属性无加成，换属性加10");
        Campaign.PreviewPlayerCheck(influence);Campaign.PreviewPlayerCheck(information);
        Check(JsonConvert.SerializeObject(Run.player)==before&&random.Equals(UnityEngine.Random.state)&&inventory==InventorySnapshot(),"预览重复调用不消费机会、不改库存或随机数");
        PendingCheck(influence,"influence",0);
        Check(Run.player.switchOpportunity&&Run.player.pendingThoughtCheck.hadSwitchOpportunity&&Run.pendingDice.modifier==switched.modifier,"掷骰冻结机会上下文，尚未确认不消费");
        var frozen=Copy(Run);
        Check(frozen.player.pendingThoughtCheck.key==frozen.pendingDice.key&&frozen.player.switchOpportunity,"待检定上下文与已掷骰可一起保存");
        Run.pendingDice.roll=100;Run.pendingDice.confirmed=true;State.stage="playing";Check((bool)Call(Campaign,"Check","influence","MY",0)&&!Run.player.switchOpportunity,"正式确认后仅消费一次换属性机会");
        Run.player=new PlayerBuildState();Run.player.Create(0,1);Run.player.thoughtConfirmedPhase=1;
        var preview=Campaign.PreviewPlayerCheck(influence);PendingCheck(influence,"influence",2);
        Check(Run.pendingDice.modifier==preview.modifier+Mathf.Clamp(2*D20Service.Balance.dialogueBonus,-D20Service.Balance.dialogueBonusCap,D20Service.Balance.dialogueBonusCap),"已知预览与正式公式相同，答案隐藏修正在提交后添加");
        Run.pendingDice.roll=100;Run.pendingDice.confirmed=true;State.stage="playing";Call(Campaign,"Check","influence","MY",0);
        actor.currentSevenStats[SevenDimension.Resolve]=50;
        Call(Campaign,"HandleAction","action:attribute actor=MY stat=resolve direction=up amount=config");
        Check(actor.currentSevenStats[SevenDimension.Resolve]==52,"蛋糕成功后的默认+3变+2");
        Call(Campaign,"HandleAction","action:attribute actor=MY stat=resolve direction=down amount=config");
        Check(actor.currentSevenStats[SevenDimension.Resolve]==50,"蛋糕默认-3变-2");
        Call(Campaign,"HandleAction","action:attribute actor=MY stat=resolve direction=up amount=5");
        Check(actor.currentSevenStats[SevenDimension.Resolve]==55,"明确的5点变化不受蛋糕缩减");
        Call(Campaign,"FlushAttributeFeedback");actor.validInfo.Clear();actor.validInfo.AddRange(savedInfo);Run.player.EndReceptionThoughts();ToastManager.Instance?.Clear();
    }
    static void PendingCheck(string checkId,string kind,int bonus)
    {
        State.checkId=checkId;State.displaySerial++;bool paused=false;
        try{Call(Campaign,"Check",kind,"MY",bonus);}catch(TargetInvocationException error){paused=error.InnerException is NarrativeRuntime.PendingCheckException;}
        Check(paused&&Run.pendingDice!=null,"正式检定有且只有一个待确认结果 "+State.displaySerial);
    }
    static string InventorySnapshot()=>JsonConvert.SerializeObject(new{slots=Enumerable.Range(0,InventoryModel.Instance.Capacity).Select(i=>
        {var slot=InventoryModel.Instance.GetSlot(i);return new{id=slot.IsEmpty?0:slot.data.id,count=slot.quantity};}).ToArray(),pending=Run.itemInventory});
    static ItemView CreateMonster()
    {
        var board=BackpackBoard.Instance;var shape=ConfigDatabase.Instance.ShapeDb.Get("101");
        Vector2Int origin=default;bool found=false;
        for(int y=0;y<board.Height&&!found;y++)for(int x=0;x<board.Width&&!found;x++)if(board.CanPlace(shape,new Vector2Int(x,y),0)){origin=new Vector2Int(x,y);found=true;}
        Check(found,"构造状态找到合法魔物落点");
        var go=new GameObject("Doc6-构造魔物",typeof(RectTransform));go.transform.SetParent(board.ItemsRoot,false);
        var view=go.AddComponent<ItemView>();view.Init(shape,board,0,origin);view.SetInstanceState(Guid.NewGuid().ToString(),false);return view;
    }
    static void SlotChecks()
    {
        string path=Path.Combine(Application.persistentDataPath,"manual_slot_4.json");slot4Before=File.Exists(path)?File.ReadAllText(path):null;slot4Touched=true;
        RunStateManager.Instance.SaveSlot(4);string before=File.ReadAllText(path);
        var source=UIManager.Instance.Open<SettingsPanel>();SaveSlotsPanel.Show(true);
        var slots=UnityEngine.Object.FindObjectOfType<SaveSlotsPanel>(true);var button=Field<Button[]>(slots,"slots")[3];
        string summary=Field<Text[]>(slots,"summaries")[3].text;button.onClick.Invoke();button.onClick.Invoke();
        Check(Field<GameObject>(slots,"_overwriteRoot").activeSelf&&Field<Text[]>(slots,"summaries")[3].text==summary&&File.ReadAllText(path)==before,"双击已有槽只打开独立覆盖区，不写档、不替换概要");
        slots.OnEscapePressed();Check(slots.IsOpen&&!Field<GameObject>(slots,"_overwriteRoot").activeSelf&&File.ReadAllText(path)==before,"ESC先取消覆盖确认，保留四槽和旧文件");
        slots.OnEscapePressed();Check(!slots.IsOpen&&source.IsOpen&&source.gameObject.activeInHierarchy,"再次ESC只退出四槽并恢复来源设置");
        UIManager.Instance.Close<SettingsPanel>();ToastManager.Instance?.Clear();
    }
    static void ScrollCheck(PlayerBuildPanel panel,string label)
    {
        var detail=Field<Text>(panel,"_detail");var scroll=Field<ScrollRect>(panel,"_detailScroll");Canvas.ForceUpdateCanvases();
        var copy=PlayerBuildPanel.Presentation;
        Check(copy.types.Length==3&&copy.thoughts.Length==3&&copy.types.All(e=>!string.IsNullOrEmpty(e.description)&&!string.IsNullOrEmpty(e.brief)&&!string.IsNullOrEmpty(e.selected))&&copy.thoughts.All(e=>!string.IsNullOrEmpty(e.description)&&!string.IsNullOrEmpty(e.effect)&&!string.IsNullOrEmpty(e.selected)),label+"完整原稿保留在数据中");
        bool creating=Field<bool>(panel,"_creating");int selected=Field<int>(panel,creating?"_aptitude":"_thought");
        var entry=creating?copy.types[Mathf.Max(0,selected)]:copy.thoughts[Mathf.Max(0,selected)];
        Check(detail.text.Contains(entry.description)&&detail.text.Contains(entry.selected)&&(creating?detail.text.Contains(entry.brief):detail.text.Contains(entry.effect)),label+"用户原描述、效果和选中反馈均完整展示");
        Check(Field<Text>(panel,"_intro").gameObject.activeSelf&&Field<Text>(panel,"_detailTitle").text.Length>0&&scroll.content!=null&&scroll.viewport!=null&&scroll.vertical,label+"开场原文可见，长文保留滚动接口");
        Check(scroll.content.rect.height+2>=detail.preferredHeight,label+"完整正文内容无裁字");
        Check(scroll.verticalScrollbar!=null,label+"保留更长内容的滚动支持");
    }
    static void PrepareNativeFixture()
    {
        UIManager.Instance.Close<PlayerBuildPanel>();UIManager.Instance.Close<DescriptionPanel>();UIManager.Instance.Close<RouteSelectionPanel>();
        Run.currentDay=2;Run.currentPhase=1;Run.player=new PlayerBuildState();Run.player.Create(0);
        Run.pendingDice=null;Run.pendingEvolutionUid=Run.pendingEvolutionKind=null;State.pendingRoute=null;State.frame=null;State.stage="preparation";
        State.ending="";State.endingCandidate=-1;State.endingExpression=null;State.endingPresentationPhase=null;
        State.topLeftPanel=nameof(PlayerBuildPanel);State.archiveOpen=false;
        Runtime.Story.ChoosePathString("DAY02_START");SaveFixture("第二日思维选择.json");
        Check(Campaign.NeedsThoughtSelection,"提供仅隔离产品使用的第二日思维选择构造存档");
    }
    static void SaveFixture(string name)
    {
        Isolated();Campaign.Save();
        Directory.CreateDirectory(Path.Combine(Output,"构造状态"));
        // 直接复制正式序列化入口的完整文件，原生验证包可读取相同结构；不是手拼的界面子状态。
        File.Copy(Path.Combine(Application.persistentDataPath,"run_state.json"),Path.Combine(Output,"构造状态",name),true);
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

#if false // 历史验收保留参考：特殊委托已撤除，新规则使用 Doc4AcceptanceChecks。
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>仅说明3的局部功能验收，不重复跑完整27天。所有写档只允许隔离工程。</summary>
[InitializeOnLoad]
public static class Doc3AcceptanceChecks
{
    const string Key="MonsterDungeon.Doc3QA";
    static IEnumerator scenario;static bool busy;static double start,next;
    static readonly List<string> checks=new(),errors=new(),screenshots=new();
    static Doc3AcceptanceChecks(){if(SessionState.GetBool(Key,false)){EditorApplication.update+=Tick;Application.logMessageReceived+=Log;start=EditorApplication.timeSinceStartup;}}
    static T F<T>(object o,string n)=>ArtAcceptanceChecks.Field<T>(o,n);
    static void Set(object o,string n,object value)=>o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance).SetValue(o,value);
    static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance).Invoke(o,args);
    static void Assert(bool value,string text){if(!value)throw new Exception("说明3："+text);checks.Add(text);}
    static void Click(Component c)=>ArtAcceptanceChecks.Pointer(c);
    static void Log(string m,string t,LogType type){if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)errors.Add(m);}
    public static void RunBatch()
    {
        if(!Application.productName.EndsWith("_NarrativeValidation"))throw new Exception("只允许隔离验证工程");
        SaveAndLoadManager.Delete("run_state");SessionState.SetBool(Key,true);
        EditorSceneManager.OpenScene("Assets/Scenes/PlayerGame.unity");var view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));view.Show();
        typeof(NarrativeIntegrationChecks).GetMethod("SetGameViewSize",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{view,1920,1080});
        EditorApplication.update+=Tick;Application.logMessageReceived+=Log;start=EditorApplication.timeSinceStartup;EditorApplication.EnterPlaymode();
    }
    public static void OpenInteractive()
    {
        if(!Application.productName.EndsWith("_NarrativeValidation"))throw new Exception("只允许隔离验证工程");
        SaveAndLoadManager.ActivateSlot(2);EditorSceneManager.OpenScene("Assets/Scenes/PlayerGame.unity");
        var view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));view.Show();
        typeof(NarrativeIntegrationChecks).GetMethod("SetGameViewSize",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{view,1920,1080});
        view.maximized=true;EditorApplication.EnterPlaymode();
    }
    public static void BuildInteractive()
    {
        if(!Application.productName.EndsWith("_NarrativeValidation"))throw new Exception("只允许隔离验证工程");
        ArtIntegrationEditor.ConfigureDoc3();
        SaveAndLoadManager.ActivateSlot(2);
        // 图形验收专用窗口版，避免编辑器抢走原生拖拽输入；不改正式工程显示设置。
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;PlayerSettings.resizableWindow=true;
        string path=Path.GetFullPath("../Doc3Player/Doc3Player.exe");Directory.CreateDirectory(Path.GetDirectoryName(path));
        var report=BuildPipeline.BuildPlayer(new[]{"Assets/Scenes/PlayerGame.unity","Assets/Scenes/StartGame.unity"},path,BuildTarget.StandaloneWindows64,BuildOptions.Development);
        if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("图形验收构建失败："+report.summary.result);
        File.WriteAllText("Doc3Build.txt",path);
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.timeSinceStartup<next)return;
        next=EditorApplication.timeSinceStartup+.03;EditorApplication.QueuePlayerLoopUpdate();
        try{
            if(EditorApplication.timeSinceStartup-start>240)throw new Exception("局部验收超时");
            if(errors.Count>0)throw new Exception(errors[0]);
            if(!SystemManager.Instance.IsInitialized||!F<bool>(SystemManager.Instance.Narrative,"_booted")||busy)return;
            if(scenario==null)scenario=Scenario();if(!scenario.MoveNext())Finish(true,"");
        }catch(Exception e){Finish(false,e.ToString());}
    }
    static void Finish(bool passed,string failure)
    {
        File.WriteAllText("Doc3Acceptance.json",JsonConvert.SerializeObject(new{passed,failure,checks,screenshots,errors},Formatting.Indented));
        SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;EditorApplication.isPlaying=false;EditorApplication.delayCall+=()=>EditorApplication.Exit(passed?0:1);
    }
    static void Advance()
    {
        if(DiceCheckPanel.IsShowing){var d=UIManager.Instance.Get<DiceCheckPanel>();if(F<bool>(d,"_ready")){RunStateManager.Instance.run.pendingDice.roll=85;d.Complete();}return;}
        var talk=UIManager.Instance.Get<TalkPanel>();if(talk==null||!talk.IsOpen)return;talk.SetInstantText(true);Set(talk,"_clickCooldown",0f);
        var choices=UnityEngine.Object.FindObjectsOfType<DialogueOptionItem>().Where(o=>F<Button>(o,"_button").interactable).OrderBy(o=>o.transform.GetSiblingIndex()).ToArray();
        if(choices.Length>0)Click(F<Button>(choices[0],"_button"));else talk.MoveNext();
    }
    static ItemView Spawn(int id)
    {
        var board=BackpackBoard.Instance;var def=ConfigDatabase.Instance.ShapeDb.Get(id.ToString());Vector2Int? position=null;
        for(int y=0;y<7&&!position.HasValue;y++)for(int x=0;x<7;x++)if(board.CanPlace(def,new Vector2Int(x,y),0)){position=new Vector2Int(x,y);break;}
        if(!position.HasValue)throw new Exception("测试棋盘没有可用位置");var go=new GameObject("Doc3测试魔物",typeof(RectTransform));go.transform.SetParent(board.ItemsRoot,false);var v=go.AddComponent<ItemView>();v.Init(def,board,0,position);return v;
    }
    static void Select(ItemView view)=>Click(view.GetComponentsInChildren<Image>().First(i=>i.gameObject.activeInHierarchy));
    static bool Menu()=>UnityEngine.Object.FindObjectsOfType<DialogueOptionItem>().Any(o=>F<Text>(o,"_text").text.Contains("查看委托")&&F<Button>(o,"_button").interactable);
    static IEnumerator Scenario()
    {
        var sys=SystemManager.Instance;var campaign=sys.Narrative;
        while(!campaign.IsPreparing){Advance();yield return null;}
        var run=sys.RunState.run;run.cash=10000;run.EnsureBusinessDay();
        Assert(run.monsterOffers.Count==4&&run.monsterOffers.Distinct().Count()==4,"每天4只不同魔物");
        Assert(run.monsterOffers.All(id=>ConfigDatabase.Instance.Monsters[id].level==Level.D),"第一阶段只出售D级魔物");
        Assert(run.specialOffers.Count(id=>ConfigDatabase.Instance.Contracts[id].specialKind=="Gather")==2&&run.specialOffers.Count(id=>ConfigDatabase.Instance.Contracts[id].specialKind=="Battle")==3,"每日2采集加3战斗");
        var offers=string.Join(",",run.monsterOffers);run.EnsureBusinessDay();Assert(offers==string.Join(",",run.monsterOffers),"同日重复进入不重抽魔物");
        ShopPanel.OpenItemShop();yield return null;
        var shop=UIManager.Instance.Get<ShopPanel>();int index=Array.FindIndex(shop._itemSlots,s=>shop.GetSlotData(s.SlotIndex)?.id==1);int cash=run.cash;
        for(int i=0;i<4;i++)Call(shop,"TryBuy",index);
        Assert(run.cash==cash-375&&run.shopStock[1]==0,"透镜限购3件，第四次不扣钱");
        UIManager.Instance.Close<ShopPanel>();ShopPanel.OpenItemShop();Assert(run.shopStock[1]==0,"关闭重开不补库存");
        Shot(shop,"01_限购与售罄");yield return null;
        UIManager.Instance.Close<ShopPanel>();ShopPanel.OpenMonsterShop();yield return null;shop=UIManager.Instance.Get<ShopPanel>();int storage=MonsterStorageModel.Instance.Count;
        Call(shop,"TryBuy",0);Assert(MonsterStorageModel.Instance.Count==storage+1&&run.shopStock[run.monsterOffers[0]]==0,"随机魔物购买进入原仓库且售罄");
        sys.RunState.SaveSlot(3);var saved=SaveAndLoadManager.Load<RunState>(SaveAndLoadManager.SlotKey(3));Assert(saved.shopStock[1]==0&&string.Join(",",saved.monsterOffers)==offers,"商店库存与抽取结果随槽位保存");
        UIManager.Instance.Close<ShopPanel>();
        var first=Spawn(101);var second=Spawn(104);string uid=first.Item.Uid;
        RuleChecks(first);
        var placement=WarehousePlacement(first);while(placement.MoveNext())yield return placement.Current;
        var reposition=Reposition(first);while(reposition.MoveNext())yield return reposition.Current;
        Click(F<Button>(UIManager.Instance.Get<PreparationPanel>(),"_abyssButton"));yield return null;
        var altar=UIManager.Instance.Get<AbyssalAltarPanel>();Click(F<Button>(altar,"_returnButton"));Shot(first,"08_祭坛与地狱原装饰");yield return null;
        Click(F<Button>(altar,"_newMonsterButton"));yield return null;
        Assert(UIManager.Instance.IsOpen<DecomposePanel>()&&UIManager.Instance.IsOpen<ShopPanel>()&&!altar.IsOpen,"地狱保留时祭坛二级商店不叠压");
        UIManager.Instance.ReturnFromShop();Assert(altar.IsOpen&&UIManager.Instance.IsOpen<DecomposePanel>(),"二级商店返回祭坛并保留地狱");
        UIManager.Instance.Open<BackpackPanel>();Assert(!altar.IsOpen&&UIManager.Instance.IsOpen<DecomposePanel>(),"直接切换一级页面只保留地狱");UIManager.Instance.Close<DecomposePanel>();UIManager.Instance.Close<BackpackPanel>();
        InventoryModel.Instance.AddItem(new ItemStack{data=ItemData.FromConfig(ConfigDatabase.Instance.Items[7]),quantity=2});int residue=InventoryModel.Instance.CountId(7);
        sys.RunState.SaveSlot(2);UIManager.Instance.Open<BackpackPanel>();yield return null;
        var bag=UIManager.Instance.Get<BackpackPanel>();var slot=bag._itemSlots.First(s=>InventoryModel.Instance.GetSlot(s.SlotIndex)?.data?.id==7);
        var drag=DragResidue(slot,first);while(drag.MoveNext())yield return drag.Current;
        Assert(EvolutionPanel.IsShowing,"残质经过真实EventSystem拖放进入进化面板");Shot(first,"02_进化环绕方向");yield return null;
        UIManager.Instance.HandleEscape();Assert(InventoryModel.Instance.CountId(7)==residue&&!first.Item.Evolved,"取消进化不扣残质");
        UIManager.Instance.Close<BackpackPanel>();
        int beforeIncome=BoardIncomeCalculator.ComputeTotal(BackpackBoard.Instance,BackpackBoard.Instance.TileConfig,BackpackBoard.Instance.IncomeConfig);
        UIManager.Instance.Open<EvolutionPanel>(uid);yield return null;var evolution=UIManager.Instance.Get<EvolutionPanel>();Click(F<Button[]>(evolution,"_directions")[6]);evolution.Choose(6);
        Assert(first.Item.Evolved&&first.Item.EvolutionKind=="邪异"&&InventoryModel.Instance.CountId(7)==residue-1,"进化选择只消耗一次并保存方向");
        Assert(BoardIncomeCalculator.ComputeTotal(BackpackBoard.Instance,BackpackBoard.Instance.TileConfig,BackpackBoard.Instance.IncomeConfig)==beforeIncome+25,"进化收益读取进化配置");
        UIManager.Instance.Open<DescriptionPanel>(new DescriptionPanel.OpenContext{item=ItemData.FromMonster(first.Item),icon=first.Def.EvolvedIcon});Shot(first,"03_进化名称等级与效果");yield return null;
        Assert(F<Text>(UIManager.Instance.Get<DescriptionPanel>(),"_itemNameText").text.StartsWith("邪异的")&&F<Text>(UIManager.Instance.Get<DescriptionPanel>(),"_itemDescriptionText").text.Contains("等级：D"),"魔物详情包含等级、名称前缀和影响方向");UIManager.Instance.HandleEscape();
        InventoryModel.Instance.AddItem(new ItemStack{data=ItemData.FromConfig(ConfigDatabase.Instance.Items[3]),quantity=1});InventoryModel.Instance.AddItem(new ItemStack{data=ItemData.FromConfig(ConfigDatabase.Instance.Items[2]),quantity=2});
        int charm=InventoryModel.Instance.CountId(3);int seed=0;for(;seed<1000;seed++){UnityEngine.Random.InitState(seed);if(UnityEngine.Random.Range(1,101)<=5)break;}UnityEngine.Random.InitState(seed);
        var request=D20Service.CreatePresentation("qa:charm","庇护耳坠规则检查",52,100,"qa");
        Assert(request.rawRoll<=5&&request.roll>=6&&request.roll<=95&&request.charmUsed&&InventoryModel.Instance.CountId(3)==charm-1,"D100耳坠将1到5大失败重判为6到95并消耗一次");
        Assert(request.Result().Success&&!request.Result().IsFumble&&!request.Result().IsCritical,"耳坠重判加权后可成功，且不产生自然暴击");
        int callback=0;D20Service.Show(request,r=>callback++);for(int i=0;i<28;i++)yield return null;Shot(first,"04_骰点与耳坠反馈");yield return null;
        var dice=UIManager.Instance.Get<DiceCheckPanel>();int coins=InventoryModel.Instance.CountId(2);Click(F<Button>(dice,"_coin"));F<Button>(dice,"_coin").onClick.Invoke();
        Assert(InventoryModel.Instance.CountId(2)==coins-1&&request.rerolled,"每次检定硬币只允许一次重投");Click(F<Button>(dice,"_continue"));dice.Complete();Assert(callback==1,"重复确认检定只提交一次");
        Shot(first,"05_整备与原分件");yield return null;
        campaign.StartReception();while(!Menu()){Advance();yield return null;}
        var talkOption=UnityEngine.Object.FindObjectsOfType<DialogueOptionItem>().First(o=>F<Text>(o,"_text").text.Contains("搭话"));Click(F<Button>(talkOption,"_button"));
        while(!DiceCheckPanel.IsShowing){Advance();yield return null;}
        if(DiceCheckPanel.IsShowing)
        {
            for(int i=0;i<25;i++)yield return null;string key=run.pendingDice.key;int roll=run.pendingDice.roll;int count=InventoryModel.Instance.CountId(3);
            sys.RunState.SaveSlot(4);sys.Narrative.Save();SceneManager.LoadScene("PlayerGame");for(int i=0;i<15;i++)yield return null;
            sys=SystemManager.Instance;campaign=sys.Narrative;run=sys.RunState.run;
            Assert(DiceCheckPanel.IsShowing&&run.pendingDice.key==key&&run.pendingDice.roll==roll&&InventoryModel.Instance.CountId(3)==count,"剧情检定中读档恢复同一骰点且不重复消耗");
            Shot(UIManager.Instance.Get<DiceCheckPanel>(),"06_读档恢复检定");yield return null;
        }
        while(!Menu()){Advance();yield return null;}
        first=BackpackBoard.Instance.Items.First(v=>v.Item.Uid==uid);second=BackpackBoard.Instance.Items.First(v=>v.Def.Id=="104");
        var option=UnityEngine.Object.FindObjectsOfType<DialogueOptionItem>().First(o=>F<Text>(o,"_text").text.Contains("查看委托"));Click(F<Button>(option,"_button"));
        while(UIManager.Instance.Get<CommissionPanel>()?.IsOpen!=true){Advance();yield return null;}
        Click(F<Button>(UIManager.Instance.Get<CommissionPanel>(),"btn_AcceptCommission"));Select(first);Click(F<Button>(UIManager.Instance.Get<DescriptionPanel>(),"_routeConfirmButton"));
        Assert(first.Item.IsResting&&sys.DailyCommission.NormalCommissions.Last().targetUid==uid,"普通委托提交锁定单只实例");
        int target=run.specialOffers.First(id=>ConfigDatabase.Instance.Contracts[id].specialKind=="Battle");campaign.RequestSpecialRoute(target);yield return null;
        Select(first);Assert(UIManager.Instance.Get<DescriptionPanel>()?.IsOpen!=true,"当日使用过的魔物不可再次指路");Select(second);Click(F<Button>(UIManager.Instance.Get<DescriptionPanel>(),"_routeConfirmButton"));for(int i=0;i<25;i++)yield return null;
        run.pendingDice.roll=100;Click(F<Button>(UIManager.Instance.Get<DiceCheckPanel>(),"_continue"));
        Assert(second.Item.IsResting&&sys.DailyCommission.SpecialCommissions.Count==1&&!campaign.CanOfferSpecial,"特殊委托接受后占用实例，每人每日仅一次尝试");
        // 固定两份任务均为重伤，用以验证“同日取最重”而不是叠加死亡。
        foreach(var e in sys.DailyCommission.NormalCommissions.Concat(sys.DailyCommission.SpecialCommissions)){e.completionChecked=e.combatChecked=true;e.injury=InjuryResult.Severe;}
        int actor=sys.VisitSystem.CurrentVisitorId;run.adventurers[actor].isSeverelyInjured=false;sys.DailyCommission.ResolveAllChecks();
        Assert(run.adventurers[actor].isSeverelyInjured&&!run.adventurers[actor].isDead,"普通和特殊两次重伤取最重，不在同日叠加死亡");
        var settlement=DailyIncomeCalculator.Compute();settlement.notes=sys.DailyCommission.ResultNotes();UIManager.Instance.Open<DailySettlementPanel>(settlement);Shot(UIManager.Instance.Get<DailySettlementPanel>(),"07_结算与实际结果");yield return null;UIManager.Instance.Close<DailySettlementPanel>();
        var stats=new Dictionary<SevenDimension,int>(run.adventurers[actor].currentSevenStats);sys.DailyCommission.ResolveAllChecks();Assert(stats.All(p=>run.adventurers[actor].currentSevenStats[p.Key]==p.Value),"重复结算不重复应用进化或特殊属性");
        sys.RunState.SaveSlot(4);saved=SaveAndLoadManager.Load<RunState>(SaveAndLoadManager.SlotKey(4));var n=JsonConvert.DeserializeObject<NarrativeSystem.SaveData>(saved.narrativeSave);
        Assert(n.board.Any(b=>b.uid==uid&&b.evolved&&b.evolutionKind=="邪异"&&b.usedDay==run.currentDay),"使用日与进化方向一起存档");
        int today=run.currentDay;run.currentDay++;foreach(var v in BackpackBoard.Instance.Items)v.Rebuild();Assert(!first.Item.IsResting&&!second.Item.IsResting,"第二日使用状态恢复");run.currentDay=today;
        // 仅构造本轮相关的结算场景；不遍历27天剧情。读档必须恢复正在等待的同一次检定。
        sys.DailyCommission.Clear();run.adventurers[actor].isSeverelyInjured=false;run.adventurers[actor].health=HealthState.Normal;run.injuryAppliedDay.Remove(actor);
        int normalId=ConfigDatabase.Instance.Contracts.Keys.First(id=>id>1000);
        sys.DailyCommission.AddNormal(new CommissionEntry(normalId,actor){targetMonsterId=101,correctAnswerCount=0});
        sys.DailyCommission.AddSpecial(new CommissionEntry(211,actor){targetMonsterId=104});
        F<NarrativeSystem.SaveData>(campaign,"_save").stage="settlement_checks";
        sys.DailyCommission.ResolveAllChecksAsync(()=>Call(campaign,"FinishDayChecks"));for(int i=0;i<25;i++)yield return null;
        string pending=run.pendingDice.key;int pendingRoll=run.pendingDice.roll;campaign.Save();SceneManager.LoadScene("PlayerGame");for(int i=0;i<15;i++)yield return null;
        sys=SystemManager.Instance;campaign=sys.Narrative;run=sys.RunState.run;
        Assert(DiceCheckPanel.IsShowing&&run.pendingDice.key==pending&&run.pendingDice.roll==pendingRoll,"日末检定中读取恢复原骰点");
        int stages=0;while(DiceCheckPanel.IsShowing)
        {
            for(int i=0;i<25;i++)yield return null;run.pendingDice.roll=stages==0?100:45;Click(F<Button>(UIManager.Instance.Get<DiceCheckPanel>(),"_continue"));stages++;yield return null;
        }
        Assert(stages==3&&UIManager.Instance.IsOpen<DailySettlementPanel>()&&sys.DailyCommission.NormalCommissions[0].completionRate==1.25f,"异步流程恰好呈现报酬和两场战斗，大成功报酬125%");
        Shot(UIManager.Instance.Get<DailySettlementPanel>(),"10_D100异步结算读档后");yield return null;UIManager.Instance.Close<DailySettlementPanel>();
        run.specialAttemptDay.Remove(actor);int gather=run.specialOffers.First(id=>ConfigDatabase.Instance.Contracts[id].specialKind=="Gather");run.specialTaken.Remove(gather);campaign.RequestSafeSpecial(gather);for(int i=0;i<25;i++)yield return null;
        run.pendingDice.roll=100;Click(F<Button>(UIManager.Instance.Get<DiceCheckPanel>(),"_continue"));int trust=run.adventurers[actor].trust;var beforeStats=new Dictionary<SevenDimension,int>(run.adventurers[actor].currentSevenStats);
        sys.DailyCommission.ResolveAllChecks();var accepted=sys.DailyCommission.SpecialCommissions.Last();
        Assert(accepted.contractId==gather&&accepted.completionRate==1&&run.adventurers[actor].trust==trust+4&&ConfigDatabase.Instance.Contracts[gather].statDeltas.All(p=>run.adventurers[actor].currentSevenStats[p.Key]==Mathf.Clamp(beforeStats[p.Key]+p.Value,0,100)),"采集委托无需战斗，完成一次后精确应用正负属性和信任+4");
        int queued=sys.DailyCommission.SpecialCommissions.Count;run.specialAttemptDay.Remove(actor);int rejected=run.specialOffers.First(id=>id!=gather&&ConfigDatabase.Instance.Contracts[id].specialKind=="Gather");campaign.RequestSafeSpecial(rejected);for(int i=0;i<25;i++)yield return null;run.pendingDice.roll=1;Click(F<Button>(UIManager.Instance.Get<DiceCheckPanel>(),"_continue"));
        Assert(sys.DailyCommission.SpecialCommissions.Count==queued&&!campaign.CanOfferSpecial,"拒绝特殊委托不入队，仍消耗当日唯一派发机会");
        var runtime=F<NarrativeRuntime>(campaign,"_runtime");runtime.SetMember("pending_revenge","MY",true);Call(campaign,"HandleAction","action:revenge actor=MY result=scheduled expected_day=4");yield return null;
        Assert(campaign.RevengeWarning.Contains("Myth")&&campaign.RevengeWarning.Contains("日后"),"待处理报复显示人物与预计归来日期");Shot(UIManager.Instance.Get<HUDPanel>(),"11_报复提醒");yield return null;runtime.SetMember("pending_revenge","MY",false);
        Call(campaign,"HandleAction","action:revenge actor=MY result=compensate");Assert(run.cash>=0,"报复赔偿走原经济接口");
        int deathResidue=InventoryModel.Instance.CountId(7);Call(campaign,"HandleAction","action:revenge actor=MY result=target_dead residue_gain=1");Assert(run.adventurers[1].isDead&&InventoryModel.Instance.CountId(7)==deathResidue+1,"陷阱死亡与残质奖励接入，死亡标记持久化");
        Call(sys.VisitSystem,"OnVisitorsReady",new List<int>{1,2});Assert(!sys.VisitSystem.TodayVisitors.Contains(1),"死亡人物从访客队列过滤");
        int stock=run.shopStock[1];run.currentDay++;run.EnsureBusinessDay();Assert(run.tradeDay==run.currentDay&&run.monsterOffers.Count==4&&run.shopStock[1]==stock,"跨日刷新随机池但不刷新阶段限购道具");run.currentPhase=3;run.EnsureBusinessDay();Assert(run.shopStock[1]==4&&run.shopStock[7]==3,"跨阶段按配置补充限购库存");
    }
    static void RuleChecks(ItemView first)
    {
        var faces=Enumerable.Range(1,100).ToArray();
        Assert(faces.Count(r=>D20Service.Evaluate(r,52).IsCritical)==5&&faces.Count(r=>D20Service.Evaluate(r,52).IsFumble)==5,"D100两端各5%极值");
        Assert(faces.Count(r=>D20Service.Evaluate(r,47,0,false).Success)==54,"54%接受意愿精确对应54个骰面");
        Assert(faces.All(r=>!D20Service.Evaluate(r,101,0,false).Success)&&faces.All(r=>D20Service.Evaluate(r,1,0,false).Success),"百分率0与100没有强制极值例外");
        Assert(faces.Count(r=>CombatResolver.Outcome(D20Service.Evaluate(r,36),0)==InjuryResult.Severe)==15&&faces.Count(r=>CombatResolver.Outcome(D20Service.Evaluate(r,36),0)==InjuryResult.Light)==20,"同级战斗15%重伤、20%轻伤、65%无伤");
        Assert(faces.Count(r=>CombatResolver.Outcome(D20Service.Evaluate(r,36,-18),-18)==InjuryResult.Severe)==33,"越一级战斗重伤率33%");
        Assert(D20Service.AnswerModifier(99)==24&&D20Service.IncomeRate(D20Service.Evaluate(100,52))==1.25f&&D20Service.IncomeRate(D20Service.Evaluate(1,52))==.25f,"回答加成封顶与四档报酬统一配置");
        var legacy=new D20Service.Presentation{roll=20,rawRoll=20,target=11,modifier=2,charmUsed=true};legacy.Normalize();int migrated=legacy.roll;legacy.Normalize();
        Assert(legacy.sides==100&&legacy.roll==98&&legacy.target==51&&legacy.modifier==10&&legacy.roll==migrated&&legacy.charmUsed,"旧D20待确认骰点只迁移一次且保留消耗状态");
        Assert((string)Call(SystemManager.Instance.Narrative,"ExpandText","一次D20检定结束后")=="一次D100检定结束后"&&(string)Call(SystemManager.Instance.Narrative,"ExpandText","D20_V01")=="D20_V01","更新可见骰制文案但保留第20天稳定节点ID");
        var randomState=UnityEngine.Random.state;int seed=0;for(;seed<1000;seed++){UnityEngine.Random.InitState(seed);if(UnityEngine.Random.Range(1,101)<=5)break;}
        var resolver=new AdventureResolver(new D20Service());var context=new AdventureContext{adventurerLevel=Level.D,monsterLevel=Level.D,health=HealthState.Normal,isTruthRoute=true};UnityEngine.Random.InitState(seed);var firstFumble=resolver.Resolve(context);context.health=HealthState.Critical;UnityEngine.Random.InitState(seed);var secondFumble=resolver.Resolve(context);UnityEngine.Random.state=randomState;
        Assert(firstFumble==AdventureResult.HeavyRetreat&&secondFumble==AdventureResult.Death,"旧冒险接口同样遵守首次重伤、已有重伤再遭大失败死亡");
        var board=BackpackBoard.Instance;var saved=new Dictionary<BackpackCell,SpecialTileType>();foreach(var cell in board.GetComponentsInChildren<BackpackCell>()){saved[cell]=cell.SpecialType;Set(cell,"_specialType",SpecialTileType.None);}
        var key=typeof(BoardIncomeCalculator).GetMethod("TileGroupKey",BindingFlags.NonPublic|BindingFlags.Static);
        var a=new Vector2Int(0,1);var b=new Vector2Int(1,1);Set(board.GetCell(a),"_specialType",SpecialTileType.HumusPool);Set(board.GetCell(b),"_specialType",SpecialTileType.HumusPool);
        Assert((string)key.Invoke(null,new object[]{a,SpecialTileType.HumusPool,board})==(string)key.Invoke(null,new object[]{b,SpecialTileType.HumusPool,board}),"相邻同类地块归入同一连通组");
        int joined=BoardIncomeCalculator.ComputeTotal(board,board.TileConfig,board.IncomeConfig);Set(board.GetCell(b),"_specialType",SpecialTileType.None);b=new Vector2Int(2,0);Set(board.GetCell(b),"_specialType",SpecialTileType.HumusPool);
        Assert(BoardIncomeCalculator.ComputeTotal(board,board.TileConfig,board.IncomeConfig)==joined+6,"同一魔物邻接两组同类特殊地块额外收入6，同组多格不重复");
        foreach(var pair in saved)Set(pair.Key,"_specialType",pair.Value);
        var extra=new List<ItemView>();for(int i=0;i<6;i++)extra.Add(Spawn(101));Assert(board.Items.Count==8&&!board.CanPlace(first.Def,new Vector2Int(6,6),0),"棋盘最多8个真实实例");
        foreach(var item in extra){board.RemoveItem(item.Item);UnityEngine.Object.Destroy(item.gameObject);}
        Assert(ConfigDatabase.Instance.ShapeDb.Get("112").Shape.Length==5&&ConfigDatabase.Instance.ShapeDb.Get("117").Shape.Length==7,"含空洞构造与龙类占格按源图配置");
    }
    static Vector2 ScreenPosition(Component c){var canvas=c.GetComponentInParent<Canvas>();return RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,((RectTransform)c.transform).TransformPoint(((RectTransform)c.transform).rect.center));}
    static IEnumerator WarehousePlacement(ItemView owner)
    {
        ShopPanel.OpenMonsterStorage();yield return null;var shop=UIManager.Instance.Get<ShopPanel>();var board=BackpackBoard.Instance;
        string uid=shop.GetSlotInstance(0).uid;var def=shop.GetSlotShape(0);Vector2Int? origin=null;
        for(int y=0;y<7&&!origin.HasValue;y++)for(int x=0;x<7;x++)if(board.CanPlace(def,new Vector2Int(x,y),0)){origin=new Vector2Int(x,y);break;}
        Assert(origin.HasValue,"仓库拖拽找到真实空位");
        for(int pass=0;pass<2;pass++)
        {
            var source=shop._itemSlots.First(s=>shop.GetSlotInstance(s.SlotIndex)?.uid==uid);ArtAcceptanceChecks.Hit(source);var input=EventSystem.current.gameObject.GetComponent<ArtPointerInput>()??EventSystem.current.gameObject.AddComponent<ArtPointerInput>();
            var from=ScreenPosition(source);var to=ScreenPosition(board.GetCell(origin.Value));input.Process(from,true,false,true);yield return null;input.Process(Vector2.Lerp(from,to,.5f),false,false,true);yield return null;input.Process(to,false,false,true);yield return null;input.Process(to,false,true,false);yield return null;
            var pending=UnityEngine.Object.FindObjectsOfType<ItemView>().FirstOrDefault(v=>!v.IsGhost&&v.Item?.Uid==uid);
            Assert(pending!=null&&pending.IsPlacing&&board.IsEditing&&!board.Items.Contains(pending)&&!F<Button>(UIManager.Instance.Get<PreparationPanel>(),"_startReceptionButton").interactable,"仓库上盘松手仅预摆，未登记占格且禁止开始接待");
            if(pass==0){board.CancelPlacement();yield return null;Assert(MonsterStorageModel.Instance.Items.Any(m=>m.uid==uid)&&!board.IsEditing,"取消预摆按原UID安全退仓");}
            else{Assert(pending.ConfirmPlacement()&&board.Items.Contains(pending)&&!pending.IsPlacing,"空格提交入口将预摆魔物登记一次");board.RemoveItem(pending.Item);MonsterStorageModel.Instance.Add(int.Parse(pending.Def.Id),pending.Item.Uid,pending.Item.Evolved,pending.Item.UsedDay,pending.Item.EvolutionKind);UnityEngine.Object.Destroy(pending.gameObject);}
        }
        UIManager.Instance.Close<ShopPanel>();
    }
    static IEnumerator Reposition(ItemView item)
    {
        var board=BackpackBoard.Instance;var origin=item.Item.Origin;Vector2Int? destination=null;
        for(int y=2;y<7&&!destination.HasValue;y++)for(int x=0;x<7;x++)if(board.CanPlace(item.Def,new Vector2Int(x,y),0,item.Item)){destination=new Vector2Int(x,y);break;}
        Assert(destination.HasValue,"快速移动找到不同于原位置的有效格");
        var icon=item.GetComponentsInChildren<Image>().First(i=>i.gameObject.activeInHierarchy);ArtAcceptanceChecks.Hit(icon);var from=ScreenPosition(icon);var to=ScreenPosition(board.GetCell(destination.Value));
        var input=EventSystem.current.gameObject.GetComponent<ArtPointerInput>()??EventSystem.current.gameObject.AddComponent<ArtPointerInput>();
        input.Process(from,true,false,true);yield return null;input.Process(to,false,false,true);yield return null;input.Process(to,false,true,false);yield return null;
        Assert(item.IsPlacing&&F<Vector2Int>(item,"_targetOrigin")==destination.Value,"快速拖动按按下格计算抓取偏移，不吞掉位移");
        item.SetRotationSilently(90);Assert(item.ConfirmPlacement()&&item.Item.Origin==destination&&item.Item.Rotation==90,"预摆旋转后确认保留实际新位置及占格");
        board.PlaceItem(item.Item,origin,0);
    }
    static IEnumerator DragResidue(ItemSlot source,ItemView target)
    {
        ArtAcceptanceChecks.Hit(source);var input=EventSystem.current.gameObject.GetComponent<ArtPointerInput>()??EventSystem.current.gameObject.AddComponent<ArtPointerInput>();
        var from=ScreenPosition(source);var to=ScreenPosition(target.GetComponentsInChildren<Image>().First(i=>i.gameObject.activeInHierarchy));
        input.Process(from,true,false,true);yield return null;input.Process(Vector2.Lerp(from,to,.5f),false,false,true);yield return null;input.Process(to,false,false,true);yield return null;
        var ghost=F<Image>(source,"_residueGhost");Assert(ghost!=null&&ghost.GetComponent<Outline>().effectColor.g>.9f,"残质悬停可进化魔物时边框变绿");
        Shot(source,"09_残质拖放有效高亮");yield return null;while(busy)yield return null;
        input.Process(to,false,true,false);yield return null;
    }
    static void Shot(MonoBehaviour owner,string name){busy=true;owner.StartCoroutine(Capture(name));}
    static IEnumerator Capture(string name)
    {
        yield return null;yield return null;Canvas.ForceUpdateCanvases();var camera=Camera.main;var rt=new RenderTexture(1920,1080,24);var old=camera.targetTexture;var active=RenderTexture.active;
        foreach(var c in UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas)){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=1;}
        camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();
        var folder=Path.GetFullPath("../qa/Doc3");Directory.CreateDirectory(folder);var path=Path.Combine(folder,name+".png");File.WriteAllBytes(path,image.EncodeToPNG());screenshots.Add(path);
        camera.targetTexture=old;RenderTexture.active=active;UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(rt);busy=false;
    }
}
#endif

#endif

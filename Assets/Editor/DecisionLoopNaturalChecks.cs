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
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 自然资源流程：只调用真实购买、仓库摆放、进化、剧情选择与结果确认接口。
/// 不设置骰点、不补钱/魔物、不修改人物属性；这是编辑器事件流程，不是原生鼠标验收。
/// </summary>
[InitializeOnLoad]
public static class DecisionLoopNaturalChecks
{
    const string Key="MonsterDungeon.DecisionLoopNatural";
    static readonly List<object> events=new List<object>(),days=new List<object>(),rolls=new List<object>();
    static readonly List<string> errors=new List<string>(),shots=new List<string>();
    static readonly HashSet<string> visits=new HashSet<string>(),choices=new HashSet<string>(),talked=new HashSet<string>(),lensScenes=new HashSet<string>(),refused=new HashSet<string>(),loggedDice=new HashSet<string>();
    static readonly HashSet<int> loggedFees=new HashSet<int>();
    static IEnumerator routine;
    static bool busy;
    static double started,next,nextHeartbeat;
    static int preparedDay=-1,initialCash,initialLens,pointerPasses,pointerBlocked;
    static string lastProgress="启动",lastScene;
    static NarrativeSystem C=>SystemManager.Instance.Narrative;
    static NarrativeSystem.SaveData S=>F<NarrativeSystem.SaveData>(C,"_save");
    static NarrativeRuntime R=>F<NarrativeRuntime>(C,"_runtime");
    static RunState Run=>RunStateManager.Instance.run;
    static ConfigDatabase DB=>ConfigDatabase.Instance;
    static NarrativeSystem.Rules Rules=>F<NarrativeSystem.Rules>(C,"_rules");
    static string Folder=>Path.GetFullPath("../DecisionLoopNatural");
    static T F<T>(object o,string field)=>(T)o.GetType().GetField(field,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(o);
    static object Call(object o,string method,params object[] a)=>o.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(o,a);
    static void Click(Component component)
    {
        try{ArtAcceptanceChecks.Pointer(component);pointerPasses++;}
        catch(Exception e){pointerBlocked++;Event("指针未通过",new{control=component.name,reason=e.Message});throw;}
    }
    static void Log(string m,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(m+"\n"+stack);}

    static DecisionLoopNaturalChecks()
    {
        if(SessionState.GetBool(Key,false)){started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
    }
    public static void RunBatch()
    {
        if(!Application.productName.EndsWith("_NarrativeValidation"))throw new Exception("自然局只允许隔离验证产品运行");
        Directory.CreateDirectory(Folder);
        // 开始新局只移走隔离产品的自动档；正式产品和四个手动槽不参与此次检查。
        if(SaveAndLoadManager.Exists("run_state"))File.WriteAllText(Path.Combine(Folder,"PreviousIsolatedAutoSave.json"),JsonConvert.SerializeObject(SaveAndLoadManager.Load<RunState>("run_state")));
        SaveAndLoadManager.Delete("run_state");
        SessionState.SetBool(Key,true);
        EditorSceneManager.OpenScene("Assets/Scenes/PlayerGame.unity");
        var view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));view.Show();
        typeof(NarrativeIntegrationChecks).GetMethod("SetGameViewSize",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{view,1280,720});
        EditorApplication.update+=Tick;Application.logMessageReceived+=Log;started=EditorApplication.timeSinceStartup;EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.timeSinceStartup<next)return;
        next=EditorApplication.timeSinceStartup+.02;EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if(EditorApplication.timeSinceStartup-started>1800)throw new Exception("自然局超时："+lastProgress);
            if(errors.Count>0)throw new Exception(errors[0]);
            if(SystemManager.Instance.IsInitialized&&F<bool>(C,"_booted")&&EditorApplication.timeSinceStartup>=nextHeartbeat)
            {
                nextHeartbeat=EditorApplication.timeSinceStartup+5;
                var progress=new{utc=DateTime.UtcNow,elapsed=EditorApplication.timeSinceStartup-started,day=Run.currentDay,stage=S.stage,cash=Run.cash,lastProgress,preparedDay,busy,events=events.Count,dice=rolls.Count,visited=visits.Count,pointerPasses,pointerBlocked,lastEvent=events.LastOrDefault()};
                File.WriteAllText(Path.Combine(Folder,"Progress.json"),JsonConvert.SerializeObject(progress,Formatting.Indented));
                UnityEngine.Debug.Log("[NaturalProgress] day="+Run.currentDay+" stage="+S.stage+" cash="+Run.cash+" visits="+visits.Count+" dice="+rolls.Count+" busy="+busy);
            }
            if(busy||!SystemManager.Instance.IsInitialized||!F<bool>(C,"_booted"))return;
            if(routine==null)routine=Scenario();
            if(!routine.MoveNext())Finish(true,"");
        }
        catch(Exception e){Finish(false,e.ToString());}
    }
    static object ActorState(int id)
    {
        var a=Run.adventurers[id];var s=C.AssessSuccession(id);
        return new{id,name=a.Config.name,a.isDead,a.isSeverelyInjured,a.severeInjuryDelay,stats=a.currentSevenStats.ToDictionary(p=>p.Key.ToString(),p=>p.Value),s.gap,s.tendency,s.chance};
    }
    static object Ownership()=>new{
        board=BackpackBoard.Instance.Items.Where(v=>v!=null&&v.Item!=null&&!v.IsGhost).Select(v=>new{id=int.Parse(v.Def.Id),uid=v.Item.Uid,evolved=v.Item.Evolved,kind=v.Item.EvolutionKind,usedDay=v.Item.UsedDay,x=v.Item.Origin.x,y=v.Item.Origin.y,rotation=v.Item.Rotation}).ToArray(),
        storage=MonsterStorageModel.Instance.Items.ToArray(),
        items=Enumerable.Range(0,InventoryModel.Instance.Capacity).Select(i=>InventoryModel.Instance.GetSlot(i)).Where(s=>!s.IsEmpty).Select(s=>new{id=s.data.id,count=s.quantity}).ToArray()};
    static void Event(string action,object detail=null)=>events.Add(new{day=Run.currentDay,cash=Run.cash,action,detail});
    static void Finish(bool completed,string failure)
    {
        Directory.CreateDirectory(Folder);
        object result=null;
        if(SystemManager.Instance.IsInitialized&&C!=null)
        {
            result=new{day=Run.currentDay,stage=S.stage,ending=C.Ending,cash=Run.cash,visits=visits.Count,settledDays=S.settledDays.OrderBy(x=>x).ToArray(),paidPhases=S.paidPhases.OrderBy(x=>x).ToArray(),sarah=ActorState(4),da=ActorState(6),ownership=Ownership()};
            C.Capture();File.WriteAllText(Path.Combine(Folder,"FinalCheckpoint.json"),JsonConvert.SerializeObject(Run));
        }
        File.WriteAllText(Path.Combine(Folder,"NaturalAcceptance.json"),JsonConvert.SerializeObject(new{
            executionCompleted=completed,failure,mode="自然资源、真实随机；编辑器规则/界面事件驱动，不是原生鼠标",initialCash,initialLens,
            policy="精神力55/共情；Sarah优先、Da备用；缺类合法购买；保留当期分期与150金币；进化植物/构造为狂妄、犬类为胆小；不重投；缺类或重伤缺少低阶目标时拒单",
            lastProgress,result,days,rolls,events,errors,shots,pointerPasses,pointerBlocked,pointerAcceptancePassed=false},Formatting.Indented));
        SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
        EditorApplication.isPlaying=false;EditorApplication.delayCall+=()=>EditorApplication.Exit(completed?0:1);
    }

    static IEnumerator Scenario()
    {
        for(int i=0;i<10;i++)yield return null;
        initialCash=Run.cash;initialLens=InventoryModel.Instance.CountId(1);
        if(initialCash!=2000||initialLens!=2)throw new Exception("新局起点不符：cash="+initialCash+",lens="+initialLens);
        while(!PlayerBuildPanel.IsChoosing)yield return null;
        var build=UIManager.Instance.Get<PlayerBuildPanel>();
        Click(F<Button[]>(build,"_types")[2]);Click(F<Button[]>(build,"_thoughts")[1]);Click(F<Button>(build,"_confirm"));
        Event("合法开局选择",new{Run.player.willpower,Run.player.thought});
        int guard=0;
        while(!C.IsTerminal&&guard++<50000)
        {
            lastProgress="日"+Run.currentDay+" / "+S.stage+" / "+lastScene;
            foreach(int phase in S.paidPhases.Where(p=>!loggedFees.Contains(p)).ToArray())
            {loggedFees.Add(phase);Event("正常剧情缴费",new{phase,amount=DB.GetInstallmentPayment(phase)});}
            var ui=UIManager.Instance;
            if(DiceCheckPanel.IsShowing)
            {
                var dice=ui.Get<DiceCheckPanel>();if(!F<bool>(dice,"_ready")){yield return null;continue;}
                var p=Run.pendingDice;
                if(loggedDice.Add(p.key))rolls.Add(new{day=Run.currentDay,p.key,p.title,p.context,p.rawRoll,p.roll,p.modifier,p.target,p.charmUsed,p.rerolled,p.guaranteedFailure,result=p.Result()});
                dice.Complete();yield return null;continue;
            }
            if(S.stage=="ending_selection")
            {
                var candidates=new[]{4,6}.Concat(Enumerable.Range(1,7).Where(id=>id!=4&&id!=6)).Select(id=>C.AssessSuccession(id)).Where(a=>!a.dead&&a.chance>0).ToArray();
                var chosen=candidates.FirstOrDefault();
                Event("自然培养最终候选",Enumerable.Range(1,7).Select(id=>ActorState(id)).ToArray());
                var panel=ui.Get<EndingPresentationPanel>();
                if(chosen!=null){Event("尝试接任",new{chosen.actorId,chosen.gap,chosen.chance});Click(F<Button[]>(panel,"_actorButtons")[chosen.actorId-1]);}
                else {Event("无人可尝试，保留不转让入口");Click(F<Button>(panel,"_stay"));}
                yield return null;continue;
            }
            if(S.stage=="ending_confirm"){Click(F<Button>(ui.Get<EndingPresentationPanel>(),"_confirm"));yield return null;continue;}
            if(C.IsPreparing)
            {
                if(preparedDay!=Run.currentDay)
                {
                    var prepare=PrepareDay();while(prepare.MoveNext())yield return prepare.Current;
                    preparedDay=Run.currentDay;
                    Event("整备结束",new{offers=Run.monsterOffers.ToArray(),stock=new Dictionary<int,int>(Run.shopStock),ownership=Ownership(),sarah=ActorState(4),da=ActorState(6)});
                    if(Run.currentDay<=3){Shot("整备_第"+Run.currentDay+"天");yield return null;}
                }
                Click(F<Button>(ui.Get<PreparationPanel>(),"_startReceptionButton"));yield return null;continue;
            }
            if(S.stage=="settlement")
            {
                var panel=ui.Get<DailySettlementPanel>();var d=F<SettlementData>(panel,"_data");
                days.Add(new{day=Run.currentDay,cashBeforeIncome=Run.cash,d.legacyCommissionIncome,d.monsterBusinessIncome,d.totalIncome,notes=d.notes.ToArray(),sarah=ActorState(4),da=ActorState(6)});
                if(Run.currentDay<=3){C.Capture();File.WriteAllText(Path.Combine(Folder,"Day"+Run.currentDay.ToString("00")+"Checkpoint.json"),JsonConvert.SerializeObject(Run));Shot("结算_第"+Run.currentDay+"天");yield return null;}
                Click(F<Button>(panel,"btn_Close"));yield return null;continue;
            }
            if(RouteSelectionPanel.IsShowing)
            {
                var route=RouteSelectionPanel.Active;
                if(ui.Get<DescriptionPanel>()?.IsRouteContext==true)Click(F<Button>(ui.Get<DescriptionPanel>(),"_routeConfirmButton"));
                else
                {
                    var view=RouteTarget(route.State.adventurerId,DB.Contracts[route.State.contractId]);
                    if(view==null){Event("指路无可用同类，返回");route.OnEscapePressed();}
                    else {var e=Pointer(CellPoint(view.Item.GetCells().First()));route.SelectAt(view,e);Event("选择真实魔物实例",new{route.State.adventurerId,id=int.Parse(view.Def.Id),view.Item.Uid,view.Item.EvolutionKind});}
                }
                yield return null;continue;
            }
            var talk=ui.Get<TalkPanel>();
            if(S.stage=="commission")
            {
                var panel=ui.Get<CommissionPanel>();
                if(panel==null||!panel.IsOpen){talk?.MoveNext();yield return null;continue;}
                var scene=Rules.scenes.First(s=>s.id==(string)R.Get("current_scene"));int actor=ActorId(scene.actor);
                if(RouteTarget(actor,DB.Contracts[scene.contractId])!=null)Click(F<Button>(panel,"btn_AcceptCommission"));
                else {refused.Add(scene.id);Event("无合适可用目标，合法拒单",scene.id);Click(F<Button>(panel,"CloseButton"));}
                yield return null;continue;
            }
            if(S.stage=="function")
            {
                bool used=false;
                if(lastScene!=null&&!lensScenes.Contains(lastScene)&&Rules.scenes.Any(s=>s.id==lastScene&&s.actor=="SA")&&InventoryModel.Instance.CountId(1)>0)
                {lensScenes.Add(lastScene);used=C.UseBagItem(1);Event("通过现有背包使用透镜",new{scene=lastScene,used});}
                ui.Close<BackpackPanel>();yield return null;continue;
            }
            if(talk==null||!talk.IsOpen||S.frame==null){yield return null;continue;}
            talk.SetInstantText(true);talk.GetType().GetField("_clickCooldown",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(talk,0f);
            var options=S.frame.nodes[0].options;
            if(options.Count==0){talk.MoveNext();yield return null;continue;}
            var rendered=UnityEngine.Object.FindObjectsOfType<DialogueOptionItem>().Count(o=>F<Button>(o,"_button").interactable);
            if(rendered!=options.Count){talk.MoveNext();yield return null;continue;}
            string sceneId=(string)R.Get("current_scene");lastScene=sceneId;
            if(Rules.scenes.Any(s=>s.id==sceneId))visits.Add(sceneId);
            var chosenOption=ChooseOption(options,sceneId);
            // 刚出现的选择等待布局完整刷新；若期间已换帧，重新按新帧选择。
            yield return null;Canvas.ForceUpdateCanvases();
            if(S.frame==null||!S.frame.nodes[0].options.Contains(chosenOption))continue;
            Event("剧情选择",new{scene=sceneId,chosenOption.choiceId,chosenOption.text});choices.Add(chosenOption.choiceId??chosenOption.text);
            Option(chosenOption);yield return null;
        }
        if(guard>=50000)throw new Exception("剧情选择循环未结束："+lastProgress);
        Event("自然流程终点",new{C.Ending,S.stage,Run.cash,settlements=S.settledDays.Count,fees=S.paidPhases.Count,sarah=ActorState(4),da=ActorState(6)});
        Shot("自然流程终点");yield return null;
    }

    static int ActorId(string code)=>Array.IndexOf(new[]{"MY","FE","DW","SA","AU","DA","UN"},code)+1;
    static DialogueOption ChooseOption(List<DialogueOption> options,string scene)
    {
        bool menu=options.Any(o=>o.callbackKey=="open_commission_panel");
        var current=Rules.scenes.FirstOrDefault(s=>s.id==scene);bool focus=current!=null&&(current.actor=="SA"||current.actor=="DA");
        if(menu)
        {
            if(refused.Contains(scene))return options.First(o=>o.text.Contains("直接拒绝")||o.text.Contains("拒绝"));
            if(current?.actor=="SA"&&InventoryModel.Instance.CountId(1)>0&&!lensScenes.Contains(scene))
            {var bag=options.FirstOrDefault(o=>o.text.Contains("使用道具"));if(bag!=null)return bag;}
            if(focus&&Run.actionPoint>0&&talked.Add(scene))return options.First(o=>o.text.Contains("搭话"));
            return options.First(o=>o.callbackKey=="open_commission_panel");
        }
        var back=options.FirstOrDefault(o=>o.text.Contains("返回正式接待"));
        if(back!=null)
        {
            var nextOption=options.Where(o=>o!=back&&!choices.Contains(o.choiceId??o.text)).OrderByDescending(o=>o.checkAttribute=="willpower").ThenByDescending(o=>o.checkKind=="information").FirstOrDefault();
            return focus&&Run.actionPoint>0&&nextOption!=null?nextOption:back;
        }
        // 原稿的影响选项意在帮助人物自行判断；选它不保证检定成功，也不改写结果。
        return options.FirstOrDefault(o=>o.checkKind=="influence")
            ??options.FirstOrDefault(o=>o.checkKind=="information"&&(o.choiceId??"").EndsWith(".11"))
            ??options.FirstOrDefault(o=>o.checkKind=="information")
            ??options.FirstOrDefault(o=>o.text.Contains("不用")||o.text.Contains("返回"))??options[0];
    }
    static void Option(DialogueOption option)
    {
        var displayed=UnityEngine.Object.FindObjectsOfType<DialogueOptionItem>().Where(v=>F<Button>(v,"_button").interactable).OrderBy(v=>v.transform.GetSiblingIndex()).ToArray();
        int index=S.frame.nodes[0].options.IndexOf(option);if(index<0||index>=displayed.Length)throw new Exception("可见选项尚未对应剧情帧");
        var button=F<Button>(displayed[index],"_button");
        // 长选项会被正常滚动遮罩裁切：先滚到可见区域，再做真正命中检查。
        Canvas.ForceUpdateCanvases();
        foreach(var scroll in button.GetComponentsInParent<ScrollRect>())
        {
            if(!scroll.vertical||scroll.content==null)continue;
            var viewport=scroll.viewport!=null?scroll.viewport:(RectTransform)scroll.transform;
            var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(viewport,button.transform);
            var rect=viewport.rect;float delta=0;
            if(bounds.size.y>rect.height-12)delta=rect.center.y-bounds.center.y;
            else if(bounds.min.y<rect.yMin+6)delta=rect.yMin+6-bounds.min.y;
            else if(bounds.max.y>rect.yMax-6)delta=rect.yMax-6-bounds.max.y;
            if(Mathf.Abs(delta)<.1f)continue;
            scroll.StopMovement();
            var movement=scroll.content.parent.InverseTransformVector(viewport.TransformVector(new Vector3(0,delta,0)));
            scroll.content.anchoredPosition+=(Vector2)movement;
            Canvas.ForceUpdateCanvases();
        }
        Click(button);
    }
    static ItemView RouteTarget(int actor,ContractConfig contract)
    {
        bool severe=Run.adventurers[actor].isSeverelyInjured;int level=(int)DB.Adventurers[actor].baseLevel;
        return BackpackBoard.Instance.Items.Where(v=>v!=null&&v.Item!=null&&!v.Item.IsResting&&DB.Monsters[int.Parse(v.Def.Id)].monsterType==DailyCommissionSystem.TargetType(contract))
            .Where(v=>!severe||(int)DB.Monsters[int.Parse(v.Def.Id)].level<level)
            .OrderByDescending(v=>(actor==4||actor==6)&&v.Item.Evolved)
            .ThenBy(v=>Math.Max(0,(int)DB.Monsters[int.Parse(v.Def.Id)].level-level))
            .ThenBy(v=>Math.Abs((int)DB.Monsters[int.Parse(v.Def.Id)].level-level)).FirstOrDefault();
    }

    static IEnumerator PrepareDay()
    {
        var ui=UIManager.Instance;var board=BackpackBoard.Instance;
        var need=Rules.scenes.Where(s=>s.day==Run.currentDay&&!Run.adventurers[ActorId(s.actor)].isDead).ToArray();
        var shop=ui.Open<ShopPanel>(new ShopOpenData{category=ShopCategory.Monster});yield return null;
        int reserve=DB.GetInstallmentPayment(Run.currentPhase)+150;
        foreach(var group in need.GroupBy(s=>s.targetMonsterType.Value))
        {
            int wanted=Math.Min(2,group.Count()),owned=Owned().Count(x=>DB.Monsters[x.monsterId].monsterType==group.Key);
            foreach(int id in Run.monsterOffers.Where(id=>DB.Monsters[id].monsterType==group.Key).OrderBy(id=>DB.Monsters[id].level).ThenBy(id=>DB.Items[id].price).ToArray())
                if(owned<wanted&&Run.cash-DB.Items[id].price>=reserve&&Buy(shop,id)){owned++;yield return null;}
        }
        // 按仓库真实拖放路径退仓和重排，保留UID及进化状态，绝不生成免费实例。
        shop.SwitchToStorage();yield return null;
        foreach(var view in board.Items.ToArray())
        {
            if(view==null||view.Item.IsResting)continue;
            var press=CellPoint(view.Item.GetCells().First());var target=(RectTransform)shop._itemSlots[0].transform;
            var e=Pointer(RectTransformUtility.WorldToScreenPoint(CameraForUI(),target.TransformPoint(target.rect.center)),press);
            view.OnBeginDrag(e);view.OnDrag(e);view.OnEndDrag(e);yield return null;
        }
        var demandTypes=new HashSet<MonsterType>(need.Select(s=>s.targetMonsterType.Value));
        var order=MonsterStorageModel.Instance.Items.Where(m=>m.usedDay!=Run.currentDay).OrderByDescending(m=>demandTypes.Contains(DB.Monsters[m.monsterId].monsterType)).ThenByDescending(m=>m.evolved).ThenBy(m=>DB.Monsters[m.monsterId].level).ToArray();
        foreach(var instance in order)
        {
            shop.SwitchToStorage();int slot=Enumerable.Range(0,((IList)F<object>(shop,"_slots")).Count).FirstOrDefault(i=>shop.GetSlotInstance(i)?.uid==instance.uid);
            var shape=DB.ShapeDb.Get(instance.monsterId.ToString());bool placed=false;
            foreach(int rotation in new[]{0,90,180,270})
            {
                for(int y=0;y<board.Height&&!placed;y++)for(int x=0;x<board.Width&&!placed;x++)
                {
                    var origin=new Vector2Int(x,y);if(!board.CanPlace(shape,origin,rotation))continue;
                    var view=board.CreateItemOnBoard(shape,origin,rotation,F<ItemView>(shop,"_itemViewPrefab"));if(view==null)continue;
                    view.SetInstanceState(instance.uid,instance.evolved,instance.usedDay,instance.evolutionKind);
                    if(!shop.TryBuyForDrag(slot)){board.RemoveItem(view.Item);UnityEngine.Object.Destroy(view.gameObject);throw new Exception("合法仓库取出失败："+instance.uid);}
                    view.StageFromStorage();if(!view.ConfirmPlacement())throw new Exception("合法落位确认失败："+instance.uid);
                    placed=true;Event("仓库实例摆放",new{instance.monsterId,instance.uid,rotation,x,y});
                }
                if(placed)break;
            }
            yield return null;
        }
        ui.Close<ShopPanel>();
        foreach(var view in board.Items.Where(v=>v!=null&&!v.Item.Evolved&&!v.Item.IsResting).ToArray())
        {
            var type=DB.Monsters[int.Parse(view.Def.Id)].monsterType;
            int direction=type==MonsterType.Plant||type==MonsterType.Construct?2:type==MonsterType.Canine?4:-1;
            if(direction<0)continue;
            bool already=board.Items.Any(v=>v!=null&&v.Item.Evolved&&DB.Monsters[int.Parse(v.Def.Id)].monsterType==type);
            if(already)continue;
            if(InventoryModel.Instance.CountId(7)==0&&Run.cash-DB.Items[7].price>=reserve)
            {shop=ui.Open<ShopPanel>(new ShopOpenData{category=ShopCategory.Item});yield return null;Buy(shop,7);ui.Close<ShopPanel>();}
            if(InventoryModel.Instance.CountId(7)>0)
            {var evo=ui.Open<EvolutionPanel>(view.Item.Uid);evo.Choose(direction);Event("真实进化提交",new{id=int.Parse(view.Def.Id),view.Item.Uid,direction,view.Item.EvolutionKind});yield return null;}
        }
        C.Save();
    }
    static IEnumerable<MonsterInstance> Owned()=>MonsterStorageModel.Instance.Items.Concat(BackpackBoard.Instance.Items.Where(v=>v!=null&&v.Item!=null).Select(v=>new MonsterInstance{monsterId=int.Parse(v.Def.Id),uid=v.Item.Uid,evolved=v.Item.Evolved,usedDay=v.Item.UsedDay,evolutionKind=v.Item.EvolutionKind}));
    static bool Buy(ShopPanel shop,int id)
    {
        int count=((IList)F<object>(shop,"_slots")).Count;
        for(int i=0;i<count;i++)if(shop.GetSlotData(i)?.id==id)
        {
            int before=Run.cash;Call(shop,"TryBuy",i);if(Run.cash==before)return false;
            Event("真实商店购买",new{id,price=before-Run.cash,remaining=Run.shopStock.TryGetValue(id,out int n)?n:DB.Items[id].stock});return true;
        }
        return false;
    }
    static Camera CameraForUI()=>UIManager.Instance.RootCanvas.renderMode==RenderMode.ScreenSpaceOverlay?null:UIManager.Instance.RootCanvas.worldCamera;
    static Vector2 CellPoint(Vector2Int cell)
    {var rect=(RectTransform)BackpackBoard.Instance.GetCell(cell).transform;return RectTransformUtility.WorldToScreenPoint(CameraForUI(),rect.TransformPoint(rect.rect.center));}
    static PointerEventData Pointer(Vector2 position,Vector2? press=null)
    {
        var raycast=new RaycastResult{module=UIManager.Instance.RootCanvas.GetComponent<GraphicRaycaster>()};
        return new PointerEventData(EventSystem.current){position=position,pressPosition=press??position,button=PointerEventData.InputButton.Left,pointerPressRaycast=raycast,pointerCurrentRaycast=raycast};
    }
    static void Shot(string name){busy=true;UIManager.Instance.StartCoroutine(Capture(name));}
    static IEnumerator Capture(string name)
    {
        yield return new WaitForSecondsRealtime(.15f);Canvas.ForceUpdateCanvases();
        var cam=Camera.main;int w=Screen.width,h=Screen.height;var target=new RenderTexture(w,h,24);var previous=cam.targetTexture;var active=RenderTexture.active;
        var canvases=UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas).ToArray();var modes=canvases.Select(c=>c.renderMode).ToArray();var cameras=canvases.Select(c=>c.worldCamera).ToArray();
        cam.targetTexture=target;foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;}Canvas.ForceUpdateCanvases();cam.Render();RenderTexture.active=target;
        var image=new Texture2D(w,h,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,w,h),0,0);image.Apply();string path=Path.Combine(Folder,name+".png");File.WriteAllBytes(path,image.EncodeToPNG());shots.Add(path);
        for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];}cam.targetTexture=previous;RenderTexture.active=active;UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(target);busy=false;
    }
}
#endif

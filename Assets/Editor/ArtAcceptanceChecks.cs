#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>隔离验证工程使用：指针先经过 EventSystem 命中检测，再分发事件。</summary>
[InitializeOnLoad]
public static class ArtAcceptanceChecks
{
    const string PresentationKey="MonsterDungeon.ArtPresentation";
    static int postStep;
    static string postInk;
    static int expectedPostCash, expectedPostAP;
    static readonly List<string> postErrors=new List<string>();
    static double postStarted;
    static ArtAcceptanceChecks(){if(SessionState.GetBool(PresentationKey,false)){EditorApplication.update+=PresentationTick;Application.logMessageReceived+=PresentationLog;postStarted=EditorApplication.timeSinceStartup;}}
    static void PresentationLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)postErrors.Add(message);}
    public static void RunPresentation()
    {
        if(!Application.productName.EndsWith("_NarrativeValidation"))throw new Exception("只能在隔离副本运行验收");
        SessionState.SetBool(PresentationKey,true);UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/StartGame.unity");
        var view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));view.Show();
        typeof(NarrativeIntegrationChecks).GetMethod("SetGameViewSize",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{view,1920,1080});
        EditorApplication.update+=PresentationTick;Application.logMessageReceived+=PresentationLog;postStarted=EditorApplication.timeSinceStartup;EditorApplication.EnterPlaymode();
    }
    static void PresentationTick()
    {
        if(!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try
        {
            if(EditorApplication.timeSinceStartup-postStarted>180)throw new Exception("页面复核超时");
            if(postErrors.Count>0)throw new Exception(postErrors[0]);
            if(capturing)return;
            var manager=UnityEngine.Object.FindObjectOfType<UIPanelManager>();var ui=UIManager.Instance;var prep=ui!=null?ui.Get<PreparationPanel>():null;
            var talk=ui!=null?ui.Get<TalkPanel>():null;
            switch(postStep)
            {
                case 0:if(manager==null)return;Shot(manager,"10_封面");postStep++;break;
                case 1:Pointer(Field<Button>(manager,"btn_settings"));Shot(manager,"11_主页设置");postStep++;break;
                case 2:
                    var settings=Field<GameObject>(manager,"pnl_settings").GetComponent<SettingsPanel>();Assert(!Field<Button>(settings,"btn_save_game").interactable,"首页未开局禁用保存按钮");Pointer(Field<Button>(settings,"btn_read_save"));Shot(manager,"12_主页读档");postStep++;break;
                case 3:
                    var slots=UnityEngine.Object.FindObjectOfType<SaveSlotsPanel>();Pointer(Field<Button[]>(slots,"slots")[1]);postStep++;break;
                case 4:
                    if(ui==null||prep==null||!SystemManager.Instance.IsInitialized)return;
                    if(!Field<bool>(SystemManager.Instance.Narrative,"_booted"))return;
                    Assert(SystemManager.Instance.RunState.run.cash==2001,"从首页实际点击读取槽二恢复独立金币");
                    if(!postDragged)
                    {
                        postDragged=true;var board=BackpackBoard.Instance;
                        foreach(var oldView in board.Items.Where(v=>v!=null&&v.Item!=null&&v.Item.Uid=="qa-evolved").ToArray()){board.RemoveItem(oldView.Item);UnityEngine.Object.Destroy(oldView.gameObject);}
                        capturing=true;prep.StartCoroutine(DragAndStates(prep));return;
                    }
                    var runtime=Field<NarrativeRuntime>(SystemManager.Instance.Narrative,"_runtime");DialogueHistoryModel.Instance.Clear();for(int i=0;i<8;i++)DialogueHistoryModel.Instance.Add(new DialogueLine{speakerName="Myth",content=runtime.BackgroundTexts["MY:partial"]});
                    ui.Open<DialogueHistoryPanel>();Shot(prep,"13_历史长文复核");postStep++;break;
                case 5:
                    var history=ui.Get<DialogueHistoryPanel>();var scroll=Field<ScrollRect>(history,"_scrollRect");Assert(scroll.content.rect.height>scroll.viewport.rect.height,"历史长文可以滚动");Assert(history.GetComponentsInChildren<Image>().Where(im=>im.name=="DialogueHistoryEntry(Clone)").All(im=>im.color.a==0),"历史条目无白色占位底板");Pointer(Field<Button>(history,"_closeButton"));SystemManager.Instance.Narrative.StartReception();postStep++;break;
                case 6:
                    if(talk==null||!talk.IsOpen)return;talk.SetInstantText(true);typeof(TalkPanel).GetField("_clickCooldown",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(talk,0f);
                    var save=Field<NarrativeSystem.SaveData>(SystemManager.Instance.Narrative,"_save");if(save.frame==null||!save.frame.nodes[0].options.Any(o=>o.callbackKey=="open_special_commission"))
                    {
                        var choices=UnityEngine.Object.FindObjectsOfType<DialogueOptionItem>().Where(o=>Field<Button>(o,"_button").interactable).ToArray();
                        if(choices.Length>0)Pointer(Field<Button>(choices[0],"_button"));else talk.MoveNext();return;
                    }
                    var option=UnityEngine.Object.FindObjectsOfType<DialogueOptionItem>().FirstOrDefault(o=>Field<Text>(o,"_text").text.Contains("搭话")&&Field<Button>(o,"_button").interactable);
                    if(option==null){talk.MoveNext();return;}
                    Pointer(ui.Get<HUDPanel>().ButtonRoot.Find("CharacterArchive/CharacterArchiveButton").GetComponent<Button>());postInk=Field<NarrativeRuntime>(SystemManager.Instance.Narrative,"_runtime").Story.state.ToJson();
                    Pointer(Field<Button>(option,"_button"));postStep++;break;
                case 7:
                    var active=Field<NarrativeRuntime>(SystemManager.Instance.Narrative,"_runtime");if(active.Story.state.ToJson()==postInk)return;
                    Assert(ui.IsOpen<CharacterArchivePanel>(),"档案打开时右侧指针选项实际推进");
                    var archive=ui.Get<CharacterArchivePanel>();var before=active.Story.state.ToJson();Pointer(Field<Button>(archive,"_investigationLog"));Assert(active.Story.state.ToJson()==before,"点击档案页签不推进右侧");
                    ToastManager.Instance.Clear();typeof(NarrativeSystem).GetMethod("HandleAction",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(SystemManager.Instance.Narrative,new object[]{"action:attribute actor=MY stat=tolerance direction=up amount=3"});typeof(NarrativeSystem).GetMethod("FlushAttributeFeedback",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(SystemManager.Instance.Narrative,null);Shot(talk,"14_属性浮动提示");postStep++;break;
                case 8:
                    ToastManager.Instance.Clear();SystemManager.Instance.Economy.AddCash(1);Shot(talk,"15_金币浮动提示");postStep++;break;
                case 9:
                    ToastManager.Instance.Clear();SystemManager.Instance.ActionPoint.Spend(1);Shot(talk,"16_行动点浮动提示");postStep++;break;
                case 10:
                    var campaign=SystemManager.Instance.Narrative;var run=SystemManager.Instance.RunState.run;var data=Field<NarrativeSystem.SaveData>(campaign,"_save");
                    var ink=Field<NarrativeRuntime>(campaign,"_runtime");var partial=new Ink.Runtime.InkList((Ink.Runtime.InkList)ink.Get("bg_partial"));for(int i=1;i<=3;i++)partial.AddItem("B_MY"+i,ink.Story);ink.Set("bg_partial",partial);
                    data.backgroundLevels["MY"]="partial";run.adventurers[1].investigationArchive.Clear();run.adventurers[1].investigationArchive.Add(ink.BackgroundTexts["MY:partial"]);
                    data.lensDay=run.currentDay;data.lensDailyUses=2;SystemManager.Instance.RunState.SaveSlot(4);
                    var old=SaveAndLoadManager.Load<RunState>(SaveAndLoadManager.SlotKey(4));var legacy=JsonConvert.DeserializeObject<NarrativeSystem.SaveData>(old.narrativeSave);legacy.uiVersion=2;legacy.lensDailyUses=999;legacy.lensUses[1]=2;old.narrativeSave=JsonConvert.SerializeObject(legacy);SaveAndLoadManager.Save(SaveAndLoadManager.SlotKey(4),old);
                    expectedPostCash=run.cash;expectedPostAP=run.actionPoint;Assert(SaveAndLoadManager.ActivateSlot(4),"激活旧版阶段透镜存档");postStep++;UnityEngine.SceneManagement.SceneManager.LoadScene("PlayerGame");break;
                case 11:
                    if(ui==null||!SystemManager.Instance.IsInitialized||!Field<bool>(SystemManager.Instance.Narrative,"_booted"))return;
                    var migrated=Field<NarrativeSystem.SaveData>(SystemManager.Instance.Narrative,"_save");var migratedRun=SystemManager.Instance.RunState.run;
                    Assert(migrated.uiVersion==4&&SystemManager.Instance.Narrative.LensDailyUses==0,"旧版额度仅首次迁移清零当前日");Assert(migrated.backgroundLevels["MY"]=="partial"&&migratedRun.adventurers[1].investigationArchive[0].Contains("□"),"迁移保留模糊调查，不提前完整揭示");
                    Assert(migratedRun.cash==expectedPostCash&&migratedRun.actionPoint==expectedPostAP,"槽位读取不重复奖励或扣行动点");
                    migrated.lensDay=migratedRun.currentDay;migrated.lensDailyUses=2;SystemManager.Instance.RunState.SaveSlot(4);SaveAndLoadManager.ActivateSlot(4);postStep++;UnityEngine.SceneManagement.SceneManager.LoadScene("PlayerGame");break;
                case 12:
                    if(ui==null||!SystemManager.Instance.IsInitialized||!Field<bool>(SystemManager.Instance.Narrative,"_booted"))return;
                    Assert(SystemManager.Instance.Narrative.LensDailyUses==2&&!SystemManager.Instance.Narrative.CanUseLens(),"同日读档不刷新透镜额度");
                    var stage=UnityEngine.Object.FindObjectOfType<StageController>();stage.ApplyStage(stage.CurrentStage+1);
                    Assert(MonsterStorageModel.Instance.Items.Any(m=>m.uid=="qa-evolved"&&m.evolved),"阶段换图退仓保留进化状态与实例标识");postStep++;break;
                default:
                    File.WriteAllText("ArtPresentation.json",JsonConvert.SerializeObject(new{passed=true,checks,screenshots},Formatting.Indented));EndPresentation(0);break;
            }
        }
        catch(Exception e){File.WriteAllText("ArtPresentation.json",JsonConvert.SerializeObject(new{passed=false,failure=e.ToString(),checks,screenshots},Formatting.Indented));Debug.LogException(e);EndPresentation(1);}
    }
    static void EndPresentation(int code){SessionState.SetBool(PresentationKey,false);EditorApplication.update-=PresentationTick;Application.logMessageReceived-=PresentationLog;EditorApplication.isPlaying=false;EditorApplication.delayCall+=()=>EditorApplication.Exit(code);}
    static int step;
    static bool capturing, finished, extraDone;
    static bool receptionDone, postDragged;
    static readonly List<string> checks = new List<string>();
    static readonly List<string> screenshots = new List<string>();
    static RenderTexture pointerFrame;
    static ArtPointerInput pointerInput;
    static ArtPointerInput InputDriver
    {get {if(pointerInput==null)pointerInput=EventSystem.current.gameObject.AddComponent<ArtPointerInput>();return pointerInput;}}
    public static T Field<T>(object owner,string name) => (T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(owner);
    static void Assert(bool value,string message){if(!value)throw new Exception("美术验收："+message);checks.Add(message);}
    static Vector2 Position(Component component)
    {var canvas=component.GetComponentInParent<Canvas>();return RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,((RectTransform)component.transform).TransformPoint(((RectTransform)component.transform).rect.center));}
    public static GameObject Hit(Component component)
    {
        // 批处理的编辑器 Update 可能在渲染前连续执行。先渲染真实 Canvas，
        // 确保刚打开/恢复的按钮已生成深度，再做指针命中，模拟两次用户操作之间的一帧。
        var camera=Camera.main;
        foreach(var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas&&c.sortingOrder<1000))
        {canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;}
        if(pointerFrame==null)pointerFrame=new RenderTexture(1920,1080,24);
        var previous=camera.targetTexture;camera.targetTexture=pointerFrame;Canvas.ForceUpdateCanvases();camera.Render();camera.targetTexture=previous;
        var e=new PointerEventData(EventSystem.current){position=Position(component)};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);
        if(hits.Count==0)throw new Exception("指针没有命中 "+component.name+" @"+e.position);
        return hits[0].gameObject;
    }
    public static void Pointer(Component component,PointerEventData.InputButton button=PointerEventData.InputButton.Left)
    {
        var hit=Hit(component);
        if(hit.transform!=component.transform&&!hit.transform.IsChildOf(component.transform))throw new Exception("点击被遮挡："+component.name+" 实际命中 "+hit.name+" @"+Position(component)+" active="+component.gameObject.activeInHierarchy+" image="+component.GetComponent<Image>()?.raycastTarget+" depth="+component.GetComponent<Image>()?.depth);
        var position=Position(component);int index=button==PointerEventData.InputButton.Left?0:1;
        InputDriver.Process(position,true,false,true,index);InputDriver.Process(position,false,true,false,index);
    }
    static void Shot(MonoBehaviour owner,string name){capturing=true;owner.StartCoroutine(Capture(name));}
    static IEnumerator Capture(string name)
    {
        if(name.StartsWith("14_")||name.StartsWith("15_")||name.StartsWith("16_"))yield return new WaitForSecondsRealtime(.2f);
        yield return null;yield return null;Canvas.ForceUpdateCanvases();
        string folder=Path.GetFullPath("../qa/Art");Directory.CreateDirectory(folder);
        var camera=Camera.main;var target=new RenderTexture(1920,1080,24);var previous=camera.targetTexture;var active=RenderTexture.active;
        var canvases=UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas).ToArray();var modes=canvases.Select(c=>c.renderMode).ToArray();var cameras=canvases.Select(c=>c.worldCamera).ToArray();var distances=canvases.Select(c=>c.planeDistance).ToArray();
        camera.targetTexture=target;for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=RenderMode.ScreenSpaceCamera;canvases[i].worldCamera=camera;canvases[i].planeDistance=1;}
        Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();var path=Path.Combine(folder,name+".png");File.WriteAllBytes(path,image.EncodeToPNG());
        for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=distances[i];}camera.targetTexture=previous;RenderTexture.active=active;UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(target);screenshots.Add(path);capturing=false;
    }
    public static bool Preparation()
    {
        if(finished)return false;if(capturing)return true;
        var ui=UIManager.Instance;var prep=ui.Get<PreparationPanel>();var run=SystemManager.Instance.RunState.run;
        switch(step++)
        {
            case 0:
                Bindings();Shot(prep,"01_整备与棋盘");break;
            case 1:Pointer(Field<Button>(prep,"_shopButton"));Shot(prep,"02_商店道具");break;
            case 2:
                var shop=ui.Get<ShopPanel>();Pointer(shop._itemSlots[0],PointerEventData.InputButton.Right);Shot(prep,"03_商店操作");break;
            case 3:ui.HandleEscape();ui.Close<ShopPanel>();Pointer(Field<Button>(prep,"_abyssButton"));Shot(prep,"04a_深渊祭坛");break;
            case 4:Pointer(Field<Button>(ui.Get<AbyssalAltarPanel>(),"_returnButton"));Shot(prep,"04b_祭坛与地狱");break;
            case 5:ui.Close<AbyssalAltarPanel>();ui.Close<DecomposePanel>();Pointer(Field<Button>(prep,"_monsterBackpackButton"));Shot(prep,"05_魔物仓库");break;
            case 6:ui.Close<ShopPanel>();capturing=true;prep.StartCoroutine(DragAndStates(prep));break;
            case 7:
                ui.Open<SettingsPanel>();Shot(prep,"07_设置");break;
            case 8:
                var settings=ui.Get<SettingsPanel>();var volume=Field<Slider>(settings,"sld_sfx");volume.value=.37f;Assert(Mathf.Abs(AudioManager.Instance.sfxVolume-.37f)<.001f,"音效滑块连接实际音效音量");Pointer(Field<Button>(settings,"btn_save_game"));Shot(prep,"08_四槽管理");break;
            case 9:
                var slots=UnityEngine.Object.FindObjectOfType<SaveSlotsPanel>();var buttons=Field<Button[]>(slots,"slots");int cash=run.cash;
                for(int i=0;i<4;i++){run.cash=cash+i;Pointer(buttons[i]);if(Field<int>(slots,"pendingOverwrite")!=0)Pointer(buttons[i]);}
                for(int i=0;i<4;i++){var snap=SaveAndLoadManager.Load<RunState>(SaveAndLoadManager.SlotKey(i+1));Assert(snap.cash==cash+i,"存档槽"+(i+1)+"独立内容");}
                run.cash=cash;Pointer(buttons[0]);Assert(Field<int>(slots,"pendingOverwrite")==1,"覆盖已有存档需再次点击确认");slots.OnClose();
                Assert(SaveAndLoadManager.Load<RunState>(SaveAndLoadManager.SlotKey(1)).cash==cash,"取消覆盖保留原存档");
                Assert(SaveAndLoadManager.ActivateSlot(3)&&SaveAndLoadManager.Load<RunState>("run_state").cash==cash+2,"指定手动槽恢复为自动续玩快照");SystemManager.Instance.RunState.Save();
                SaveAndLoadManager.Save(SaveAndLoadManager.SlotKey(4),new RunState());Assert(!SaveAndLoadManager.GetSlotInfo(4).valid,"损坏槽位不可读取");SystemManager.Instance.RunState.SaveSlot(4);
                ui.Close<SettingsPanel>();ToastManager.Instance.Clear();break;
            default:finished=true;Write();return false;
        }
        return true;
    }
    static void Bindings()
    {
        var config=ConfigDatabase.Instance;int images=0;
        for(int id=101;id<=117;id++)
        {
            var shape=config.ShapeDb.Get(id.ToString());Assert(shape.NormalIcon!=null&&shape.EvolvedIcon!=null&&shape.NormalIcon!=shape.EvolvedIcon,"魔物"+id+"两种形态独立绑定");
            var go=new GameObject("ArtShapeCheck",typeof(RectTransform));go.SetActive(false);var v=go.AddComponent<ItemView>();v.IsGhost=true;v.Def=shape;
            foreach(bool evolved in new[]{false,true})for(int rotation=0;rotation<360;rotation+=90){v.SetInstanceState("qa",evolved);v.SetRotationSilently(rotation);var sprites=go.GetComponentsInChildren<Image>(true).Where(im=>im.gameObject.activeSelf).ToArray();Assert(sprites.Length==shape.Shape.Length&&sprites.All(im=>im.sprite==shape.GetIcon(evolved)),"魔物"+id+"形态"+evolved+"旋转"+rotation+"逐格拼接");images+=sprites.Length;}
            UnityEngine.Object.Destroy(go);
        }
        for(int id=1;id<=7;id++)Assert(Resources.Load<Sprite>(config.Items[id].iconAddress)!=null,"道具"+id+"图片可加载");
        foreach(var cell in BackpackBoard.Instance.GetComponentsInChildren<BackpackCell>(true))Assert(cell.SpecialIcons.Length==4&&cell.SpecialIcons.All(s=>s!=null),"特殊地块四图绑定");
        var font=Resources.Load<Font>("Art/Fonts/FangZhengShuSong");font.RequestCharactersInTexture("行动点调查记录□金币Myth0123456789",26);Assert("行动点调查记录□金币Myth0123456789".All(c=>font.HasCharacter(c)),"方正书宋中英文与模糊符号字形齐全");
    }
    static IEnumerator MouseDrag(Component source, Component destination)
    {
        Hit(source);var start=Position(source);var end=Position(destination);
        InputDriver.Process(start,true,false,true);yield return null;
        InputDriver.Process(Vector2.Lerp(start,end,.5f),false,false,true);yield return null;
        InputDriver.Process(end,false,false,true);yield return null;
        InputDriver.Process(end,false,true,false);yield return null;
    }
    static IEnumerator DragAndStates(MonoBehaviour owner)
    {
        var ui=UIManager.Instance;var board=BackpackBoard.Instance;var def=ConfigDatabase.Instance.ShapeDb.Get("101");
        var go=new GameObject("ArtDragMonster",typeof(RectTransform));go.transform.SetParent(board.ItemsRoot,false);var view=go.AddComponent<ItemView>();
        Vector2Int origin=Vector2Int.zero;bool found=false;for(int y=0;y<board.Height&&!found;y++)for(int x=0;x<board.Width&&!found;x++)if(board.CanPlace(def,new Vector2Int(x,y),0)){origin=new Vector2Int(x,y);found=true;}
        Assert(found,"找到鼠标拖拽验收起点");view.Init(def,board,0,origin);view.SetInstanceState("qa-evolved",true);yield return null;
        Shot(owner,"05b_拖拽前棋盘");while(capturing)yield return null;capturing=true;
        ShopPanel.OpenMonsterStorage();yield return null;var shop=ui.Get<ShopPanel>();
        yield return MouseDrag(view.GetComponentsInChildren<Image>().First(),shop._itemSlots[0]);
        Assert(MonsterStorageModel.Instance.Items.Any(m=>m.uid=="qa-evolved"&&m.evolved),"原输入模块鼠标拖回仓库保留进化状态");
        Assert(!board.Items.Any(v=>v!=null&&v.Item!=null&&v.Item.Uid=="qa-evolved"),"退仓同步清除棋盘登记");
        yield return MouseDrag(shop._itemSlots[0],board.GetCell(origin));
        var placed=board.Items.FirstOrDefault(v=>v!=null&&v.Item!=null&&v.Item.Uid=="qa-evolved");
        Assert(placed!=null&&placed.Item.Evolved,"原输入模块鼠标从仓库拖上棋盘保持实例与进化图片");
        ui.Close<ShopPanel>();Canvas.ForceUpdateCanvases();var expected=board.ItemsRoot.TransformPoint(board.GetCellLocalPosition(placed.Item.Origin));Assert(Vector3.Distance(placed.transform.position,expected)<.01f,"魔物显示位置与真实占格对齐");
        var layout=board.GridRoot.GetComponent<GridLayoutGroup>();Assert(layout.constraint==GridLayoutGroup.Constraint.FixedColumnCount&&layout.constraintCount==7,"棋盘固定七列，缩放不改变坐标映射");
        ui.Open<AbyssalAltarPanel>();ui.Open<DecomposePanel>();yield return null;
        yield return MouseDrag(placed.GetComponentsInChildren<Image>().First(),Field<ItemSlot>(ui.Get<DecomposePanel>(),"_slot"));
        Assert(ui.Get<DecomposePanel>().PendingItem!=null&&ui.Get<DecomposePanel>().PendingItem.Evolved,"拖入地狱待确认框保留进化图片");
        SystemManager.Instance.Narrative.Capture();var snapshot=JsonConvert.DeserializeObject<NarrativeSystem.SaveData>(SystemManager.Instance.RunState.run.narrativeSave);
        Assert(snapshot.storedInstances.Any(m=>m.uid=="qa-evolved"&&m.evolved),"存档保留尚未确认分解的魔物，读档退仓不丢失");
        ui.Close<AbyssalAltarPanel>();Assert(ui.Get<DecomposePanel>().IsOpen,"左侧祭坛关闭时右侧地狱保持");ui.Close<DecomposePanel>();yield return null;Assert(board.Items.Any(v=>v!=null&&v.Item!=null&&v.Item.Uid=="qa-evolved"),"取消分解回到原棋盘");
        int cash=SystemManager.Instance.RunState.run.cash;Pointer(Field<Button>(ui.Get<PreparationPanel>(),"_calculateIncomeButton"));Assert(SystemManager.Instance.RunState.run.cash==cash,"计算收益按钮只预览，不提前入账");
        yield return Capture("06_进化图标与占格");
    }
    public static bool ExtraUI(NarrativeSystem campaign,TalkPanel talk)
    {
        if(capturing)return false;if(extraDone){UIManager.Instance.Close<DialogueHistoryPanel>();return true;}
        var ui=UIManager.Instance;ui.Open<DialogueHistoryPanel>();Shot(talk,"09_历史记录");extraDone=true;Write();return false;
    }
    public static void Reception(NarrativeSystem campaign, TalkPanel talk)
    {
        if(receptionDone)return;receptionDone=true;var ui=UIManager.Instance;
        var save=Field<NarrativeSystem.SaveData>(campaign,"_save");var run=SystemManager.Instance.RunState.run;
        var runtime=Field<NarrativeRuntime>(campaign,"_runtime");string actor=(string)runtime.Get("current_actor");
        int lensDay=save.lensDay,used=save.lensDailyUses;bool had=save.backgroundLevels.TryGetValue(actor,out var level);
        save.backgroundLevels.Remove(actor);save.lensDay=run.currentDay;save.lensDailyUses=2;
        Assert(!campaign.CanUseLens(),"第一阶段每日两次透镜限制");
        run.currentDay++;Assert(campaign.CanUseLens(),"跨天获得新透镜额度");run.currentDay--;
        save.lensDailyUses=0;save.backgroundLevels[actor]="full";Assert(!campaign.CanUseLens(),"个人完整揭示后禁止再次调查");
        if(had)save.backgroundLevels[actor]=level;else save.backgroundLevels.Remove(actor);save.lensDay=lensDay;save.lensDailyUses=used;
        talk.OpenArchive(1,2);Assert(!ui.HasLeftOverlay,"档案允许操作右侧");
        var visitors=SystemManager.Instance.VisitSystem;visitors.SelectNarrativeVisitor(2);Assert(ui.Get<CharacterArchivePanel>().CurrentActor==2&&ui.Get<CharacterArchivePanel>().CurrentTab==2,"并行查档时访客切换同步人物并保留页签");visitors.SelectNarrativeVisitor(1);
        var archive=talk.leftDisplayArea.GetComponentInChildren<CharacterArchivePanel>(true);float scroll=Field<ScrollRect>(archive,"_recordScroll").verticalNormalizedPosition;int value=run.adventurers[1].currentSevenStats[SevenDimension.Reason];run.adventurers[1].currentSevenStats[SevenDimension.Reason]=value+1;TopBarRefreshEvent.Trigger();
        Assert(archive.statTexts[0].text.EndsWith((value+1).ToString())&&archive.CurrentTab==2&&Field<ScrollRect>(archive,"_recordScroll").verticalNormalizedPosition==scroll,"七维变化即时刷新且保留页签滚动");run.adventurers[1].currentSevenStats[SevenDimension.Reason]=value;TopBarRefreshEvent.Trigger();ui.Close(archive);
        var bag=ui.Open<BackpackPanel>();Assert(!ui.HasLeftOverlay&&Time.timeScale==1,"浏览背包不阻塞右侧也不冻结时间");ui.Close(bag);ToastManager.Instance.Clear();
        Write();
    }
    static void Write(){File.WriteAllText("ArtAcceptance.json",JsonConvert.SerializeObject(new{checks,screenshots,preparationComplete=finished},Formatting.Indented));}
}
#endif

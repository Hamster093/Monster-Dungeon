#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>说明2的独立验收记录；只在隔离验证工程被调用，不改变正式游戏规则。</summary>
[InitializeOnLoad]
public static class Doc2AcceptanceChecks
{
    const string EdgeKey="MonsterDungeon.Doc2Edges";
    static IEnumerator edge;
    static double edgeStarted,nextEdgeTick;
    static readonly List<string> edgeErrors=new List<string>();
    static Doc2AcceptanceChecks(){if(SessionState.GetBool(EdgeKey,false)){EditorApplication.update+=EdgeTick;edgeStarted=EditorApplication.timeSinceStartup;Application.logMessageReceived+=EdgeLog;}}
    static void EdgeLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)edgeErrors.Add(message);}
    public static void OpenInteractive()=>OpenValidation(false);
    public static void RunEdges()=>OpenValidation(true);
    static void OpenValidation(bool automated)
    {
        if(!Application.productName.EndsWith("_NarrativeValidation"))throw new Exception("只能在隔离副本运行");
        if(!SaveAndLoadManager.ActivateSlot(4))throw new Exception("需要完整回归生成的待指路槽四");
        SessionState.SetBool(EdgeKey,automated);
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/PlayerGame.unity");
        var view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));view.Show();view.Focus();
        typeof(NarrativeIntegrationChecks).GetMethod("SetGameViewSize",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{view,1920,1080});
        if(!Application.isBatchMode)view.maximized=true;
        if(automated){EditorApplication.update+=EdgeTick;Application.logMessageReceived+=EdgeLog;edgeStarted=EditorApplication.timeSinceStartup;}
        EditorApplication.EnterPlaymode();
    }
    static void EdgeTick()
    {
        if(!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.timeSinceStartup<nextEdgeTick)return;
        nextEdgeTick=EditorApplication.timeSinceStartup+.03;EditorApplication.QueuePlayerLoopUpdate();
        try{
            if(EditorApplication.timeSinceStartup-edgeStarted>180)throw new Exception("边界验收超时");
            if(edgeErrors.Count>0)throw new Exception(edgeErrors[0]);
            if(SystemManager.Instance?.IsInitialized!=true||!F<bool>(SystemManager.Instance.Narrative,"_booted"))return;
            if(busy)return;if(edge==null)edge=Edges();
            if(!edge.MoveNext())EndEdges(true,"");
        }catch(Exception e){EndEdges(false,e.ToString());}
    }
    static void EndEdges(bool passed,string failure)
    {
        File.WriteAllText("Doc2Edges.json",JsonConvert.SerializeObject(new{passed,failure,checks,screenshots,errors=edgeErrors},Formatting.Indented));
        SessionState.SetBool(EdgeKey,false);EditorApplication.update-=EdgeTick;Application.logMessageReceived-=EdgeLog;
        EditorApplication.isPlaying=false;EditorApplication.delayCall+=()=>EditorApplication.Exit(passed?0:1);
    }
    static void CellClick(Vector2Int coordinate)
    {
        var cell=BackpackBoard.Instance.GetCell(coordinate);ArtAcceptanceChecks.Hit(cell);
        var canvas=cell.GetComponentInParent<Canvas>();var point=RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,cell.Rect.TransformPoint(cell.Rect.rect.center));
        var input=EventSystem.current.GetComponent<ArtPointerInput>()??EventSystem.current.gameObject.AddComponent<ArtPointerInput>();
        input.Process(point,true,false,true);input.Process(point,false,true,false);
    }
    static ItemView Fixture(string id,Vector2Int origin,int rotation,bool evolved=false,bool lShape=false)
    {
        var source=SystemManager.Instance.Config.ShapeDb.Get(id);var def=UnityEngine.Object.Instantiate(source);
        if(lShape)def.Shape=new[]{Vector2Int.zero,Vector2Int.up,new Vector2Int(1,0)};
        var go=new GameObject("Doc2隔离验收魔物",typeof(RectTransform));go.transform.SetParent(BackpackBoard.Instance.ItemsRoot,false);
        var view=go.AddComponent<ItemView>();view.Init(def,BackpackBoard.Instance,rotation,origin);view.SetInstanceState("doc2-"+id,evolved);return view;
    }
    static void ClearFixtureBoard()
    {
        foreach(var view in BackpackBoard.Instance.Items.ToArray()){BackpackBoard.Instance.RemoveItem(view.Item);view.gameObject.SetActive(false);UnityEngine.Object.Destroy(view.gameObject);}
    }
    static IEnumerator Edges()
    {
        var ui=UIManager.Instance;var campaign=SystemManager.Instance.Narrative;var daily=SystemManager.Instance.DailyCommission;
        var talk=ui.Get<TalkPanel>();talk.SetInstantText(true);
        for(int i=0;i<8;i++)yield return null;
        Check(RouteSelectionPanel.IsShowing&&ui.Get<DescriptionPanel>().IsRouteContext,"独立边界测试恢复真实待指路存档");
        ui.HandleEscape();ClearFixtureBoard();yield return null;
        var wrong=Fixture("104",new Vector2Int(0,0),90,true,true);var correct=Fixture("101",new Vector2Int(3,0),0);
        yield return null;
        var shape=wrong.Item.GetCells();var hole=new[]{new Vector2Int(0,0),new Vector2Int(1,0),new Vector2Int(0,1),new Vector2Int(1,1)}.First(c=>!shape.Contains(c));
        CellClick(hole);Check(!ui.IsOpen<DescriptionPanel>(),"旋转多格形状空洞不打开魔物详情");
        CellClick(new Vector2Int(6,6));Check(!ui.IsOpen<DescriptionPanel>(),"空格不选中魔物");
        foreach(var c in shape){CellClick(c);Check(ui.Get<RouteSelectionPanel>().State.selectedUid==wrong.Item.Uid&&ui.Get<DescriptionPanel>().IsRouteContext,"旋转魔物每一实际占格均选中同一实例");ui.HandleEscape();}
        CellClick(shape[0]);Check(F<Image>(ui.Get<DescriptionPanel>(),"_itemImage").sprite==wrong.Def.EvolvedIcon,"指路详情显示实例进化素材");
        ui.HandleEscape();ui.HandleEscape();
        Check(daily.NormalCommissions.Count==0,"多格查看及取消均不提前接单");
        Click(F<Button>(ui.Get<CommissionPanel>(),"btn_AcceptCommission"));CellClick(wrong.Item.GetCells()[0]);
        var confirm=F<Button>(ui.Get<DescriptionPanel>(),"_routeConfirmButton");Click(confirm);confirm.onClick.Invoke();
        Check(daily.NormalCommissions.Count==1&&daily.NormalCommissions[0].targetMonsterId==104,"错误目标仍可正式指路并记录实际魔物");
        Check(F<NarrativeSystem.SaveData>(campaign,"_save").routedWrong,"错误指路沿用原种类判定");
        Check(!RouteSelectionPanel.IsShowing,"重复确认只提交一次并还原原棋盘");
        for(int i=0;i<8;i++)yield return null;
        // 说明4已撤除特殊委托，旧的专项测试停止运行；普通指路工具继续复用。
        for(int i=0;i<12;i++)yield return null;
        var option=UnityEngine.Object.Instantiate(Resources.Load<DialogueOptionItem>("Panel/DialogueOptionItem"),ui.PanelRoot);
        {
            var button=F<Button>(option,"_button");var text=F<Text>(option,"_text");
            Check(button.targetGraphic==text&&button.transition==Selectable.Transition.ColorTint&&button.colors.highlightedColor!=button.colors.normalColor,"剧情选项悬停提亮绑定到文本");
            Check(button.colors.selectedColor==button.colors.normalColor,"鼠标离开已选选项后恢复原亮度");
            button.interactable=false;ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);
            for(int i=0;i<12;i++)yield return null;
            var color=text.canvasRenderer.GetColor();Check(Mathf.Abs(color.r-button.colors.disabledColor.r)<.05f,"禁用选项悬停仍保持灰色");
            UnityEngine.Object.Destroy(option.gameObject);
        }
    }
    static int prepStep, routeStep;
    static bool busy, done;
    static PlacedItem pendingMonster;
    static string ink, uid;
    static int cash, ap, records;
    static readonly List<string> checks = new List<string>();
    static readonly List<string> screenshots = new List<string>();
    static T F<T>(object o,string n)=>ArtAcceptanceChecks.Field<T>(o,n);
    static void Check(bool value,string text){if(!value)throw new Exception("说明2验收："+text);checks.Add(text);}
    static void Click(Component c)=>ArtAcceptanceChecks.Pointer(c);
    static Button HUD(string name)=>UIManager.Instance.Get<HUDPanel>().ButtonRoot.Find(name+"/"+name+"Button").GetComponent<Button>();
    static void Option(string text)
    {
        var item=UnityEngine.Object.FindObjectsOfType<DialogueOptionItem>().First(o=>F<Button>(o,"_button").interactable&&F<Text>(o,"_text").text.Contains(text));Click(F<Button>(item,"_button"));
    }
    public static void ConfirmRoute()
    {
        var route=UIManager.Instance.Get<RouteSelectionPanel>();
        Check(route!=null&&route.IsOpen,"接取后进入原棋盘指路弹窗");
        var view=BackpackBoard.Instance.Items.First(v=>v!=null&&v.Item!=null&&v.Def.Id=="101");
        Click(view.GetComponentsInChildren<Image>().First(i=>i.gameObject.activeInHierarchy));
        var detail=UIManager.Instance.Get<DescriptionPanel>();
        Check(detail.IsOpen&&detail.IsRouteContext,"点击魔物复用现有详情面板");
        var button=F<Button>(detail,"_routeConfirmButton");Click(button);button.onClick.Invoke();
        Check(!RouteSelectionPanel.IsShowing,"确认之后关闭弹窗并还原原棋盘");
    }
    public static bool Preparation()
    {
        if(busy)return true;if(prepStep>=11)return false;
        var ui=UIManager.Instance;var prep=ui.Get<PreparationPanel>();var hell=ui.Get<DecomposePanel>();
        switch(prepStep++)
        {
            case 0:
                Check(!HUD("CharacterArchive").interactable,"整备阶段档案按钮置灰");
                Click(F<Button>(prep,"_abyssButton"));Shot(prep,"01_祭坛布局");break;
            case 1:
                Click(F<Button>(ui.Get<AbyssalAltarPanel>(),"_returnButton"));
                pendingMonster=BackpackBoard.Instance.Items.First(v=>v!=null&&v.Item!=null).Item;
                hell=ui.Get<DecomposePanel>();hell.Accept(pendingMonster);BackpackBoard.Instance.RemoveItem(pendingMonster);pendingMonster.View.gameObject.SetActive(false);
                Shot(prep,"02_地狱文字与分解槽");break;
            case 2:
                Click(F<Button>(ui.Get<AbyssalAltarPanel>(),"_newMonsterButton"));
                Check(!ui.IsOpen<AbyssalAltarPanel>()&&ui.IsOpen<ShopPanel>(),"祭坛进入二级魔物商店不叠加");
                Check(hell.IsOpen&&hell.PendingItem==pendingMonster,"进入二级商店保留地狱待分解实例");Shot(prep,"03_二级魔物商店与独立地狱");break;
            case 3:
                Click(F<Button>(ui.Get<ShopPanel>(),"_closeButton"));Check(ui.IsOpen<AbyssalAltarPanel>()&&!ui.IsOpen<ShopPanel>(),"二级魔物商店返回祭坛");break;
            case 4:
                ShopPanel.OpenItemShop();Check(!ui.IsOpen<AbyssalAltarPanel>()&&hell.PendingItem==pendingMonster,"道具商店替换祭坛且保留地狱");Shot(prep,"04_道具商店与地狱");break;
            case 5:
                ShopPanel.OpenMonsterStorage();Check(ui.Get<ShopPanel>().IsStorageMode&&hell.PendingItem==pendingMonster,"仓库切换保留右侧地狱");break;
            case 6:
                Click(HUD("Backpack"));Check(!ui.IsOpen<ShopPanel>()&&ui.IsOpen<BackpackPanel>()&&hell.PendingItem==pendingMonster,"顶部背包替换左侧页面且保留地狱");Shot(prep,"05_背包与地狱");break;
            case 7:
                Click(HUD("Backpack"));Check(!ui.IsOpen<AbyssalAltarPanel>()&&!ui.IsOpen<ShopPanel>(),"一级页面返回不意外恢复旧祭坛");
                Click(F<Button>(hell,"_closeButton"));Check(BackpackBoard.Instance.Items.Any(v=>v!=null&&v.Item==pendingMonster),"单独退出地狱退回未分解魔物");break;
            case 8:
                var toast=ToastManager.Instance;Check(Mathf.Abs(F<float>(toast,"duration")-2.25f)<.001f,"提示完整时长为2.25秒");Check(F<Vector2>(toast,"anchorOffset").y>=350,"提示锚点移至头部上方");
                ToastManager.Instance.Clear();ToastManager.Show("Myth的理智上升了，坚定上升了，宽容下降了。");Shot(prep,"06_顶部半透明黑色提示");break;
            case 9:
                Click(F<Button>(prep,"_shopButton"));CheckBack(F<Button>(ui.Get<ShopPanel>(),"_closeButton"),(RectTransform)ui.Get<ShopPanel>().transform.Find("BGImage"));ui.Close<ShopPanel>();break;
            case 10:Write(false);break;
        }
        return true;
    }
    static void CheckBack(Button button,RectTransform frame)
    {
        var corners=new Vector3[4];((RectTransform)button.transform).GetWorldCorners(corners);
        Check(corners.All(v=>frame.rect.Contains(frame.InverseTransformPoint(v))),"返回按钮完整位于框体内部且保留尺寸");
    }
    public static bool Reception(NarrativeSystem campaign,NarrativeRuntime runtime,NarrativeSystem.SaveData save,TalkPanel talk)
    {
        if(done)return false;if(busy)return true;
        var ui=UIManager.Instance;var run=SystemManager.Instance.RunState.run;
        bool menu=save.stage=="playing"&&save.frame?.nodes[0].options.Any(o=>o.callbackKey=="open_commission_panel")==true&&UnityEngine.Object.FindObjectsOfType<DialogueOptionItem>().Any(o=>F<Button>(o,"_button").interactable&&F<Text>(o,"_text").text.Contains("查看委托"));
        if(routeStep==0&&(!menu||SystemManager.Instance.VisitSystem.CurrentVisitorId!=1||run.adventurers[1].validInfo.Count==0))return false;
        var panel=ui.Get<CommissionPanel>();var route=ui.Get<RouteSelectionPanel>();var detail=ui.Get<DescriptionPanel>();
        switch(routeStep)
        {
            case 0:
                Check(HUD("CharacterArchive").interactable,"冒险者来访后档案按钮恢复");
                Option("查看委托");routeStep++;return true;
            case 1:
                if(panel==null||!panel.IsOpen){talk.MoveNext();return true;}
                ink=runtime.Story.state.ToJson();cash=run.cash;ap=run.actionPoint;
                Click(F<Button>(panel,"btn_AcceptCommission"));
                Check(RouteSelectionPanel.IsShowing&&SystemManager.Instance.DailyCommission.NormalCommissions.Count==0,"普通委托打开指路不入队");
                Check(runtime.Story.state.ToJson()==ink,"打开指路不提前执行原稿接单标签");
                Shot(talk,"07_真实棋盘模态指路");routeStep++;return true;
            case 2:
                var board=BackpackBoard.Instance;var view=board.Items.First(v=>v!=null&&v.Item!=null&&v.Def.Id=="101");uid=view.Item.Uid;
                var before=view.Item.Origin;int count=board.Items.Count;
                view.OnBeginDrag(new PointerEventData(EventSystem.current));Check(board.Items.Count==count&&view.Item.Origin==before,"指路期间拖拽不会移走魔物");
                Click(view.GetComponentsInChildren<Image>().First(i=>i.gameObject.activeInHierarchy));
                Check(detail==null||!detail.IsOpen||detail.IsRouteContext,"详情上下文隔离");
                Check(SystemManager.Instance.DailyCommission.NormalCommissions.Count==0,"查看魔物详情仍不接单");
                Shot(talk,"08_魔物详情内置确认");routeStep++;return true;
            case 3:
                Check(detail!=null&&detail.IsOpen&&F<Button>(detail,"_routeConfirmButton").gameObject.activeInHierarchy,"确认按钮只在指路详情显示");
                Click(F<Button>(detail,"_confirmButton"));Check(RouteSelectionPanel.IsShowing&&!detail.IsOpen,"详情返回棋盘");
                Click(F<Button>(route,"_returnButton"));Check(panel.IsOpen&&!RouteSelectionPanel.IsShowing,"棋盘返回来源委托详情");
                Check(runtime.Story.state.ToJson()==ink&&SystemManager.Instance.DailyCommission.NormalCommissions.Count==0,"取消指路不改剧情或队列");routeStep++;return true;
            case 4:
                Click(F<Button>(panel,"btn_AcceptCommission"));
                Click(BackpackBoard.Instance.Items.First(v=>v?.Item?.Uid==uid).GetComponentsInChildren<Image>().First(i=>i.gameObject.activeInHierarchy));
                records=run.adventurers[1].historyDialogue.Count;SystemManager.Instance.RunState.SaveSlot(4);
                routeStep++;SceneManager.LoadScene("PlayerGame");return true;
            case 5:
                if(!RouteSelectionPanel.IsShowing||detail==null||!detail.IsOpen)return true;
                Check(save.pendingRoute!=null&&save.pendingRoute.selectedUid==uid&&save.pendingRoute.detailOpen,"读档恢复指路实例与魔物详情");
                Check(run.cash==cash&&run.actionPoint==ap&&run.adventurers[1].historyDialogue.Count==records&&SystemManager.Instance.DailyCommission.NormalCommissions.Count==0,"指路读档不接单不扣款不重复记录");
                Shot(talk,"09_读档恢复指路详情");routeStep++;return true;
            case 6:
                if(RouteSelectionPanel.IsShowing){ui.HandleEscape();ui.HandleEscape();ui.HandleEscape();return true;}
                if(!menu){talk.MoveNext();return true;}
                // 构造真实旧版待指路存档，验证只撤回当前未完成的一单。
                var sceneId=runtime.Get("current_scene").ToString();var rules=F<NarrativeSystem.Rules>(campaign,"_rules");var rule=rules.scenes.First(r=>r.id==sceneId);
                SystemManager.Instance.DailyCommission.AddNormal(rule.contractId,1);save.stage="route";save.frame=null;save.uiVersion=3;
                campaign.Save();routeStep++;SceneManager.LoadScene("PlayerGame");return true;
            case 7:
                if(!RouteSelectionPanel.IsShowing)return true;
                Check(save.uiVersion==4&&SystemManager.Instance.DailyCommission.NormalCommissions.Count==0,"旧版提前接单迁移为待确认");
                Check(SystemManager.Instance.DailyCommission.SpecialCommissions.Count==1,"迁移未清除已经确认的特殊委托");
                ui.HandleEscape();ui.HandleEscape();routeStep++;return true;
            case 8:
                if(!menu){talk.MoveNext();return true;}
                done=true;Write(true);return false;
        }
        return false;
    }
    static void Shot(MonoBehaviour owner,string name){busy=true;owner.StartCoroutine(Capture(name));}
    static IEnumerator Capture(string name)
    {
        yield return null;
        var camera=Camera.main;var rt=new RenderTexture(1920,1080,24);var old=camera.targetTexture;var active=RenderTexture.active;
        var canvases=UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas).ToArray();var modes=canvases.Select(c=>c.renderMode).ToArray();var cameras=canvases.Select(c=>c.worldCamera).ToArray();
        foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=1;}
        camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
        var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();
        string folder=Path.GetFullPath("../qa/Doc2");Directory.CreateDirectory(folder);var path=Path.Combine(folder,name+".png");File.WriteAllBytes(path,image.EncodeToPNG());screenshots.Add(path);
        for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];}camera.targetTexture=old;RenderTexture.active=active;
        UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(rt);busy=false;Write(done);
    }
    public static void Write(bool passed)=>File.WriteAllText("Doc2Acceptance.json",JsonConvert.SerializeObject(new{passed,prepStep,routeStep,checks,screenshots},Formatting.Indented));
}
#endif

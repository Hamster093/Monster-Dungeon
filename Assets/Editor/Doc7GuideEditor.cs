#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>只给既有面板增加问号入口和高亮引用；原标题、委托、四槽和经营布局保持编辑器配置。</summary>
public static class Doc7GuideEditor
{
    static Font font;
    static readonly Color Paper=new Color(.95f,.89f,.76f,1),Gold=new Color(.92f,.70f,.40f,1);
    [MenuItem("Tools/说明7/配置首次引导与帮助")]
    public static void Configure()
    {
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Art/Fonts/FangZhengShuSong.ttf");
        CreateOverlay();
        Bind("PreparationPanel","preparation","RightPanel/UIRepairTitleStrip",new[]{"GridBackpack/Board","IncomeStrip"});
        Bind("ShopPanel","shop","UIRepairTitleStrip",new[]{"Scroll View"});
        Bind("BackpackPanel","backpack","UIRepairTitleStrip",new[]{"Scroll View"});
        Bind("AbyssalAltarPanel","altar","UIRepairTitleStrip",new[]{"LeftPanel"});
        Bind("DecomposePanel","decompose","UIRepairTitleStrip",new[]{"Window"});
        Bind("TalkPanel/CommissionPanel","commission","UIRepairTitleStrip",new[]{"img_BG"});
        Bind("TalkPanel/CharacterArchivePanel","archive","UIRepairTitleStrip",new[]{"Doc2RecordViewport"});
        Bind("RouteSelectionPanel","route","Instruction",new[]{"BoardHost"});
        Edit("EvolutionPanel",root=>{
            var frame=Require(root.transform,"Frame");
            var area=GuideArea(frame,"Doc7DirectionGuideArea",155,26,690,480);
            Attach(root,"evolution",Require(frame,"Title"),new[]{area});
        });
        Bind("DiceCheckPanel","dice","Reveal/Frame/Title",new[]{"Reveal/Frame"});
        Bind("DailySettlementPanel","settlement","Doc3Title",new[]{"NotesScroll"});
        Bind("SettingsPanel","saves","txt_settings_title",new[]{"btn_save_game","btn_read_save"});
        Edit("PlayerBuildPanel",root=>{
            var p=root.GetComponent<PlayerBuildPanel>();
            var so=new SerializedObject(p);
            var choice=(GameObject)so.FindProperty("_choiceRoot").objectReferenceValue;
            var status=(GameObject)so.FindProperty("_statusRoot").objectReferenceValue;
            Attach(choice,"player_type",Find(choice.transform,"Title"),new[]{Find(choice.transform,"DetailScroll")});
            Attach(status,"player_status",Find(status.transform,"Header"),new[]{Find(status.transform,"Attributes")});
        });
        Edit("TalkPanel/TalkPanel",root=>{
            var right=Require(root.transform,"RightDisplayArea");
            // 接待入口归属整个TalkPanel；右侧的嵌套Panel并非UI栈中的当前页面。
            // 复用原问号，只移动引导引用，正文/选项和原布局均不改动。
            var previous=right.GetComponent<GuideTopicAnchor>();
            if(previous){var owner=root.GetComponent<GuideTopicAnchor>()??root.AddComponent<GuideTopicAnchor>();owner.help=previous.help;UnityEngine.Object.DestroyImmediate(previous);}
            var anchor=Attach(root,"reception",right,new[]{right});
            At((RectTransform)anchor.help.transform,0,0,52,52);anchor.help.transform.SetParent(right,false);
            var r=(RectTransform)anchor.help.transform;r.anchorMin=r.anchorMax=new Vector2(1,1);r.anchoredPosition=new Vector2(-55,-28);
        });
        Edit("EndingPresentationPanel",root=>{
            var select=Find(root.transform,"Selection");
            Attach(select.gameObject,"ending",Find(select,"Title"),new[]{Find(select,"Actor1")});
        });
        const string galleryPath="Assets/Resources/UI/CGGalleryPanel.prefab";
        var galleryRoot=PrefabUtility.LoadPrefabContents(galleryPath);
        try {
            var list=Find(galleryRoot.transform,"List");
            Attach(list.gameObject,"gallery",Find(list,"Title"),new[]{Find(list,"UnlockedGrid")});
            PrefabUtility.SaveAsPrefabAsset(galleryRoot,galleryPath);
        } finally { PrefabUtility.UnloadPrefabContents(galleryRoot); }
        AudioIntegrationEditor.LayoutOnly();AssetDatabase.SaveAssets();Debug.Log("DOC7_GUIDE_CONFIGURED");
    }
    static void Bind(string prefab,string id,string title,string[] targets)
    {Edit(prefab,root=>Attach(root,id,Require(root.transform,title),targets.Select(n=>Require(root.transform,n)).ToArray()));}
    static GuideTopicAnchor Attach(GameObject root,string id,Transform title,Transform[] targets)
    {
        var anchor=root.GetComponent<GuideTopicAnchor>()??root.AddComponent<GuideTopicAnchor>();anchor.topic=id;
        var parent=title?title:root.transform;
        // 重复配置时沿用原入口，防止旧标题下遗留第二个问号。
        if(anchor.help&&anchor.help.transform.parent!=parent)anchor.help.transform.SetParent(parent,false);
        var button=Button(parent,"MechanismHelp","?",0,0,52,52,34);
        var rect=(RectTransform)button.transform;rect.anchorMin=rect.anchorMax=new Vector2(1,.5f);rect.anchoredPosition=new Vector2(-29,0);
        button.image.color=new Color(.045f,.032f,.025f,.9f);button.GetComponentInChildren<Text>().color=Gold;
        anchor.help=button;anchor.highlights=targets.Select(t=>t as RectTransform).Where(t=>t).ToArray();
        if(anchor.highlights.Length==0)throw new InvalidOperationException("引导缺少可见高亮区域："+id);
        return anchor;
    }
    static void CreateOverlay()
    {
        var go=new GameObject("GuideHelpOverlay",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(GuideHelpOverlay));
        var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32762;
        var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        var panel=go.GetComponent<GuideHelpOverlay>();Set(panel,"_root",go.transform);
        var tutorial=Node(go.transform,"Tutorial");Stretch((RectTransform)tutorial.transform);Set(panel,"_tutorialGroup",tutorial.AddComponent<CanvasGroup>());
        var dims=Enumerable.Range(0,4).Select(i=>Image(tutorial.transform,"Dim"+i,0,0,1920,1080,new Color(0,0,0,.84f))).ToArray();SetArray(panel,"_dimmers",dims);
        var advance=Button(tutorial.transform,"Advance","",0,0,1920,1080,24);Stretch((RectTransform)advance.transform);advance.image.color=Color.clear;advance.navigation=new Navigation{mode=Navigation.Mode.None};Set(panel,"_advance",advance);
        var edges=Enumerable.Range(0,4).Select(i=>Image(tutorial.transform,"Highlight"+i,0,0,1,1,new Color(.86f,.67f,.37f,.8f))).ToArray();SetArray(panel,"_outlines",edges);
        var copy=Node(tutorial.transform,"Copy");At((RectTransform)copy.transform,490,0,750,350);Set(panel,"_tutorialCopy",copy.transform);
        Set(panel,"_tutorialTitle",Text(copy.transform,"Title","",0,170,690,64,40));
        var body=Text(copy.transform,"Body","",0,0,690,270,32);body.lineSpacing=1.3f;Set(panel,"_tutorialText",body);
        var step=Text(copy.transform,"Step","",0,-170,690,42,24);step.color=Gold;Set(panel,"_stepText",step);
        var skip=Button(copy.transform,"Skip","略过本项",0,-230,180,48,24);skip.image.color=Color.clear;Set(panel,"_skip",skip);
        var help=Node(go.transform,"Help");Stretch((RectTransform)help.transform);Set(panel,"_helpGroup",help.AddComponent<CanvasGroup>());
        var dim=Image(help.transform,"Dim",0,0,1920,1080,new Color(0,0,0,.78f));Stretch(dim.rectTransform);dim.raycastTarget=true;Set(panel,"_helpDim",dim);
        var card=Image(help.transform,"RuleCard",0,0,1100,780,new Color(.035f,.031f,.03f,.97f));card.raycastTarget=true;Set(panel,"_helpCard",card.rectTransform);
        // 说明页采用简单实色正文底，尺寸变化不会拉扯原金属纹理。
        var line=Image(card.transform,"TopLine",0,0,1020,1,Gold);line.rectTransform.anchorMin=line.rectTransform.anchorMax=new Vector2(.5f,1);line.rectTransform.anchoredPosition=new Vector2(0,-32);
        Set(panel,"_helpTitle",Text(card.transform,"Title","",0,316,960,64,44));
        var main=Text(card.transform,"Main","",0,182,960,194,32);main.lineSpacing=1.35f;main.alignment=TextAnchor.MiddleLeft;Set(panel,"_mainText",main);
        var details=Button(card.transform,"Details","详细信息  ＋",0,44,320,52,28);details.image.color=Color.clear;Set(panel,"_detailsButton",details);Set(panel,"_detailsLabel",details.GetComponentInChildren<Text>());
        var scroll=Node(card.transform,"DetailScroll").AddComponent<ScrollRect>();At((RectTransform)scroll.transform,0,-118,960,248);scroll.horizontal=false;scroll.vertical=true;scroll.scrollSensitivity=30;scroll.movementType=ScrollRect.MovementType.Clamped;
        var viewport=Image(scroll.transform,"Viewport",-8,0,934,248,Color.clear);viewport.gameObject.AddComponent<RectMask2D>();viewport.raycastTarget=true;
        var content=Text(viewport.transform,"Text","",0,0,934,248,28);content.alignment=TextAnchor.UpperLeft;content.lineSpacing=1.25f;content.verticalOverflow=VerticalWrapMode.Overflow;content.rectTransform.anchorMin=new Vector2(0,1);content.rectTransform.anchorMax=Vector2.one;content.rectTransform.pivot=new Vector2(.5f,1);content.rectTransform.anchoredPosition=Vector2.zero;content.rectTransform.sizeDelta=new Vector2(0,248);
        scroll.viewport=viewport.rectTransform;scroll.content=content.rectTransform;Set(panel,"_detailText",content);Set(panel,"_detailsScroll",scroll);
        var track=Image(scroll.transform,"Track",478,0,5,248,new Color(.3f,.25f,.18f,.45f));var bar=track.gameObject.AddComponent<Scrollbar>();var handle=Image(track.transform,"Handle",0,0,5,50,Gold);Stretch(handle.rectTransform);bar.handleRect=handle.rectTransform;bar.targetGraphic=handle;bar.direction=Scrollbar.Direction.BottomToTop;scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        var replay=Button(card.transform,"Replay","重看引导",-235,-328,300,62,28);Set(panel,"_replay",replay);
        var close=Button(card.transform,"Close","返回",235,-328,300,62,28);Set(panel,"_close",close);
        details.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnDown=replay};replay.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnUp=details,selectOnRight=close};close.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnUp=details,selectOnLeft=replay};
        PrefabUtility.SaveAsPrefabAsset(go,"Assets/Resources/Panel/GuideHelpOverlay.prefab");UnityEngine.Object.DestroyImmediate(go);
    }
    static void Edit(string name,Action<GameObject> action){string path="Assets/Resources/Panel/"+name+".prefab";var root=PrefabUtility.LoadPrefabContents(path);try{action(root);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}}
    // 同名旧装饰可能被禁用；优先按完整路径查找，其次选择当前启用的同名对象。
    // 不检查根节点自身activeSelf：进化等弹窗预制体原本就是关闭状态。
    static Transform Find(Transform root,string name)
    {
        if(!root)return null;
        var exact=root.Find(name);if(exact&&Available(root,exact))return exact;
        return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name&&Available(root,t));
    }
    static bool Available(Transform root,Transform target)
    {for(var t=target;t&&t!=root;t=t.parent)if(!t.gameObject.activeSelf)return false;return true;}
    static Transform Require(Transform root,string name)
    {var value=Find(root,name);if(!value)throw new InvalidOperationException("引导引用不存在或位于关闭的旧界面："+root.name+"/"+name);return value;}
    static Transform GuideArea(Transform parent,string name,float x,float y,float width,float height)
    {var area=Node(parent,name);At((RectTransform)area.transform,x,y,width,height);return area.transform;}
    static GameObject Node(Transform parent,string name){var old=parent.Find(name);if(old)return old.gameObject;var go=new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);return go;}
    static Image Image(Transform p,string name,float x,float y,float w,float h,Color c){var go=Node(p,name);var im=go.GetComponent<Image>()??go.AddComponent<Image>();im.color=c;im.raycastTarget=false;At(im.rectTransform,x,y,w,h);return im;}
    static Text Text(Transform p,string name,string value,float x,float y,float w,float h,int size){var go=Node(p,name);var text=go.GetComponent<Text>()??go.AddComponent<Text>();text.font=font;text.fontSize=size;text.color=Paper;text.text=value;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;At(text.rectTransform,x,y,w,h);return text;}
    static Button Button(Transform p,string name,string value,float x,float y,float w,float h,int size){var image=Image(p,name,x,y,w,h,new Color(.18f,.12f,.07f,.9f));image.raycastTarget=true;var button=image.GetComponent<Button>()??image.gameObject.AddComponent<Button>();button.targetGraphic=image;var colors=button.colors;colors.highlightedColor=colors.selectedColor=new Color(1,.85f,.58f,1);colors.pressedColor=new Color(.65f,.47f,.26f,1);button.colors=colors;Text(button.transform,"Label",value,0,0,w-12,h-8,size);return button;}
    static void At(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.sizeDelta=new Vector2(w,h);r.anchoredPosition=new Vector2(x,y);r.localScale=Vector3.one;}
    static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
    static void Set(UnityEngine.Object obj,string field,UnityEngine.Object value){var s=new SerializedObject(obj);s.FindProperty(field).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    static void SetArray<T>(UnityEngine.Object obj,string field,T[] values)where T:UnityEngine.Object{var s=new SerializedObject(obj);var a=s.FindProperty(field);a.arraySize=values.Length;for(int i=0;i<values.Length;i++)a.GetArrayElementAtIndex(i).objectReferenceValue=values[i];s.ApplyModifiedPropertiesWithoutUndo();}
}
#endif

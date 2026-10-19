#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>检定和每日结算的布局只写入原预制体；不创建第二套业务面板。</summary>
public static class ConversationChecksUiEditor
{
    private static Font font;
    private static Sprite frameSprite,buttonSprite;
    private static readonly Color Paper=new Color(.95f,.9f,.79f,1);
    private static readonly Color Gold=new Color(1,.87f,.61f,1);
    private static readonly Color Muted=new Color(.82f,.72f,.57f,1);
    private static readonly Color Rule=new Color(.62f,.43f,.22f,.75f);

    [MenuItem("Tools/UI打磨/检定与每日结算")]
    public static void Configure()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("请退出播放后保存界面布局。");
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Art/Fonts/FangZhengShuSong.ttf");
        frameSprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Art/UI/Doc6LandscapeFrame.asset");
        buttonSprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Art/UI/Doc6SmallFrame.asset");
        if(!font||!frameSprite||!buttonSprite)throw new InvalidOperationException("缺少检定和结算所需字体或横版分件，请先运行说明6通用分件配置。");
        Edit("Assets/Resources/Panel/DiceCheckPanel.prefab",ConfigureDice);
        AssetDatabase.SaveAssets();
        Debug.Log("CONVERSATION_CHECKS_UI_CONFIGURED");
    }

    private static void ConfigureDice(GameObject root)
    {
        var panel=root.GetComponent<DiceCheckPanel>();
        ResetRoot(root,new Color(0,0,0,.58f));
        // 背景立即拦住输入，仅内容随原有骰点演出淡入；不重复压暗背景。
        var reveal=Node(root.transform,"Reveal");Stretch(R(reveal));
        var fade=reveal.AddComponent<CanvasGroup>();Set(panel,"_fade",fade);
        var frame=Frame(reveal.transform);
        At(R(frame),0,0,960,640);At(R(reveal.transform.Find("FrameBacking")),0,0,900,580);
        Text(frame.transform,"Caption","D100",0,265,830,30,22,Muted);
        var title=Text(frame.transform,"Title","",0,212,830,52,32,Gold);
        Divider(frame.transform,"HeaderRule",0,165,830,1);
        // 三个数值并排，结果居中；来源只占小字区，不保留大块规则栏。
        Text(frame.transform,"RollCaption","骰点",-270,127,230,34,24,Muted);
        Text(frame.transform,"ModifierCaption","加权",0,127,230,34,24,Muted);
        Text(frame.transform,"RequirementCaption","过关门槛",270,127,230,34,24,Muted);
        var roll=Text(frame.transform,"Roll","",-270,43,230,120,84,Gold);
        var modifier=Text(frame.transform,"Modifier","",0,43,230,120,64,Paper);
        var requirement=Text(frame.transform,"Requirement","",270,43,230,120,64,Paper);
        var outcome=Text(frame.transform,"Outcome","",0,-60,830,66,46,Gold);
        var total=Text(frame.transform,"Total","",0,0,1,1,24,Paper);total.gameObject.SetActive(false);
        var noteScroll=Scroll(frame.transform,"ModifierSources",0,-150,830,78,out var note);
        note.fontSize=22;note.lineSpacing=1.1f;note.alignment=TextAnchor.UpperCenter;
        Divider(frame.transform,"FooterRule",0,-211,830,1);
        var actions=Node(frame.transform,"Actions");At(R(actions),0,-263,830,66);
        var group=actions.AddComponent<HorizontalLayoutGroup>();group.spacing=24;
        group.childAlignment=TextAnchor.MiddleCenter;group.childControlHeight=group.childControlWidth=true;
        group.childForceExpandHeight=group.childForceExpandWidth=false;
        var coin=Button(actions.transform,"Coin","重掷 · 幸运硬币×1",340,64);
        var next=Button(actions.transform,"Continue","继续",340,64);
        Set(panel,"_title",title);Set(panel,"_requirement",requirement);Set(panel,"_rollText",roll);
        Set(panel,"_outcome",outcome);Set(panel,"_note",note);Set(panel,"_modifierText",modifier);
        Set(panel,"_totalText",total);Set(panel,"_noteScroll",noteScroll);Set(panel,"_continue",next);Set(panel,"_coin",coin);
        coin.gameObject.SetActive(false);
    }

    private static void ConfigureSettlement(GameObject root)
    {
        var panel=root.GetComponent<DailySettlementPanel>();
        // 此面板使用 UIManager 的原遮罩，根节点只拦输入，避免双层黑幕。
        ResetRoot(root,Color.clear);
        var frame=Frame(root.transform);
        Text(frame.transform,"Title","今日账目",0,385,1180,68,44,Gold);
        var total=Text(frame.transform,"Total","",-386,238,350,146,28,Gold);
        var commission=Text(frame.transform,"Commission1","",0,238,350,146,28,Paper);
        var monster=Text(frame.transform,"Monster","",386,238,350,146,28,Paper);
        Divider(frame.transform,"SummaryRule1",-194,239,2,110);
        Divider(frame.transform,"SummaryRule2",194,239,2,110);
        Divider(frame.transform,"HeaderRule",0,139,1190,2);
        var heading=Text(frame.transform,"NotesHeading","今日结果",0,96,1170,45,28,Muted);
        heading.alignment=TextAnchor.MiddleLeft;
        var scroll=Scroll(frame.transform,"NotesScroll",0,-103,1190,330,out var notes);
        notes.fontSize=30;notes.lineSpacing=1.28f;
        Divider(frame.transform,"FooterRule",0,-300,1190,2);
        var installment=Text(frame.transform,"InstallmentDue","",-253,-361,670,72,28,Muted);
        installment.alignment=TextAnchor.MiddleLeft;
        var close=Button(frame.transform,"btn_Close","收款并继续",410,76);At(R(close),391,-361,410,76);
        // 序列字段保留，旧存档中废弃的特殊委托栏始终隐藏。
        var special=Text(frame.transform,"Commission2","",0,0,1,1,28,Paper);special.gameObject.SetActive(false);
        Set(panel,"txt_LegacyCommission",commission);Set(panel,"txt_SpecialCommission",special);
        Set(panel,"txt_MonsterBusiness",monster);Set(panel,"txt_Total",total);Set(panel,"txt_Installment",installment);
        Set(panel,"btn_Close",close);Set(panel,"_detailsText",notes);Set(panel,"_detailsScroll",scroll);
    }

    private static void ConfigureShopMetadata(GameObject root)
    {
        var shop=root.GetComponent<ShopPanel>();
        var grades=new Text[shop._itemSlots.Length];
        var prices=new GameObject[shop._itemSlots.Length];
        for(int i=0;i<shop._itemSlots.Length;i++)
        {
            var slot=shop._itemSlots[i];
            var priceArt=slot.transform.Find("ArtPrice");
            if(priceArt){priceArt.gameObject.SetActive(true);prices[i]=priceArt.gameObject;}
            var label=new SerializedObject(slot).FindProperty("_subText").objectReferenceValue as Text;
            if(label)
            {
                // 购买底栏恢复原钱袋与单行价格，不加入类别或实例状态。
                At(R(label),0,-82,180,45);label.font=font;label.fontSize=24;label.resizeTextForBestFit=false;
                label.supportRichText=true;label.alignment=TextAnchor.MiddleCenter;label.lineSpacing=1;
                label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;label.raycastTarget=false;
            }
            var oldBadge=slot.transform.Find("MonsterGradeBadge");if(oldBadge)UnityEngine.Object.DestroyImmediate(oldBadge.gameObject);
            var badge=Image(slot.transform,"MonsterGradeBadge",null,58,79,62,34,new Color(.045f,.035f,.025f,.86f));
            grades[i]=Text(badge.transform,"Grade","",0,0,54,32,24,Gold);
            badge.gameObject.SetActive(false);
        }
        var serialized=new SerializedObject(shop);
        var gradeBindings=serialized.FindProperty("_monsterGrades");gradeBindings.arraySize=grades.Length;
        var priceBindings=serialized.FindProperty("_priceDecorations");priceBindings.arraySize=prices.Length;
        for(int i=0;i<grades.Length;i++){gradeBindings.GetArrayElementAtIndex(i).objectReferenceValue=grades[i];priceBindings.GetArrayElementAtIndex(i).objectReferenceValue=prices[i];}
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Edit(string path,Action<GameObject> configure)
    {
        var root=PrefabUtility.LoadPrefabContents(path);
        try{configure(root);PrefabUtility.SaveAsPrefabAsset(root,path);}
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }

    private static void ResetRoot(GameObject root,Color shade)
    {
        // 保留根面板组件及预制体 GUID，替换的是原有视觉子层。
        for(int i=root.transform.childCount-1;i>=0;i--)UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
        Stretch(R(root));
        var image=Ensure<Image>(root);image.sprite=null;image.type=UnityEngine.UI.Image.Type.Simple;image.color=shade;image.raycastTarget=true;
        var group=root.GetComponent<CanvasGroup>();if(group){group.alpha=1;group.interactable=true;group.blocksRaycasts=true;}
    }

    private static Image Frame(Transform parent)
    {
        Image(parent,"FrameBacking",null,0,0,1320,860,new Color(.055f,.042f,.034f,1));
        var image=Image(parent,"Frame",frameSprite,0,0,1380,920,Color.white,true);
        // 素材本身是 3:2，此处整体等比显示，保留边角与皮革纹理。
        image.type=UnityEngine.UI.Image.Type.Simple;image.preserveAspect=true;
        return image;
    }

    private static ScrollRect Scroll(Transform parent,string name,float x,float y,float w,float h,out Text text)
    {
        var node=Node(parent,name);At(R(node),x,y,w,h);
        var scroll=node.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.vertical=true;
        scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=38;scroll.inertia=true;
        var viewport=Node(node.transform,"Viewport");Stretch(R(viewport),0,28,0,0);
        var hit=viewport.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;viewport.AddComponent<RectMask2D>();
        text=Text(viewport.transform,"Content","",0,0,w-40,h,30,Paper);
        text.alignment=TextAnchor.UpperLeft;text.verticalOverflow=VerticalWrapMode.Overflow;
        var content=R(text);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;
        content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;content.sizeDelta=new Vector2(-12,h);
        var track=Image(node.transform,"Scrollbar",null,w/2-5,0,8,h,new Color(.12f,.085f,.05f,.85f),true);
        var scrollbar=track.gameObject.AddComponent<Scrollbar>();scrollbar.direction=Scrollbar.Direction.BottomToTop;
        var slide=Node(track.transform,"SlidingArea");Stretch(R(slide));
        var handle=Image(slide.transform,"Handle",null,0,0,8,60,new Color(.74f,.57f,.34f,1),true);Stretch(R(handle));
        scrollbar.handleRect=R(handle);scrollbar.targetGraphic=handle;
        scroll.viewport=R(viewport);scroll.content=content;scroll.verticalScrollbar=scrollbar;
        scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        return scroll;
    }

    private static Button Button(Transform parent,string name,string label,float w,float h)
    {
        var image=Image(parent,name,buttonSprite,0,0,w,h,Color.white,true);image.type=UnityEngine.UI.Image.Type.Sliced;
        var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;
        button.transition=Selectable.Transition.ColorTint;
        var colors=button.colors;colors.highlightedColor=colors.selectedColor=new Color(1,.92f,.74f,1);
        colors.pressedColor=new Color(.72f,.65f,.52f,1);colors.disabledColor=new Color(.4f,.38f,.34f,1);colors.fadeDuration=.12f;button.colors=colors;
        var layout=image.gameObject.AddComponent<LayoutElement>();layout.minWidth=layout.preferredWidth=w;layout.minHeight=layout.preferredHeight=h;layout.flexibleWidth=layout.flexibleHeight=0;
        Text(image.transform,"Label",label,0,0,w-38,h-12,32,Paper);
        return button;
    }

    private static void Divider(Transform parent,string name,float x,float y,float w,float h)=>Image(parent,name,null,x,y,w,h,Rule);
    private static Image Image(Transform parent,string name,Sprite sprite,float x,float y,float w,float h,Color color,bool hit=false)
    {var go=Node(parent,name);var image=go.AddComponent<Image>();image.sprite=sprite;image.color=color;image.raycastTarget=hit;At(R(image),x,y,w,h);return image;}
    private static Text Text(Transform parent,string name,string value,float x,float y,float w,float h,int size,Color color)
    {var go=Node(parent,name);var text=go.AddComponent<Text>();text.font=font;text.text=value;text.fontSize=size;text.color=color;text.supportRichText=true;text.alignment=TextAnchor.MiddleCenter;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;text.lineSpacing=1.12f;text.raycastTarget=false;At(R(text),x,y,w,h);return text;}
    private static GameObject Node(Transform parent,string name)
    {var go=new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);return go;}
    private static T Ensure<T>(GameObject go)where T:Component{var component=go.GetComponent<T>();return component?component:go.AddComponent<T>();}
    private static RectTransform R(GameObject go)=>(RectTransform)go.transform;
    private static RectTransform R(Component c)=>(RectTransform)c.transform;
    private static void At(RectTransform rect,float x,float y,float w,float h)
    {rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(w,h);rect.localScale=Vector3.one;}
    private static void Stretch(RectTransform rect,float left=0,float right=0,float top=0,float bottom=0)
    {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(left,bottom);rect.offsetMax=new Vector2(-right,-top);rect.localScale=Vector3.one;}
    private static void Set(UnityEngine.Object target,string field,UnityEngine.Object value)
    {var so=new SerializedObject(target);var property=so.FindProperty(field);if(property==null)throw new InvalidOperationException(target.name+" 缺少绑定 "+field);property.objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
}
#endif

#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>说明6补充布局：复用原面板和分件，只把布局、字号及新字段绑定保存到预制体。</summary>
public static class Doc6SupplementEditor
{
    private static Font font;
    private static Sprite frameSprite,buttonSprite;
    private static readonly Color Ink=new Color(.065f,.043f,.027f,.985f);
    private static readonly Color Gold=new Color(1f,.9f,.74f,1f);
    private static readonly Color MutedGold=new Color(.84f,.73f,.56f,1f);

    [MenuItem("Tools/说明6/补充详情终局与存档布局")]
    public static void Configure()
    {
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Art/Fonts/FangZhengShuSong.ttf");
        frameSprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Art/UI/Doc6LandscapeFrame.asset");
        buttonSprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Art/UI/Doc6SmallFrame.asset");
        if(!font||!frameSprite||!buttonSprite)throw new InvalidOperationException("说明6布局所需原字体或分件缺失。");
        Edit("Assets/Resources/Panel/EndingPresentationPanel.prefab",ConfigureEnding);
        AssetDatabase.SaveAssets();
    }

    private static void ConfigureDescription(GameObject root)
    {
        var panel=root.GetComponent<DescriptionPanel>();
        var frame=(RectTransform)root.transform.Find("ItemDescription");
        At(frame,0,-10,1140,760);
        var image=frame.GetComponent<Image>();image.enabled=true;Frame(image);
        var body=Image(frame,"Doc6OpaqueBody",null,0,0,1064,684,Ink);PlaceBacking(R(body),frame);
        var title=Text(frame,"Doc6ContextTitle","魔物详情",0,326,840,54,38);Set(panel,"_contextTitle",title);
        var back=Get<Button>(panel,"_confirmButton");Back(back,frame,-468,326);
        var icon=Get<Image>(panel,"_itemImage");icon.transform.SetParent(frame,false);At(R(icon),-374,195,170,170);icon.preserveAspect=true;icon.raycastTarget=false;
        var name=Get<Text>(panel,"_itemNameText");name.transform.SetParent(frame,false);At(R(name),99,239,650,62);Style(name,36,TextAnchor.MiddleLeft);
        var identity=Get<Text>(panel,"_quantityText");identity.transform.SetParent(frame,false);At(R(identity),99,172,650,66);Style(identity,28,TextAnchor.MiddleLeft);identity.color=MutedGold;
        Image(frame,"Doc6IdentityDivider",null,0,93,940,2,new Color(.62f,.42f,.2f,.85f));

        // 固定区域与正文共用自动布局，非指路浏览时正文自然获得整块空间。
        var stack=Node(frame,"Doc6DescriptionStack");At(R(stack),0,-98,940,350);
        var layout=Ensure<VerticalLayoutGroup>(stack);layout.padding=new RectOffset(0,0,0,0);layout.spacing=12;
        layout.childAlignment=TextAnchor.UpperLeft;layout.childControlWidth=layout.childControlHeight=true;
        layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
        var area=Get<GameObject>(panel,"_combatArea");area.transform.SetParent(stack.transform,false);
        var areaImage=Ensure<Image>(area);areaImage.sprite=null;areaImage.color=new Color(.16f,.105f,.055f,.88f);areaImage.raycastTarget=false;
        var areaLayout=Ensure<LayoutElement>(area);areaLayout.minHeight=areaLayout.preferredHeight=116;areaLayout.flexibleHeight=0;
        var summary=Text(area.transform,"Doc6RouteSummary","",0,21,902,72,28);summary.alignment=TextAnchor.UpperLeft;Set(panel,"_routeSummary",summary);
        var difficulty=Get<Text>(panel,"_combatDifficulty");difficulty.transform.SetParent(area.transform,false);At(R(difficulty),0,-37,902,38);Style(difficulty,28,TextAnchor.MiddleLeft);

        var scroll=Get<ScrollRect>(panel,"_descriptionScroll");scroll.transform.SetParent(stack.transform,false);
        var scrollLayout=Ensure<LayoutElement>(scroll.gameObject);scrollLayout.minHeight=180;scrollLayout.preferredHeight=222;scrollLayout.flexibleHeight=1;
        scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=32;
        var scrollBackground=Ensure<Image>(scroll.gameObject);scrollBackground.color=Ink;scrollBackground.raycastTarget=true;
        var oldMask=scroll.GetComponent<RectMask2D>();if(oldMask)UnityEngine.Object.DestroyImmediate(oldMask);
        var viewport=Node(scroll.transform,"Doc6DescriptionViewport");Stretch(R(viewport),0,24,0,0);Ensure<RectMask2D>(viewport);
        var viewImage=Ensure<Image>(viewport);viewImage.color=Color.clear;viewImage.raycastTarget=true;
        var description=Get<Text>(panel,"_itemDescriptionText");description.transform.SetParent(viewport.transform,false);Style(description,32,TextAnchor.UpperLeft);
        description.lineSpacing=1.15f;description.verticalOverflow=VerticalWrapMode.Overflow;
        var rect=R(description);rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=Vector2.zero;rect.sizeDelta=Vector2.zero;
        var fitter=description.GetComponent<ContentSizeFitter>();if(fitter)fitter.enabled=false;
        scroll.viewport=R(viewport);scroll.content=rect;
        scroll.verticalScrollbar=Scrollbar(scroll.transform,"Doc6DescriptionScrollbar");scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        scroll.verticalScrollbarSpacing=8;
        area.SetActive(false);
        var confirm=Get<Button>(panel,"_routeConfirmButton");confirm.transform.SetParent(frame,false);StyleButton(confirm,"确认指路",0,-326,290,70,32);
        confirm.gameObject.SetActive(false);
        // 页头、身份、指路和正文不共享文字层，避免滚动时相互覆盖。
        title.transform.SetAsLastSibling();back.transform.SetAsLastSibling();confirm.transform.SetAsLastSibling();
        foreach(var text in root.GetComponentsInChildren<Text>(true))text.font=font;
    }

    private static void ConfigureEnding(GameObject root)
    {
        var panel=root.GetComponent<EndingPresentationPanel>();
        var confirmation=Get<GameObject>(panel,"_confirmRoot");var frame=confirmation.transform.Find("Frame").GetComponent<Image>();
        Frame(frame);At(R(frame),0,0,1230,820);
        var body=Image(frame.transform,"Doc6OpaqueBody",null,0,0,1154,744,Ink);PlaceBacking(R(body),R(frame));
        var title=Text(frame.transform,"Doc6RequestTitle","我得想点办法……我应该从哪里谈起呢？",0,343,1070,60,36);Set(panel,"_requestTitle",title);
        var info=Get<Text>(panel,"_confirmText");At(R(info),0,225,1040,150);Style(info,30,TextAnchor.UpperLeft);info.lineSpacing=1.1f;
        var expressions=Node(frame.transform,"Doc6Expressions");At(R(expressions),0,-57,1040,354);
        var oldDivider=frame.transform.Find("Doc6ConfirmDivider");if(oldDivider)oldDivider.SetParent(expressions.transform,false);
        Image(expressions.transform,"Doc6ConfirmDivider",null,0,197,1040,2,new Color(.62f,.42f,.2f,.8f));
        var buttons=new Button[3];var texts=new Text[3];var marks=new Text[3];
        for(int i=0;i<3;i++) {
            var node=Node(expressions.transform,"Expression"+i);var background=Ensure<Image>(node);
            buttons[i]=Ensure<Button>(node);buttons[i].targetGraphic=background;
            // 这是选择表达的正文行，不使用操作按钮的厚框；整行仍保留相同点击与导航接口。
            At(R(node),0,118-i*118,1040,102);
            background.sprite=null;background.type=UnityEngine.UI.Image.Type.Simple;background.color=new Color(.13f,.078f,.04f,.92f);background.raycastTarget=true;
            buttons[i].transition=Selectable.Transition.ColorTint;
            var rowColors=buttons[i].colors;rowColors.normalColor=new Color(.75f,.75f,.75f,1);rowColors.highlightedColor=rowColors.selectedColor=Color.white;rowColors.pressedColor=new Color(.6f,.6f,.6f,1);buttons[i].colors=rowColors;
            texts[i]=Text(node.transform,"Label","",25,0,930,94,30);texts[i].alignment=TextAnchor.MiddleLeft;texts[i].lineSpacing=1.15f;
            marks[i]=Text(node.transform,"ChoiceMark","○",-483,0,42,60,32);
            Image(node.transform,"BottomRule",null,0,-51,1040,1,new Color(.65f,.45f,.23f,.58f));
        }
        Set(panel,"_expressionRoot",expressions);SetArray(panel,"_expressionButtons",buttons);SetArray(panel,"_expressionTexts",texts);SetArray(panel,"_expressionMarks",marks);
        var confirm=Get<Button>(panel,"_confirm");StyleButton(confirm,"就这样开口",225,-337,340,74,32);
        var cancel=Get<Button>(panel,"_cancel");StyleButton(cancel,"再想一想",-225,-337,340,74,32);
        Set(panel,"_confirmFrame",R(frame));Set(panel,"_confirmBacking",R(body));
        SetConfirmationLayout(panel,"_expandedLayout",new Vector2(1230,820),new Vector2(1154,744),new Vector2(0,343),new Vector2(1070,60),new Vector2(0,225),new Vector2(1040,150),new Vector2(225,-337),new Vector2(-225,-337));
        SetConfirmationLayout(panel,"_compactLayout",new Vector2(990,660),new Vector2(914,584),new Vector2(0,235),new Vector2(850,60),new Vector2(0,35),new Vector2(850,210),new Vector2(215,-228),new Vector2(-215,-228));
        var confirmNav=confirm.navigation;confirmNav.mode=Navigation.Mode.Explicit;confirmNav.selectOnLeft=cancel;confirmNav.selectOnRight=cancel;confirmNav.selectOnUp=buttons[2];confirm.navigation=confirmNav;
        var cancelNav=cancel.navigation;cancelNav.mode=Navigation.Mode.Explicit;cancelNav.selectOnLeft=confirm;cancelNav.selectOnRight=confirm;cancelNav.selectOnUp=buttons[2];cancel.navigation=cancelNav;
        for(int i=0;i<3;i++) {
            var nav=buttons[i].navigation;nav.mode=Navigation.Mode.Explicit;nav.selectOnUp=i>0?buttons[i-1]:buttons[2];nav.selectOnDown=i<2?buttons[i+1]:confirm;buttons[i].navigation=nav;
        }
        ConfigureEndingSelectionAndCompletion(panel);
        // 明确叠放顺序；底层经营控件由现有面板的 CanvasGroup 保留业务而隐藏视觉。
        Get<Image>(panel,"_dim").transform.SetAsFirstSibling();
        Get<GameObject>(panel,"_selectionRoot").transform.SetSiblingIndex(1);
        Get<GameObject>(panel,"_cgRoot").transform.SetSiblingIndex(2);
        Get<GameObject>(panel,"_completeRoot").transform.SetSiblingIndex(3);
        confirmation.transform.SetSiblingIndex(4);Get<Image>(panel,"_black").transform.SetAsLastSibling();
        foreach(var text in root.GetComponentsInChildren<Text>(true))text.font=font;
    }

    private static void ConfigureEndingSelectionAndCompletion(EndingPresentationPanel panel)
    {
        var selection=Get<GameObject>(panel,"_selectionRoot").transform;
        var actors=References<Button>(panel,"_actorButtons");var portraits=References<Image>(panel,"_portraits");
        var names=References<Text>(panel,"_names");var tendencies=References<Text>(panel,"_tendencies");
        var focus=new Image[actors.Length];float[] widths={225,220,260,228,225,330,240};float[] positions={-800,-535,-270,-8,240,530,825};
        for(int i=0;i<actors.Length;i++) {
            At(R(actors[i]),positions[i],0,Mathf.Max(244,widths[i]),655);
            float width=widths[i];var sprite=portraits[i].sprite;
            float height=sprite?width*sprite.rect.height/sprite.rect.width:430;
            At(R(portraits[i]),0,-190+height/2,width,height);portraits[i].preserveAspect=true;
            At(R(names[i]),0,-245,244,62);Style(names[i],i==2?25:29,TextAnchor.MiddleCenter);
            At(R(tendencies[i]),0,-300,244,42);Style(tendencies[i],23,TextAnchor.MiddleCenter);
            focus[i]=Image(actors[i].transform,"SelectionFocus",null,0,-331,180,2,new Color(1f,.74f,.32f,0));
        }
        SetArray(panel,"_actorFocus",focus);
        Text(selection,"SelectionHelp","方向键选择　确认键查看",0,-377,850,36,24);
        StyleButton(Get<Button>(panel,"_stay"),"不转让经营权",0,-448,340,68,29);
        StyleButton(Get<Button>(panel,"_settings"),"设置",815,-448,145,64,28);
        var hint=Get<Text>(panel,"_continueHint");At(R(hint),0,-475,760,42);Style(hint,27,TextAnchor.MiddleCenter);
        var shadow=Ensure<Shadow>(hint.gameObject);shadow.effectColor=new Color(0,0,0,.9f);shadow.effectDistance=new Vector2(1,-2);
        var complete=Get<GameObject>(panel,"_completeRoot").transform;
        var artwork=Image(complete,"CompletionArtwork",null,0,0,1920,1080,new Color(.34f,.34f,.34f,1));artwork.preserveAspect=true;Set(panel,"_completionImage",artwork);
        artwork.transform.SetAsFirstSibling();var background=complete.Find("Background");if(background)background.SetAsFirstSibling();
        Image(complete,"CompletionReadingBand",null,0,10,1640,205,new Color(0,0,0,.66f));
        var ending=Get<Text>(panel,"_endingText");At(R(ending),0,15,1550,130);Style(ending,42,TextAnchor.MiddleCenter);ending.transform.SetAsLastSibling();
        var returnTitle=Get<Button>(panel,"_returnTitle");StyleButton(returnTitle,"返回标题",0,-170,280,74,31);returnTitle.transform.SetAsLastSibling();
    }

    private static void ConfigureSaveSlots(GameObject root)
    {
        var panel=root.GetComponent<SaveSlotsPanel>();
        // 不重建四槽及其概要，只补一个独立覆盖确认层。
        var overlay=Node(root.transform,"Doc6OverwriteOverlay");Stretch(R(overlay),0,0,0,0);
        var blocker=Ensure<Image>(overlay);blocker.color=new Color(0,0,0,.72f);blocker.raycastTarget=true;
        var frame=Image(overlay.transform,"Frame",frameSprite,0,0,825,550,Color.white,true);Frame(frame);
        var body=Image(frame.transform,"Doc6OpaqueBody",null,0,0,749,474,Ink);PlaceBacking(R(body),R(frame));
        var text=Text(frame.transform,"OverwriteText","覆盖这个存档？",0,55,700,210,32);text.lineSpacing=1.2f;
        var confirm=Ensure<Button>(Node(frame.transform,"Confirm"));StyleButton(confirm,"确认覆盖",175,-178,280,68,30);
        var cancel=Ensure<Button>(Node(frame.transform,"Cancel"));StyleButton(cancel,"返回",-175,-178,280,68,30);
        var nav=confirm.navigation;nav.mode=Navigation.Mode.Explicit;nav.selectOnLeft=nav.selectOnRight=nav.selectOnUp=nav.selectOnDown=cancel;confirm.navigation=nav;
        nav=cancel.navigation;nav.mode=Navigation.Mode.Explicit;nav.selectOnLeft=nav.selectOnRight=nav.selectOnUp=nav.selectOnDown=confirm;cancel.navigation=nav;
        Set(panel,"_overwriteRoot",overlay);Set(panel,"_overwriteText",text);Set(panel,"_overwriteConfirm",confirm);Set(panel,"_overwriteCancel",cancel);
        overlay.transform.SetAsLastSibling();overlay.SetActive(false);
        foreach(var t in root.GetComponentsInChildren<Text>(true))t.font=font;
    }

    private static void Edit(string path,Action<GameObject> configure)
    {
        var root=PrefabUtility.LoadPrefabContents(path);
        try { configure(root);PrefabUtility.SaveAsPrefabAsset(root,path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static RectTransform R(Component component)=>(RectTransform)component.transform;
    private static RectTransform R(GameObject go)=>(RectTransform)go.transform;
    private static T Get<T>(UnityEngine.Object target,string field)where T:UnityEngine.Object=>new SerializedObject(target).FindProperty(field).objectReferenceValue as T;
    private static T[] References<T>(UnityEngine.Object target,string field)where T:UnityEngine.Object
    {var values=new SerializedObject(target).FindProperty(field);var result=new T[values.arraySize];for(int i=0;i<result.Length;i++)result[i]=values.GetArrayElementAtIndex(i).objectReferenceValue as T;return result;}
    private static void Set(UnityEngine.Object target,string field,UnityEngine.Object value)
    {var so=new SerializedObject(target);so.FindProperty(field).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
    private static void SetArray<T>(UnityEngine.Object target,string field,T[] values)where T:UnityEngine.Object
    {var so=new SerializedObject(target);var array=so.FindProperty(field);array.arraySize=values.Length;for(int i=0;i<values.Length;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=values[i];so.ApplyModifiedPropertiesWithoutUndo();}
    private static void SetConfirmationLayout(UnityEngine.Object target,string field,Vector2 frame,Vector2 backing,Vector2 titlePosition,Vector2 titleSize,Vector2 infoPosition,Vector2 infoSize,Vector2 confirmPosition,Vector2 cancelPosition)
    {
        var so=new SerializedObject(target);var preset=so.FindProperty(field);
        preset.FindPropertyRelative("frameSize").vector2Value=frame;preset.FindPropertyRelative("backingSize").vector2Value=backing;
        preset.FindPropertyRelative("titlePosition").vector2Value=titlePosition;preset.FindPropertyRelative("titleSize").vector2Value=titleSize;
        preset.FindPropertyRelative("infoPosition").vector2Value=infoPosition;preset.FindPropertyRelative("infoSize").vector2Value=infoSize;
        preset.FindPropertyRelative("confirmPosition").vector2Value=confirmPosition;preset.FindPropertyRelative("cancelPosition").vector2Value=cancelPosition;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static T Ensure<T>(GameObject go)where T:Component{var component=go.GetComponent<T>();return component?component:go.AddComponent<T>();}
    private static GameObject Node(Transform parent,string name)
    {
        var child=parent.Find(name);
        if(!child&&name=="Doc6OpaqueBody"&&parent.parent)child=parent.parent.Find(name);
        if(child)return child.gameObject;
        var go=new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);return go;
    }
    private static void At(RectTransform rect,float x,float y,float width,float height)
    {rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(width,height);rect.localScale=Vector3.one;}
    private static void Stretch(RectTransform rect,float left,float right,float bottom,float top)
    {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,.5f);rect.offsetMin=new Vector2(left,bottom);rect.offsetMax=new Vector2(-right,-top);rect.localScale=Vector3.one;}
    private static void PlaceBacking(RectTransform backing,RectTransform frame)
    {
        backing.SetParent(frame.parent,false);backing.anchoredPosition=frame.anchoredPosition;
        // 先移出旧位置再取框体序号，反复配置也始终位于框体与所有文字的下面。
        backing.SetAsLastSibling();backing.SetSiblingIndex(frame.GetSiblingIndex());
    }
    private static Sprite Old(string token)=>AssetDatabase.LoadAssetAtPath<Sprite>(Directory.GetFiles("Assets/Resources_moved/Icons","*.png",SearchOption.AllDirectories).First(path=>Path.GetFileName(path).Contains(token)).Replace('\\','/'));
    private static Image Image(Transform parent,string name,Sprite sprite,float x,float y,float width,float height,Color color,bool hit=false)
    {
        var image=Ensure<Image>(Node(parent,name));image.sprite=sprite;image.color=color;image.raycastTarget=hit;image.preserveAspect=false;At(R(image),x,y,width,height);return image;
    }
    private static Text Text(Transform parent,string name,string value,float x,float y,float width,float height,int size)
    {var text=Ensure<Text>(Node(parent,name));text.text=value;Style(text,size,TextAnchor.MiddleCenter);At(R(text),x,y,width,height);return text;}
    private static void Style(Text text,int size,TextAnchor alignment)
    {text.font=font;text.fontSize=size;text.fontStyle=FontStyle.Normal;text.resizeTextForBestFit=false;text.color=Gold;text.alignment=alignment;text.raycastTarget=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;}
    private static void Frame(Image image)
    {
        // 大框统一使用原生3:2横图，整体等比显示；按钮仍使用独立的小框九宫格。
        image.sprite=frameSprite;image.type=UnityEngine.UI.Image.Type.Simple;image.color=Color.white;image.raycastTarget=true;image.preserveAspect=true;
    }
    private static void StyleButton(Button button,string label,float x,float y,float width,float height,int size)
    {
        var image=Ensure<Image>(button.gameObject);image.sprite=buttonSprite;image.color=Color.white;image.type=UnityEngine.UI.Image.Type.Sliced;image.raycastTarget=true;
        At(R(button),x,y,width,height);button.targetGraphic=image;button.transition=Selectable.Transition.ColorTint;
        var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=colors.selectedColor=new Color(1f,.96f,.77f,1);colors.pressedColor=new Color(.78f,.65f,.42f,1);colors.disabledColor=new Color(.3f,.28f,.25f,.65f);button.colors=colors;
        var text=button.GetComponentInChildren<Text>(true);if(!text)text=Text(button.transform,"Label",label,0,0,width-36,height-12,size);
        text.gameObject.SetActive(true);text.enabled=true;text.text=label;Style(text,size,TextAnchor.MiddleCenter);At(R(text),0,0,width-36,height-12);
    }
    private static void Back(Button button,Transform parent,float x,float y)
    {
        button.transform.SetParent(parent,false);At(R(button),x,y,71,61);
        var image=Ensure<Image>(button.gameObject);image.sprite=Old("返回1");image.color=Color.white;image.preserveAspect=true;image.raycastTarget=true;
        button.targetGraphic=image;button.transition=Selectable.Transition.SpriteSwap;button.spriteState=new SpriteState{highlightedSprite=Old("返回2"),pressedSprite=Old("返回2")};
        foreach(var text in button.GetComponentsInChildren<Text>(true))text.enabled=false;
    }
    private static Scrollbar Scrollbar(Transform parent,string name)
    {
        var go=Node(parent,name);var r=R(go);r.anchorMin=new Vector2(1,0);r.anchorMax=Vector2.one;r.pivot=new Vector2(1,.5f);r.anchoredPosition=Vector2.zero;r.sizeDelta=new Vector2(10,0);
        var track=Ensure<Image>(go);track.color=new Color(.27f,.19f,.1f,.7f);track.raycastTarget=true;
        var sliding=Node(go.transform,"SlidingArea");Stretch(R(sliding),1,1,2,2);
        var handle=Image(sliding.transform,"Handle",null,0,0,8,70,new Color(.75f,.56f,.3f,1),true);Stretch(R(handle),0,0,0,0);
        var scrollbar=Ensure<Scrollbar>(go);scrollbar.handleRect=R(handle);scrollbar.targetGraphic=handle;scrollbar.direction=UnityEngine.UI.Scrollbar.Direction.BottomToTop;
        return scrollbar;
    }
}
#endif

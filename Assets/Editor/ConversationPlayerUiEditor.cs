#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>玩家类型、思维与自身状态补充布局。保留原控件引用，放在基础布局生成后执行。</summary>
public static class ConversationPlayerUiEditor
{
    private static Font font;
    private static Sprite buttonSprite;
    private static readonly Color Paper=new Color(.96f,.9f,.78f,1);
    private static readonly Color Gold=new Color(1f,.8f,.45f,1);

    [MenuItem("Tools/新增系统界面/玩家与思维补充打磨")]
    public static void Configure()
    {
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Art/Fonts/FangZhengShuSong.ttf");
        buttonSprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Art/UI/Doc6SmallFrame.asset");
        Edit("Assets/Resources/Panel/PlayerBuildPanel.prefab",Player);
        AssetDatabase.SaveAssets();
    }

    private static void Player(GameObject root)
    {
        var panel=root.GetComponent<PlayerBuildPanel>();var choice=Get<GameObject>(panel,"_choiceRoot");var frame=choice.transform.Find("Frame");
        // 选择页以地牢画面与文字为主，不显示大框；仅关闭底图组件，保留全部原文控件。
        At((RectTransform)frame,0,0,1720,1000);var frameImage=frame.GetComponent<Image>();if(frameImage)frameImage.enabled=false;
        var backing=choice.transform.Find("FrameOpaqueBacking");if(backing&&backing.GetComponent<Image>())backing.GetComponent<Image>().enabled=false;
        var dim=choice.transform.Find("Dim");if(dim&&dim.GetComponent<Image>())dim.GetComponent<Image>().color=new Color(0,0,0,.86f);
        // 复用游戏原地牢背景，只留下脑内选择的画面；不把棋盘和经营按钮当成背景。
        var backdrop=Node(choice.transform,"MindScene");var backdropImage=Ensure<Image>(backdrop);
        backdropImage.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath("a848faf5799a04e48a48ab6695886e48"));
        backdropImage.raycastTarget=false;Stretch(R(backdropImage));backdrop.transform.SetAsFirstSibling();
        At(R(Get<Text>(panel,"_choiceTitle")),0,425,1040,60);
        var intro=Get<Text>(panel,"_intro");intro.gameObject.SetActive(true);At(R(intro),0,287,1040,200);intro.fontSize=24;intro.lineSpacing=1.05f;
        Get<Text>(panel,"_summary").gameObject.SetActive(false);Get<Text>(panel,"_scope").gameObject.SetActive(false);
        var divider=frame.Find("Divider");if(divider&&divider.GetComponent<Image>())divider.GetComponent<Image>().enabled=false;
        foreach(var field in new[]{"_types","_thoughts"})foreach(var button in References<Button>(panel,field)) {
            int index=Array.IndexOf(References<Button>(panel,field),button);
            var positions=new[]{new Vector2(-650,42),new Vector2(650,42),new Vector2(0,-312)};
            At(R(button),positions[index].x,positions[index].y,330,118);
            var hit=button.GetComponent<Image>();hit.sprite=null;hit.type=UnityEngine.UI.Image.Type.Simple;hit.color=new Color(.1f,.07f,.04f,.005f);hit.raycastTarget=true;
            var marker=Text(button.transform,"Selected","○",134,41,28,30,24);marker.color=Gold;
            var label=button.transform.Find("Label").GetComponent<Text>();var position=R(label).anchoredPosition;var size=R(label).sizeDelta;
            At(R(label),-10,position.y,292,size.y);label.color=Gold;button.targetGraphic=label;
            if(field=="_thoughts"){At(R(label),0,0,292,64);At(R(marker),-155,0,28,30);}
            var shortCopy=button.transform.Find("Short");if(shortCopy)shortCopy.gameObject.SetActive(false);
        }
        // 用户原稿完整展示并保留滚动；只隐藏我们后来添加的常驻教学和重复说明。
        var detail=Get<Text>(panel,"_detail");detail.font=font;detail.fontSize=30;detail.lineSpacing=1.28f;
        var scroll=Get<ScrollRect>(panel,"_detailScroll");At(R(scroll),0,-44,720,340);At(scroll.viewport,-10,0,696,340);
        if(scroll.verticalScrollbar)At(R(scroll.verticalScrollbar),355,0,6,340);
        var title=Get<Text>(panel,"_detailTitle");At(R(title),0,159,720,45);title.alignment=TextAnchor.MiddleCenter;
        var readingBackground=frame.Find("ReadingBackground");if(readingBackground&&readingBackground.GetComponent<Image>())readingBackground.GetComponent<Image>().enabled=false;
        detail.color=Paper;
        var readingHint=Text(frame,"ReadingHint","向下滚动",197,-277,690,28,22);readingHint.color=new Color(.77f,.67f,.49f);Set(panel,"_readingHint",readingHint);

        // 同一排按当前可见按钮居中；说明仍严格只在悬停或键盘焦点时出现。
        readingHint.gameObject.SetActive(false);
        var actions=Node(frame,"DecisionActions");At(R(actions),0,-451,1000,76);
        var group=Ensure<HorizontalLayoutGroup>(actions);group.spacing=20;group.padding=new RectOffset();group.childAlignment=TextAnchor.MiddleCenter;
        group.childControlWidth=group.childControlHeight=false;group.childForceExpandWidth=group.childForceExpandHeight=false;
        var keep=Get<Button>(panel,"_keep");var confirm=Get<Button>(panel,"_confirm");var none=Get<Button>(panel,"_none");
        foreach(var button in new[]{keep,confirm,none}){
            button.transform.SetParent(actions.transform,false);At(R(button),0,0,315,76);
            var label=button.transform.Find("Label").GetComponent<Text>();At(R(label),0,0,279,62);
        }
        Hint(confirm,"采用当前选中的思维");Hint(keep,"保留当前思维");Hint(none,"不采用任何思维");

        var effect=Get<Text>(panel,"_effect");var status=Get<GameObject>(panel,"_statusRoot").transform.Find("Body");
        var effectScroll=Scroll(status,"StatusEffectScroll",0,-155,390,232,effect);effect.fontSize=28;effect.lineSpacing=1.28f;Set(panel,"_effectScroll",effectScroll);
        var mode=Get<Text>(panel,"_mode");At(R(mode),0,10,398,74);mode.fontSize=28;
        foreach(var text in root.GetComponentsInChildren<Text>(true))text.font=font;
    }

    private static void Evolution(GameObject root)
    {
        var panel=root.GetComponent<EvolutionPanel>();var frame=root.transform.Find("Frame");
        var icon=Get<Image>(panel,"_monsterIcon");At(R(icon),-430,155,190,190);
        var name=Text(frame,"MonsterIdentity","",-430,26,340,44,29);Set(panel,"_monsterName",name);
        var description=Get<Text>(panel,"_description");At(R(description),-425,-130,340,220);description.fontSize=29;description.lineSpacing=1.28f;description.alignment=TextAnchor.UpperCenter;
        var directions=References<Button>(panel,"_directions");var marks=new Text[directions.Length];
        for(int i=0;i<directions.Length;i++) {
            var button=directions[i];marks[i]=Text(button.transform,"ChoiceMark","○",143,36,28,30,22);marks[i].color=Gold;
            var label=button.transform.Find("Label").GetComponent<Text>();At(R(label),-7,0,287,94);label.lineSpacing=1.18f;
        }
        SetArray(panel,"_directionMarks",marks);
        var confirm=Get<Button>(panel,"_confirm");At(R(confirm),215,-352,320,76);
        var cancel=Ensure<Button>(Node(frame,"CancelEvolution"));Button(cancel,"取消",-215,-352,320,76);Set(panel,"_cancel",cancel);
        for(int i=0;i<directions.Length;i++) {
            var nav=directions[i].navigation;nav.mode=Navigation.Mode.Explicit;
            nav.selectOnUp=i>=2?directions[i-2]:null;nav.selectOnDown=i+2<directions.Length?directions[i+2]:i%2==0?cancel:confirm;
            nav.selectOnLeft=i%2==1?directions[i-1]:null;nav.selectOnRight=i%2==0&&i+1<directions.Length?directions[i+1]:null;
            directions[i].navigation=nav;
        }
        var cancelNav=cancel.navigation;cancelNav.mode=Navigation.Mode.Explicit;cancelNav.selectOnUp=directions[6];cancelNav.selectOnRight=confirm;cancel.navigation=cancelNav;
        var confirmNav=confirm.navigation;confirmNav.mode=Navigation.Mode.Explicit;confirmNav.selectOnUp=directions[5];confirmNav.selectOnLeft=cancel;confirm.navigation=confirmNav;
        var cost=Get<Text>(panel,"_cost");At(R(cost),0,-270,1150,52);cost.fontSize=29;
        foreach(var text in root.GetComponentsInChildren<Text>(true))text.font=font;
    }

    private static ScrollRect Scroll(Transform parent,string name,float x,float y,float width,float height,Text content)
    {
        var go=Node(parent,name);At(R(go),x,y,width,height);var scroll=Ensure<ScrollRect>(go);scroll.horizontal=false;scroll.vertical=true;scroll.scrollSensitivity=28;scroll.movementType=ScrollRect.MovementType.Clamped;
        var view=Image(go.transform,"Viewport",0,0,width-18,height,Color.clear,true);Ensure<RectMask2D>(view.gameObject);
        content.transform.SetParent(view.transform,false);var r=R(content);r.anchorMin=new Vector2(0,1);r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,1);r.anchoredPosition=Vector2.zero;r.sizeDelta=Vector2.zero;
        content.alignment=TextAnchor.UpperLeft;content.horizontalOverflow=HorizontalWrapMode.Wrap;content.verticalOverflow=VerticalWrapMode.Overflow;
        scroll.viewport=R(view);scroll.content=r;
        var track=Image(go.transform,"Scrollbar",width/2-3,0,6,height,new Color(.2f,.14f,.07f,.8f),true);var scrollbar=Ensure<Scrollbar>(track.gameObject);
        var handle=Image(track.transform,"Handle",0,0,6,70,new Color(.7f,.52f,.29f),true);Stretch(R(handle));scrollbar.handleRect=R(handle);scrollbar.targetGraphic=handle;scrollbar.direction=UnityEngine.UI.Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar=scrollbar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;return scroll;
    }
    private static void Hint(Button button,string copy)
    {
        var hint=Image(button.transform,"Hint",0,100,350,76,new Color(.09f,.055f,.03f,.98f));
        Text(hint.transform,"Text",copy,0,0,310,60,26);
        var canvas=Ensure<CanvasGroup>(hint.gameObject);canvas.blocksRaycasts=canvas.interactable=false;
        Set(Ensure<UIHoverHint>(button.gameObject),"hint",hint.gameObject);hint.gameObject.SetActive(false);
    }
    private static void Button(Button button,string copy,float x,float y,float width,float height)
    {
        var image=Ensure<Image>(button.gameObject);image.sprite=buttonSprite;image.type=UnityEngine.UI.Image.Type.Sliced;image.color=Color.white;image.raycastTarget=true;
        button.targetGraphic=image;button.transition=Selectable.Transition.ColorTint;At(R(button),x,y,width,height);
        var colors=button.colors;colors.highlightedColor=colors.selectedColor=new Color(1f,.93f,.76f);colors.disabledColor=new Color(.43f,.4f,.36f);button.colors=colors;
        Text(button.transform,"Label",copy,0,0,width-36,height-12,30);
    }
    private static GameObject Node(Transform parent,string name)
    {var existing=parent.Find(name);if(existing)return existing.gameObject;var go=new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);return go;}
    private static T Ensure<T>(GameObject go)where T:Component{var component=go.GetComponent<T>();return component?component:go.AddComponent<T>();}
    private static RectTransform R(Component component)=>(RectTransform)component.transform;
    private static RectTransform R(GameObject go)=>(RectTransform)go.transform;
    private static void At(RectTransform rect,float x,float y,float width,float height)
    {rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(width,height);rect.localScale=Vector3.one;}
    private static void Stretch(RectTransform rect)
    {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
    private static Image Image(Transform parent,string name,float x,float y,float width,float height,Color color,bool hit=false)
    {var image=Ensure<Image>(Node(parent,name));image.sprite=null;image.color=color;image.raycastTarget=hit;At(R(image),x,y,width,height);return image;}
    private static Text Text(Transform parent,string name,string value,float x,float y,float width,float height,int size)
    {var text=Ensure<Text>(Node(parent,name));text.text=value;text.font=font;text.fontSize=size;text.resizeTextForBestFit=false;text.color=Paper;text.raycastTarget=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;At(R(text),x,y,width,height);text.alignment=TextAnchor.MiddleCenter;return text;}
    private static T Get<T>(UnityEngine.Object target,string field)where T:UnityEngine.Object=>new SerializedObject(target).FindProperty(field).objectReferenceValue as T;
    private static T[] References<T>(UnityEngine.Object target,string field)where T:UnityEngine.Object
    {var values=new SerializedObject(target).FindProperty(field);var result=new T[values.arraySize];for(int i=0;i<result.Length;i++)result[i]=values.GetArrayElementAtIndex(i).objectReferenceValue as T;return result;}
    private static void Set(UnityEngine.Object target,string field,UnityEngine.Object value)
    {var so=new SerializedObject(target);so.FindProperty(field).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
    private static void SetArray<T>(UnityEngine.Object target,string field,T[] values)where T:UnityEngine.Object
    {var so=new SerializedObject(target);var array=so.FindProperty(field);array.arraySize=values.Length;for(int i=0;i<values.Length;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=values[i];so.ApplyModifiedPropertiesWithoutUndo();}
    private static void Edit(string path,Action<GameObject> action)
    {var root=PrefabUtility.LoadPrefabContents(path);try{action(root);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}}
}
#endif

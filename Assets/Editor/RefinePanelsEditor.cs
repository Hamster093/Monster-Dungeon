#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>本轮只重排四个既有面板的视觉与绑定，不生成第二套业务界面。</summary>
public static class RefinePanelsEditor
{
    private static Font font;
    private static readonly Color Paper=new Color(.95f,.91f,.83f,1);
    private static readonly Color Gold=new Color(.98f,.79f,.48f,1);
    private static readonly Color Muted=new Color(.74f,.71f,.65f,1);
    private static readonly Color Rule=new Color(.79f,.64f,.4f,.3f);

    [MenuItem("Tools/界面调整/进化自身状态检定结算")]
    public static void Configure()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("请退出播放后配置界面。");
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Art/Fonts/FangZhengShuSong.ttf");
        if(!font)throw new InvalidOperationException("缺少现有书宋字体。");
        Edit("EvolutionPanel",Evolution);
        Edit("PlayerBuildPanel",Status);
        Edit("DiceCheckPanel",Dice);
        Edit("DailySettlementPanel",Settlement);
        AssetDatabase.SaveAssets();Debug.Log("REFINE_PANELS_CONFIGURED");
    }
    private static void Evolution(GameObject root)
    {
        var panel=root.GetComponent<EvolutionPanel>();Reset(root,new Color(0,0,0,.98f));
        var frame=Node(root.transform,"Frame");At(R(frame),0,0,1600,1000);
        var title=Txt(frame.transform,"Title","你希望这只魔物更加……",0,442,1100,56,40,Gold);
        var icon=Img(frame.transform,"MonsterIcon",0,60,245,245,Color.white);icon.preserveAspect=true;
        var identity=Txt(frame.transform,"MonsterIdentity","",0,-91,310,44,29,Paper);
        var directions=new Button[7];var marks=new Text[7];
        for(int i=0;i<7;i++)
        {
            float angle=(90-i*360f/7)*Mathf.Deg2Rad;
            directions[i]=Btn(frame.transform,"Direction"+i,EvolutionPanel.Names[i],Mathf.Cos(angle)*525,Mathf.Sin(angle)*270+40,330,90);
            var label=directions[i].GetComponentInChildren<Text>();label.fontSize=31;label.lineSpacing=1.15f;At(R(label),0,0,302,84);
            marks[i]=Txt(directions[i].transform,"ChoiceMark","○",-155,23,24,28,21,Gold);
            var nav=directions[i].navigation;nav.mode=Navigation.Mode.Explicit;
            nav.selectOnLeft=directions[i];directions[i].navigation=nav;
        }
        var description=Txt(frame.transform,"Description","",0,-293,1260,60,27,Paper);
        var cost=Txt(frame.transform,"Cost","",0,-355,1000,38,25,Muted);
        var back=Btn(frame.transform,"Back","返回",-195,-423,300,64);
        var confirm=Btn(frame.transform,"Confirm","确认进化",195,-423,300,64);
        // 键盘顺着环形浏览，取消/确认沿用原回调，不增加扣料入口。
        for(int i=0;i<directions.Length;i++)
        {
            var nav=directions[i].navigation;nav.mode=Navigation.Mode.Explicit;
            nav.selectOnLeft=directions[(i+6)%7];nav.selectOnRight=directions[(i+1)%7];
            nav.selectOnUp=directions[(i+6)%7];nav.selectOnDown=i==3||i==4?confirm:directions[(i+1)%7];directions[i].navigation=nav;
        }
        back.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnRight=confirm,selectOnUp=directions[4]};
        confirm.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnLeft=back,selectOnUp=directions[3]};
        Set(panel,"_title",title);Set(panel,"_monsterIcon",icon);Set(panel,"_monsterName",identity);
        Set(panel,"_description",description);Set(panel,"_cost",cost);Set(panel,"_back",back);Set(panel,"_confirm",confirm);
        Array(panel,"_directions",directions);Array(panel,"_directionMarks",marks);
        var area=Node(frame.transform,"DirectionGuideArea");At(R(area),0,40,1450,670);
        Guide(root,"evolution",title.transform,R(area));
    }
    private static void Status(GameObject root) { Round2UiEditor.LayoutStatus(root); }
    private static void Dice(GameObject root)
    {
        var panel=root.GetComponent<DiceCheckPanel>();Reset(root,new Color(0,0,0,.98f));
        var reveal=Node(root.transform,"Reveal");Stretch(R(reveal));var fade=reveal.AddComponent<CanvasGroup>();
        var frame=Node(reveal.transform,"Frame");At(R(frame),0,0,1160,900);
        var context=Group(frame.transform,"ContextStage");Stretch(R(context));
        var title=Txt(context.transform,"Title","",0,342,1080,60,40,Paper);
        Txt(context.transform,"TargetCaption","过关门槛",-95,247,220,48,28,Muted);
        var requirement=Txt(context.transform,"Requirement","",110,247,150,58,44,Gold);
        Img(context.transform,"TopRule",0,190,1000,1,Rule);
        var rollGroup=Group(frame.transform,"RollStage");At(R(rollGroup),-330,62,280,200);
        Txt(rollGroup.transform,"Caption","基础骰点",0,80,270,38,27,Muted);
        var roll=Txt(rollGroup.transform,"Roll","",0,0,280,126,88,Gold);
        var modifierGroup=Group(frame.transform,"ModifierStage");Stretch(R(modifierGroup));
        Txt(modifierGroup.transform,"Caption","加权",0,142,270,38,27,Muted);
        var modifier=Txt(modifierGroup.transform,"Modifier","",0,62,270,126,72,Paper);
        var noteScroll=Scroll(modifierGroup.transform,"ModifierSources",0,-83,1000,108,out var note);
        Style(note,24,Muted,TextAnchor.UpperCenter);note.verticalOverflow=VerticalWrapMode.Overflow;note.lineSpacing=1.18f;
        var totalGroup=Group(frame.transform,"TotalStage");At(R(totalGroup),330,62,280,200);
        Txt(totalGroup.transform,"Caption","总骰点",0,80,270,38,27,Muted);
        var total=Txt(totalGroup.transform,"Total","",0,0,280,126,88,Gold);
        var outcomeGroup=Group(frame.transform,"OutcomeStage");At(R(outcomeGroup),0,-224,1000,125);
        var outcome=Txt(outcomeGroup.transform,"Outcome","",0,0,1000,125,58,Gold);
        var actions=Group(frame.transform,"ActionsStage");At(R(actions),0,-373,800,66);
        var layout=actions.gameObject.AddComponent<HorizontalLayoutGroup>();layout.spacing=26;layout.childAlignment=TextAnchor.MiddleCenter;
        layout.childControlWidth=layout.childControlHeight=false;layout.childForceExpandWidth=layout.childForceExpandHeight=false;
        var coin=Btn(actions.transform,"Coin","重掷 · 幸运硬币×1",0,0,355,66);
        var next=Btn(actions.transform,"Continue","继续",0,0,355,66);
        Set(panel,"_fade",fade);Set(panel,"_title",title);Set(panel,"_requirement",requirement);
        Set(panel,"_rollText",roll);Set(panel,"_modifierText",modifier);Set(panel,"_totalText",total);
        Set(panel,"_outcome",outcome);Set(panel,"_note",note);Set(panel,"_noteScroll",noteScroll);Set(panel,"_continue",next);Set(panel,"_coin",coin);
        Set(panel,"_contextStage",context);Set(panel,"_rollStage",rollGroup);Set(panel,"_modifierStage",modifierGroup);
        Set(panel,"_totalStage",totalGroup);Set(panel,"_outcomeStage",outcomeGroup);Set(panel,"_actionsStage",actions);
        Guide(root,"dice",title.transform,R(frame));
    }
    private static void Settlement(GameObject root)
    {
        var panel=root.GetComponent<DailySettlementPanel>();Reset(root,Color.clear);
        // 结算仍用UIManager原遮罩，正文只加轻薄的暗底，避免双层全屏压暗。
        var card=Img(root.transform,"Statement",0,0,1220,860,new Color(.023f,.022f,.021f,.995f));card.raycastTarget=true;
        var title=Txt(card.transform,"Title","今日账目",0,350,1080,58,42,Gold);
        Img(card.transform,"TopRule",0,298,1090,1,Rule);
        var total=Txt(card.transform,"Total","",-361,200,365,145,27,Gold);
        var commission=Txt(card.transform,"Commission","",0,200,310,132,27,Paper);
        var business=Txt(card.transform,"Business","",361,200,310,132,27,Paper);
        Img(card.transform,"SummaryRule",0,105,1090,1,Rule);
        var label=Txt(card.transform,"NotesHeading","今日结果",0,65,1080,38,27,Muted);label.alignment=TextAnchor.MiddleLeft;
        var scroll=Scroll(card.transform,"NotesScroll",0,-110,1090,294,out var notes);
        Style(notes,28,Paper,TextAnchor.UpperLeft);notes.lineSpacing=1.26f;notes.verticalOverflow=VerticalWrapMode.Overflow;
        Img(card.transform,"FooterRule",0,-290,1090,1,Rule);
        var installment=Txt(card.transform,"InstallmentDue","",-213,-355,640,64,27,Muted);installment.alignment=TextAnchor.MiddleLeft;
        var close=Btn(card.transform,"btn_Close","收款并继续",366,-355,344,66);
        var special=Txt(card.transform,"LegacySpecial","",0,0,1,1,24,Paper);special.gameObject.SetActive(false);
        Set(panel,"txt_LegacyCommission",commission);Set(panel,"txt_SpecialCommission",special);Set(panel,"txt_MonsterBusiness",business);
        Set(panel,"txt_Total",total);Set(panel,"txt_Installment",installment);Set(panel,"_detailsText",notes);Set(panel,"_detailsScroll",scroll);Set(panel,"btn_Close",close);
        Guide(root,"settlement",title.transform,R(scroll));
    }
    private static void Guide(GameObject root,string topic,Transform title,RectTransform target)
    {
        // 仅更新被重排控件的引用，沿用原首次引导与帮助逻辑。
        var anchor=root.GetComponent<GuideTopicAnchor>();if(!anchor)return;
        anchor.topic=topic;anchor.highlights=new[]{target};
        var help=Btn(title,"MechanismHelp","?",490,0,48,48);anchor.help=help;
    }
    private static void Reset(GameObject root,Color color)
    {
        for(int i=root.transform.childCount-1;i>=0;i--)UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
        Stretch(R(root));var image=root.GetComponent<Image>()??root.AddComponent<Image>();image.sprite=null;image.type=Image.Type.Simple;image.color=color;image.raycastTarget=true;
        var group=root.GetComponent<CanvasGroup>();if(group){group.alpha=1;group.interactable=group.blocksRaycasts=true;}
    }
    private static CanvasGroup Group(Transform parent,string name)=>Node(parent,name).AddComponent<CanvasGroup>();
    private static ScrollRect Scroll(Transform parent,string name,float x,float y,float w,float h,out Text text)
    {
        var go=Node(parent,name);At(R(go),x,y,w,h);var scroll=go.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.vertical=true;scroll.scrollSensitivity=32;scroll.movementType=ScrollRect.MovementType.Clamped;
        var viewport=Img(go.transform,"Viewport",0,0,w-22,h,Color.clear);viewport.raycastTarget=true;viewport.gameObject.AddComponent<RectMask2D>();
        text=Txt(viewport.transform,"Content","",0,0,w-32,h,28,Paper);text.alignment=TextAnchor.UpperLeft;text.verticalOverflow=VerticalWrapMode.Overflow;
        var content=R(text);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;content.sizeDelta=new Vector2(-10,h);
        var track=Img(go.transform,"Track",w/2-3,0,5,h,new Color(.5f,.43f,.32f,.16f));var bar=track.gameObject.AddComponent<Scrollbar>();
        var handle=Img(track.transform,"Handle",0,0,5,60,new Color(.8f,.65f,.42f,.6f));Stretch(R(handle));bar.handleRect=R(handle);bar.targetGraphic=handle;bar.direction=Scrollbar.Direction.BottomToTop;
        scroll.viewport=R(viewport);scroll.content=content;scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        return scroll;
    }
    private static Button Btn(Transform parent,string name,string value,float x,float y,float w,float h)
    {
        var image=Img(parent,name,x,y,w,h,new Color(.28f,.23f,.16f,.13f));image.raycastTarget=true;
        var button=image.gameObject.AddComponent<Button>();var text=Txt(image.transform,"Label",value,0,0,w-30,h-6,30,Paper);button.targetGraphic=text;
        var colors=button.colors;colors.highlightedColor=colors.selectedColor=new Color(1,.85f,.56f);colors.pressedColor=new Color(.73f,.59f,.38f);colors.disabledColor=new Color(.35f,.35f,.33f);colors.fadeDuration=.12f;button.colors=colors;
        Img(image.transform,"BottomRule",0,-h/2+2,w-20,1,Rule);return button;
    }
    private static Image Img(Transform parent,string name,float x,float y,float w,float h,Color color)
    {var go=Node(parent,name);var image=go.AddComponent<Image>();image.color=color;image.raycastTarget=false;At(R(go),x,y,w,h);return image;}
    private static Text Txt(Transform parent,string name,string value,float x,float y,float w,float h,int size,Color color)
    {var go=Node(parent,name);var text=go.AddComponent<Text>();text.text=value;Style(text,size,color,TextAnchor.MiddleCenter);text.supportRichText=true;text.lineSpacing=1.14f;At(R(go),x,y,w,h);return text;}
    private static void Style(Text text,int size,Color color,TextAnchor alignment)
    {text.font=font;text.fontSize=size;text.color=color;text.alignment=alignment;text.raycastTarget=false;text.resizeTextForBestFit=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;}
    private static GameObject Node(Transform parent,string name)
    {var go=new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);return go;}
    private static RectTransform R(Component value)=>(RectTransform)value.transform;
    private static RectTransform R(GameObject value)=>(RectTransform)value.transform;
    private static void At(RectTransform rect,float x,float y,float w,float h)
    {rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(w,h);rect.localScale=Vector3.one;}
    private static void Stretch(RectTransform rect,float left=0,float right=0,float top=0,float bottom=0)
    {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(left,bottom);rect.offsetMax=new Vector2(-right,-top);rect.localScale=Vector3.one;}
    private static T Get<T>(UnityEngine.Object owner,string field)where T:UnityEngine.Object=>new SerializedObject(owner).FindProperty(field).objectReferenceValue as T;
    private static void Set(UnityEngine.Object owner,string field,UnityEngine.Object value)
    {var so=new SerializedObject(owner);var property=so.FindProperty(field);if(property==null)throw new InvalidOperationException(owner.name+"缺少字段"+field);property.objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
    private static void Array(UnityEngine.Object owner,string field,UnityEngine.Object[] values)
    {var so=new SerializedObject(owner);var property=so.FindProperty(field);property.arraySize=values.Length;for(int i=0;i<values.Length;i++)property.GetArrayElementAtIndex(i).objectReferenceValue=values[i];so.ApplyModifiedPropertiesWithoutUndo();}
    private static void Edit(string name,Action<GameObject> configure)
    {string path="Assets/Resources/Panel/"+name+".prefab";var root=PrefabUtility.LoadPrefabContents(path);try{configure(root);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}}
}
#endif

#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>说明6布局只在编辑器写入原预制体，正式运行仅刷新状态和内容高度。</summary>
public static class Doc6IntegrationEditor
{
    static Font font;
    static Sprite skin, frame, portraitFrame;
    static readonly Color Ink = new Color(.055f, .042f, .034f, 1);
    static readonly Color Paper = new Color(.95f, .90f, .79f, 1);
    static readonly Color Gold = new Color(1, .87f, .61f, 1);
    static RectTransform R(Component c) => (RectTransform)c.transform;
    static void At(RectTransform r, float x, float y, float w, float h)
    { r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f,.5f); r.anchoredPosition = new Vector2(x,y); r.sizeDelta = new Vector2(w,h); r.localScale = Vector3.one; }
    static void Stretch(RectTransform r) { r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero; }
    static GameObject Node(Transform parent,string name)
    {var go=new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);return go;}
    static Sprite Old(string token)=>AssetDatabase.LoadAssetAtPath<Sprite>(Directory.GetFiles("Assets/Resources_moved/Icons","*.png",SearchOption.AllDirectories).First(p=>Path.GetFileName(p).Contains(token)).Replace('\\','/'));
    static Image Img(Transform parent,string name,Sprite sprite,float x,float y,float w,float h,bool hit=false)
    {var go=Node(parent,name);var img=go.AddComponent<Image>();img.sprite=sprite;img.color=Color.white;img.raycastTarget=hit;At(R(img),x,y,w,h);return img;}
    static Text Txt(Transform parent,string name,string text,float x,float y,float w,float h,int size=32)
    {var go=Node(parent,name);var t=go.AddComponent<Text>();t.text=text;t.font=font;t.fontSize=size;t.color=Paper;t.alignment=TextAnchor.MiddleCenter;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;t.raycastTarget=false;t.lineSpacing=1.35f;At(R(t),x,y,w,h);return t;}
    static Image Framed(Transform parent,string name,float x,float y,float w,float h,bool hit=false,bool portrait=false)
    {
        // 底色放在原分件后面，保留原皮革纹理，避免不透明子图把材质盖成纯黑。
        var backing=Img(parent,name+"OpaqueBacking",null,x,y,w-30,h-30);backing.color=Ink;
        var image=Img(parent,name,portrait?portraitFrame:frame,x,y,w,h,hit);
        image.type=portrait?Image.Type.Sliced:Image.Type.Simple;image.preserveAspect=!portrait;
        return image;
    }
    static Button Btn(Transform parent,string name,string label,float x,float y,float w,float h)
    {
        var image=Img(parent,name,skin,x,y,w,h,true);image.type=Image.Type.Sliced;
        var b=image.gameObject.AddComponent<Button>();b.targetGraphic=image;b.transition=Selectable.Transition.ColorTint;
        var colors=b.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1,.92f,.74f);colors.pressedColor=new Color(.72f,.65f,.52f);colors.disabledColor=new Color(.43f,.41f,.38f);colors.fadeDuration=.12f;b.colors=colors;
        Txt(image.transform,"Label",label,0,0,w-42,h-14,30);
        var outline=image.gameObject.AddComponent<Outline>();outline.effectColor=new Color(1,.72f,.28f,1);outline.effectDistance=new Vector2(2,2);outline.enabled=false;
        return b;
    }
    static Button Back(Transform parent,string name,float x,float y)
    {
        var image=Img(parent,name,Old("返回1"),x,y,71,61,true);image.preserveAspect=true;
        var b=image.gameObject.AddComponent<Button>();b.targetGraphic=image;b.transition=Selectable.Transition.SpriteSwap;
        b.spriteState=new SpriteState{highlightedSprite=Old("返回2"),pressedSprite=Old("返回2")};return b;
    }
    static void Set(UnityEngine.Object c,string name,UnityEngine.Object value)
    {var so=new SerializedObject(c);var p=so.FindProperty(name);if(p==null)throw new Exception(c.name+"缺少绑定"+name);p.objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
    static void Arr(UnityEngine.Object c,string name,UnityEngine.Object[] values)
    {var so=new SerializedObject(c);var a=so.FindProperty(name);a.arraySize=values.Length;for(int i=0;i<values.Length;i++)a.GetArrayElementAtIndex(i).objectReferenceValue=values[i];so.ApplyModifiedPropertiesWithoutUndo();}
    static T Get<T>(UnityEngine.Object c,string name) where T:UnityEngine.Object=>new SerializedObject(c).FindProperty(name).objectReferenceValue as T;
    static void Edit(string path,Action<GameObject> action)
    {var go=PrefabUtility.LoadPrefabContents(path);try{action(go);PrefabUtility.SaveAsPrefabAsset(go,path);}finally{PrefabUtility.UnloadPrefabContents(go);}}
    static ScrollRect Scroll(Transform parent,string name,float x,float y,float w,float h,out Text content)
    {
        var node=Node(parent,name);At(R(node.transform),x,y,w,h);var scroll=node.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.vertical=true;scroll.scrollSensitivity=32;scroll.movementType=ScrollRect.MovementType.Clamped;
        var view=Img(node.transform,"Viewport",null,-10,0,w-24,h,true);view.color=new Color(1,1,1,.001f);view.gameObject.AddComponent<RectMask2D>();scroll.viewport=R(view);
        content=Txt(view.transform,"Content","",0,0,w-36,h,32);content.alignment=TextAnchor.UpperLeft;content.lineSpacing=1.45f;content.verticalOverflow=VerticalWrapMode.Overflow;
        var r=R(content);r.anchorMin=new Vector2(0,1);r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,1);r.anchoredPosition=Vector2.zero;r.sizeDelta=new Vector2(-12,h);scroll.content=r;
        var track=Img(node.transform,"Scrollbar",null,w/2-5,0,8,h,true);track.color=new Color(.15f,.11f,.07f,.85f);
        var bar=track.gameObject.AddComponent<Scrollbar>();bar.direction=Scrollbar.Direction.BottomToTop;
        var handle=Img(track.transform,"Handle",null,0,0,8,64,true);handle.color=new Color(.72f,.54f,.29f);bar.handleRect=R(handle);bar.targetGraphic=handle;
        Stretch(R(handle));
        scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        return scroll;
    }
    static void Hint(Button button,string text)
    {
        var go=Img(button.transform,"Hint",skin,0,100,350,76,false);go.type=Image.Type.Sliced;
        Txt(go.transform,"Text",text,0,0,310,60,26);var group=go.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;group.interactable=false;
        Set(button.gameObject.AddComponent<UIHoverHint>(),"hint",go.gameObject);go.gameObject.SetActive(false);
    }
    static void CreateSkin()
    {
        Directory.CreateDirectory("Assets/Resources/Art/UI");
        const string path="Assets/Resources/Art/UI/Doc6SmallFrame.asset";
        skin=AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if(!skin){var source=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Art/Preparation/地狱_0001s_0002_框框.png");skin=Sprite.Create(source.texture,source.rect,new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(20,20,20,20));AssetDatabase.CreateAsset(skin,path);}
        // 横向大面板使用按原风格重新绘制的横框，避免将竖版皮革纹理大幅拉扁。
        const string texturePath="Assets/Resources/Art/UI/Doc6LandscapeFrame.png";
        AssetDatabase.ImportAsset(texturePath,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=2048;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.spriteBorder=new Vector4(36,36,36,36);
        importer.SaveAndReimport();
        const string landscapePath="Assets/Resources/Art/UI/Doc6LandscapeFrame.asset";
        frame=AssetDatabase.LoadAssetAtPath<Sprite>(landscapePath);
        if(!frame){var source=AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);frame=Sprite.Create(source.texture,source.rect,new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(36,36,36,36));AssetDatabase.CreateAsset(frame,landscapePath);}
        portraitFrame=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Art/Endings/ConfirmationFrame.asset");
    }
    [MenuItem("Tools/说明6/接入文案与统一新增界面")]
    public static void Configure()
    {
        if(EditorApplication.isPlaying)throw new Exception("请先退出播放再保存预制体");
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Art/Fonts/FangZhengShuSong.ttf");CreateSkin();CreatePlayer();ConfigureHUD();
        Doc6SupplementEditor.Configure();AssetDatabase.SaveAssets();Debug.Log("DOC6_UI_CONFIGURED");
    }
    static void CreatePlayer()
    {
        var root=Node(null,"PlayerBuildPanel");Stretch(R(root.transform));var p=root.AddComponent<PlayerBuildPanel>();
        var choice=Node(root.transform,"Creation");Stretch(R(choice.transform));var dim=Img(choice.transform,"Dim",null,0,0,1920,1080,true);Stretch(R(dim));dim.color=new Color(0,0,0,.58f);
        var body=Framed(choice.transform,"Frame",0,0,1440,960,true);
        var title=Txt(body.transform,"Title","",0,410,1180,60,44);title.color=Gold;
        var intro=Txt(body.transform,"Intro","",0,280,1200,196,26);intro.lineSpacing=1.1f;
        var divider=Img(body.transform,"Divider",null,0,164,1240,2);divider.color=new Color(.54f,.36f,.17f,.7f);
        Button[] types=new Button[3],thoughts=new Button[3];Text[] values=new Text[3],descs=new Text[3];
        string[] names={"敏锐型","博识型","坚毅型"};string[] subtitles={"你藏不到哪去","百科全书派","史莱姆风格"};
        for(int i=0;i<3;i++)
        {
            float y=77-i*151;
            types[i]=Btn(body.transform,"Type"+i,names[i],-436,y,355,130);var label=types[i].GetComponentInChildren<Text>();At(R(label),0,31,312,45);label.fontSize=34;label.color=Gold;
            values[i]=Txt(types[i].transform,"Values","",0,-27,315,52,22);values[i].lineSpacing=1.1f;
            thoughts[i]=Btn(body.transform,"Thought"+i,PlayerBuildState.ThoughtNames[i],-436,y,355,130);var t=thoughts[i].GetComponentInChildren<Text>();At(R(t),0,18,312,46);t.fontSize=31;t.color=Gold;
            descs[i]=Txt(thoughts[i].transform,"Short","",0,-30,308,36,24);descs[i].text=i==0?"有效信息 · 属性运用":i==1?"影响修正 · 温和改变":"失败之后 · 换种思路";
            foreach(var b in new[]{types[i],thoughts[i]}){var m=Txt(b.transform,"Selected","已选",120,46,60,30,20);m.color=Gold;m.gameObject.SetActive(false);}
        }
        var detailBacking=Img(body.transform,"ReadingBackground",null,197,-68,755,434);detailBacking.color=new Color(.045f,.033f,.026f,.93f);
        var detailTitle=Txt(body.transform,"DetailTitle","",190,110,690,52,36);detailTitle.color=Gold;detailTitle.alignment=TextAnchor.MiddleLeft;
        Text detail;var scroll=Scroll(body.transform,"DetailScroll",197,-100,710,326,out detail);
        var summary=Txt(body.transform,"Summary","",0,-319,1190,42,26);
        var confirm=Btn(body.transform,"Confirm","就凭这个",0,-390,365,76);
        var keep=Btn(body.transform,"Keep","我不想思考了",-414,-390,365,76);Hint(keep,"保留当前思维");
        var none=Btn(body.transform,"None","我什么都想不起来",414,-390,365,76);Hint(none,"不采用任何思维");
        var status=Node(root.transform,"Status");Stretch(R(status.transform));var statusBody=Framed(status.transform,"Body",-691,-99,482,842,false,true);
        var strip=Img(statusBody.transform,"Header",Old("左标题"),31,467,414,65);Txt(strip.transform,"Title","自身状态",0,0,340,54,32);var back=Back(statusBody.transform,"Back",-221,467);
        var stats=Txt(statusBody.transform,"Attributes","",0,215,390,242,32);stats.alignment=TextAnchor.UpperLeft;stats.lineSpacing=1.35f;
        var line=Img(statusBody.transform,"Line",null,0,70,385,2);line.color=new Color(.54f,.36f,.17f,.7f);
        var mode=Txt(statusBody.transform,"Thought","",0,12,398,78,30);
        var effect=Txt(statusBody.transform,"Effect","",0,-141,385,220,29);effect.alignment=TextAnchor.UpperLeft;effect.lineSpacing=1.4f;
        var scope=Txt(statusBody.transform,"Scope","",0,-335,385,85,25);
        Set(p,"_choiceRoot",choice);Set(p,"_statusRoot",status);Set(p,"_confirm",confirm);Set(p,"_keep",keep);Set(p,"_none",none);Set(p,"_back",back);
        Set(p,"_summary",summary);Set(p,"_stats",stats);Set(p,"_mode",mode);Set(p,"_effect",effect);Set(p,"_scope",scope);Set(p,"_choiceTitle",title);Set(p,"_intro",intro);Set(p,"_detailTitle",detailTitle);Set(p,"_detail",detail);Set(p,"_detailScroll",scroll);
        Arr(p,"_types",types);Arr(p,"_thoughts",thoughts);Arr(p,"_typeValues",values);Arr(p,"_thoughtDescriptions",descs);
        root.SetActive(false);PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/Panel/PlayerBuildPanel.prefab");UnityEngine.Object.DestroyImmediate(root);
    }
    static void CreateEvolution()
    {
        var root=Node(null,"EvolutionPanel");Stretch(R(root.transform));var p=root.AddComponent<EvolutionPanel>();var dim=Img(root.transform,"Dim",null,0,0,1920,1080,true);Stretch(R(dim));dim.color=new Color(0,0,0,.58f);
        var body=Framed(root.transform,"Frame",0,0,1380,920,true);Set(p,"_title",Txt(body.transform,"Title","你希望这只魔物更加……",0,348,1120,70,44));
        var icon=Img(body.transform,"MonsterIcon",null,-430,144,190,190);icon.preserveAspect=true;Set(p,"_monsterIcon",icon);
        var description=Txt(body.transform,"Preview","",-425,-66,310,200,32);description.lineSpacing=1.45f;Set(p,"_description",description);
        var directions=new Button[7];for(int i=0;i<7;i++)
        {int column=i%2,row=i/2;directions[i]=Btn(body.transform,"Direction"+i,"",-24+column*355,212-row*124,335,108);directions[i].GetComponentInChildren<Text>().fontSize=30;}
        Set(p,"_cost",Txt(body.transform,"Cost","",0,-270,1150,52,30));
        Set(p,"_confirm",Btn(body.transform,"Confirm","确认进化",215,-352,320,76));Set(p,"_back",Back(body.transform,"Back",-570,348));
        Arr(p,"_directions",directions);root.SetActive(false);PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/Panel/EvolutionPanel.prefab");UnityEngine.Object.DestroyImmediate(root);
    }
    static void ConfigureHUD()
    {
        Edit("Assets/Resources/Panel/HUDPanel.prefab",root=>
        {var p=root.GetComponent<HUDPanel>();var b=Get<Button>(p,"_playerButton");At(R(b),-691,417,450,46);var image=b.GetComponent<Image>();image.sprite=skin;image.type=Image.Type.Sliced;image.color=Color.white;b.targetGraphic=image;b.transition=Selectable.Transition.ColorTint;var text=Get<Text>(p,"_playerText");text.color=Paper;text.fontSize=23;At(R(text),0,0,414,38);});
    }
    public static void BuildInteractive()
    {
        if(!Application.productName.EndsWith("_NarrativeValidation"))throw new Exception("仅允许隔离工程构建验证版");
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;PlayerSettings.resizableWindow=true;
        string output=Environment.GetEnvironmentVariable("MONSTER_DUNGEON_QA_BUILD")??Path.GetFullPath("../Doc6Player");Directory.CreateDirectory(output);
        var report=BuildPipeline.BuildPlayer(new[]{"Assets/Scenes/StartGame.unity","Assets/Scenes/PlayerGame.unity"},Path.Combine(output,"MonsterDungeon.exe"),BuildTarget.StandaloneWindows64,BuildOptions.Development);
        if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("说明6验证版构建失败");
    }
}
#endif

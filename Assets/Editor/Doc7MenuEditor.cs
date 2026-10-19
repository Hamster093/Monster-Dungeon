#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>说明7封面与CG鉴赏配置。只保存StartGame场景和独立鉴赏预制体。</summary>
public static class Doc7MenuEditor
{
    private static Font font;
    private static readonly Color Paper=new Color(.96f,.91f,.8f,1);
    private static readonly Color Gold=new Color(.96f,.72f,.4f,1);
    private static readonly Color Dark=new Color(.055f,.045f,.04f,.96f);
    private const string CoverRoot="Assets/Resources/Art/MenuCover/";

    [MenuItem("Tools/说明7/封面与CG鉴赏")]
    public static void Configure()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("请退出播放后配置封面。");
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Art/Fonts/FangZhengShuSong.ttf");
        foreach(string name in new[]{"封面底图","进入地牢1","进入地牢2","继续经营1","继续经营2","请假1","请假2"})Import(CoverRoot+name+".png");
        Import("Assets/Resources/Art/UI/CgGalleryIcon.png");
        ConfigureGallery();ConfigureMenu();AssetDatabase.SaveAssets();Debug.Log("DOC7_MENU_CONFIGURED");
    }
    private static void Import(string path)
    {
        if(!File.Exists(path))throw new FileNotFoundException("封面分件缺失",path);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;
        importer.spriteImportMode=SpriteImportMode.Single;
        var spriteSettings=new TextureImporterSettings();importer.ReadTextureSettings(spriteSettings);
        spriteSettings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(spriteSettings);
        importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.maxTextureSize=4096;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
    }
    private static Sprite Cover(string name)=>AssetDatabase.LoadAssetAtPath<Sprite>(CoverRoot+name+".png");
    private static void ConfigureMenu()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/StartGame.unity");
        // 非16:9窗口的留边使用黑色，不露出Unity默认的蓝色背景。
        if(Camera.main){Camera.main.clearFlags=CameraClearFlags.SolidColor;Camera.main.backgroundColor=Color.black;}
        var manager=UnityEngine.Object.FindObjectOfType<UIPanelManager>();var start=UnityEngine.Object.FindObjectOfType<StartGame>();
        var parent=Get<GameObject>(manager,"pnl_start_game");
        var existing=parent.transform.Find("ArtCover");
        var background=existing?existing.GetComponent<Image>():Image(parent.transform,"ArtCover",0,0,1920,1080,Color.white);
        background.enabled=true;background.color=Color.white;background.sprite=Cover("封面底图");background.type=UnityEngine.UI.Image.Type.Simple;background.preserveAspect=true;
        At(R(background),0,0,1920,1080);background.raycastTarget=false;background.transform.SetAsFirstSibling();
        var newGame=Get<Button>(manager,"btn_start_game");var resume=Get<Button>(manager,"btn_continue_game");
        // 三个按钮各用自身比例，不将不同宽高的原画强制拉成同一块矩形。
        SkinCover(newGame,parent.transform,"进入地牢",-585,-80,.8f);
        SkinCover(resume,parent.transform,"继续经营",-585,-230,.8f);
        var quitTransform=parent.transform.Find("QuitGame");
        var quit=quitTransform?quitTransform.GetComponent<Button>():Node(parent.transform,"QuitGame").AddComponent<Button>();
        SkinCover(quit,parent.transform,"请假",-585,-365,.8f);
        quit.onClick=new Button.ButtonClickedEvent();UnityEventTools.AddPersistentListener(quit.onClick,start.QuitGame);
        var oldGallery=parent.transform.Find("btn_cg_gallery");if(oldGallery)UnityEngine.Object.DestroyImmediate(oldGallery.gameObject);
        var galleryObject=Node(parent.transform,"btn_cg_gallery");At(R(galleryObject),-864,-440,108,112);
        var hit=galleryObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
        var gallery=galleryObject.AddComponent<Button>();
        var icon=Image(galleryObject.transform,"Icon",0,9,84,84,Color.white);
        icon.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Art/UI/CgGalleryIcon.png");icon.preserveAspect=true;
        gallery.targetGraphic=icon;var colors=gallery.colors;colors.highlightedColor=colors.selectedColor=new Color(1,.86f,.65f,1);colors.pressedColor=new Color(.72f,.6f,.45f,1);gallery.colors=colors;
        Text(galleryObject.transform,"Label","CG鉴赏",0,-49,108,30,22,Paper);
        Set(start,"_galleryButton",gallery);Set(start,"_continueButton",resume);
        var settings=Get<Button>(manager,"btn_settings");
        Navigate(newGame,settings,resume,gallery,settings);Navigate(resume,newGame,quit,gallery,settings);
        Navigate(quit,resume,gallery,gallery,settings);Navigate(gallery,quit,newGame,quit,newGame);
        var eventSystem=UnityEngine.Object.FindObjectOfType<EventSystem>();if(eventSystem)eventSystem.firstSelectedGameObject=newGame.gameObject;
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }
    private static void SkinCover(Button button,Transform parent,string name,float x,float y,float scale)
    {
        button.transform.SetParent(parent,false);var normal=Cover(name+"1");var hover=Cover(name+"2");
        var image=button.GetComponent<Image>();if(!image)image=button.gameObject.AddComponent<Image>();
        image.enabled=true;image.sprite=normal;image.type=UnityEngine.UI.Image.Type.Simple;image.preserveAspect=true;image.color=Color.white;image.raycastTarget=true;
        At(R(button),x,y,normal.rect.width*scale,normal.rect.height*scale);
        button.targetGraphic=image;button.transition=Selectable.Transition.SpriteSwap;
        button.spriteState=new SpriteState{highlightedSprite=hover,selectedSprite=hover,pressedSprite=hover,disabledSprite=normal};
        foreach(var label in button.GetComponentsInChildren<Text>(true))label.enabled=false;
        foreach(var label in button.GetComponentsInChildren<TMPro.TMP_Text>(true))label.enabled=false;
    }
    private static void ConfigureGallery()
    {
        const string path="Assets/Resources/UI/CGGalleryPanel.prefab";
        Directory.CreateDirectory("Assets/Resources/UI");
        var root=Node(null,"CGGalleryPanel");var panel=root.AddComponent<CGGalleryPanel>();
        var canvas=root.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32760;
        var scaler=root.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        root.AddComponent<GraphicRaycaster>();Stretch(R(root));
        var backdrop=Image(root.transform,"Backdrop",0,0,1920,1080,new Color(.018f,.016f,.018f,.97f),true);Stretch(R(backdrop));
        var list=Node(root.transform,"List");Stretch(R(list));
        Text(list.transform,"Title","CG鉴赏",0,430,1100,68,42,Gold);
        var count=Text(list.transform,"Count","",615,430,170,50,28,Paper);
        var back=PlainButton(list.transform,"Back","返回",-655,430,130,58);
        var divider=Image(list.transform,"HeaderRule",0,369,1440,1,new Color(.61f,.38f,.2f,.65f));
        var grid=Node(list.transform,"UnlockedGrid");At(R(grid),0,-16,1272,486);
        var layout=grid.AddComponent<GridLayoutGroup>();layout.cellSize=new Vector2(300,225);layout.spacing=new Vector2(24,30);
        layout.constraint=GridLayoutGroup.Constraint.FixedColumnCount;layout.constraintCount=4;layout.childAlignment=TextAnchor.UpperLeft;
        var tiles=new Button[7];var thumbnails=new Image[7];var names=new Text[7];
        for(int i=0;i<7;i++)
        {
            var tile=Image(grid.transform,"UnlockedCG"+i,0,0,300,225,new Color(.085f,.065f,.048f,.8f),true);
            tiles[i]=tile.gameObject.AddComponent<Button>();tiles[i].targetGraphic=tile;
            var colors=tiles[i].colors;colors.normalColor=new Color(.78f,.78f,.78f,1);colors.highlightedColor=colors.selectedColor=Color.white;colors.pressedColor=new Color(.6f,.5f,.4f,1);tiles[i].colors=colors;
            var edge=tile.gameObject.AddComponent<Outline>();edge.effectDistance=new Vector2(1,-1);edge.effectColor=new Color(.59f,.37f,.17f,.8f);
            thumbnails[i]=Image(tile.transform,"CG",0,20,280,158,Color.white);thumbnails[i].preserveAspect=true;tiles[i].targetGraphic=thumbnails[i];
            names[i]=Text(tile.transform,"Name","",0,-84,280,40,27,Paper);tile.gameObject.SetActive(false);
        }
        var empty=Text(list.transform,"Empty","尚未解锁CG",0,10,1200,80,30,new Color(.73f,.66f,.57f,1));
        var viewer=Node(root.transform,"Viewer");Stretch(R(viewer));
        var cg=Image(viewer.transform,"FullCG",0,0,1660,870,Color.white);cg.preserveAspect=true;
        var viewerBack=PlainButton(viewer.transform,"Back","返回",-835,480,130,58);
        var viewerTitle=Text(viewer.transform,"Name","",0,480,1370,52,30,Paper);
        var previous=PlainButton(viewer.transform,"Previous","上一张",-400,-485,230,64);
        var next=PlainButton(viewer.transform,"Next","下一张",400,-485,230,64);
        // Viewer方向键由面板切图，按钮本身不再用方向键移动焦点。
        foreach(var button in new[]{viewerBack,previous,next}){var navigation=button.navigation;navigation.mode=Navigation.Mode.None;button.navigation=navigation;}
        Set(panel,"_listRoot",list);Set(panel,"_viewerRoot",viewer);Set(panel,"_count",count);Set(panel,"_empty",empty);
        Set(panel,"_back",back);Set(panel,"_viewerBack",viewerBack);Set(panel,"_previous",previous);Set(panel,"_next",next);
        Set(panel,"_viewerImage",cg);Set(panel,"_viewerTitle",viewerTitle);
        Array(panel,"_tiles",tiles);Array(panel,"_thumbnails",thumbnails);Array(panel,"_names",names);
        viewer.SetActive(false);PrefabUtility.SaveAsPrefabAsset(root,path);UnityEngine.Object.DestroyImmediate(root);
    }
    private static Button PlainButton(Transform parent,string name,string value,float x,float y,float w,float h)
    {
        var image=Image(parent,name,x,y,w,h,new Color(.12f,.08f,.052f,.9f),true);
        var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;
        var colors=button.colors;colors.highlightedColor=colors.selectedColor=new Color(1,.86f,.66f,1);colors.pressedColor=new Color(.66f,.52f,.4f,1);button.colors=colors;
        var outline=image.gameObject.AddComponent<Outline>();outline.effectDistance=new Vector2(1,-1);outline.effectColor=new Color(.7f,.43f,.22f,.9f);
        Text(image.transform,"Label",value,0,0,w-18,h-8,28,Paper);return button;
    }
    private static void Navigate(Button button,Selectable up,Selectable down,Selectable left,Selectable right)
    {var navigation=button.navigation;navigation.mode=Navigation.Mode.Explicit;navigation.selectOnUp=up;navigation.selectOnDown=down;navigation.selectOnLeft=left;navigation.selectOnRight=right;button.navigation=navigation;}
    private static GameObject Node(Transform parent,string name)
    {var node=new GameObject(name,typeof(RectTransform));node.layer=5;node.transform.SetParent(parent,false);return node;}
    private static Image Image(Transform parent,string name,float x,float y,float w,float h,Color color,bool hit=false)
    {var image=Node(parent,name).AddComponent<Image>();At(R(image),x,y,w,h);image.color=color;image.raycastTarget=hit;return image;}
    private static Text Text(Transform parent,string name,string value,float x,float y,float w,float h,int size,Color color)
    {var text=Node(parent,name).AddComponent<Text>();At(R(text),x,y,w,h);text.font=font;text.text=value;text.fontSize=size;text.color=color;text.alignment=TextAnchor.MiddleCenter;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;text.raycastTarget=false;return text;}
    private static RectTransform R(Component component)=>(RectTransform)component.transform;
    private static RectTransform R(GameObject go)=>(RectTransform)go.transform;
    private static void At(RectTransform rect,float x,float y,float w,float h)
    {rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(w,h);rect.localScale=Vector3.one;}
    private static void Stretch(RectTransform rect)
    {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
    private static T Get<T>(UnityEngine.Object target,string field)where T:UnityEngine.Object=>new SerializedObject(target).FindProperty(field).objectReferenceValue as T;
    private static void Set(UnityEngine.Object target,string field,UnityEngine.Object value)
    {var serialized=new SerializedObject(target);serialized.FindProperty(field).objectReferenceValue=value;serialized.ApplyModifiedPropertiesWithoutUndo();}
    private static void Array(UnityEngine.Object target,string field,UnityEngine.Object[] values)
    {var serialized=new SerializedObject(target);var property=serialized.FindProperty(field);property.arraySize=values.Length;for(int i=0;i<values.Length;i++)property.GetArrayElementAtIndex(i).objectReferenceValue=values[i];serialized.ApplyModifiedPropertiesWithoutUndo();}
}
#endif

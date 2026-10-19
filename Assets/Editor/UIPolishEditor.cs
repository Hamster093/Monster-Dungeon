#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>按效果图配置原预制体。位置保存于编辑器，不在每次打开页面时重排。</summary>
public static class UIPolishEditor
{
    static Font font;
    static readonly Color Gold = new Color(1f, .89f, .71f);
    static RectTransform R(Component c) => (RectTransform)c.transform;
    static T Get<T>(UnityEngine.Object c, string name) where T : UnityEngine.Object => new SerializedObject(c).FindProperty(name).objectReferenceValue as T;
    static void Set(UnityEngine.Object c, string name, UnityEngine.Object value)
    { var so = new SerializedObject(c); so.FindProperty(name).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    static Sprite Old(string token) => AssetDatabase.LoadAssetAtPath<Sprite>(Directory.GetFiles("Assets/Resources_moved/Icons", "*.png", SearchOption.AllDirectories).First(p => Path.GetFileName(p).Contains(token)).Replace('\\', '/'));
    static void At(RectTransform r, float x, float y, float w, float h)
    { r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h); r.localScale = Vector3.one; }
    static void Edit(string path, Action<GameObject> action)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try { action(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    static Image Image(Transform parent, string name, Sprite sprite, float x, float y, float w, float h)
    {
        var image = parent.Find(name)?.GetComponent<Image>();
        if (!image) { var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.layer = 5; go.transform.SetParent(parent, false); image = go.GetComponent<Image>(); }
        image.gameObject.SetActive(true); image.sprite = sprite; image.color = Color.white; image.raycastTarget = false; image.preserveAspect = false;
        At(R(image), x, y, w, h); return image;
    }
    static Text Text(Transform parent, string name, string value, float x, float y, float w, float h, int size)
    {
        var text = parent.Find(name)?.GetComponent<Text>();
        if (!text) { var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.layer = 5; go.transform.SetParent(parent, false); text = go.GetComponent<Text>(); }
        text.text = value; text.font = font; text.fontSize = size; text.color = Gold; text.raycastTarget = false;
        text.alignment = TextAnchor.MiddleCenter; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
        At(R(text), x, y, w, h); return text;
    }
    static void Hide(Transform root, params string[] names)
    { foreach (string name in names) { var t = root.Find(name); if (t) t.gameObject.SetActive(false); } }
    static void Back(Button button, Transform parent, float x, float y)
    {
        if (!button) return;
        button.transform.SetParent(parent, false); At(R(button), x, y, 71, 61);
        var image = button.GetComponent<Image>(); image.sprite = Old("返回1"); image.color = Color.white; image.preserveAspect = true; image.raycastTarget = true;
        button.targetGraphic = image; button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState { highlightedSprite = Old("返回2"), pressedSprite = Old("返回2") };
        foreach (var text in button.GetComponentsInChildren<Text>(true)) text.enabled = false;
    }
    // 左侧标题与返回共用同一条基线：效果图 y=172。正文顶部 y=224。
    static void Header(Transform frame, Sprite bakedTitle, Text dynamicTitle, Button back, bool right = false)
    {
        float x = right ? -31 : 31;
        Image(frame, "UIRepairTitleStrip", Old("左标题"), x, 467, 414, 65);
        if (bakedTitle) Image(frame, "UIRepairTitle", bakedTitle, x, 467, bakedTitle.rect.width, bakedTitle.rect.height);
        if (dynamicTitle)
        {
            dynamicTitle.transform.SetParent(frame, false); At(R(dynamicTitle), x, 467, 340, 48);
            dynamicTitle.enabled = true; dynamicTitle.font = font; dynamicTitle.fontSize = 32; dynamicTitle.color = Gold;
            dynamicTitle.alignment = TextAnchor.MiddleCenter; dynamicTitle.raycastTarget = false;
            // 标题条后创建时也要确保动态文字在其上方，避免被不透明底板盖住。
            dynamicTitle.transform.SetAsLastSibling();
        }
        Back(back, frame, right ? 221 : -221, 467);
    }
    static void Body(Image frame) { frame.sprite = Old("_0000s_0005_左底板"); frame.color = Color.white; frame.preserveAspect = false; }

    [MenuItem("Tools/UI修复/按效果图修复标题头像与装饰")]
    public static void Configure()
    {
        font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Art/Fonts/FangZhengShuSong.ttf");
        if (!font) throw new Exception("缺少方正书宋字体");
        ConfigureAltar(false); ConfigureAltar(true); ConfigurePreparation(); ConfigureShop(); ConfigureBag();
        var portraits = Portraits();
        Edit("Assets/Resources/Panel/TalkPanel/CharacterArchivePanel.prefab", go => Archive(go, portraits, false));
        // 嵌套档案曾保留局部覆盖，必须同时保存 TalkPanel 中的实际绑定和位置。
        Edit("Assets/Resources/Panel/TalkPanel/TalkPanel.prefab", go => { Archive(go, portraits, true); ConfigureCurrentDialogue(go); });
        Edit("Assets/Resources/Panel/TalkPanel/CommissionPanel.prefab", go =>
        {
            var panel = go.GetComponent<CommissionPanel>(); At(R(panel), -691, -99, 482, 842);
            Body(go.GetComponent<Image>()); Hide(go.transform, "img_BG/img_Title"); Header(go.transform, null, Text(go.transform, "UIRepairCommissionTitle", "委托信息", 0, 0, 340, 48, 32), Get<Button>(panel, "CloseButton"));
            var payment=Get<Text>(panel, "txt_Payment"); At(R(payment), -5, -319, 335, 68); payment.fontSize=22; payment.resizeTextForBestFit=false;
        });
        ConfigureDescription(); ConfigurePlayer(); ConfigurePopupHeaders();
        foreach (string path in new[] { "Assets/Scenes/PlayerGame.unity", "Assets/Scenes/StartGame.unity" })
        {
            var scene = EditorSceneManager.OpenScene(path);
            foreach (var root in scene.GetRootGameObjects()) foreach (var scaler in root.GetComponentsInChildren<CanvasScaler>(true))
            {
                // 窗口最大化后常不是16:9；取能完整容纳设计画布的比例，避免底部按钮被裁掉。
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; EditorUtility.SetDirty(scaler);
            }
            EditorSceneManager.SaveScene(scene);
        }
        // 固定菜单的位置在画布设计坐标中；各行锚点也保存为中心，和脚本一致。
        Edit("Assets/Resources/Panel/ItemActionPanel.prefab", go =>
        {
            foreach (var button in go.GetComponentsInChildren<Button>(true))
            { R(button).anchorMin = R(button).anchorMax = R(button).pivot = new Vector2(.5f, .5f); }
        });
        AssetDatabase.SaveAssets();
        File.WriteAllText("UIPolishConfigured.txt", "标题、返回、独立头像、装饰、菜单与存档来源页配置完成");
    }
    static void ConfigureAltar(bool hell)
    {
        Edit("Assets/Resources/Panel/" + (hell ? "DecomposePanel" : "AbyssalAltarPanel") + ".prefab", go =>
        {
            Transform frame = hell ? go.transform : go.transform.Find("LeftPanel");
            At((RectTransform)frame, hell ? 691 : -691, -99, 482, 842);
            var body = hell ? frame.Find("Background").GetComponent<Image>() : frame.GetComponent<Image>();
            Body(body); if (hell) { At(R(body), 0, 0, 482, 842); body.transform.SetAsFirstSibling(); }
            Hide(frame, "TitleBarBG", "ArtTitle", "DescriptionText");
            var title = frame.Find("ArtTitle").GetComponent<Image>().sprite;
            var back = hell ? Get<Button>(go.GetComponent<DecomposePanel>(), "_closeButton") : Get<Button>(go.GetComponent<AbyssalAltarPanel>(), "_closelButton");
            Header(frame, title, null, back, hell);
            var decoration = frame.Find("ArtDecoration").GetComponent<Image>();
            var r = R(decoration); r.anchorMin = r.anchorMax = r.pivot = Vector2.one;
            // 以可见火焰右缘对齐内框；原图最右侧细枝允许自然伸出，不能裁掉。
            r.anchoredPosition = new Vector2(20, -16); r.sizeDelta = new Vector2(454, 454 * decoration.sprite.rect.height / decoration.sprite.rect.width);
            decoration.preserveAspect = true; decoration.raycastTarget = false;
            if (hell)
            {
                var p = go.GetComponent<DecomposePanel>(); At(R(Get<ItemSlot>(p, "_slot")), 0, -35, 196, 202);
                At(R(Get<Button>(p, "_decomposeButton")), 0, -245, 357, 83);
            }
            else
            {
                var p = go.GetComponent<AbyssalAltarPanel>(); At(R(Get<Button>(p, "_newMonsterButton")), 0, -190, 357, 83);
                At(R(Get<Button>(p, "_returnButton")), 0, -295, 357, 83);
            }
        });
    }
    static void ConfigurePreparation()
    {
        Edit("Assets/Resources/Panel/PreparationPanel.prefab", go =>
        {
            var frame = go.transform.Find("RightPanel"); At((RectTransform)frame, 691, -99, 482, 842);
            var body = frame.Find("Doc3Body").GetComponent<Image>(); Body(body); At(R(body), 0, 0, 482, 842);
            Hide(frame, "ArtTitle", "Doc2TitleStrip", "TitleBarBG");
            Header(frame, frame.Find("ArtTitle").GetComponent<Image>().sprite, null, null, true);
            var p = go.GetComponent<PreparationPanel>(); At(R(Get<Button>(p, "_startReceptionButton")), 0, -315, 410, 143);
            var grid = go.transform.Find("GridBackpack"); ((RectTransform)grid).anchoredPosition = new Vector2(0, -60);
        });
    }
    static void ConfigureShop()
    {
        Edit("Assets/Resources/Panel/ShopPanel.prefab", go =>
        {
            var p = go.GetComponent<ShopPanel>(); var frame = go.transform.Find("BGImage");
            At((RectTransform)frame, -691, -99, 482, 842); Body(frame.GetComponent<Image>());
            Header(frame, null, Get<Text>(p, "_titleText"), Get<Button>(p, "_closeButton"));
            var scroll = go.GetComponentInChildren<ScrollRect>(true); At(R(scroll), -691, -48, 422, 682);
        });
    }
    static void ConfigureBag()
    {
        Edit("Assets/Resources/Panel/BackpackPanel.prefab", go =>
        {
            var p = go.GetComponent<BackpackPanel>(); var frame = go.transform.Find("BGImage");
            At((RectTransform)frame, -691, -99, 482, 842); Body(frame.GetComponent<Image>());
            Hide(frame, "Image (1)");
            Header(frame, Old("背包界面标题"), null, go.GetComponentsInChildren<Button>(true).First(b => b.GetComponent<Image>()?.sprite?.name.Contains("返回") == true));
            Set(p, "_emptySprite", null);
            var scroll = go.GetComponentInChildren<ScrollRect>(true); At(R(scroll), -691, -90, 422, 708);
            var viewport = scroll.viewport; viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one; viewport.offsetMin = viewport.offsetMax = Vector2.zero;
            var grid = scroll.content.GetComponent<GridLayoutGroup>(); grid.padding = new RectOffset(22, 22, 10, 12); grid.cellSize = new Vector2(171, 174); grid.spacing = new Vector2(30, 26);
            var bottom = go.transform.Find("Image"); if (bottom) { At((RectTransform)bottom, -691, -467, 393, 60); bottom.SetAsLastSibling(); }
        });
    }
    static Sprite[] Portraits()
    {
        Directory.CreateDirectory("Assets/Resources/Art/Portraits");
        string[] paths = { "myth/IMG_7518(1).PNG", "费尔迪亚/IMG_7533.PNG", "sarah/IMG_7484(1).PNG", "奥德丽/IMG_7509.PNG", "Da/新版/IMG_7493(1).PNG" };
        // 每项为原纹理的归一化左上角和宽高，按各人物实际脸部单独取景。
        Rect[] crops = { new Rect(.28f,.08f,.47f,.293f), new Rect(.27f,.12f,.50f,.312f), new Rect(.25f,.275f,.53f,.381f), new Rect(.30f,.09f,.44f,.312f), new Rect(.28f,.06f,.45f,.41f) };
        int[] ids = { 1, 2, 4, 5, 6 }; var result = new Sprite[7];
        for (int i = 0; i < ids.Length; i++)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources_moved/Icons/立绘/" + paths[i]); var c = crops[i];
            var rect = new Rect(c.x * texture.width, (1 - c.y - c.height) * texture.height, c.width * texture.width, c.height * texture.height);
            string path = "Assets/Resources/Art/Portraits/Actor" + ids[i] + ".asset";
            var sprite = Sprite.Create(texture, rect, new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect); sprite.name = "档案头像_" + ids[i];
            var old = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (old) { EditorUtility.CopySerialized(sprite, old); UnityEngine.Object.DestroyImmediate(sprite); EditorUtility.SetDirty(old); result[ids[i] - 1] = old; }
            else { AssetDatabase.CreateAsset(sprite, path); result[ids[i] - 1] = sprite; }
        }
        return result;
    }
    static void Archive(GameObject root, Sprite[] portraits, bool embedded)
    {
        var p = root.GetComponentInChildren<CharacterArchivePanel>(true); if (!p) return;
        var frame = p.transform; At(R(p), embedded ? -691 : 0, embedded ? -99 : 0, 482, 842); Body(p.GetComponent<Image>());
        // 原标题仅为字形，补同一条黑色标题底条；参考整图不作为通用底板。
        foreach (var oldTitle in frame.GetComponentsInChildren<Image>(true).Where(im => im.name != "UIRepairTitle" && im.sprite && (im.sprite.name.Contains("冒险者档案标题") || im.sprite.name.Contains("左标-冒险者档案")))) oldTitle.gameObject.SetActive(false);
        Header(frame, Old("冒险者档案标题"), null, null);
        if (embedded)
        {
            // 返回与四页签属于另一个嵌套预制体，保留原父级及绑定，转换坐标后保存局部覆盖。
            var back = Get<Button>(p, "_closeButton"); var parent = (RectTransform)back.transform.parent;
            var world = frame.TransformPoint(new Vector2(-221, 467)); var point = (Vector2)parent.InverseTransformPoint(world) - parent.rect.center;
            Back(back, parent, point.x, point.y);
        }
        var identity = Image(frame, "UIRepairIdentityBackground", null, 0, 300, 434, 195); identity.color = new Color(.08f, .06f, .045f, .87f); identity.transform.SetAsFirstSibling();
        var divider = Image(frame, "UIRepairIdentityDivider", null, 0, 198, 420, 2); divider.color = new Color(.56f, .39f, .20f, .8f);
        var name = Get<Text>(p, "_name"); At(R(name), 82, 349, 270, 42); name.fontSize = 30; name.color = Gold;
        var intro = Get<Text>(p, "_headerIntroduction"); At(R(intro), 86, 249, 284, 94); intro.fontSize = 20; intro.resizeTextMinSize = 20; intro.resizeTextMaxSize = 20; intro.lineSpacing = 1.12f;
        var rank=Text(frame,"AdventurerLevel","等级：C",82,311,270,32,24); rank.alignment=TextAnchor.MiddleLeft; Set(p,"_level",rank);
        var icon = Get<Image>(p, "_iconImage"); var view = icon.transform.parent;
        At((RectTransform)view, -128, 312, 110, 110); At(R(icon), 0, 0, 110, 110); icon.preserveAspect = true;
        var iconFrame = frame.Find("Doc2PortraitFrame").GetComponent<Image>(); At(R(iconFrame), -128, 312, 130, 130);
        var placeholder = Text(view, "PortraitPlaceholder", "头像\n待补", 0, 0, 100, 100, 23); placeholder.color = new Color(.72f, .65f, .54f); Set(p, "_portraitPlaceholder", placeholder);
        var stats = Get<GameObject>(p, "_statsRoot"); At((RectTransform)stats.transform, 0, 26, 424, 350);
        Vector2[] positions = { new Vector2(0,-146), new Vector2(142,-87), new Vector2(165,43), new Vector2(85,133), new Vector2(-85,133), new Vector2(-165,43), new Vector2(-142,-87) };
        for (int i = 0; i < p.statTexts.Count; i++) { At(R(p.statTexts[i]), positions[i].x, positions[i].y, 118, 38); p.statTexts[i].fontSize = 23; }
        var so = new SerializedObject(p); var list = so.FindProperty("_portraits"); list.arraySize = 7;
        for (int i = 0; i < 7; i++) { list.GetArrayElementAtIndex(i).FindPropertyRelative("actorId").intValue = i + 1; list.GetArrayElementAtIndex(i).FindPropertyRelative("portrait").objectReferenceValue = portraits[i]; }
        so.FindProperty("_basicRecordPosition").vector2Value = new Vector2(0, -310); so.FindProperty("_basicRecordSize").vector2Value = new Vector2(400, 160);
        so.FindProperty("_otherRecordPosition").vector2Value = new Vector2(0, -100); so.FindProperty("_otherRecordSize").vector2Value = new Vector2(400, 580); so.ApplyModifiedPropertiesWithoutUndo();
        var body = Get<Text>(p, "_Introduction"); body.fontSize = 25; body.lineSpacing = 1.12f;
    }
    // 原底部整屏台词框超出了右侧纸张；只调整已有显示控件，不改变剧情推进或存档。
    static void ConfigureCurrentDialogue(GameObject root)
    {
        var talk=root.GetComponent<TalkPanel>(); var right=root.GetComponentInChildren<TalkPanelRight>(true);
        var scroll=Get<ScrollRect>(right,"_scrollRect"); At(R(scroll),0,65,440,570);
        scroll.viewport.anchorMin=Vector2.zero; scroll.viewport.anchorMax=Vector2.one;
        scroll.viewport.pivot=new Vector2(.5f,.5f); scroll.viewport.offsetMin=scroll.viewport.offsetMax=Vector2.zero;
        var content=scroll.content; content.anchorMin=new Vector2(0,1); content.anchorMax=Vector2.one;
        content.pivot=new Vector2(.5f,1); content.anchoredPosition=Vector2.zero; content.sizeDelta=Vector2.zero;
        var layout=content.GetComponent<VerticalLayoutGroup>(); layout.childAlignment=TextAnchor.UpperLeft;
        layout.childControlWidth=layout.childControlHeight=true; layout.childForceExpandHeight=false;
        var fit=content.GetComponent<ContentSizeFitter>(); fit.enabled=true; fit.horizontalFit=ContentSizeFitter.FitMode.Unconstrained; fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        var box=root.GetComponentsInChildren<RectTransform>(true).First(r=>r.name=="DialogBox"); box.gameObject.SetActive(true); box.SetParent(talk.RightDisplayArea.transform,false); At((RectTransform)box,0,-335,440,210);
        foreach(var image in box.GetComponentsInChildren<Image>(true)) { image.color=Color.clear; image.raycastTarget=false; }
        var name=Get<Text>(talk,"_speakerNameText"); At(R(name),0,80,410,38); name.fontSize=26; name.alignment=TextAnchor.MiddleLeft; name.color=new Color(.2f,.15f,.1f);
        var writer=Get<MonoBehaviour>(talk,"_typewriterComponent"); var line=Get<Text>(writer,"text");
        foreach(var text in box.GetComponentsInChildren<Text>(true)) if(text!=name && text!=line) text.gameObject.SetActive(false);
        var view=Image(box,"UIRepairCurrentLineViewport",null,0,-25,410,150); view.color=Color.clear; view.raycastTarget=true;
        if(!view.GetComponent<RectMask2D>()) view.gameObject.AddComponent<RectMask2D>();
        line.transform.SetParent(view.transform,false); var rect=R(line); rect.anchorMin=new Vector2(0,1); rect.anchorMax=Vector2.one; rect.pivot=new Vector2(.5f,1); rect.anchoredPosition=Vector2.zero; rect.sizeDelta=Vector2.zero;
        line.fontSize=26; line.color=new Color(.2f,.15f,.1f); line.alignment=TextAnchor.UpperLeft; line.horizontalOverflow=HorizontalWrapMode.Wrap; line.verticalOverflow=VerticalWrapMode.Overflow;
        var lineFit=line.GetComponent<ContentSizeFitter>(); if(!lineFit) lineFit=line.gameObject.AddComponent<ContentSizeFitter>(); lineFit.horizontalFit=ContentSizeFitter.FitMode.Unconstrained; lineFit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        var reading=view.GetComponent<ScrollRect>(); if(!reading) reading=view.gameObject.AddComponent<ScrollRect>(); reading.viewport=R(view); reading.content=rect; reading.horizontal=false; reading.vertical=true; reading.scrollSensitivity=24; reading.movementType=ScrollRect.MovementType.Clamped;
        var divider=Image(talk.RightDisplayArea.transform,"UIRepairDialogueDivider",null,0,-225,410,2); divider.color=new Color(.3f,.2f,.1f,.35f);
    }
    static void ConfigureDescription()
    {
        Edit("Assets/Resources/Panel/DescriptionPanel.prefab", go =>
        {
            var p = go.GetComponent<DescriptionPanel>(); var frame = go.transform.Find("ItemDescription");
            At((RectTransform)frame, 0, -20, 665, 545); Back(Get<Button>(p, "_confirmButton"), frame, -295, 267);
            At(R(Get<Image>(p, "_itemImage")), -193, 82, 144, 144);
            var name = Get<Text>(p, "_itemNameText"); At(R(name), 145, 139, 235, 54); name.alignment = TextAnchor.MiddleLeft; name.fontStyle = FontStyle.Normal; name.color = Gold;
            var quantity = Get<Text>(p, "_quantityText"); At(R(quantity), 142, 63, 230, 42); quantity.alignment = TextAnchor.MiddleLeft;
            Get<Text>(p, "_itemDescriptionText").fontSize = 25;
            var area=Image(frame,"CombatDifficultyArea",null,-95,-220,320,52); area.color=new Color(.06f,.05f,.04f,.68f);
            var difficulty=Text(area.transform,"Difficulty","战斗难度：一般",0,0,300,46,26); difficulty.alignment=TextAnchor.MiddleLeft;
            Set(p,"_combatArea",area.gameObject); Set(p,"_combatDifficulty",difficulty); area.gameObject.SetActive(false);
        });
    }
    static void ConfigurePlayer()
    {
        Edit("Assets/Resources/Panel/PlayerBuildPanel.prefab", go =>
        {
            var p = go.GetComponent<PlayerBuildPanel>(); var status = go.transform.Find("Status"); var body = status.Find("Body").GetComponent<Image>();
            At(R(body), -691, -99, 482, 842); Body(body);
            var title = status.GetComponentsInChildren<Text>(true).First(t => t.name == "Title"); Header(body.transform, null, title, Get<Button>(p, "_back"));
            foreach (var b in go.transform.Find("Creation").GetComponentsInChildren<Button>(true))
            {
                if (!b.name.StartsWith("Type") && !b.name.StartsWith("Thought")) continue;
                b.targetGraphic = b.GetComponent<Image>(); var outline = b.GetComponent<Outline>(); if (outline) outline.enabled = false;
            }
            var confirm = Get<Button>(p, "_confirm"); var colors = confirm.colors; colors.disabledColor = new Color(.32f, .29f, .24f, .65f); confirm.colors = colors;
            foreach (var t in go.transform.Find("Creation").GetComponentsInChildren<Text>(true).Where(t => t.name == "Description")) { t.fontSize = 23; t.lineSpacing = 1.15f; At(R(t), 0, -25, 330, 210); }
        });
        Edit("Assets/Resources/Panel/HUDPanel.prefab", go =>
        {
            var entry = go.transform.Find("PlayerStatusEntry"); At((RectTransform)entry, -691, 417, 450, 40);
            var image = entry.GetComponent<Image>(); image.color = new Color(.07f, .045f, .025f, .92f); image.raycastTarget = true;
            var button = entry.GetComponent<Button>(); button.targetGraphic = image;
            var text = entry.GetComponentInChildren<Text>(); text.fontSize = 22; text.color = Gold;
        });
    }
    static void ConfigurePopupHeaders()
    {
        Edit("Assets/Resources/Panel/SettingsPanel.prefab", go => Back(Get<Button>(go.GetComponent<SettingsPanel>(), "btn_close"), go.transform, -300, 247));
        Edit("Assets/Resources/UI/SaveSlotsPanel.prefab", go =>
        {
            var p = go.GetComponent<SaveSlotsPanel>(); var frame = go.transform.Find("ManagementBackground");
            Back(Get<Button>(p, "back"), frame, -285, 325);
        });
    }
    public static void BuildInteractive()
    {
        if (!Application.productName.EndsWith("_NarrativeValidation")) throw new Exception("仅允许隔离验证工程构建");
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720; PlayerSettings.resizableWindow = true;
        string path = Path.GetFullPath("../UIPolishPlayer/UIPolishPlayer.exe"); Directory.CreateDirectory(Path.GetDirectoryName(path));
        var report = BuildPipeline.BuildPlayer(new[] { "Assets/Scenes/StartGame.unity", "Assets/Scenes/PlayerGame.unity" }, path, BuildTarget.StandaloneWindows64, BuildOptions.Development);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("UI修复构建失败：" + report.summary.result);
    }
}
#endif

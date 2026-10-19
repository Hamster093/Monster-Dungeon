#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>说明4只在编辑器保存布局；正式运行不重新定位，也不生成一套临时界面。</summary>
public static class Doc4IntegrationEditor
{
    static Font font;
    static readonly Color Gold = new Color(1f, .89f, .68f);
    static Sprite Old(string token) => AssetDatabase.LoadAssetAtPath<Sprite>(Directory.GetFiles("Assets/Resources_moved/Icons", "*.png", SearchOption.AllDirectories).First(p => Path.GetFileName(p).Contains(token)).Replace('\\', '/'));
    static Sprite Settings(string token) => AssetDatabase.LoadAssetAtPath<Sprite>(Directory.GetFiles("Assets/Resources/Art/Settings", "*.png").First(p => Path.GetFileName(p).Contains(token)).Replace('\\', '/'));
    static RectTransform R(Component c) => (RectTransform)c.transform;
    static void At(RectTransform r, float x, float y, float w, float h)
    { r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h); r.localScale = Vector3.one; }
    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    static GameObject Node(Transform parent, string name)
    { var go = new GameObject(name, typeof(RectTransform)); go.layer = 5; go.transform.SetParent(parent, false); return go; }
    static Image Image(Transform parent, string name, Sprite sprite, float x, float y, float w, float h, bool hit = false)
    { var go = Node(parent, name); var image = go.AddComponent<Image>(); image.sprite = sprite; image.raycastTarget = hit; At(R(image), x, y, w, h); return image; }
    static Text Text(Transform parent, string name, string value, float x, float y, float w, float h, int size = 25)
    {
        var go = Node(parent, name); var text = go.AddComponent<Text>(); text.font = font; text.text = value;
        text.color = Gold; text.fontSize = size; text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false; At(R(text), x, y, w, h); return text;
    }
    static Button Button(Transform parent, string name, string title, float x, float y, float w, float h, Sprite sprite = null)
    {
        var image = Image(parent, name, sprite, x, y, w, h, true);
        image.color = sprite ? Color.white : new Color(.15f, .10f, .065f, .98f);
        var b = image.gameObject.AddComponent<Button>();
        var label = Text(image.transform, "Label", title, 0, 0, w - 20, h - 8, 28);
        b.targetGraphic = sprite ? (Graphic)image : label;
        var outline = image.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(.62f, .43f, .23f); outline.effectDistance = new Vector2(1, 1);
        return b;
    }
    static void Set(UnityEngine.Object component, string property, UnityEngine.Object value)
    { var so = new SerializedObject(component); so.FindProperty(property).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    static T Get<T>(UnityEngine.Object component, string property) where T : UnityEngine.Object => new SerializedObject(component).FindProperty(property).objectReferenceValue as T;
    static void Array(UnityEngine.Object component, string property, UnityEngine.Object[] values)
    { var so = new SerializedObject(component); var a = so.FindProperty(property); a.arraySize = values.Length; for (int i = 0; i < values.Length; i++) a.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; so.ApplyModifiedPropertiesWithoutUndo(); }
    static void Edit(string path, Action<GameObject> action)
    { var go = PrefabUtility.LoadPrefabContents(path); try { action(go); PrefabUtility.SaveAsPrefabAsset(go, path); } finally { PrefabUtility.UnloadPrefabContents(go); } }

    public static void Configure()
    {
        font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Art/Fonts/FangZhengShuSong.ttf");
        if (!font) throw new Exception("缺少现有方正书宋字体");
        CreatePlayerPanel();
        Edit("Assets/Resources/Panel/HUDPanel.prefab", root =>
        {
            var old = root.transform.Find("PlayerStatusEntry"); if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var button = Button(root.transform, "PlayerStatusEntry", "感知 55　学识 35　精神力 35  ›", -702, 414, 460, 38);
            button.GetComponent<Image>().color = Color.clear; UnityEngine.Object.DestroyImmediate(button.GetComponent<Outline>());
            var label = button.GetComponentInChildren<Text>(); label.fontSize = 23;
            Set(root.GetComponent<HUDPanel>(), "_playerButton", button); Set(root.GetComponent<HUDPanel>(), "_playerText", label);
        });
        Edit("Assets/Resources/Panel/DialogueOptionItem.prefab", root =>
        {
            var text = root.GetComponentInChildren<Text>(true); text.font = font; text.supportRichText = true;
            text.fontSize = 25; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
            var layout = root.GetComponent<VerticalLayoutGroup>();
            if (layout) { layout.padding = new RectOffset(14, 14, 12, 12); layout.childControlHeight = true; layout.childControlWidth = true; layout.childForceExpandHeight = false; }
        });
        Edit("Assets/Resources/Panel/DailySettlementPanel.prefab", root =>
        {
            var panel = root.GetComponent<DailySettlementPanel>();
            var retired = Get<Text>(panel, "txt_SpecialCommission");
            if (retired) { var parent = retired.transform.parent; Set(panel, "txt_SpecialCommission", null); UnityEngine.Object.DestroyImmediate(parent.gameObject); }
            string[] fields = { "txt_MonsterBusiness", "txt_LegacyCommission", "txt_Total", "txt_Installment" };
            for (int i = 0; i < fields.Length; i++) { var text = Get<Text>(panel, fields[i]); At(R(text.transform.parent), -225, 190 - i * 85, 400, 65); }
        });
        Edit("Assets/Resources/Panel/PreparationPanel.prefab", root =>
        {
            var panel = root.GetComponent<PreparationPanel>(); var retired = Get<Button>(panel, "_specialPreviewButton");
            if (retired) { Set(panel, "_specialPreviewButton", null); UnityEngine.Object.DestroyImmediate(retired.gameObject); }
        });
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/PlayerGame.unity");
        foreach (var ui in UnityEngine.Object.FindObjectsOfType<UIManager>(true))
        {
            ui.Configs.RemoveAll(c => c.panelName == nameof(SpecialCommissionPanel));
            if (!ui.Configs.Any(c => c.panelName == nameof(PlayerBuildPanel))) ui.Configs.Add(new UIManager.PanelConfig
            { panelName = nameof(PlayerBuildPanel), prefab = AssetDatabase.LoadAssetAtPath<PlayerBuildPanel>("Assets/Resources/Panel/PlayerBuildPanel.prefab"), layer = UILayer.Top });
            EditorUtility.SetDirty(ui);
        }
        // 退役预制体移出运行资源，不删除备份；旧类型壳仅供历史编辑器检查编译。
        foreach (string name in new[] { "SpecialCommissionPanel", "SpecialCommissionDetailPanel", "SpecialCommissionItem" })
        {
            var path = "Assets/Resources/Panel/TalkPanel/" + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path)) AssetDatabase.DeleteAsset(path);
        }
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        // 旧接入工具重跑时也保留最新效果图布局，不把标题和返回恢复为旧坐标。
        UIPolishEditor.Configure();
        File.WriteAllText("Doc4Configured.txt", "说明4预制体、字体、场景及退役资源配置完成");
    }
    static void CreatePlayerPanel()
    {
        var root = new GameObject("PlayerBuildPanel", typeof(RectTransform), typeof(PlayerBuildPanel));
        root.layer = 5; Stretch(R(root.transform)); var panel = root.GetComponent<PlayerBuildPanel>();
        var choice = Node(root.transform, "Creation"); Stretch(R(choice.transform));
        var dim = Image(choice.transform, "Dim", null, 0, 0, 1920, 1080, true); Stretch(R(dim)); dim.color = new Color(.025f, .018f, .014f, .97f);
        Text(choice.transform, "Title", "你将怎样经营这座地牢？", 0, 430, 1400, 70, 42);
        Text(choice.transform, "TypeCaption", "自身类型", 0, 342, 1200, 40, 25);
        Button[] types = new Button[3], modes = new Button[3]; Text[] values = new Text[3], descriptions = new Text[3];
        string[] typeNames = { "敏锐型", "博识型", "坚毅型" };
        string[] desc = {
            "掌握人物信息，再深入了解。\n\nⅠ 信息检定＋5\nⅡ 熟练度10：＋10\nⅢ 熟练度25：＋15\n需已有该人物的有效信息。",
            "你的话，更能改变对方。\n\nⅠ 七维变化幅度额外1点\nⅡ 熟练度10：额外2点\nⅢ 熟练度25：额外3点\n上升、下降都会扩大。",
            "每次失利，都为下一次蓄力。\n\n玩家检定失败累计＋5\nⅠ 最多＋10\nⅡ 熟练度10：最多＋15\nⅢ 熟练度25：最多＋20\n成功后累计清零。" };
        for (int i = 0; i < 3; i++)
        {
            float x = (i - 1) * 410;
            types[i] = Button(choice.transform, "Type" + i, "", x, 240, 370, 145, Old("小弹窗用按钮（透明）1"));
            var typeLabel = types[i].GetComponentInChildren<Text>(); typeLabel.text = typeNames[i]; At(R(typeLabel), 0, 30, 340, 45); typeLabel.fontSize = 31;
            values[i] = Text(types[i].transform, "Values", "", 0, -27, 345, 60, 22);
            modes[i] = Button(choice.transform, "Thought" + i, "", x, -72, 370, 285);
            var modeLabel = modes[i].GetComponentInChildren<Text>(); modeLabel.text = PlayerBuildState.ThoughtNames[i]; At(R(modeLabel), 0, 102, 340, 44); modeLabel.fontSize = 31;
            descriptions[i] = Text(modes[i].transform, "Description", desc[i], 0, -27, 326, 210, 21);
            descriptions[i].color = new Color(.84f, .79f, .69f);
        }
        Text(choice.transform, "ThoughtCaption", "思维模式 · 随剧情选择成长，本局固定", 0, 110, 1250, 40, 24);
        var summary = Text(choice.transform, "Summary", "", 0, -300, 1300, 72, 24);
        var confirm = Button(choice.transform, "Confirm", "确定选择", 0, -410, 330, 74, Old("小弹窗用按钮（透明）1"));
        var status = Node(root.transform, "Status"); Stretch(R(status.transform));
        Image(status.transform, "Body", Old("_0000s_0005_左底板"), -702, -40, 482, 842, true);
        Text(status.transform, "Title", "自身状态", -680, 325, 330, 65, 35);
        var back = Button(status.transform, "Back", "", -885, 325, 71, 61, Old("返回1"));
        var stats = Text(status.transform, "Attributes", "", -700, 170, 380, 220, 31);
        var mode = Text(status.transform, "Thought", "", -700, 5, 400, 70, 28);
        var effect = Text(status.transform, "Effect", "", -700, -105, 360, 150, 25);
        var empty = Image(status.transform, "ProgressTrack", Settings("空进度条"), -700, -223, 350, 17);
        var fill = Image(status.transform, "ProgressFill", Settings("满进度条"), -700, -223, 350, 17);
        fill.type = UnityEngine.UI.Image.Type.Filled; fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; fill.fillAmount = 0;
        var progress = Text(status.transform, "Progress", "", -700, -268, 390, 50, 24);
        var scope = Text(status.transform, "Scope", "", -700, -365, 360, 105, 23); scope.color = new Color(.77f, .73f, .65f);
        Set(panel, "_choiceRoot", choice); Set(panel, "_statusRoot", status); Set(panel, "_confirm", confirm); Set(panel, "_back", back);
        Set(panel, "_summary", summary); Set(panel, "_stats", stats); Set(panel, "_mode", mode); Set(panel, "_effect", effect);
        Set(panel, "_progress", progress); Set(panel, "_scope", scope); Set(panel, "_progressFill", fill);
        Array(panel, "_types", types); Array(panel, "_thoughts", modes); Array(panel, "_typeValues", values); Array(panel, "_thoughtDescriptions", descriptions);
        root.SetActive(false); PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/Panel/PlayerBuildPanel.prefab"); UnityEngine.Object.DestroyImmediate(root);
    }

    public static void BuildInteractive()
    {
        if (!Application.productName.EndsWith("_NarrativeValidation")) throw new Exception("仅允许隔离验证工程构建");
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.resizableWindow = true;
        string path = Path.GetFullPath("../Doc4Player/Doc4Player.exe"); Directory.CreateDirectory(Path.GetDirectoryName(path));
        var report = BuildPipeline.BuildPlayer(new[] { "Assets/Scenes/StartGame.unity", "Assets/Scenes/PlayerGame.unity" }, path, BuildTarget.StandaloneWindows64, BuildOptions.Development);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("说明4构建失败：" + report.summary.result);
        File.WriteAllText("Doc4Build.txt", path);
    }
}
#endif

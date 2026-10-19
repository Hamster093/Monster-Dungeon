#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>只在编辑器配置原预制体和素材导入参数；运行时不生成第二套页面。</summary>
public static class ArtIntegrationEditor
{
    static Font font;
    static readonly Color Ink = new Color(.20f,.12f,.08f);
    static Sprite S(string folder, string token) => AssetDatabase.LoadAssetAtPath<Sprite>(Directory.GetFiles("Assets/Resources/Art/" + folder, "*.png").Single(p => Path.GetFileName(p).Contains(token)).Replace('\\','/'));
    static Sprite Old(string token) => AssetDatabase.LoadAssetAtPath<Sprite>(Directory.GetFiles("Assets/Resources_moved/Icons", "*.png", SearchOption.AllDirectories).First(p => Path.GetFileName(p).Contains(token)).Replace('\\','/'));
    static T Ref<T>(UnityEngine.Object c, string name) where T : UnityEngine.Object => new SerializedObject(c).FindProperty(name).objectReferenceValue as T;
    static void Set(UnityEngine.Object c, string name, UnityEngine.Object value) { var so = new SerializedObject(c); so.FindProperty(name).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    static RectTransform R(Component c) => (RectTransform)c.transform;
    static void At(RectTransform r, float x, float y, float w, float h)
    { r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f,.5f); r.anchoredPosition = new Vector2(x,y); r.sizeDelta = new Vector2(w,h); r.localScale = Vector3.one; }
    static void Stretch(RectTransform r) { r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;r.localScale=Vector3.one; }
    static Image Img(Transform parent, string name, Sprite sprite, float x, float y, float w, float h, bool hit=false)
    { var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var im=go.GetComponent<Image>();im.sprite=sprite;im.raycastTarget=hit;At(R(im),x,y,w,h);return im; }
    static Text Txt(Transform parent, string name, string value, float x, float y, float w, float h, int size=26, Color? color=null)
    { var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var t=go.GetComponent<Text>();t.text=value;t.font=font;t.fontSize=size;t.color=color??Ink;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;At(R(t),x,y,w,h);return t; }
    static void Skin(Button b, Sprite normal, Sprite selected=null, bool baked=true)
    {
        if (b==null)return;var im=b.GetComponent<Image>();im.sprite=normal;im.color=Color.white;im.type=Image.Type.Simple;im.preserveAspect=true;im.raycastTarget=true;b.targetGraphic=im;
        if(selected!=null){b.transition=Selectable.Transition.SpriteSwap;b.spriteState=new SpriteState{highlightedSprite=selected,pressedSprite=selected,selectedSprite=selected,disabledSprite=normal};}
        if(baked)foreach(var t in b.GetComponentsInChildren<Text>(true))t.enabled=false;
    }
    static void Edit(string path, Action<GameObject> action)
    {var root=PrefabUtility.LoadPrefabContents(path);action(root);ApplyFont(root);PrefabUtility.SaveAsPrefabAsset(root,path);PrefabUtility.UnloadPrefabContents(root);}
    static void ApplyFont(GameObject root)
    {
        // 各个独立编辑器入口都能直接调用，不能依赖另一次配置留下的静态字体引用。
        if(!font)font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Art/Fonts/FangZhengShuSong.ttf");
        if(!font)throw new Exception("未找到方正书宋字体，停止保存预制体以保留原绑定");
        foreach(var text in root.GetComponentsInChildren<Text>(true)){text.font=font;text.raycastTarget=false;}foreach(var im in root.GetComponentsInChildren<Image>(true)){if(im.GetComponent<ScrollRect>())im.raycastTarget=true;if(im.name=="LeftDisplayArea")im.raycastTarget=false;}
    }
    static void FreezeLayouts(GameObject root)
    {foreach(var l in root.GetComponentsInChildren<LayoutGroup>(true))l.enabled=false;foreach(var f in root.GetComponentsInChildren<ContentSizeFitter>(true))f.enabled=false;}
    static void Back(Button b, float x, float y)
    {if(!b)return;Skin(b,Old("返回1"),Old("返回2"));At(R(b),x,y,71,61);}

    public static void Configure()
    {
        foreach(var path in Directory.GetFiles("Assets/Resources/Art","*.png",SearchOption.AllDirectories))
        {
            var ti=(TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));ti.textureType=TextureImporterType.Sprite;ti.spriteImportMode=SpriteImportMode.Single;
            ti.alphaIsTransparency=true;ti.mipmapEnabled=false;ti.textureCompression=TextureImporterCompression.Uncompressed;ti.maxTextureSize=4096;
            ti.filterMode=path.Contains("Board")?FilterMode.Point:FilterMode.Bilinear;ti.spritePixelsPerUnit=100;ti.SaveAndReimport();
        }
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Art/Fonts/FangZhengShuSong.ttf");
        Edit("Assets/Resources/Panel/NamePanle.prefab",root=>{
            foreach(var image in root.GetComponentsInChildren<Image>(true)){image.color=new Color(.07f,.05f,.04f,.95f);image.raycastTarget=false;}
            foreach(var text in root.GetComponentsInChildren<Text>(true)){text.color=new Color(1,.9f,.7f);text.raycastTarget=false;}
        });
        if(!font)throw new Exception("方正书宋字体导入失败");
        BindMonsters();
        foreach(var path in Directory.GetFiles("Assets/Resources","*.prefab",SearchOption.AllDirectories)) Edit(path,go=>{});
        ConfigurePreparation();ConfigureShop();ConfigureAltar();ConfigureSettings();ConfigureHistory();ConfigureSpecial();ConfigureArchive();CreateSaveSlots();ConfigureScenes();
        AssetDatabase.SaveAssets();
        File.WriteAllText("ArtConfigured.txt","美术、字体、预制体及四槽配置完成");
    }
    static void BindMonsters()
    {
        string[] normal={"0001_史莱姆","0002_强壮史莱姆","0003_高达史莱姆","0006_哥布林","0005_强壮哥布林","0004_首领哥布林","0007_地狱犬","0008_三头地狱犬","0009_灰烬地狱犬","0012_曼德拉草","0011_树精","0010_食人花","0015_石巨人","0014_水晶魔像","0013_璀璨魔像","0016_双足飞龙","0017_暗黑龙"};
        string[] evolved={"0000_史莱姆","0001_强壮史莱姆","0002_高大史莱姆","0003_哥布林","0004_强壮哥布林","0005_哥布林首领","0006_地狱犬","0007_三头地狱犬","0008_灰烬地狱犬","0009_曼德拉草","0010_树精","0011_食人花","0012_石巨人","0013_水晶魔像","0014_璀璨魔像","0016_双足巨龙","0015_巨龙"};
        for(int i=0;i<17;i++){var def=AssetDatabase.LoadAssetAtPath<ItemShapeDefinition>($"Assets/Scripts/qipan/Monster-shaped/Monster{i+1:00}.asset");def.NormalIcon=S("Board","棋盘图标_"+normal[i]);def.EvolvedIcon=S("Board","进化_"+evolved[i]);EditorUtility.SetDirty(def);}
        foreach(var path in new[]{"Assets/Scripts/qipan/Cell.prefab","Assets/Scripts/qipan/GridBackpack.prefab","Assets/Resources/Panel/PreparationPanel.prefab"}) Edit(path,root=>{
            foreach(var cell in root.GetComponentsInChildren<BackpackCell>(true)){cell.SpecialIcons=new[]{S("Board","_矿石"),S("Board","_池沼"),S("Board","_监牢"),S("Board","_裂口")};foreach(var text in cell.GetComponentsInChildren<Text>(true))text.enabled=false;}
        });
    }
    static void ConfigurePreparation()
    {
        Edit("Assets/Resources/Panel/PreparationPanel.prefab",root=>{
            var panel=root.GetComponent<PreparationPanel>();var right=root.transform.Find("RightPanel");FreezeLayouts(right.gameObject);At((RectTransform)right,710,-78,482,923);
            right.GetComponent<Image>().sprite=S("Preparation","_整备界面右底板");right.GetComponent<Image>().color=Color.white;
            var title=right.Find("TitleBarBG");title.gameObject.SetActive(false);Img(right,"ArtTitle",S("Preparation","_整备界面标题"),0,388,141,39);
            var room=right.Find("ButtonRoom");Stretch((RectTransform)room);
            string[] fields={"_monsterBackpackButton","_abyssButton","_shopButton"};string[] tokens={"魔物仓库","深渊祭坛","商店"};
            for(int i=0;i<3;i++){var b=Ref<Button>(panel,fields[i]);Skin(b,S("Preparation","_"+tokens[i]+"1"),S("Preparation","_"+tokens[i]+"2"));At(R(b),0,240-i*160,314,111);}
            foreach(var obsolete in root.GetComponentsInChildren<OpenMonsterStorageButton>(true))UnityEngine.Object.DestroyImmediate(obsolete);
            var start=Ref<Button>(panel,"_startReceptionButton");Skin(start,S("Preparation","_开始接待1"),S("Preparation","_开始接待2"));At(R(start),0,-360,410,143);
            Img(right,"ArtDecoration",S("Preparation","_整备界面装饰"),0,-192,393,147);
            var grid=root.transform.Find("GridBackpack");var board=grid.Find("Board");At((RectTransform)grid,0,-42,1920,1080);At((RectTransform)board,0,0,705,705);
            var bg=Img(grid,"ArtBoardFrame",S("Preparation","_棋盘"),0,0,855,856);bg.transform.SetAsFirstSibling();
            board.GetComponent<Image>().color=new Color(0,0,0,.12f);
        });
    }
    static void ConfigureShop()
    {
        Edit("Assets/Resources/Panel/ShopPanel.prefab",root=>{
            var panel=root.GetComponent<ShopPanel>();var bg=root.transform.Find("BGImage");bg.GetComponent<Image>().sprite=Old("左底板");At((RectTransform)bg,-688,-77,482,923);
            root.transform.Find("DialogueText").gameObject.SetActive(false);var title=Ref<Text>(panel,"_titleText");At(R(title),-660,350,300,48);title.fontSize=34;title.color=new Color(1,.85f,.58f);
            var scroll=root.GetComponentInChildren<ScrollRect>(true);At(R(scroll),-680,-84,436,720);scroll.GetComponent<Image>().color=Color.clear;
            var grid=scroll.content.GetComponent<GridLayoutGroup>();grid.cellSize=new Vector2(196,220);grid.spacing=new Vector2(12,16);grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=2;
            foreach(var slot in panel._itemSlots){var image=Ref<Image>(slot,"_itemImage");At(R(image),0,18,164,158);image.preserveAspect=true;var sub=Ref<Text>(slot,"_subText");if(sub){At(R(sub),0,-82,180,45);sub.fontSize=24;sub.color=new Color(1,.85f,.58f);var price=Img(slot.transform,"ArtPrice",S("Preparation","_价格框"),0,-82,190,59);price.transform.SetSiblingIndex(sub.transform.GetSiblingIndex());}}
            Back(Ref<Button>(panel,"_closeButton"),-900,350);
        });
        Edit("Assets/Resources/Panel/ShopItemActionPanel.prefab",root=>{
            var panel=root.GetComponent<ShopItemActionPanel>();var image=root.GetComponent<Image>();if(image){image.sprite=S("Preparation","_购买小弹窗");image.color=Color.white;}
            At((RectTransform)root.transform,-420,80,214,199);
            var so=new SerializedObject(panel);so.FindProperty("_fixedPosition").vector2Value=new Vector2(-420,80);so.ApplyModifiedPropertiesWithoutUndo();
        });
    }
    static void ConfigureAltar()
    {
        Edit("Assets/Resources/Panel/AbyssalAltarPanel.prefab",root=>{
            var p=root.GetComponent<AbyssalAltarPanel>();var left=root.transform.Find("LeftPanel");At((RectTransform)left,-688,-77,482,923);left.GetComponent<Image>().sprite=Old("左底板");left.GetComponent<Image>().color=Color.white;
            left.Find("TitleBarBG").gameObject.SetActive(false);left.Find("DescriptionText").gameObject.SetActive(false);
            Img(left,"ArtTitle",S("Preparation","_深渊祭坛标题"),30,430,140,37);Img(left,"ArtDecoration",S("Preparation","_祭坛装饰文字"),0,40,468,591);
            var a=Ref<Button>(p,"_newMonsterButton");Skin(a,S("Preparation","_祭坛选项1-1"),S("Preparation","_祭坛选项1-2"));At(R(a),0,-270,357,83);
            var b=Ref<Button>(p,"_returnButton");Skin(b,S("Preparation","_祭坛选项2-1"),S("Preparation","_祭坛选项2-2"));At(R(b),0,-370,357,83);Back(Ref<Button>(p,"_closelButton"),-220,430);
        });
        Edit("Assets/Resources/Panel/DecomposePanel.prefab",root=>{
            var p=root.GetComponent<DecomposePanel>();At((RectTransform)root.transform,710,-77,482,923);
            root.transform.Find("Background").GetComponent<Image>().sprite=S("Preparation","_整备界面右底板");
            root.transform.Find("TitleBarBG").gameObject.SetActive(false);root.transform.Find("DescriptionText").gameObject.SetActive(false);
            foreach(var image in root.GetComponentsInChildren<Image>(true).Where(i=>i.transform.parent==root.transform&&i.name=="Window"))image.enabled=false;
            Img(root.transform,"ArtTitle",S("Preparation","_地狱标题"),0,388,74,37);Img(root.transform,"ArtDecoration",S("Preparation","_地狱界面文字装饰"),0,20,464,591);
            var slot=Ref<ItemSlot>(p,"_slot");At(R(slot),0,-40,196,202);slot.GetComponent<Image>().sprite=S("Preparation","_框框");slot.transform.SetParent(root.transform,false);At(R(slot),0,-40,196,202);
            var b=Ref<Button>(p,"_decomposeButton");b.transform.SetParent(root.transform,false);Skin(b,S("Preparation","_地狱界面选项1"),S("Preparation","_地狱界面选项2"));At(R(b),0,-370,357,83);
            var back=Ref<Button>(p,"_closeButton");back.transform.SetParent(root.transform,false);Back(back,-220,430);
            foreach(var t in root.GetComponentsInChildren<Text>(true))if(t.name=="Title")t.enabled=false;
        });
    }
    static void ConfigureSettings()
    {
        Edit("Assets/Resources/Panel/SettingsPanel.prefab",root=>{
            var p=root.GetComponent<SettingsPanel>();At((RectTransform)root.transform,0,0,665,543);root.GetComponent<Image>().sprite=S("Settings","_设置界面底板");root.GetComponent<Image>().color=Color.white;FreezeLayouts(root);
            foreach(var text in root.GetComponentsInChildren<Text>(true))text.enabled=false;
            root.transform.Find("pnl_settings_header/img_settings_icon").gameObject.SetActive(false);root.transform.Find("pnl_settings_header").GetComponent<Image>().enabled=false;
            foreach(var child in new[]{root.transform.Find("pnl_settings_content"),root.transform.Find("pnl_settings_footer"),root.transform.Find("pnl_settings_header")})Stretch((RectTransform)child);
            foreach(var field in new[]{"tgl_music","tgl_sfx"}){var toggle=Ref<Toggle>(p,field);toggle.transform.SetParent(root.transform,false);At(R(toggle),-224,field=="tgl_music"?120:16,37,34);var bg=toggle.targetGraphic as Image;bg.sprite=S("Settings","_音量未选中");bg.color=Color.white;Stretch(R(bg));var check=toggle.graphic as Image;check.sprite=S("Settings","_音量选中");check.color=Color.white;Stretch(R(check));}
            foreach(var field in new[]{"sld_music","sld_sfx"}){var slider=Ref<Slider>(p,field);slider.transform.SetParent(root.transform,false);At(R(slider),30,field=="sld_music"?74:-30,359,28);var bg=slider.transform.Find("Background").GetComponent<Image>();bg.sprite=S("Settings","_空进度条");bg.color=Color.white;At(R(bg),0,0,359,8);var fill=slider.fillRect.GetComponent<Image>();fill.sprite=S("Settings","_满进度条");fill.color=Color.white;}
            var resolution=Ref<Dropdown>(p,"dropdown_resolution");resolution.gameObject.SetActive(false);
            var full=Ref<Toggle>(p,"tgl_fullscreen");full.transform.SetParent(root.transform,false);At(R(full),153,-86,160,36);foreach(var image in full.GetComponentsInChildren<Image>(true))image.enabled=false;
            var graphic=Img(full.transform,"FullscreenChoice",S("Settings","_选中全屏"),0,0,160,36,true);full.targetGraphic=graphic;full.graphic=null;full.transition=Selectable.Transition.None;
            var read=Ref<Button>(p,"btn_read_save");read.transform.SetParent(root.transform,false);Skin(read,S("Settings","_读取档案1"),S("Settings","_读取档案2"));At(R(read),120,-188,182,63);
            var save=Ref<Button>(p,"btn_save_game");save.transform.SetParent(root.transform,false);Skin(save,S("Settings","_保存游戏1"),S("Settings","_保存游戏2"));At(R(save),-120,-188,182,63);
            var close=Ref<Button>(p,"btn_close");close.transform.SetParent(root.transform,false);Back(close,-300,265);
        });
    }
    static void ConfigureHistory()
    {
        Edit("Assets/Resources/Panel/DialogueHistoryPanel.prefab",root=>{
            Stretch((RectTransform)root.transform);root.GetComponent<Image>().color=Color.clear;root.transform.Find("Background").GetComponent<Image>().sprite=S("Settings","_历史记录");root.transform.Find("TitleText").gameObject.SetActive(false);
            var p=root.GetComponent<DialogueHistoryPanel>();var scroll=Ref<ScrollRect>(p,"_scrollRect");At(R(scroll),80,-10,1010,660);scroll.GetComponent<Image>().color=Color.clear;
            Stretch(scroll.viewport);var bg=scroll.viewport.GetComponent<Image>();if(bg)bg.color=Color.white;
            if(scroll.verticalScrollbar)scroll.verticalScrollbar.gameObject.SetActive(false);scroll.verticalScrollbar=null;
            foreach(var text in root.GetComponentsInChildren<Text>(true))text.color=Ink;Back(Ref<Button>(p,"_closeButton"),850,-410);
        });
        Edit("Assets/Resources/DialogueHistoryEntry.prefab",root=>{foreach(var text in root.GetComponentsInChildren<Text>(true)){text.fontSize=26;text.color=Ink;}});
    }
    static void ConfigureSpecial()
    {
        Action<GameObject> detail=root=>{
            var p=root.GetComponent<SpecialCommissionDetailPanel>();At(R(p),0,0,665,544);
            var name=Ref<Text>(p,"_nameText");At(R(name),0,193,540,56);name.fontSize=32;name.color=new Color(1,.85f,.6f);
            var reward=Ref<Text>(p,"_rewardText");At(R(reward),0,-100,540,44);reward.fontSize=26;reward.color=new Color(1,.85f,.6f);
            var influence=Ref<Text>(p,"_influenceText");At(R(influence),0,-150,540,44);influence.fontSize=24;influence.color=new Color(1,.85f,.6f);
            var text=Ref<Text>(p,"_descriptionText");text.fontSize=26;text.color=new Color(1,.9f,.75f);text.alignment=TextAnchor.UpperLeft;text.verticalOverflow=VerticalWrapMode.Overflow;
            var view=Img(root.transform,"DescriptionViewport",null,0,35,550,225,true);view.color=Color.clear;view.gameObject.AddComponent<RectMask2D>();var scroll=view.gameObject.AddComponent<ScrollRect>();text.transform.SetParent(view.transform,false);var r=R(text);r.anchorMin=new Vector2(0,1);r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,1);r.anchoredPosition=Vector2.zero;r.sizeDelta=new Vector2(0,225);scroll.viewport=R(view);scroll.content=r;scroll.horizontal=false;scroll.scrollSensitivity=24;Set(p,"_descriptionScroll",scroll);
            var accept=Ref<Button>(p,"_acceptButton");At(R(accept),220,-220,130,60);Back(Ref<Button>(p,"_closeButton"),-300,245);
        };
        Edit("Assets/Resources/Panel/TalkPanel/SpecialCommissionDetailPanel.prefab",detail);
        Edit("Assets/Resources/Panel/TalkPanel/SpecialCommissionPanel.prefab",root=>{var p=root.GetComponent<SpecialCommissionPanel>();var d=Ref<SpecialCommissionDetailPanel>(p,"_detailPanel");
            // 嵌套详情原有的局部位置会覆盖源预制体；这里明确配置为画面中央。
            At(R(d),680,80,665,544);d.gameObject.SetActive(false);
        });
    }
    static void ConfigureArchive()
    {
        foreach(var path in new[]{"Assets/Resources/Panel/TalkPanel/CharacterArchivePanel.prefab","Assets/Resources/Panel/TalkPanel/TalkPanel.prefab"})Edit(path,root=>{
            var a=root.GetComponentInChildren<CharacterArchivePanel>(true);if(!a)return;
            At(R(a.radarChart),20,133,300,300);a.radarChart.radius=130;a.radarChart.raycastTarget=false;
            var name=Ref<Text>(a,"_name");At(R(name),0,150,400,30);name.fontSize=24;name.gameObject.SetActive(true);
            foreach(var t in a.statTexts)t.fontSize=22;
        });
    }
    static void CreateSaveSlots()
    {
        Directory.CreateDirectory("Assets/Resources/UI");var root=new GameObject("SaveSlotsPanel",typeof(RectTransform),typeof(Image),typeof(SaveSlotsPanel));Stretch((RectTransform)root.transform);root.GetComponent<Image>().color=new Color(0,0,0,.65f);root.GetComponent<Image>().raycastTarget=true;
        var p=root.GetComponent<SaveSlotsPanel>();var bg=Img(root.transform,"ManagementBackground",S("Settings","_档案管理"),0,-20,491,719,true);var title=Txt(bg.transform,"Title","存档管理",0,280,380,55,32,new Color(1,.85f,.6f));
        var buttons=new Button[4];var texts=new Text[4];for(int i=0;i<4;i++){var image=Img(bg.transform,"Slot"+(i+1),S("Settings","_存档单元"),0,170-i*140,408,116,true);buttons[i]=image.gameObject.AddComponent<Button>();buttons[i].targetGraphic=image;var colors=buttons[i].colors;colors.highlightedColor=new Color(1,.9f,.65f);buttons[i].colors=colors;texts[i]=Txt(image.transform,"Summary","空存档",0,0,350,100,24,new Color(1,.88f,.68f));}
        var close=Img(bg.transform,"Back",Old("返回1"),-220,325,71,61,true).gameObject.AddComponent<Button>();Skin(close,Old("返回1"),Old("返回2"));
        var so=new SerializedObject(p);var s=so.FindProperty("slots");s.arraySize=4;var t=so.FindProperty("summaries");t.arraySize=4;for(int i=0;i<4;i++){s.GetArrayElementAtIndex(i).objectReferenceValue=buttons[i];t.GetArrayElementAtIndex(i).objectReferenceValue=texts[i];}so.FindProperty("title").objectReferenceValue=title;so.FindProperty("back").objectReferenceValue=close;so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/UI/SaveSlotsPanel.prefab");UnityEngine.Object.DestroyImmediate(root);
    }
    static void ConfigureScenes()
    {
        foreach(var scenePath in new[]{"Assets/Scenes/PlayerGame.unity","Assets/Scenes/StartGame.unity"})
        {
            var scene=EditorSceneManager.OpenScene(scenePath);
            foreach(var root in scene.GetRootGameObjects())
            {
                foreach(var c in root.GetComponentsInChildren<CanvasScaler>(true)){c.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;c.referenceResolution=new Vector2(1920,1080);c.matchWidthOrHeight=.5f;}
                foreach(var t in root.GetComponentsInChildren<Text>(true))t.font=font;
                foreach(var toast in root.GetComponentsInChildren<ToastManager>(true)){var so=new SerializedObject(toast);so.FindProperty("font").objectReferenceValue=font;so.FindProperty("padding").vector2Value=new Vector2(40,18);so.FindProperty("anchorOffset").vector2Value=new Vector2(0,120);so.ApplyModifiedPropertiesWithoutUndo();}
            }
            if(scenePath.EndsWith("StartGame.unity"))ConfigureCover(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
    static void ConfigureCover(UnityEngine.SceneManagement.Scene scene)
    {
        var manager=UnityEngine.Object.FindObjectOfType<UIPanelManager>();var parent=Ref<GameObject>(manager,"pnl_start_game");
        foreach(var im in parent.GetComponentsInChildren<Image>(true))if(!im.GetComponent<Button>())im.enabled=false;
        var bg=Img(parent.transform,"ArtCover",S("Preparation","游戏封面"),0,0,1920,1080);bg.transform.SetAsFirstSibling();
        string[] fields={"btn_start_game","btn_continue_game","btn_settings"};string[] names={"新游戏","继续游戏","设置"};
        for(int i=0;i<3;i++){var b=Ref<Button>(manager,fields[i]);b.transform.SetParent(parent.transform,false);At(R(b),-650,-75-i*105,300,70);b.GetComponent<Image>().color=new Color(0,0,0,.15f);var text=b.GetComponentInChildren<Text>(true);text.enabled=true;text.text=names[i];text.font=font;text.fontSize=42;text.color=new Color(1,.86f,.6f);Stretch(R(text));}
        var settings=Ref<GameObject>(manager,"pnl_settings");var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Panel/SettingsPanel.prefab");var replacement=(GameObject)PrefabUtility.InstantiatePrefab(source,settings.transform.parent);replacement.name="SettingsPanel";replacement.SetActive(false);Set(manager,"pnl_settings",replacement);settings.SetActive(false);
    }
    public static void RepairInput()
    {
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Art/Fonts/FangZhengShuSong.ttf");
        foreach(var path in Directory.GetFiles("Assets/Resources","*.prefab",SearchOption.AllDirectories)) Edit(path,root=>{
            foreach(var button in root.GetComponentsInChildren<Button>(true))if(button.targetGraphic)button.targetGraphic.raycastTarget=true;
            foreach(var scroll in root.GetComponentsInChildren<ScrollRect>(true)){var image=scroll.GetComponent<Image>();if(image)image.raycastTarget=true;}
            if(root.GetComponent<DialogueHistoryPanel>()){var scroll=root.GetComponentInChildren<ScrollRect>(true);var mask=scroll.viewport.GetComponent<Mask>();if(mask)mask.showMaskGraphic=false;}
            foreach(var slot in root.GetComponentsInChildren<ItemSlot>(true)){var quantity=Ref<Text>(slot,"_quantityText");var image=quantity.transform.parent.GetComponent<Image>();if(image&&image.transform!=slot.transform)image.enabled=false;}
            foreach(var cell in root.GetComponentsInChildren<BackpackCell>(true))cell.NormalColor=Color.clear;
            var preparation=root.GetComponent<PreparationPanel>();if(preparation){root.GetComponent<Image>().color=new Color(.08f,.07f,.08f);var title=root.transform.Find("RightPanel/ArtTitle");if(title)At((RectTransform)title,0,430,141,39);var grid=root.GetComponentInChildren<BackpackBoard>(true);At(grid.GridRoot,0,0,835,835);var layout=grid.GridRoot.GetComponent<GridLayoutGroup>();layout.cellSize=new Vector2(115,115);layout.spacing=new Vector2(5,5);}
            var shop=root.GetComponent<ShopPanel>();if(shop){var so=new SerializedObject(shop);so.FindProperty("_cellSize").floatValue=115;so.FindProperty("_spacing").floatValue=5;so.ApplyModifiedPropertiesWithoutUndo();}
        });
        foreach(var path in new[]{"Assets/Scripts/qipan/Cell.prefab","Assets/Scripts/qipan/GridBackpack.prefab","Assets/Scripts/qipan/ItemView.prefab"})Edit(path,root=>{foreach(var cell in root.GetComponentsInChildren<BackpackCell>(true))cell.NormalColor=Color.clear;foreach(var item in root.GetComponentsInChildren<ItemView>(true))item.CellSize=115;});
        foreach(var name in new[]{"PlayerGame","StartGame"}){var scene=EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity");foreach(var test in UnityEngine.Object.FindObjectsOfType<EventTestButton>(true))test.gameObject.SetActive(false);EditorSceneManager.SaveScene(scene);}
        AssetDatabase.SaveAssets();
    }
    public static void Polish()
    {
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Art/Fonts/FangZhengShuSong.ttf");
        Edit("Assets/Resources/Panel/SettingsPanel.prefab",root=>{
            var im=root.GetComponent<Image>();im.sprite=null;im.color=Color.clear;
            var body=Img(root.transform,"OpaqueBody",null,0,-30,640,457);body.color=new Color(.12f,.09f,.07f);body.transform.SetAsFirstSibling();
            var frame=Img(root.transform,"ArtFrame",S("Settings","_设置界面底板"),0,0,665,543);frame.transform.SetSiblingIndex(1);
            foreach(var slider in root.GetComponentsInChildren<Slider>(true)){if(slider.handleRect){var handle=slider.handleRect.GetComponent<Image>();handle.color=new Color(1,.8f,.45f);}}
        });
        Edit("Assets/Resources/UI/SaveSlotsPanel.prefab",root=>{
            var p=root.GetComponent<SaveSlotsPanel>();var bg=root.transform.Find("ManagementBackground");var body=Img(root.transform,"OpaqueBody",null,0,-49,465,645);body.color=new Color(.12f,.09f,.07f);body.transform.SetSiblingIndex(bg.GetSiblingIndex());
            At(R(Ref<Text>(p,"title")),130,329,200,45);Back(Ref<Button>(p,"back"),-280,325);
            var so=new SerializedObject(p);var phases=so.FindProperty("phaseNumbers");var days=so.FindProperty("dayNumbers");phases.arraySize=days.arraySize=4;
            for(int i=0;i<4;i++)
            {
                var button=bg.Find("Slot"+(i+1));At((RectTransform)button,0,180-i*140,408,116);
                var summary=button.Find("Summary").GetComponent<Text>();At(R(summary),67,-10,262,36);summary.fontSize=22;
                Txt(button,"SlotIndex","存档"+(i+1),155,40,86,36,22,new Color(1,.86f,.6f));
                phases.GetArrayElementAtIndex(i).objectReferenceValue=Txt(button,"PhaseNumber","—",-142,40,36,36,28,new Color(1,.75f,.35f));
                days.GetArrayElementAtIndex(i).objectReferenceValue=Txt(button,"DayNumber","—",29,40,40,36,28,new Color(1,.75f,.35f));
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        });
        Edit("Assets/Resources/Panel/DecomposePanel.prefab",root=>{var title=root.transform.Find("ArtTitle");At((RectTransform)title,0,430,74,37);});
        // 首页实例更新为同一个设置预制体，保持首页和游戏内的控件与美术一致。
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/StartGame.unity");var manager=UnityEngine.Object.FindObjectOfType<UIPanelManager>();var previous=Ref<GameObject>(manager,"pnl_settings");var replacement=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Panel/SettingsPanel.prefab"),previous.transform.parent);replacement.SetActive(false);Set(manager,"pnl_settings",replacement);UnityEngine.Object.DestroyImmediate(previous);EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }
    public static void PolishDetails()
    {
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Art/Fonts/FangZhengShuSong.ttf");
        Edit("Assets/Resources/Panel/TalkPanel/SpecialCommissionDetailPanel.prefab",root=>{
            var p=root.GetComponent<SpecialCommissionDetailPanel>();var source=root.GetComponent<Image>().sprite;root.GetComponent<Image>().sprite=null;root.GetComponent<Image>().color=Color.clear;
            var body=Img(root.transform,"OpaqueBody",null,0,-30,640,457);body.color=new Color(.12f,.09f,.07f);body.transform.SetAsFirstSibling();var frame=Img(root.transform,"ArtFrame",source,0,0,665,544);frame.transform.SetSiblingIndex(1);
            At(R(Ref<Text>(p,"_nameText")),140,-12,280,44);Ref<Text>(p,"_nameText").fontSize=26;
            At(R(Ref<Text>(p,"_rewardText")),40,-94,340,44);At(R(Ref<Text>(p,"_influenceText")),70,-177,340,44);
            var scroll=Ref<ScrollRect>(p,"_descriptionScroll");At(R(scroll),0,120,575,150);scroll.content.sizeDelta=new Vector2(0,150);Ref<Text>(p,"_descriptionText").fontSize=24;
        });
        Edit("Assets/Resources/Panel/TalkPanel/SpecialCommissionItem.prefab",root=>{var p=root.GetComponent<SpecialCommissionItem>();Skin(Ref<Button>(p,"_viewButton"),Old("派发1"),Old("派发2"));});
        Edit("Assets/Resources/Panel/PreparationPanel.prefab",root=>{
            var p=root.GetComponent<PreparationPanel>();var talk=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Panel/TalkPanel/TalkPanel.prefab");var room=talk.transform.Find("BGImage").GetComponent<Image>().sprite;
            if(room){var bg=Img(root.transform,"ArtRoom",room,0,0,1920,1080);bg.color=new Color(.4f,.4f,.4f);bg.transform.SetAsFirstSibling();}
            var strip=Img(root.transform,"IncomeStrip",S("Preparation","_收益显示"),0,-505,640,68);var text=Txt(strip.transform,"IncomeValue","—",55,0,210,50,28,new Color(1,.85f,.6f));
            var image=Img(root.transform,"CalculateIncome",S("Preparation","_计算收益1"),340,-505,182,63,true);var button=image.gameObject.AddComponent<Button>();Skin(button,S("Preparation","_计算收益1"),S("Preparation","_计算收益2"));Set(p,"_calculateIncomeButton",button);Set(p,"_incomeText",text);
        });
        AssetDatabase.SaveAssets();
    }
    public static void RepairGrid()
    {
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Art/Fonts/FangZhengShuSong.ttf");
        foreach(var path in new[]{"Assets/Resources/Panel/PreparationPanel.prefab","Assets/Scripts/qipan/GridBackpack.prefab"})Edit(path,root=>{
            var board=root.GetComponentInChildren<BackpackBoard>(true);At(board.GridRoot,0,0,835,835);var layout=board.GridRoot.GetComponent<GridLayoutGroup>();layout.constraint=GridLayoutGroup.Constraint.FixedColumnCount;layout.constraintCount=7;layout.padding=new RectOffset();layout.cellSize=new Vector2(115,115);layout.spacing=new Vector2(5,5);layout.startCorner=GridLayoutGroup.Corner.UpperLeft;layout.startAxis=GridLayoutGroup.Axis.Horizontal;
        });
        AssetDatabase.SaveAssets();
    }
    public static void FinishPresentation()
    {
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Art/Fonts/FangZhengShuSong.ttf");
        Edit("Assets/Resources/Panel/DialogueHistoryPanel.prefab",root=>{
            root.transform.Find("Background").GetComponent<Image>().color=Color.white;
            var scroll=root.GetComponentInChildren<ScrollRect>(true);var layout=scroll.content.GetComponent<VerticalLayoutGroup>();layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.spacing=8;
        });
        Edit("Assets/Resources/DialogueHistoryEntry.prefab",root=>{
            foreach(var image in root.GetComponentsInChildren<Image>(true))image.color=Color.clear;
            var row=root.GetComponent<HorizontalLayoutGroup>();if(!row)row=root.AddComponent<HorizontalLayoutGroup>();row.spacing=16;row.childControlHeight=true;row.childControlWidth=true;row.childForceExpandHeight=false;row.childForceExpandWidth=false;row.childAlignment=TextAnchor.UpperLeft;row.padding=new RectOffset();
            var p=root.GetComponent<DialogueHistoryEntry>();foreach(var field in new[]{"_speakerNameText","_contentText"}){var text=Ref<Text>(p,field);text.alignment=TextAnchor.UpperLeft;text.fontSize=26;text.color=Ink;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Overflow;var e=text.GetComponent<LayoutElement>();if(!e)e=text.gameObject.AddComponent<LayoutElement>();e.preferredWidth=field=="_contentText"?840:140;e.flexibleWidth=0;}
            var fit=root.GetComponent<ContentSizeFitter>();if(fit)fit.enabled=false;var height=root.GetComponent<LayoutElement>();if(!height)height=root.AddComponent<LayoutElement>();height.preferredHeight=48;
        });
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/StartGame.unity");var manager=UnityEngine.Object.FindObjectOfType<UIPanelManager>();var parent=Ref<GameObject>(manager,"pnl_start_game");var settings=Ref<Button>(manager,"btn_settings");Skin(settings,Old("设置1"),Old("设置2"));At(R(settings),872,465,89,87);
        var quit=Img(parent.transform,"QuitGame",null,-650,-285,300,70,true);quit.color=new Color(0,0,0,.15f);var button=quit.gameObject.AddComponent<Button>();button.targetGraphic=quit;Txt(quit.transform,"Label","退出游戏",0,0,300,70,42,new Color(1,.86f,.6f));UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick,UnityEngine.Object.FindObjectOfType<StartGame>().QuitGame);
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
    }
    public static void SeparateMenuAudio()
    {
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Art/Fonts/FangZhengShuSong.ttf");
        foreach(var path in new[]{"Assets/Resources/Panel/TalkPanel/CharacterArchivePanel.prefab","Assets/Resources/Panel/TalkPanel/TalkPanel.prefab"})Edit(path,root=>{
            var archive=root.GetComponentInChildren<CharacterArchivePanel>(true);var image=Ref<Image>(archive,"_iconImage");
            if(!image.transform.parent.GetComponent<RectMask2D>())
            {var viewport=new GameObject("PortraitViewport",typeof(RectTransform),typeof(RectMask2D));viewport.transform.SetParent(image.transform.parent,false);At((RectTransform)viewport.transform,0,267,442,194);image.transform.SetParent(viewport.transform,false);}
            var r=R(image);r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,1);r.anchoredPosition=new Vector2(0,40);r.sizeDelta=new Vector2(442,650);image.preserveAspect=true;image.raycastTarget=false;
        });
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/StartGame.unity");var old=UnityEngine.Object.FindObjectOfType<AudioManager>();
        if(old.GetComponent<StartGame>()!=null)
        {
            // 音频跨场景保留，但首页的旧测试流程不能跟着保留到 27 天剧情中。
            var source=Ref<AudioSource>(old,"bgmSource");var go=new GameObject("AudioManager");var music=go.AddComponent<AudioSource>();if(source)EditorUtility.CopySerialized(source,music);
            var audio=go.AddComponent<AudioManager>();EditorUtility.CopySerialized(old,audio);Set(audio,"bgmSource",music);UnityEngine.Object.DestroyImmediate(old);if(source)UnityEngine.Object.DestroyImmediate(source);
        }
        EditorSceneManager.SaveScene(scene);
    }
    public static void CleanCoverAndToast()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/StartGame.unity");var manager=UnityEngine.Object.FindObjectOfType<UIPanelManager>();var parent=Ref<GameObject>(manager,"pnl_start_game");
        foreach(var text in parent.GetComponentsInChildren<TMPro.TMP_Text>(true))text.gameObject.SetActive(false);
        foreach(var text in parent.GetComponentsInChildren<Text>(true))if(text.GetComponentInParent<Button>()==null)text.gameObject.SetActive(false);
        EditorSceneManager.SaveScene(scene);
        scene=EditorSceneManager.OpenScene("Assets/Scenes/PlayerGame.unity");foreach(var toast in UnityEngine.Object.FindObjectsOfType<ToastManager>(true)){var so=new SerializedObject(toast);so.FindProperty("backgroundSprite").objectReferenceValue=null;so.FindProperty("backgroundColor").colorValue=new Color(0,0,0,.78f);so.ApplyModifiedPropertiesWithoutUndo();}EditorSceneManager.SaveScene(scene);
    }
    public static void FinalSpacing()
    {
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Art/Fonts/FangZhengShuSong.ttf");
        Edit("Assets/Resources/Panel/PreparationPanel.prefab",root=>{At((RectTransform)root.transform.Find("IncomeStrip"),-110,-505,640,68);At((RectTransform)root.transform.Find("IncomeStrip/IncomeValue"),178,0,80,45);});AssetDatabase.SaveAssets();
    }
    public static void RepairItemActions()
    {
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Art/Fonts/FangZhengShuSong.ttf");
        // 原面板用负 sizeDelta 抵消全屏拉伸；改为固定锚点后必须一并改为正尺寸。
        Edit("Assets/Resources/Panel/ItemActionPanel.prefab",root=>At((RectTransform)root.transform,0,0,220,225));
        Edit("Assets/Resources/Panel/DescriptionPanel.prefab",root=>Ref<Image>(root.GetComponent<DescriptionPanel>(),"_itemImage").preserveAspect=true);
        AssetDatabase.SaveAssets();
    }
    // 本轮布局在编辑器持久保存到原预制体；不要在运行时重新定位覆盖这些配置。
    public static void ConfigureDoc2()
    {
        font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Art/Fonts/FangZhengShuSong.ttf");
        Edit("Assets/Resources/Panel/PreparationPanel.prefab", root => {
            var right = root.transform.Find("RightPanel");
            var title = right.Find("ArtTitle");
            // 原 141×39 只有标题字形；给它补回同宽的标题底条。
            var strip = Img(right, "Doc2TitleStrip", Old("左标题"), 0, 413, 482, 80);
            strip.transform.SetAsFirstSibling();
            At((RectTransform)title, 0, 413, 141, 39);
        });
        Edit("Assets/Resources/Panel/AbyssalAltarPanel.prefab", root => {
            var p = root.GetComponent<AbyssalAltarPanel>(); var frame = (RectTransform)root.transform.Find("LeftPanel");
            frame.Find("ArtDecoration").gameObject.SetActive(false);
            Doc2Decoration(frame, S("Preparation", "_祭坛装饰文字"), "Altar", 120, 370);
            var text = Txt(frame, "Doc2AltarText", "“虽然地牢各处已经习见\n这种诡异的石砖造物了”\n\n“但是你总觉得\n祂在盯着你”", 0, 135, 390, 270, 27, new Color(1,.9f,.73f));
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            Doc2Back(Ref<Button>(p,"_closelButton"),frame);
        });
        Edit("Assets/Resources/Panel/DecomposePanel.prefab", root => {
            var p=root.GetComponent<DecomposePanel>(); var frame=(RectTransform)root.transform;
            frame.Find("ArtDecoration").gameObject.SetActive(false);
            Doc2Decoration(frame,S("Preparation","_地狱界面文字装饰"),"Hell",72,275);
            var text=Txt(frame,"Doc2HellText","“洞口透出静谧的黑”\n“呃呃……希望不要突然\n爬出来个什么东西吧”\n你暗自忖道",0,170,380,190,26,new Color(1,.9f,.73f));
            text.horizontalOverflow=HorizontalWrapMode.Wrap;
            At(R(Ref<ItemSlot>(p,"_slot")),0,-95,196,202);
            Doc2Back(Ref<Button>(p,"_closeButton"),frame);
        });
        foreach(var path in new[]{"Assets/Resources/Panel/TalkPanel/CharacterArchivePanel.prefab","Assets/Resources/Panel/TalkPanel/TalkPanel.prefab"}) Edit(path,Doc2Archive);
        Edit("Assets/Resources/Panel/ItemActionPanel.prefab",root=>{
            var p=root.GetComponent<ItemActionPanel>();
            Set(p,"_browseBackground",Old("_背包小弹窗")); Set(p,"_useBackground",Old("_使用道具小弹窗"));
            var use=Img(root.transform,"UseItemButton",null,0,88,192,80,true);use.color=Color.clear;
            var button=use.gameObject.AddComponent<Button>();button.targetGraphic=use;Set(p,"_useButton",button);
            foreach(var b in new[]{Ref<Button>(p,"_examineButton"),Ref<Button>(p,"_dropButton")}){
                b.GetComponent<Image>().color=Color.clear;
                foreach(var t in b.GetComponentsInChildren<Text>(true))t.gameObject.SetActive(false);
            }
        });
        Edit("Assets/Resources/Panel/DialogueOptionItem.prefab",root=>{
            var p=root.GetComponent<DialogueOptionItem>();var b=Ref<Button>(p,"_button");var t=Ref<Text>(p,"_text");
            var normal=t.color;t.color=Color.white;b.targetGraphic=t;b.transition=Selectable.Transition.ColorTint;
            var colors=b.colors;colors.normalColor=normal;colors.highlightedColor=new Color(1,.93f,.65f);colors.selectedColor=colors.highlightedColor;colors.pressedColor=new Color(1,.77f,.33f);colors.disabledColor=new Color(.42f,.37f,.3f);colors.fadeDuration=.08f;b.colors=colors;
        });
        Edit("Assets/Resources/Panel/DescriptionPanel.prefab",root=>{
            var p=root.GetComponent<DescriptionPanel>();var frame=(RectTransform)root.transform.Find("ItemDescription");
            Doc2Back(Ref<Button>(p,"_confirmButton"),frame);
            At(R(Ref<Text>(p,"_itemDescriptionText")),0,-105,535,180);
            var confirm=Img(frame,"RouteConfirm",null,210,-220,150,54,true);confirm.color=new Color(.26f,.13f,.06f,.95f);
            var button=confirm.gameObject.AddComponent<Button>();button.targetGraphic=confirm;
            Txt(confirm.transform,"Label","确认",0,0,140,48,28,new Color(1,.86f,.56f));Set(p,"_routeConfirmButton",button);confirm.gameObject.SetActive(false);
        });
        Edit("Assets/Resources/Panel/ShopPanel.prefab",root=>Doc2Back(Ref<Button>(root.GetComponent<ShopPanel>(),"_closeButton"),(RectTransform)root.transform.Find("BGImage")));
        Edit("Assets/Resources/Panel/BackpackPanel.prefab",root=>{
            var frame=(RectTransform)root.transform.Find("BGImage");
            foreach(var b in root.GetComponentsInChildren<Button>(true).Where(b=>b.GetComponent<Image>()?.sprite?.name.Contains("返回")==true).ToArray())Doc2Back(b,frame);
        });
        Edit("Assets/Resources/Panel/TalkPanel/CommissionPanel.prefab",root=>Doc2Back(Ref<Button>(root.GetComponent<CommissionPanel>(),"CloseButton"),(RectTransform)root.transform));
        Edit("Assets/Resources/Panel/TalkPanel/SpecialCommissionPanel.prefab",root=>{
            var p=root.GetComponent<SpecialCommissionPanel>();Doc2Back(Ref<Button>(p,"_closeButton"),(RectTransform)root.transform);
            var d=Ref<SpecialCommissionDetailPanel>(p,"_detailPanel");Doc2Back(Ref<Button>(d,"_closeButton"),R(d));
        });
        Edit("Assets/Resources/Panel/TalkPanel/SpecialCommissionDetailPanel.prefab",root=>Doc2Back(Ref<Button>(root.GetComponent<SpecialCommissionDetailPanel>(),"_closeButton"),(RectTransform)root.transform));
        Edit("Assets/Resources/Panel/SettingsPanel.prefab",root=>Doc2Back(Ref<Button>(root.GetComponent<SettingsPanel>(),"btn_close"),(RectTransform)root.transform));
        Edit("Assets/Resources/UI/SaveSlotsPanel.prefab",root=>Doc2Back(Ref<Button>(root.GetComponent<SaveSlotsPanel>(),"back"),(RectTransform)root.transform.Find("ManagementBackground")));
        Edit("Assets/Resources/Panel/DialogueHistoryPanel.prefab",root=>At(R(Ref<Button>(root.GetComponent<DialogueHistoryPanel>(),"_closeButton")),-540,370,71,61));

        var route=new GameObject("RouteSelectionPanel",typeof(RectTransform),typeof(Image),typeof(RouteSelectionPanel));
        Stretch((RectTransform)route.transform);route.GetComponent<Image>().color=Color.clear;route.GetComponent<Image>().raycastTarget=true;
        var rp=route.GetComponent<RouteSelectionPanel>();
        var host=new GameObject("BoardHost",typeof(RectTransform));host.transform.SetParent(route.transform,false);At((RectTransform)host.transform,0,15,900,900);Set(rp,"_boardHost",host.transform);
        var help=Txt(route.transform,"Instruction","选择魔物后查看信息并确认指路",0,485,1000,48,28,new Color(1,.9f,.7f));Set(rp,"_instruction",help);
        var back=Img(route.transform,"Back",Old("返回1"),0,-465,71,61,true).gameObject.AddComponent<Button>();Skin(back,Old("返回1"),Old("返回2"));Set(rp,"_returnButton",back);
        PrefabUtility.SaveAsPrefabAsset(route,"Assets/Resources/Panel/RouteSelectionPanel.prefab");UnityEngine.Object.DestroyImmediate(route);
        foreach(var scenePath in new[]{"Assets/Scenes/PlayerGame.unity","Assets/Scenes/StartGame.unity"}){
            var scene=EditorSceneManager.OpenScene(scenePath);
            foreach(var toast in UnityEngine.Object.FindObjectsOfType<ToastManager>(true)){
                var so=new SerializedObject(toast);so.FindProperty("duration").floatValue=2.25f;so.FindProperty("anchorOffset").vector2Value=new Vector2(0,360);so.FindProperty("riseDistance").floatValue=10;so.FindProperty("startOffsetY").floatValue=-4;so.FindProperty("padding").vector2Value=new Vector2(40,12);so.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach(var ui in UnityEngine.Object.FindObjectsOfType<UIManager>(true)){
                if(!ui.Configs.Any(c=>c.panelName==nameof(RouteSelectionPanel)))ui.Configs.Add(new UIManager.PanelConfig{panelName=nameof(RouteSelectionPanel),prefab=AssetDatabase.LoadAssetAtPath<RouteSelectionPanel>("Assets/Resources/Panel/RouteSelectionPanel.prefab"),layer=UILayer.Top});EditorUtility.SetDirty(ui);
            }
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();File.WriteAllText("Doc2Configured.txt","原预制体布局及指路面板配置完成");
    }
    static void Doc2Back(Button b,RectTransform frame)
    {
        if(!b||!frame)return;var size=R(b).rect.size;
        var world=frame.TransformPoint(frame.rect.center+new Vector2(-frame.rect.width*.5f+48,frame.rect.height*.5f-48));
        var parent=(RectTransform)b.transform.parent;var point=(Vector2)parent.InverseTransformPoint(world)-parent.rect.center;
        Back(b,point.x,point.y);
        if(size.x>0&&size.y>0)R(b).sizeDelta=size;
    }
    static void Doc2Decoration(Transform parent,Sprite source,string tag,int topPixels,int textBottom)
    {
        // 装饰与文字原本烘在同一 PNG；编辑器裁片仅引用原纹理，原图字节不变。
        // 保留顶部、右侧、下部装饰，正文改为可排版的同一句原文。
        float w=source.rect.width,h=source.rect.height,scale=440f/w,top=350;
        Rect[] slices={new Rect(0,h-topPixels,w,topPixels),new Rect(w-118,0,118,h-topPixels),new Rect(0,0,w-118,h-textBottom)};
        for(int i=0;i<slices.Length;i++){
            var q=slices[i];var sprite=Sprite.Create(source.texture,new Rect(source.rect.x+q.x,source.rect.y+q.y,q.width,q.height),new Vector2(.5f,.5f),100);
            string asset="Assets/Resources/Art/Preparation/Doc2"+tag+i+".asset";AssetDatabase.CreateAsset(sprite,asset);
            Img(parent,"Doc2"+tag+i,sprite,(q.center.x-w*.5f)*scale,top-(h-q.center.y)*scale,q.width*scale,q.height*scale);
        }
    }
    static void Doc2Archive(GameObject root)
    {
        var a=root.GetComponentInChildren<CharacterArchivePanel>(true);if(!a)return;
        var container=a.transform;
        // 去掉底板自带的静态七维，动态网格、标签、数值全部放入唯一组。
        foreach(var image in a.GetComponentsInChildren<Image>(true))if(image.sprite!=null&&image.sprite.name.Contains("基本信息底板")){image.sprite=null;image.color=Color.clear;}
        var stats=container.Find("Doc2Stats");if(!stats){stats=new GameObject("Doc2Stats",typeof(RectTransform)).transform;stats.SetParent(container,false);}
        At((RectTransform)stats,0,-40,424,380);Set(a,"_statsRoot",stats.gameObject);
        a.radarChart.transform.SetParent(stats,false);At(R(a.radarChart),0,0,290,290);a.radarChart.radius=125;a.radarChart.raycastTarget=false;
        Vector2[] positions={new Vector2(0,-174),new Vector2(142,-108),new Vector2(165,40),new Vector2(85,164),new Vector2(-85,164),new Vector2(-165,40),new Vector2(-142,-108)};
        for(int i=0;i<a.statTexts.Count;i++){var t=a.statTexts[i];t.transform.SetParent(stats,false);At(R(t),positions[i].x,positions[i].y,118,38);t.fontSize=23;t.alignment=TextAnchor.MiddleCenter;t.color=new Color(1,.9f,.73f);}
        var name=Ref<Text>(a,"_name");At(R(name),60,345,270,45);name.fontSize=30;name.alignment=TextAnchor.MiddleLeft;
        var intro=container.Find("Doc2HeaderIntro")?.GetComponent<Text>()??Txt(container,"Doc2HeaderIntro","",60,270,270,105,22,new Color(1,.9f,.73f));intro.alignment=TextAnchor.UpperLeft;intro.horizontalOverflow=HorizontalWrapMode.Wrap;intro.resizeTextForBestFit=true;intro.resizeTextMinSize=19;intro.resizeTextMaxSize=22;Set(a,"_headerIntroduction",intro);
        var icon=Ref<Image>(a,"_iconImage");var view=icon.transform.parent;
        if(view==container||!view.GetComponent<RectMask2D>()){view=new GameObject("Doc2PortraitViewport",typeof(RectTransform),typeof(RectMask2D)).transform;view.SetParent(container,false);icon.transform.SetParent(view,false);}
        At((RectTransform)view,-148,300,112,128);
        var portraitFrame=container.Find("Doc2PortraitFrame")?.GetComponent<Image>()??Img(container,"Doc2PortraitFrame",Old("_0000s_0004_物品框"),-148,300,132,150);portraitFrame.transform.SetSiblingIndex(view.GetSiblingIndex());
        var ir=R(icon);ir.anchorMin=ir.anchorMax=ir.pivot=new Vector2(.5f,1);ir.anchoredPosition=new Vector2(0,22);ir.sizeDelta=new Vector2(205,330);icon.preserveAspect=true;icon.raycastTarget=false;
        var body=Ref<Text>(a,"_Introduction");var scroll=Ref<ScrollRect>(a,"_recordScroll");
        if(!scroll){var viewport=new GameObject("Doc2RecordViewport",typeof(RectTransform),typeof(Image),typeof(RectMask2D),typeof(ScrollRect));viewport.transform.SetParent(container,false);viewport.GetComponent<Image>().color=Color.clear;body.transform.SetParent(viewport.transform,false);scroll=viewport.GetComponent<ScrollRect>();scroll.viewport=(RectTransform)viewport.transform;scroll.content=R(body);scroll.horizontal=false;scroll.scrollSensitivity=24;scroll.movementType=ScrollRect.MovementType.Clamped;Set(a,"_recordScroll",scroll);}
        At(scroll.viewport,0,-337,400,165);var br=R(body);br.anchorMin=new Vector2(0,1);br.anchorMax=Vector2.one;br.pivot=new Vector2(.5f,1);br.anchoredPosition=Vector2.zero;br.sizeDelta=new Vector2(0,165);body.fontSize=25;body.horizontalOverflow=HorizontalWrapMode.Wrap;body.verticalOverflow=VerticalWrapMode.Overflow;body.alignment=TextAnchor.UpperLeft;
        var so=new SerializedObject(a);so.FindProperty("_basicRecordPosition").vector2Value=new Vector2(0,-337);so.FindProperty("_basicRecordSize").vector2Value=new Vector2(400,165);so.FindProperty("_otherRecordPosition").vector2Value=new Vector2(0,-115);so.FindProperty("_otherRecordSize").vector2Value=new Vector2(400,610);so.ApplyModifiedPropertiesWithoutUndo();
        Doc2Back(Ref<Button>(a,"_closeButton"),(RectTransform)container);
    }

    public static void FinishDoc2Visuals()
    {
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Art/Fonts/FangZhengShuSong.ttf");
        Edit("Assets/Resources/Panel/DecomposePanel.prefab",root=>{
            var slot=Ref<ItemSlot>(root.GetComponent<DecomposePanel>(),"_slot");var image=Ref<Image>(slot,"_itemImage");At(R(image),0,10,160,160);image.preserveAspect=true;
            var sub=Ref<Text>(slot,"_subText");At(R(sub),0,-78,170,30);sub.fontSize=22;sub.color=new Color(1,.9f,.7f);
        });
        foreach(var path in new[]{"Assets/Resources/Panel/TalkPanel/CharacterArchivePanel.prefab","Assets/Resources/Panel/TalkPanel/TalkPanel.prefab"})Edit(path,root=>{
            var a=root.GetComponentInChildren<CharacterArchivePanel>(true);if(a)Doc2BackAt(Ref<Button>(a,"_closeButton"),R(a),new Vector2(-142,433));
        });
        Edit("Assets/Resources/Panel/BackpackPanel.prefab",root=>{
            var frame=(RectTransform)root.transform.Find("BGImage");foreach(var b in root.GetComponentsInChildren<Button>(true).Where(b=>b.GetComponent<Image>()?.sprite?.name.Contains("返回")==true).ToArray())Doc2BackAt(b,frame,new Vector2(-142,433));
        });AssetDatabase.SaveAssets();
    }
    static void Doc2BackAt(Button b,RectTransform frame,Vector2 offset)
    {
        if(!b)return;var world=frame.TransformPoint(frame.rect.center+offset);var parent=(RectTransform)b.transform.parent;var point=(Vector2)parent.InverseTransformPoint(world)-parent.rect.center;
        var size=R(b).rect.size;Back(b,point.x,point.y);R(b).sizeDelta=size;
    }

    public static void RemoveDoc2QuickNavigation()
    {
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Art/Fonts/FangZhengShuSong.ttf");
        Edit("Assets/Resources/Panel/HUDPanel.prefab",root=>{var group=root.transform.Find("PreparationQuickLinks");if(group)UnityEngine.Object.DestroyImmediate(group.gameObject);});AssetDatabase.SaveAssets();
    }
    public static void FinishDoc2RouteLayout()
    {
        Edit("Assets/Resources/Panel/DialogueOptionItem.prefab",root=>{
            var b=Ref<Button>(root.GetComponent<DialogueOptionItem>(),"_button");var colors=b.colors;colors.selectedColor=colors.normalColor;b.colors=colors;
        });
        Edit("Assets/Resources/Panel/RouteSelectionPanel.prefab",root=>{
            var p=root.GetComponent<RouteSelectionPanel>();
            At(R(Ref<Text>(p,"_instruction")),0,-435,1000,36);
            At(R(Ref<Button>(p,"_returnButton")),0,-490,71,61);
        });
        Edit("Assets/Resources/Panel/TalkPanel/CommissionPanel.prefab",root=>Doc2BackAt(Ref<Button>(root.GetComponent<CommissionPanel>(),"CloseButton"),(RectTransform)root.transform,new Vector2(-142,433)));
        Edit("Assets/Resources/Panel/TalkPanel/SpecialCommissionPanel.prefab",root=>Doc2BackAt(Ref<Button>(root.GetComponent<SpecialCommissionPanel>(),"_closeButton"),(RectTransform)root.transform,new Vector2(-142,433)));
        Edit("Assets/Resources/Panel/DescriptionPanel.prefab",root=>{
            var p=root.GetComponent<DescriptionPanel>();var frame=(RectTransform)root.transform.Find("ItemDescription");
            Doc2BackAt(Ref<Button>(p,"_confirmButton"),frame,new Vector2(-275,200));
        });
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/PlayerGame.unity");
        foreach(var ui in UnityEngine.Object.FindObjectsOfType<UIManager>(true))Ref<Image>(ui,"_modalBlocker").color=new Color(0,0,0,.45f);
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
    }
    public static void Dump()
    {
        var sb = new StringBuilder();
        foreach (string path in Directory.GetFiles("Assets/Resources/Panel", "*.prefab", SearchOption.AllDirectories))
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            sb.AppendLine("\nPREFAB " + path);
            foreach (var t in root.GetComponentsInChildren<RectTransform>(true))
            {
                sb.AppendLine(PathOf(t, root.transform) + " pos=" + t.anchoredPosition + " size=" + t.sizeDelta + " anchor=" + t.anchorMin + "/" + t.anchorMax +
                    " scripts=" + string.Join(",", t.GetComponents<MonoBehaviour>().Where(c => c != null).Select(c => c.GetType().Name)));
            }
            PrefabUtility.UnloadPrefabContents(root);
        }
        File.WriteAllText("ArtPrefabDump.txt", sb.ToString());
    }
    [MenuItem("Tools/说明3/应用界面与交互配置")]
    public static void ConfigureDoc3()
    {
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Art/Fonts/FangZhengShuSong.ttf");
        Edit("Assets/Resources/Panel/NamePanle.prefab",root=>{
            foreach(var image in root.GetComponentsInChildren<Image>(true)){image.color=new Color(.07f,.05f,.04f,.95f);image.raycastTarget=false;}
            foreach(var text in root.GetComponentsInChildren<Text>(true)){text.color=new Color(1,.9f,.7f);text.raycastTarget=false;}
        });
        Edit("Assets/Resources/Panel/HUDPanel.prefab",root=>{
            var label=root.transform.Find("RevengeWarning")?.GetComponent<Text>()??Txt(root.transform,"RevengeWarning","",0,410,1000,38,23,new Color(.94f,.68f,.5f));
            label.resizeTextForBestFit=true;label.resizeTextMinSize=18;label.resizeTextMaxSize=23;Set(root.GetComponent<HUDPanel>(),"_revengeWarning",label);
        });
        foreach(var path in new[]{"Assets/Resources/Panel/AbyssalAltarPanel.prefab","Assets/Resources/Panel/DecomposePanel.prefab"})Edit(path,root=>{
            bool altar=root.GetComponent<AbyssalAltarPanel>()!=null;var frame=altar?root.transform.Find("LeftPanel"):root.transform;
            foreach(var t in frame.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Doc2Altar")||t.name.StartsWith("Doc2Hell")).ToArray())if(t)UnityEngine.Object.DestroyImmediate(t.gameObject);
            var decoration=frame.Find("ArtDecoration");decoration.gameObject.SetActive(true);At((RectTransform)decoration,0,75,440,560);
            Doc2BackAt(altar?Ref<Button>(root.GetComponent<AbyssalAltarPanel>(),"_closelButton"):Ref<Button>(root.GetComponent<DecomposePanel>(),"_closeButton"),(RectTransform)frame,new Vector2(-142,433));
            if(!altar)At(R(Ref<ItemSlot>(root.GetComponent<DecomposePanel>(),"_slot")),0,-145,196,202);
        });
        Edit("Assets/Resources/Panel/ShopPanel.prefab",root=>Doc2BackAt(Ref<Button>(root.GetComponent<ShopPanel>(),"_closeButton"),(RectTransform)root.transform.Find("BGImage"),new Vector2(-142,433)));
        Edit("Assets/Resources/Panel/DescriptionPanel.prefab",root=>{
            var p=root.GetComponent<DescriptionPanel>();var frame=root.transform.Find("ItemDescription");
            // 底部左右两端容纳返回和确认，避开原分件烘焙的标题、头像和名称。
            Doc2BackAt(Ref<Button>(p,"_confirmButton"),(RectTransform)frame,new Vector2(-265,-220));
            var name=Ref<Text>(p,"_itemNameText");At(R(name),103,116,172,58);name.fontSize=27;name.resizeTextForBestFit=true;name.resizeTextMinSize=18;name.resizeTextMaxSize=27;name.horizontalOverflow=HorizontalWrapMode.Wrap;name.verticalOverflow=VerticalWrapMode.Truncate;
            var text=Ref<Text>(p,"_itemDescriptionText");text.fontSize=26;text.fontStyle=FontStyle.Normal;text.color=new Color(1,.9f,.73f);text.alignment=TextAnchor.UpperLeft;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Overflow;
            var scroll=Ref<ScrollRect>(p,"_descriptionScroll");
            if(!scroll){var go=new GameObject("DescriptionScroll",typeof(RectTransform),typeof(Image),typeof(RectMask2D),typeof(ScrollRect));go.transform.SetParent(frame,false);go.GetComponent<Image>().color=Color.clear;scroll=go.GetComponent<ScrollRect>();scroll.viewport=(RectTransform)go.transform;text.transform.SetParent(go.transform,false);scroll.content=R(text);scroll.horizontal=false;scroll.scrollSensitivity=24;Set(p,"_descriptionScroll",scroll);}
            At(scroll.viewport,0,-105,535,140);var r=R(text);r.anchorMin=new Vector2(0,1);r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,1);r.anchoredPosition=Vector2.zero;r.sizeDelta=new Vector2(0,140);
            var quantity=frame.Find("ActualQuantity")?.GetComponent<Text>()??Txt(frame,"ActualQuantity","1",130,43,220,42,27,new Color(1,.9f,.73f));quantity.alignment=TextAnchor.MiddleLeft;Set(p,"_quantityText",quantity);
        });
        Edit("Assets/Resources/Panel/PreparationPanel.prefab",root=>{
            var right=root.transform.Find("RightPanel");var deco=right.Find("ArtDecoration");if(deco)deco.gameObject.SetActive(false);
            // 原底板已经烘有窄标题框；只引用正文裁片，再保留唯一一条同宽标题，避免双框叠压。
            right.GetComponent<Image>().color=Color.clear;
            string asset="Assets/Resources/Art/Preparation/Doc3PreparationBody.asset";var source=S("Preparation","_整备界面右底板");var bodySprite=AssetDatabase.LoadAssetAtPath<Sprite>(asset);
            if(!bodySprite){bodySprite=Sprite.Create(source.texture,new Rect(source.rect.x,source.rect.y,source.rect.width,source.rect.height-78),new Vector2(.5f,.5f),100);AssetDatabase.CreateAsset(bodySprite,asset);}
            var body=right.Find("Doc3Body")?.GetComponent<Image>()??Img(right,"Doc3Body",bodySprite,0,-39,482,845);body.sprite=bodySprite;body.transform.SetAsFirstSibling();
            At((RectTransform)right.Find("Doc2TitleStrip"),0,430,482,65);At((RectTransform)right.Find("ArtTitle"),0,430,141,39);
            var button=right.Find("SpecialPreview")?.GetComponent<Button>();
            if(!button){var image=Img(right,"SpecialPreview",null,0,-225,350,64,true);image.color=new Color(.08f,.08f,.07f,.85f);button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;Txt(image.transform,"Label","查看今日特殊委托",0,0,340,58,28,new Color(1,.88f,.64f));}
            Set(root.GetComponent<PreparationPanel>(),"_specialPreviewButton",button);
        });
        Edit("Assets/Resources/Panel/DailySettlementPanel.prefab",root=>{
            var p=root.GetComponent<DailySettlementPanel>();var notes=Ref<Text>(p,"_detailsText");
            if(!notes){notes=Txt(root.transform,"OutcomeNotes","",0,-205,680,140,25,new Color(1,.9f,.73f));notes.alignment=TextAnchor.UpperLeft;Set(p,"_detailsText",notes);}
            At((RectTransform)root.transform,0,0,900,780);root.GetComponent<Image>().sprite=null;root.GetComponent<Image>().color=Color.clear;
            var body=root.transform.Find("Doc3Body")?.GetComponent<Image>()??Img(root.transform,"Doc3Body",null,0,0,900,780);
            body.color=new Color(.07f,.05f,.04f,1);body.type=Image.Type.Simple;body.transform.SetAsFirstSibling();
            var border=body.GetComponent<Outline>()??body.gameObject.AddComponent<Outline>();border.effectColor=new Color(.65f,.45f,.23f);border.effectDistance=new Vector2(2,2);
            var title=root.transform.Find("Doc3Title")?.GetComponent<Text>()??Txt(root.transform,"Doc3Title","今日账目",0,315,800,70,38,new Color(1,.88f,.6f));
            var room=root.transform.Find("Room");At((RectTransform)room,0,0,900,780);
            string[] fields={"txt_MonsterBusiness","txt_LegacyCommission","txt_SpecialCommission","txt_Total","txt_Installment"};
            for(int i=0;i<fields.Length;i++){var text=Ref<Text>(p,fields[i]);var row=(RectTransform)text.transform.parent;At(row,-225,190-i*75,400,65);if(row.GetComponent<Image>())row.GetComponent<Image>().color=Color.clear;At(R(text),0,0,400,65);text.fontSize=29;text.color=new Color(1,.9f,.73f);text.alignment=TextAnchor.MiddleLeft;}
            var scroll=Ref<ScrollRect>(p,"_detailsScroll");
            if(!scroll){var go=new GameObject("NotesScroll",typeof(RectTransform),typeof(Image),typeof(RectMask2D),typeof(ScrollRect));go.transform.SetParent(root.transform,false);go.GetComponent<Image>().color=Color.clear;scroll=go.GetComponent<ScrollRect>();scroll.viewport=(RectTransform)go.transform;notes.transform.SetParent(go.transform,false);scroll.content=R(notes);scroll.horizontal=false;scroll.scrollSensitivity=24;Set(p,"_detailsScroll",scroll);}
            At(scroll.viewport,210,35,390,400);notes.fontSize=25;var n=R(notes);n.anchorMin=new Vector2(0,1);n.anchorMax=Vector2.one;n.pivot=new Vector2(.5f,1);n.anchoredPosition=Vector2.zero;n.sizeDelta=new Vector2(0,400);notes.verticalOverflow=VerticalWrapMode.Overflow;
            var close=Ref<Button>(p,"btn_Close");At(R(close),0,-310,260,72);close.GetComponent<Image>().sprite=null;close.GetComponent<Image>().color=new Color(.14f,.12f,.08f);var label=close.GetComponentInChildren<Text>(true);label.enabled=true;label.text="收好金币，继续";At(R(label),0,0,250,65);label.fontSize=29;label.color=new Color(1,.9f,.65f);
        });
        CreateDoc3Modals();
        ConfigureDoc3BoardData();
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/PlayerGame.unity");
        foreach(var ui in UnityEngine.Object.FindObjectsOfType<UIManager>(true)){
            foreach(var config in ui.Configs.Where(c=>c.panelName==nameof(DailySettlementPanel)))config.layer=UILayer.Top;
            foreach(string name in new[]{nameof(EvolutionPanel),nameof(DiceCheckPanel)})if(!ui.Configs.Any(c=>c.panelName==name))ui.Configs.Add(new UIManager.PanelConfig{panelName=name,prefab=AssetDatabase.LoadAssetAtPath<PanelBase>("Assets/Resources/Panel/"+name+".prefab"),layer=UILayer.Top});
            EditorUtility.SetDirty(ui);
        }
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();File.WriteAllText("Doc3Configured.txt","说明3预制体与面板配置完成");
    }
    static Button Doc3TextButton(Transform parent,string name,string label,float x,float y,float width=340,float height=90)
    {
        var hit=Img(parent,name,null,x,y,width,height,true);hit.color=Color.clear;var b=hit.gameObject.AddComponent<Button>();
        var text=Txt(hit.transform,"Label",label,0,0,width,height,32,Color.white);text.supportRichText=true;b.targetGraphic=text;b.transition=Selectable.Transition.ColorTint;
        var c=b.colors;c.normalColor=new Color(.8f,.7f,.53f);c.highlightedColor=new Color(1,.94f,.73f);c.selectedColor=c.normalColor;c.pressedColor=new Color(1,.68f,.3f);c.disabledColor=Color.gray;c.fadeDuration=.1f;b.colors=c;return b;
    }
    static void ConfigureDoc3BoardData()
    {
        // 按《新魔物管理系统v0.1》原始形状图逐格配置；图标始终正向，形状只改占格。
        string[] shapes={"XX","XX/XX","XX/XX/X.","XX","X./X./XX","XXX/.X./.X.","XXX","XXX/.X.","X../X../XXX","X./XX","XXX/.X.","X.X/XXX","XXX","XX/XX","XX/XX/X.","XXXX/.X../.X..","XXXX/.XX./.X.."};
        for(int i=0;i<shapes.Length;i++)
        {
            var def=AssetDatabase.LoadAssetAtPath<ItemShapeDefinition>($"Assets/Scripts/qipan/Monster-shaped/Monster{i+1:00}.asset");var rows=shapes[i].Split('/');var cells=new System.Collections.Generic.List<Vector2Int>();
            for(int y=0;y<rows.Length;y++)for(int x=0;x<rows[y].Length;x++)if(rows[y][x]=='X')cells.Add(new Vector2Int(x,rows.Length-y-1));
            def.Shape=cells.ToArray();EditorUtility.SetDirty(def);
        }
        var income=AssetDatabase.LoadAssetAtPath<MonsterIncomeConfig>("Assets/Scripts/qipan/MonsterIncomeConfig.asset");var so=new SerializedObject(income);var entries=so.FindProperty("_entries");
        for(int i=0;i<entries.arraySize;i++){var e=entries.GetArrayElementAtIndex(i);int id=e.FindPropertyRelative("monsterId").intValue;int n=id==117?40:id==116?24:(id-101)%3==0?6:(id-101)%3==1?14:24;e.FindPropertyRelative("baseIncome").intValue=n;e.FindPropertyRelative("evolvedIncome").intValue=n+25;}so.ApplyModifiedPropertiesWithoutUndo();
        var tile=AssetDatabase.LoadAssetAtPath<TileAffinityConfig>("Assets/Scripts/qipan/TileAffinityConfig.asset");var ts=new SerializedObject(tile);
        foreach(var field in tile.GetType().GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public)){
            var array=ts.FindProperty(field.Name);if(array==null||!array.isArray||array.propertyType==SerializedPropertyType.String)continue;
            for(int i=0;i<array.arraySize;i++){var e=array.GetArrayElementAtIndex(i);var type=e.FindPropertyRelative("tileType");if(type==null)type=e.FindPropertyRelative("type");var adapted=e.FindPropertyRelative("adaptedTypes");if(type!=null&&type.intValue==(int)SpecialTileType.HumusPool&&adapted!=null){adapted.arraySize=3;int[] v={(int)MonsterType.Slime,(int)MonsterType.Plant,(int)MonsterType.Canine};for(int j=0;j<3;j++)adapted.GetArrayElementAtIndex(j).intValue=v[j];}}
        }ts.ApplyModifiedPropertiesWithoutUndo();
    }
    static void CreateDoc3Modals()
    {
        var root=new GameObject("EvolutionPanel",typeof(RectTransform),typeof(Image),typeof(EvolutionPanel));Stretch((RectTransform)root.transform);root.GetComponent<Image>().color=new Color(0,0,0,.72f);
        var p=root.GetComponent<EvolutionPanel>();Set(p,"_title",Txt(root.transform,"Title","你希望这只魔物更加……",0,365,1100,70,40,new Color(1,.9f,.65f)));
        Img(root.transform,"IconFrame",Old("_0000s_0004_物品框"),0,-25,270,290);
        Set(p,"_monsterIcon",Img(root.transform,"MonsterIcon",null,0,-22,218,218));
        Set(p,"_description",Txt(root.transform,"Description","选择一种倾向",0,-180,350,54,24,new Color(.9f,.85f,.73f)));
        var directions=new Button[7];for(int i=0;i<7;i++){float angle=(90+i*360f/7)*Mathf.Deg2Rad;directions[i]=Doc3TextButton(root.transform,"Direction"+i,EvolutionPanel.Names[i],Mathf.Cos(angle)*570,Mathf.Sin(angle)*260-30,350,110);}
        var so=new SerializedObject(p);var a=so.FindProperty("_directions");a.arraySize=7;for(int i=0;i<7;i++)a.GetArrayElementAtIndex(i).objectReferenceValue=directions[i];so.ApplyModifiedPropertiesWithoutUndo();
        var back=Img(root.transform,"Back",Old("返回1"),0,-445,71,61,true).gameObject.AddComponent<Button>();Skin(back,Old("返回1"),Old("返回2"));Set(p,"_back",back);
        ApplyFont(root);PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/Panel/EvolutionPanel.prefab");UnityEngine.Object.DestroyImmediate(root);

        root=new GameObject("DiceCheckPanel",typeof(RectTransform),typeof(Image),typeof(CanvasGroup),typeof(DiceCheckPanel));Stretch((RectTransform)root.transform);root.GetComponent<Image>().color=new Color(0,0,0,.6f);
        var dice=root.GetComponent<DiceCheckPanel>();Set(dice,"_fade",root.GetComponent<CanvasGroup>());
        var body=Img(root.transform,"ResultFrame",null,0,0,900,650);body.color=new Color(.075f,.055f,.045f,.96f);var outline=body.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.65f,.45f,.23f,.8f);outline.effectDistance=new Vector2(2,2);
        Txt(body.transform,"Caption","D100 · 命 运 检 定",0,265,700,55,27,new Color(.73f,.57f,.35f));
        Set(dice,"_title",Txt(body.transform,"Title","",0,180,820,65,32,new Color(1,.9f,.7f)));
        Set(dice,"_requirement",Txt(body.transform,"Requirement","需要：11 点",0,100,700,55,32,new Color(.85f,.8f,.7f)));
        Set(dice,"_rollText",Txt(body.transform,"Roll","",0,5,820,100,38,Color.white));
        Set(dice,"_outcome",Txt(body.transform,"Outcome","",0,-83,700,70,42,new Color(1,.85f,.5f)));
        Set(dice,"_note",Txt(body.transform,"Note","",0,-165,780,75,24,new Color(.8f,.77f,.7f)));
        Set(dice,"_continue",Doc3TextButton(body.transform,"Continue","继续",240,-265,260,70));
        Set(dice,"_coin",Doc3TextButton(body.transform,"Coin","幸运硬币 · 重掷",-215,-265,350,70));
        ApplyFont(root);PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/Panel/DiceCheckPanel.prefab");UnityEngine.Object.DestroyImmediate(root);
    }
    static string PathOf(Transform t, Transform root) => t == root ? t.name : PathOf(t.parent, root) + "/" + t.name;
}
#endif

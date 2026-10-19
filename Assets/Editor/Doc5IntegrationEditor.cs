#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;

/// <summary>说明5布局写入预制体，运行时只切换状态和内容。</summary>
public static class Doc5IntegrationEditor
{
    static Font font;static readonly Color Gold=new Color(1,.89f,.71f);
    static RectTransform R(Component c)=>(RectTransform)c.transform;
    static T Get<T>(UnityEngine.Object obj,string name) where T:UnityEngine.Object=>new SerializedObject(obj).FindProperty(name).objectReferenceValue as T;
    static void Set(UnityEngine.Object obj,string name,UnityEngine.Object value){var s=new SerializedObject(obj);s.FindProperty(name).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    static void At(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);r.localScale=Vector3.one;}
    static GameObject Node(Transform p,string name){var g=new GameObject(name,typeof(RectTransform));g.layer=5;if(p)g.transform.SetParent(p,false);return g;}
    static Image Image(Transform p,string name,Sprite sprite,float x,float y,float w,float h,Color color,bool hit=false){var g=Node(p,name);var im=g.AddComponent<Image>();im.sprite=sprite;im.color=color;im.raycastTarget=hit;At(R(im),x,y,w,h);return im;}
    static Text Text(Transform p,string name,string value,float x,float y,float w,float h,int size){var g=Node(p,name);var t=g.AddComponent<Text>();t.text=value;t.font=font;t.fontSize=size;t.color=Gold;t.alignment=TextAnchor.MiddleCenter;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;t.raycastTarget=false;At(R(t),x,y,w,h);return t;}
    static Sprite Old(string token)=>AssetDatabase.LoadAssetAtPath<Sprite>(Directory.GetFiles("Assets/Resources_moved/Icons","*.png",SearchOption.AllDirectories).First(p=>Path.GetFileName(p).Contains(token)).Replace('\\','/'));
    static Button Button(Transform p,string name,string label,float x,float y,float w,float h){var im=Image(p,name,Old("小弹窗用按钮（透明）1"),x,y,w,h,new Color(.8f,.65f,.42f,1),true);var b=im.gameObject.AddComponent<Button>();var text=Text(im.transform,"Label",label,0,0,w-20,h-8,28);b.targetGraphic=im;var c=b.colors;c.normalColor=Color.white;c.highlightedColor=new Color(1,1,.75f);c.pressedColor=new Color(.75f,.65f,.45f);b.colors=c;return b;}
    static void SaveSprite(Sprite sprite,string path){Directory.CreateDirectory(Path.GetDirectoryName(path));var old=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(old){EditorUtility.CopySerialized(sprite,old);UnityEngine.Object.DestroyImmediate(sprite);EditorUtility.SetDirty(old);}else AssetDatabase.CreateAsset(sprite,path);}
    static void Edit(string path,Action<GameObject> fn){var root=PrefabUtility.LoadPrefabContents(path);try{fn(root);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}}
    [MenuItem("Tools/说明5/接入终局与人物差分")]
    public static void Configure()
    {
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Art/Fonts/FangZhengShuSong.ttf");
        foreach(string file in Directory.GetFiles("Assets/Resources/Art/Characters","*",SearchOption.AllDirectories).Concat(Directory.GetFiles("Assets/Resources/Art/Endings","*",SearchOption.AllDirectories))) {
            if(!new[]{".png",".jpg"}.Contains(Path.GetExtension(file).ToLowerInvariant()))continue;
            var importer=AssetImporter.GetAtPath(file.Replace('\\','/')) as TextureImporter;if(!importer)continue;
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
        }
        var rules=JsonConvert.DeserializeObject<NarrativeSystem.Rules>(File.ReadAllText("Assets/Resources/Narrative/rules.json"));
        string[] codes={"MY","FE","DW","SA","AU","DA","UN"};
        var rects=new[]{new Rect(139,331,1796,2855),new Rect(278,455,1463,2658),new Rect(502,329,2597,1765),new Rect(260,188,1560,2494),new Rect(164,157,1527,2629),new Rect(108,179,1965,2217),new Rect(243,730,1788,2390)};
        var originals=new[]{new Vector2(2100,3369),new Vector2(2100,3369),new Vector2(3602,2300),new Vector2(2043,2843),new Vector2(2048,2889),new Vector2(2186,2396),new Vector2(2100,3369)};
        var faces=new[]{new Rect(700,380,850,850),new Rect(650,470,850,850),new Rect(470,240,2700,1600),new Rect(470,780,1060,1060),new Rect(700,200,950,950),new Rect(500,130,1100,1100),new Rect(600,760,1000,1000)};
        var avatars=new Sprite[7];
        for(int i=0;i<7;i++) {
            string code=codes[i];var variants=rules.portraitVariants[code];
            foreach(string pose in variants.Keys.ToArray()) {
                string name=Path.GetFileName(variants[pose]);string source="Assets/Resources/Art/Characters/"+code+"/"+name+".png";
                if(!File.Exists(source))source=Directory.GetFiles("Assets/Resources/Art/Characters/"+code).First(p=>Path.GetFileNameWithoutExtension(p)==name&&!p.EndsWith(".meta")).Replace('\\','/');
                var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(source);Rect c=rects[i];Vector2 dims=originals[i];
                if(name=="Sarah-hurt-transparent"){c=new Rect(170,52,883,1328);dims=new Vector2(1064,1478);}
                Rect pixels=new Rect(c.x/dims.x*tex.width,(1-(c.y+c.height)/dims.y)*tex.height,c.width/dims.x*tex.width,c.height/dims.y*tex.height);
                var sprite=Sprite.Create(tex,pixels,new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);sprite.name=code+"_"+pose;
                string dest="Assets/Resources/Art/CharacterSprites/"+code+"/"+name+".asset";SaveSprite(sprite,dest);variants[pose]="Art/CharacterSprites/"+code+"/"+name;
                if(pose=="neutral") {
                    var fc=faces[i];var fr=new Rect(fc.x/dims.x*tex.width,(1-(fc.y+fc.height)/dims.y)*tex.height,fc.width/dims.x*tex.width,fc.height/dims.y*tex.height);
                    var face=Sprite.Create(tex,fr,new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);string fp="Assets/Resources/Art/Portraits/Doc5Actor"+(i+1)+".asset";SaveSprite(face,fp);avatars[i]=AssetDatabase.LoadAssetAtPath<Sprite>(fp);
                }
            }
        }
        foreach(var p in rules.portraits.Keys.ToArray()) {
            int i=Array.IndexOf(new[]{"Myth","Ferdia","Clot·Lach·Atros","Sarah","Audrey","Da","Nobody"},p);
            if(i>=0)rules.portraits[p]=rules.portraitVariants[codes[i]]["neutral"];
        }
        File.WriteAllText("Assets/Resources/Narrative/rules.json",JsonConvert.SerializeObject(rules,Formatting.Indented));
        var blank=Old("_0000s_0005_左底板");var border=Sprite.Create(blank.texture,blank.rect,new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(28,28,28,28));SaveSprite(border,"Assets/Resources/Art/Endings/ConfirmationFrame.asset");
        ConfigureArchive("Assets/Resources/Panel/TalkPanel/CharacterArchivePanel.prefab",avatars);
        ConfigureArchive("Assets/Resources/Panel/TalkPanel/TalkPanel.prefab",avatars);
        var panel=BuildPanel(rules,codes);
        foreach(string path in new[]{"Assets/Scenes/PlayerGame.unity","Assets/Scenes/StartGame.unity"}) {
            var scene=EditorSceneManager.OpenScene(path);
            foreach(var manager in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<UIManager>(true))) {
                manager.Configs.RemoveAll(c=>c.panelName==nameof(EndingPresentationPanel));manager.Configs.Add(new UIManager.PanelConfig{panelName=nameof(EndingPresentationPanel),prefab=panel,layer=UILayer.Top});EditorUtility.SetDirty(manager);
            }
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();
    }
    static void ConfigureArchive(string path,Sprite[] avatars)
    {
        Edit(path,root=>{
            var p=root.GetComponentInChildren<CharacterArchivePanel>(true);if(!p)return;
            // 嵌套预制体可能已有继承的同名节点，复用绑定并关闭旧覆盖节点，避免两层文字叠印。
            var t=Get<Text>(p,"_succession");
            if(!t)t=Text(p.transform,"SuccessionInfo","",0,-225,410,104,22);
            foreach(var duplicate in p.GetComponentsInChildren<Text>(true).Where(v=>v.name=="SuccessionInfo"&&v!=t))duplicate.gameObject.SetActive(false);
            t.gameObject.SetActive(true);t.text="";t.font=font;t.fontSize=22;t.lineSpacing=1.3f;At(R(t),0,-225,410,104);Set(p,"_succession",t);
            var so=new SerializedObject(p);so.FindProperty("_basicRecordPosition").vector2Value=new Vector2(0,-345);so.FindProperty("_basicRecordSize").vector2Value=new Vector2(400,85);
            var arr=so.FindProperty("_portraits");arr.arraySize=7;for(int i=0;i<7;i++){arr.GetArrayElementAtIndex(i).FindPropertyRelative("actorId").intValue=i+1;arr.GetArrayElementAtIndex(i).FindPropertyRelative("portrait").objectReferenceValue=avatars[i];}so.ApplyModifiedPropertiesWithoutUndo();
        });
    }
    static EndingPresentationPanel BuildPanel(NarrativeSystem.Rules rules,string[] codes)
    {
        var root=Node(null,"EndingPresentationPanel");At((RectTransform)root.transform,0,0,1920,1080);var panel=root.AddComponent<EndingPresentationPanel>();
        var grey=AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Art/Endings/DeadPortrait.mat");if(!grey){grey=new Material(Shader.Find("UI/EndingPortraitGray"));AssetDatabase.CreateAsset(grey,"Assets/Resources/Art/Endings/DeadPortrait.mat");}Set(panel,"_deadMaterial",grey);
        Set(panel,"_dim",Image(root.transform,"Dim",null,0,0,2200,1400,new Color(0,0,0,.74f),true));
        var select=Node(root.transform,"Selection");var cg=Node(root.transform,"CG");var complete=Node(root.transform,"Complete");var confirm=Node(root.transform,"Confirmation");
        Text(select.transform,"Title","选择接任者",0,405,1000,70,42);
        var buttons=new Button[7];var portraits=new Image[7];var names=new Text[7];var tendencies=new Text[7];
        for(int i=0;i<7;i++) {
            float x=-765+i*255;
            var hit=Image(select.transform,"Actor"+(i+1),null,x,0,244,655,Color.clear,true);buttons[i]=hit.gameObject.AddComponent<Button>();buttons[i].transition=Selectable.Transition.None;
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/"+rules.portraitVariants[codes[i]]["neutral"]+".asset");
            float h=250*sprite.rect.height/sprite.rect.width;
            portraits[i]=Image(hit.transform,"Portrait",sprite,0,-190+h/2,250,h,Color.white);portraits[i].preserveAspect=true;
            names[i]=Text(hit.transform,"Name","",0,-245,244,62,i==2?23:29);tendencies[i]=Text(hit.transform,"Tendency","",0,-300,244,42,23);
        }
        SetArray(panel,"_actorButtons",buttons);SetArray(panel,"_portraits",portraits);SetArray(panel,"_names",names);SetArray(panel,"_tendencies",tendencies);
        Set(panel,"_stay",Button(select.transform,"Stay","不转让经营权",0,-440,310,65));
        var settings=Button(select.transform,"Settings","设置",815,-440,130,60);Set(panel,"_settings",settings);
        Image(confirm.transform,"Block",null,0,0,2200,1400,new Color(0,0,0,.7f),true);
        var frame=Image(confirm.transform,"Frame",AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Art/Endings/ConfirmationFrame.asset"),0,0,720,475,Color.white,true);frame.type=UnityEngine.UI.Image.Type.Sliced;
        Set(panel,"_confirmText",Text(frame.transform,"Info","",0,45,650,315,28));
        Set(panel,"_confirm",Button(frame.transform,"Confirm","确认",150,-175,200,66));Set(panel,"_cancel",Button(frame.transform,"Cancel","返回",-150,-175,200,66));
        Image(cg.transform,"Background",null,0,0,2200,1400,Color.black,true);var picture=Image(cg.transform,"Picture",null,0,0,1920,1080,Color.white);picture.preserveAspect=true;Set(panel,"_cgImage",picture);Set(panel,"_continueHint",Text(cg.transform,"ContinueHint","",0,-475,600,42,25));
        Image(complete.transform,"Background",null,0,0,2200,1400,Color.black,true);Set(panel,"_endingText",Text(complete.transform,"Ending","",0,15,1700,100,42));Set(panel,"_returnTitle",Button(complete.transform,"ReturnTitle","返回标题",0,-170,230,65));
        Set(panel,"_black",Image(root.transform,"Fade",null,0,0,2200,1400,new Color(0,0,0,0),false));
        Set(panel,"_selectionRoot",select);Set(panel,"_confirmRoot",confirm);Set(panel,"_cgRoot",cg);Set(panel,"_completeRoot",complete);
        const string path="Assets/Resources/Panel/EndingPresentationPanel.prefab";PrefabUtility.SaveAsPrefabAsset(root,path);UnityEngine.Object.DestroyImmediate(root);return AssetDatabase.LoadAssetAtPath<EndingPresentationPanel>(path);
    }
    public static void BuildWindows()
    {
        if(!Application.productName.EndsWith("_NarrativeValidation"))throw new Exception("只允许隔离工程构建");
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;PlayerSettings.resizableWindow=true;
        string path=Path.GetFullPath("../Doc5Player/Doc5Player.exe");Directory.CreateDirectory(Path.GetDirectoryName(path));
        var report=BuildPipeline.BuildPlayer(new[]{"Assets/Scenes/StartGame.unity","Assets/Scenes/PlayerGame.unity"},path,BuildTarget.StandaloneWindows64,BuildOptions.Development);
        if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("说明5构建失败");
    }
    static void SetArray<T>(UnityEngine.Object obj,string field,T[] values) where T:UnityEngine.Object {var so=new SerializedObject(obj);var a=so.FindProperty(field);a.arraySize=values.Length;for(int i=0;i<values.Length;i++)a.GetArrayElementAtIndex(i).objectReferenceValue=values[i];so.ApplyModifiedPropertiesWithoutUndo();}
}
#endif

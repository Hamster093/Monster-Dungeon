#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>把本轮状态、身份区与说明图标直接写入原预制体，运行时只刷新状态。</summary>
public static class Round2UiEditor
{
    static Font font;
    static Sprite question,questionHover,smallTick,wideTick,buttonSkin;
    static readonly Color Paper=new Color(.95f,.91f,.82f),Gold=new Color(1,.84f,.55f);
    static void Assets()
    {
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Art/Fonts/FangZhengShuSong.ttf");
        question=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Art/Preparation/问号1.png");
        questionHover=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Art/Preparation/问号2.png");
        smallTick=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Art/Settings/-_0012_√_音量选中.png");
        wideTick=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Art/Settings/-_0010_√_选中全屏.png");
        buttonSkin=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources_moved/Icons/ui组件_2/_0000s_0000_小弹窗用按钮（透明）2.png");
        if(!font||!question||!questionHover||!smallTick||!wideTick||!buttonSkin)throw new Exception("本轮引用的原素材未找到");
    }
    public static void Configure()
    {
        Assets();CenterArchiveSprites();Edit("Assets/Resources/Panel/PlayerBuildPanel.prefab",LayoutPlayer);
        Edit("Assets/Resources/Panel/HUDPanel.prefab",LayoutHud);
        foreach(string path in new[]{"Assets/Resources/Panel/TalkPanel/CharacterArchivePanel.prefab","Assets/Resources/Panel/TalkPanel/TalkPanel.prefab"})Edit(path,LayoutArchive);
        Edit("Assets/Resources/Panel/EvolutionPanel.prefab",LayoutEvolution);
        var bindings=new List<object>();
        foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Resources/Panel"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);
            if(!AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<GuideTopicAnchor>(true).Any())continue;
            Edit(path,root=>{foreach(var anchor in root.GetComponentsInChildren<GuideTopicAnchor>(true))
            {
                if(!anchor.help)continue;Question(anchor.help);
                bindings.Add(new{prefab=path,topic=anchor.topic,automatic=anchor.automatic&&anchor.topic!="player_type"});
            }});
        }
        string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../outputs/状态档案与教程文案"));
        Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"教程入口绑定.json"),JsonConvert.SerializeObject(bindings,Formatting.Indented));
        ExportSharedCopy();AudioIntegrationEditor.LayoutOnly();AssetDatabase.SaveAssets();Debug.Log("ROUND2_UI_CONFIGURED");
    }
    public static void ExportSharedCopy()
    {
        const string path="Assets/Resources/Narrative/mechanismHelp.json";
        var catalog=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));
        if(catalog["common"]!=null)return;
        var guide=AssetDatabase.LoadAssetAtPath<GuideHelpOverlay>("Assets/Resources/Panel/GuideHelpOverlay.prefab");
        string Label(string field)=>Get<Button>(guide,field).GetComponentInChildren<Text>(true).text;
        catalog["common"]=Newtonsoft.Json.Linq.JObject.FromObject(new GuideHelpOverlay.SharedCopy{skip=Label("_skip"),close=Label("_close"),replay=Label("_replay")});
        File.WriteAllText(path,catalog.ToString(Formatting.Indented));AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
    }
    public static void LayoutStatus(GameObject root){Assets();LayoutPlayer(root);}
    static void CenterArchiveSprites()
    {
        // 只重设头像Sprite的透明裁边，不改原PNG，不影响中央立绘和终局选人。
        for(int actor=1;actor<=7;actor++)
        {
            string path="Assets/Resources/Art/Portraits/Doc5Actor"+actor+".asset";var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(!sprite)continue;
            var texture=sprite.texture;var temp=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;Texture2D read=null;
            try
            {
                Graphics.Blit(texture,temp);RenderTexture.active=temp;read=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);read.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);read.Apply();
                var pixels=read.GetPixels32();var r=sprite.rect;int minX=Mathf.CeilToInt(r.xMax),minY=Mathf.CeilToInt(r.yMax),maxX=-1,maxY=-1;
                for(int y=Mathf.CeilToInt(r.yMin);y<Mathf.FloorToInt(r.yMax);y++)for(int x=Mathf.CeilToInt(r.xMin);x<Mathf.FloorToInt(r.xMax);x++)if(pixels[y*texture.width+x].a>8){minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);}
                if(maxX<minX)continue;
                minX=Math.Max(Mathf.CeilToInt(r.xMin),minX-4);minY=Math.Max(Mathf.CeilToInt(r.yMin),minY-4);maxX=Math.Min(Mathf.FloorToInt(r.xMax)-1,maxX+4);maxY=Math.Min(Mathf.FloorToInt(r.yMax)-1,maxY+4);
                var centered=Sprite.Create(texture,new Rect(minX,minY,maxX-minX+1,maxY-minY+1),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
                EditorUtility.CopySerialized(centered,sprite);UnityEngine.Object.DestroyImmediate(centered);EditorUtility.SetDirty(sprite);
            }
            finally{if(read)UnityEngine.Object.DestroyImmediate(read);RenderTexture.active=previous;RenderTexture.ReleaseTemporary(temp);}
        }
    }
    static void LayoutPlayer(GameObject root)
    {
        var panel=root.GetComponent<PlayerBuildPanel>();var status=Get<GameObject>(panel,"_statusRoot");
        for(int i=status.transform.childCount-1;i>=0;i--)UnityEngine.Object.DestroyImmediate(status.transform.GetChild(i).gameObject);
        Stretch(R(status));var dim=status.GetComponent<Image>()??status.AddComponent<Image>();
        dim.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources_moved/Icons/场景/IMG_5500.PNG");dim.type=Image.Type.Simple;dim.color=new Color(.18f,.18f,.18f,1);dim.raycastTarget=true;
        var content=Node(status.transform,"CenteredStatus");At(R(content),0,0,1120,850);
        var title=Txt(content.transform,"Title","自身状态",0,340,900,64,44,Gold);
        var stats=Txt(content.transform,"Attributes","",0,215,1000,150,32,Paper);stats.lineSpacing=1.25f;
        var rule=Img(content.transform,"Rule",null,0,95,740,1,new Color(.8f,.65f,.4f,.32f));
        var mode=Txt(content.transform,"Thought","",0,39,1020,64,35,Gold);
        var active=Img(content.transform,"AdoptedThought",wideTick,-40,-17,160,36,Color.white);active.preserveAspect=true;
        var adopted=Txt(content.transform,"AdoptedText","已采用",50,-17,130,36,23,Paper);
        // 标识与当前思维共显隐；不把横向标识图拉成长文本底板。
        adopted.transform.SetParent(active.transform,true);At(R(adopted),90,0,120,36);
        var scroll=MakeScroll(content.transform,"EffectScroll",0,-156,870,240,out var effect);effect.fontSize=30;effect.lineSpacing=1.25f;
        var back=Btn(content.transform,"Back","返回",0,-347,190,80);
        var help=Btn(content.transform,"StatusHelp","?",480,340,48,48);
        var scope=Txt(content.transform,"LegacyScope","",0,0,1,1,20,Paper);scope.gameObject.SetActive(false);
        Set(panel,"_stats",stats);Set(panel,"_mode",mode);Set(panel,"_effect",effect);Set(panel,"_effectScroll",scroll);Set(panel,"_back",back);Set(panel,"_scope",scope);Set(panel,"_statusThoughtMark",active);
        var anchor=status.GetComponent<GuideTopicAnchor>()??status.AddComponent<GuideTopicAnchor>();anchor.topic="player_status";anchor.help=help;anchor.highlights=new[]{R(stats),R(mode)};
        foreach(var button in Get<Button[]>(panel,"_types").Concat(Get<Button[]>(panel,"_thoughts")))
        {
            var marker=button.transform.Find("Selected");if(marker)UnityEngine.Object.DestroyImmediate(marker.gameObject);
            var tick=Img(button.transform,"Selected",smallTick,153,4,34,32,Color.white);tick.preserveAspect=true;tick.gameObject.SetActive(false);
        }
    }
    static void LayoutHud(GameObject root)
    {
        var panel=root.GetComponent<HUDPanel>();var button=Get<Button>(panel,"_playerButton");var text=Get<Text>(panel,"_playerText");
        // 1920参考坐标：金币正文结束约410，阶段框从680开始，左右均留出独立间距。
        var rect=R(button);rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(530,-68);rect.sizeDelta=new Vector2(190,48);rect.localScale=Vector3.one;
        var image=button.GetComponent<Image>();image.sprite=null;image.type=Image.Type.Simple;image.color=new Color(.045f,.035f,.025f,.35f);button.targetGraphic=text;
        text.fontSize=27;text.color=Gold;At(R(text),0,0,176,44);text.text="自身状态　›";
    }
    static void LayoutArchive(GameObject root)
    {
        var panel=root.GetComponentInChildren<CharacterArchivePanel>(true);if(!panel)return;
        var icon=Get<Image>(panel,"_iconImage");var frame=panel.transform.Find("Doc2PortraitFrame") as RectTransform;
        if(!frame)throw new Exception("原档案头像框不存在");
        var viewport=icon.transform.parent as RectTransform;At(viewport,frame.anchoredPosition.x,frame.anchoredPosition.y,96,96);At(R(icon),0,0,96,96);icon.preserveAspect=true;
        var name=Get<Text>(panel,"_name");At(R(name),83,349,270,46);name.fontSize=26;name.horizontalOverflow=HorizontalWrapMode.Wrap;name.verticalOverflow=VerticalWrapMode.Overflow;
        var level=Get<Text>(panel,"_level");level.transform.SetParent(panel.transform,false);At(R(level),82,306,270,34);level.gameObject.SetActive(true);
        var intro=Get<Text>(panel,"_headerIntroduction");At(R(intro),86,245,284,82);intro.lineSpacing=1.18f;
    }
    static void LayoutEvolution(GameObject root)
    {
        var panel=root.GetComponent<EvolutionPanel>();var buttons=Get<Button[]>(panel,"_directions");var marks=new Image[buttons.Length];
        for(int i=0;i<buttons.Length;i++)
        {
            var old=buttons[i].transform.Find("ChoiceMark");if(old)old.gameObject.SetActive(false);
            var previous=buttons[i].transform.Find("SelectedTick");if(previous)UnityEngine.Object.DestroyImmediate(previous.gameObject);
            marks[i]=Img(buttons[i].transform,"SelectedTick",smallTick,-151,22,30,28,Color.white);marks[i].preserveAspect=true;marks[i].gameObject.SetActive(false);
        }
        Arr(panel,"_directionCheckmarks",marks);
    }
    static void Question(Button button)
    {
        var image=button.GetComponent<Image>()??button.gameObject.AddComponent<Image>();image.sprite=question;image.type=Image.Type.Simple;image.preserveAspect=true;image.color=Color.white;image.raycastTarget=true;
        button.targetGraphic=image;button.transition=Selectable.Transition.SpriteSwap;button.spriteState=new SpriteState{highlightedSprite=questionHover,selectedSprite=questionHover,pressedSprite=questionHover,disabledSprite=question};
        foreach(var text in button.GetComponentsInChildren<Text>(true))text.gameObject.SetActive(false);
        foreach(var child in button.GetComponentsInChildren<Image>(true))if(child!=image)child.gameObject.SetActive(false);
        var r=R(button);r.sizeDelta=new Vector2(48,48);r.localScale=Vector3.one;
    }
    static ScrollRect MakeScroll(Transform parent,string name,float x,float y,float w,float h,out Text text)
    {
        var root=Node(parent,name);At(R(root),x,y,w,h);var scroll=root.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.vertical=true;scroll.scrollSensitivity=32;scroll.movementType=ScrollRect.MovementType.Clamped;
        var view=Img(root.transform,"Viewport",null,-9,0,w-24,h,new Color(1,1,1,.001f));view.raycastTarget=true;view.gameObject.AddComponent<RectMask2D>();
        text=Txt(view.transform,"Text","",0,0,w-48,h,30,Paper);text.alignment=TextAnchor.UpperLeft;text.verticalOverflow=VerticalWrapMode.Overflow;
        var r=R(text);r.anchorMin=new Vector2(0,1);r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,1);r.sizeDelta=new Vector2(-16,h);r.anchoredPosition=Vector2.zero;
        var track=Img(root.transform,"Track",null,w/2-3,0,5,h,new Color(.8f,.65f,.42f,.13f));var bar=track.gameObject.AddComponent<Scrollbar>();var handle=Img(track.transform,"Handle",null,0,0,5,60,new Color(.8f,.65f,.42f,.65f));Stretch(R(handle));bar.handleRect=R(handle);bar.targetGraphic=handle;bar.direction=Scrollbar.Direction.BottomToTop;
        scroll.viewport=R(view);scroll.content=r;scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;return scroll;
    }
    static Button Btn(Transform parent,string name,string label,float x,float y,float w,float h)
    {var image=Img(parent,name,buttonSkin,x,y,w,h,new Color(.48f,.40f,.30f,.65f));image.preserveAspect=true;image.raycastTarget=true;var b=image.gameObject.AddComponent<Button>();var t=Txt(image.transform,"Label",label,0,0,w-16,h-8,30,Paper);b.targetGraphic=t;var c=b.colors;c.highlightedColor=c.selectedColor=Gold;c.pressedColor=new Color(.7f,.6f,.45f);b.colors=c;return b;}
    static Image Img(Transform parent,string name,Sprite sprite,float x,float y,float w,float h,Color color)
    {var go=Node(parent,name);var image=go.AddComponent<Image>();image.sprite=sprite;image.color=color;image.raycastTarget=false;At(R(go),x,y,w,h);return image;}
    static Text Txt(Transform parent,string name,string value,float x,float y,float w,float h,int size,Color color)
    {var go=Node(parent,name);var t=go.AddComponent<Text>();t.font=font;t.text=value;t.color=color;t.fontSize=size;t.alignment=TextAnchor.MiddleCenter;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;t.raycastTarget=false;At(R(go),x,y,w,h);return t;}
    static GameObject Node(Transform parent,string name){var go=new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);return go;}
    static RectTransform R(Component o)=>(RectTransform)o.transform;static RectTransform R(GameObject o)=>(RectTransform)o.transform;
    static void At(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);r.localScale=Vector3.one;}
    static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;r.localScale=Vector3.one;}
    static T Get<T>(UnityEngine.Object o,string f)
    {
        var p=new SerializedObject(o).FindProperty(f);
        if(typeof(T).IsArray){var type=typeof(T).GetElementType();var array=System.Array.CreateInstance(type,p.arraySize);for(int i=0;i<p.arraySize;i++)array.SetValue(p.GetArrayElementAtIndex(i).objectReferenceValue,i);return (T)(object)array;}
        return (T)(object)p.objectReferenceValue;
    }
    // Arrays are serialized explicitly; public state and business models are unchanged.
    static void Set(UnityEngine.Object o,string f,UnityEngine.Object v){var so=new SerializedObject(o);so.FindProperty(f).objectReferenceValue=v;so.ApplyModifiedPropertiesWithoutUndo();}
    static void Arr(UnityEngine.Object o,string f,UnityEngine.Object[] values){var so=new SerializedObject(o);var a=so.FindProperty(f);a.arraySize=values.Length;for(int i=0;i<values.Length;i++)a.GetArrayElementAtIndex(i).objectReferenceValue=values[i];so.ApplyModifiedPropertiesWithoutUndo();}
    static void Edit(string path,Action<GameObject> fn){var go=PrefabUtility.LoadPrefabContents(path);try{fn(go);PrefabUtility.SaveAsPrefabAsset(go,path);}finally{PrefabUtility.UnloadPrefabContents(go);}}
}
#endif

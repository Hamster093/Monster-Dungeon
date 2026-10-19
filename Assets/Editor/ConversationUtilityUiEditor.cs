#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>顶部自身状态入口与原道具菜单命中区域；存档保持原有竖排布局。</summary>
public static class ConversationUtilityUiEditor
{
    static Font font;
    static Sprite frame,button;
    static Color Paper=new Color(.95f,.90f,.79f,1),Gold=new Color(1,.86f,.60f,1);
    static RectTransform R(Component c)=>(RectTransform)c.transform;
    static void At(RectTransform r,float x,float y,float w,float h)
    {r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);r.localScale=Vector3.one;}
    static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
    static GameObject Node(Transform p,string name){var g=new GameObject(name,typeof(RectTransform));g.layer=5;g.transform.SetParent(p,false);return g;}
    static Image Img(Transform p,string name,Sprite sprite,float x,float y,float w,float h,Color color,bool hit=false)
    {var g=Node(p,name);var i=g.AddComponent<Image>();i.sprite=sprite;i.color=color;i.raycastTarget=hit;At(R(i),x,y,w,h);return i;}
    static Text Txt(Transform p,string name,string value,float x,float y,float w,float h,int size,TextAnchor align=TextAnchor.MiddleLeft)
    {var g=Node(p,name);var t=g.AddComponent<Text>();t.font=font;t.fontSize=size;t.text=value;t.color=Paper;t.alignment=align;t.lineSpacing=1.2f;t.raycastTarget=false;At(R(t),x,y,w,h);return t;}
    static T Get<T>(UnityEngine.Object o,string n)where T:UnityEngine.Object=>new SerializedObject(o).FindProperty(n).objectReferenceValue as T;
    static void Set(UnityEngine.Object o,string n,UnityEngine.Object value){var s=new SerializedObject(o);s.FindProperty(n).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    static void Arr(UnityEngine.Object o,string n,UnityEngine.Object[] values){var s=new SerializedObject(o);var a=s.FindProperty(n);a.arraySize=values.Length;for(int i=0;i<values.Length;i++)a.GetArrayElementAtIndex(i).objectReferenceValue=values[i];s.ApplyModifiedPropertiesWithoutUndo();}
    static void Edit(string path,Action<GameObject> action){var g=PrefabUtility.LoadPrefabContents(path);try{action(g);PrefabUtility.SaveAsPrefabAsset(g,path);}finally{PrefabUtility.UnloadPrefabContents(g);}}
    public static void Configure()
    {
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Art/Fonts/FangZhengShuSong.ttf");
        frame=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Art/UI/Doc6LandscapeFrame.asset");
        button=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Art/UI/Doc6SmallFrame.asset");
        Edit("Assets/Resources/Panel/HUDPanel.prefab",PlayerEntry);
        AssetDatabase.SaveAssets();
    }
    static void PlayerEntry(GameObject root)
    {
        var panel=root.GetComponent<HUDPanel>();var b=Get<Button>(panel,"_playerButton");var t=Get<Text>(panel,"_playerText");
        At(R(b),-808,417,248,50);At(R(t),0,0,218,42);t.fontSize=28;
        var image=b.GetComponent<Image>();image.sprite=button;image.type=Image.Type.Sliced;
        var colors=b.colors;colors.highlightedColor=colors.selectedColor=new Color(1,.94f,.77f,1);colors.disabledColor=new Color(.4f,.38f,.34f,1);b.colors=colors;
    }
    static void ShopActions(GameObject root)
    {
        // 沿用原两行整图，只把点击区域按菜单中心对齐；不再让可见行与命中框错开。
        var panel=root.GetComponent<ShopItemActionPanel>();var view=Get<Button>(panel,"_examineButton");var buy=Get<Button>(panel,"_buyButton");
        At(R(view),0,46,192,86);At(R(buy),0,-46,192,86);
        foreach(var b in new[]{view,buy})HoverRow(b);
    }
    static void ItemActions(GameObject root)
    {
        var panel=root.GetComponent<ItemActionPanel>();
        foreach(var key in new[]{"_examineButton","_dropButton","_useButton"})HoverRow(Get<Button>(panel,key));
    }
    static void HoverRow(Button b)
    {
        var image=b.GetComponent<Image>();image.color=Color.white;image.sprite=null;b.targetGraphic=image;
        b.transition=Selectable.Transition.ColorTint;var c=b.colors;
        c.normalColor=Color.clear;c.highlightedColor=c.selectedColor=new Color(.96f,.72f,.38f,.16f);
        c.pressedColor=new Color(.96f,.72f,.38f,.28f);c.disabledColor=new Color(.1f,.1f,.1f,.3f);b.colors=c;
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;

/// <summary>用户修订稿只调整教程能力与排版，不补正文、不删除玩法。所有改动写入原预制体。</summary>
public static class TutorialRevisionEditor
{
    public static void Configure(){AudioIntegrationEditor.LayoutOnly();AssetDatabase.SaveAssets();Debug.Log("TUTORIAL_REVISION_CONFIGURED");}
    public static void ConfigureAndCheck(){Configure();TutorialRevisionChecks.RunBatch();}
    public static void PatchRoot(GameObject root)
    {
        var data=JObject.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Narrative/mechanismHelp.json").text);
        foreach(var a in root.GetComponentsInChildren<GuideTopicAnchor>(true))
        {
            var entry=((JArray)data["entries"]).OfType<JObject>().FirstOrDefault(e=>(string)e["id"]==a.topic);if(entry==null)continue;
            bool help=((JArray)entry["main"]).Count+((JArray)entry["details"]).Count>0;
            a.automatic=((JArray)entry["intro"]).Count>0&&a.topic!="player_type";
            if(a.Owner is ShopPanel)a.automatic=((JArray)entry["intro"]).Count>0||((JArray)((JArray)data["entries"]).OfType<JObject>().First(e=>(string)e["id"]=="storage")["intro"]).Count>0;
            if(!help&&a.help)
            {
                // 类型和思维共用同一个入口，保留对象供思维阶段启用，类型阶段由运行状态隐藏。
                if(a.Owner is PlayerBuildPanel)a.help.gameObject.SetActive(false);
                else{UnityEngine.Object.DestroyImmediate(a.help.gameObject);a.help=null;}
            }
            if(a.Owner is CharacterArchivePanel&&a.help)
            {var r=(RectTransform)a.help.transform;r.anchoredPosition=new Vector2(r.anchoredPosition.x,r.anchorMax.y>.9f?-38:6);}
        }
        foreach(var hud in root.GetComponentsInChildren<HUDPanel>(true))
        {var b=Get<Button>(hud,"_playerButton");if(b)((RectTransform)b.transform).anchoredPosition=new Vector2(530,-68);}
        var overlay=root.GetComponent<GuideHelpOverlay>();if(overlay)PatchOverlay(overlay,data["common"] as JObject);
    }
    static void PatchOverlay(GuideHelpOverlay overlay,JObject common)
    {
        var text=Get<Text>(overlay,"_tutorialText");var copy=Get<RectTransform>(overlay,"_tutorialCopy");
        var old=copy.Find("TutorialScroll");var go=old?old.gameObject:new GameObject("TutorialScroll",typeof(RectTransform),typeof(ScrollRect));if(!old)go.transform.SetParent(copy,false);
        var scroll=go.GetComponent<ScrollRect>();var vp=go.transform.Find("Viewport");
        if(!vp){var view=new GameObject("Viewport",typeof(RectTransform),typeof(Image),typeof(RectMask2D));view.transform.SetParent(go.transform,false);vp=view.transform;}
        go.layer=copy.gameObject.layer;vp.gameObject.layer=copy.gameObject.layer;
        var rect=(RectTransform)vp;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        var bg=vp.GetComponent<Image>();bg.color=Color.clear;bg.raycastTarget=true;
        text.transform.SetParent(vp,false);text.raycastTarget=false;
        var tr=text.rectTransform;tr.anchorMin=tr.anchorMax=new Vector2(.5f,1);tr.pivot=new Vector2(.5f,1);tr.anchoredPosition=Vector2.zero;
        text.verticalOverflow=VerticalWrapMode.Overflow;scroll.viewport=rect;scroll.content=tr;scroll.horizontal=false;scroll.vertical=true;scroll.scrollSensitivity=32;scroll.movementType=ScrollRect.MovementType.Clamped;
        Set(overlay,"_tutorialScroll",scroll);
        foreach(var p in new[]{("_skip","skip"),("_close","close"),("_replay","replay")})
        {var b=Get<Button>(overlay,p.Item1);var label=b.GetComponentInChildren<Text>(true);if(label)label.text=(string)common[p.Item2];}
    }
    static T Get<T>(UnityEngine.Object o,string name)where T:UnityEngine.Object=>new SerializedObject(o).FindProperty(name).objectReferenceValue as T;
    static void Set(UnityEngine.Object o,string name,UnityEngine.Object value){var s=new SerializedObject(o);s.FindProperty(name).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
}
#endif

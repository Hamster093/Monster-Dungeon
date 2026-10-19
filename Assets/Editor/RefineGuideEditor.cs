#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>保存本轮教程绑定；只补充指路摘要的状态行，不重构普通委托和存档界面。</summary>
public static class RefineGuideEditor
{
    public static void Configure()
    {
        Edit("GuideHelpOverlay",root=>{
            foreach(var image in root.GetComponentsInChildren<Image>(true))
                if(image.name.StartsWith("Dim",StringComparison.Ordinal))image.color=new Color(0,0,0,.985f);
        });
        Edit("DescriptionPanel",Description);
        // 冒险者档案的五步高亮由原面板临时切页，结束后恢复原页与滚动。
        Edit("TalkPanel/CharacterArchivePanel",root=>{
            var anchor=root.GetComponent<GuideTopicAnchor>();if(anchor)anchor.automatic=true;
        });
        AssetDatabase.SaveAssets();Debug.Log("REFINE_GUIDES_CONFIGURED");
    }
    private static void Description(GameObject root)
    {
        var p=root.GetComponent<DescriptionPanel>();
        var quantity=Get<Text>(p,"_quantityText");var title=Get<Text>(p,"_contextTitle");
        var summary=Get<Text>(p,"_routeSummary");var difficulty=Get<Text>(p,"_combatDifficulty");
        var state=Node(quantity.transform.parent,"MonsterState").GetComponent<Text>();
        if(!state)state=Node(quantity.transform.parent,"MonsterState").AddComponent<Text>();
        state.font=quantity.font;state.fontSize=27;state.color=quantity.color;state.raycastTarget=false;
        state.alignment=TextAnchor.MiddleLeft;state.horizontalOverflow=HorizontalWrapMode.Wrap;
        state.verticalOverflow=VerticalWrapMode.Truncate;At(state.rectTransform,99,116,650,38);
        Set(p,"_monsterState",state);
        var reward=(RectTransform)Node(summary.transform,"GuideReward").transform;
        var injury=(RectTransform)Node(summary.transform,"GuideInjury").transform;
        At(reward,0,18,summary.rectTransform.sizeDelta.x,36);
        At(injury,0,-18,summary.rectTransform.sizeDelta.x,36);
        var help=Node(title.transform,"MechanismHelp");At((RectTransform)help.transform,440,0,52,52);
        var image=help.GetComponent<Image>()??help.AddComponent<Image>();image.color=new Color(.04f,.03f,.02f,.7f);image.raycastTarget=true;
        var b=help.GetComponent<Button>()??help.AddComponent<Button>();
        var label=Node(help.transform,"Label").GetComponent<Text>();if(!label)label=Node(help.transform,"Label").AddComponent<Text>();
        label.font=quantity.font;label.fontSize=32;label.text="?";label.color=new Color(.98f,.8f,.5f);label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;At(label.rectTransform,0,0,52,52);
        b.targetGraphic=label;
        var anchor=root.GetComponent<GuideTopicAnchor>()??root.AddComponent<GuideTopicAnchor>();
        anchor.topic="route_detail";anchor.automatic=true;anchor.help=b;
        anchor.highlights=new[]{quantity.rectTransform,reward,injury,difficulty.rectTransform,state.rectTransform};
    }
    private static GameObject Node(Transform parent,string name)
    {var old=parent.Find(name);if(old)return old.gameObject;var go=new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);return go;}
    private static void At(RectTransform r,float x,float y,float w,float h)
    {r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);r.localScale=Vector3.one;}
    private static T Get<T>(UnityEngine.Object o,string n)where T:UnityEngine.Object=>new SerializedObject(o).FindProperty(n).objectReferenceValue as T;
    private static void Set(UnityEngine.Object o,string n,UnityEngine.Object v){var s=new SerializedObject(o);s.FindProperty(n).objectReferenceValue=v;s.ApplyModifiedPropertiesWithoutUndo();}
    private static void Edit(string name,Action<GameObject> action)
    {string path="Assets/Resources/Panel/"+name+".prefab";var root=PrefabUtility.LoadPrefabContents(path);try{action(root);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}}
}
#endif

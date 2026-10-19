#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>把阅读反馈与各人物显示预设写入现有预制体；不调整经营框体或剧情正文。</summary>
public static class RefineNarrativeEditor
{
    private static readonly Color Ink=new Color(.19f,.15f,.11f,1);
    [MenuItem("Tools/交互精修/对话与人物显示")]
    public static void Configure()
    {
        Edit("Assets/Resources/Panel/TalkPanel/TalkPanel.prefab",root=>{
            var panel=root.GetComponent<TalkPanel>();var so=new SerializedObject(panel);
            so.FindProperty("_clickCooldown").floatValue=.26f;
            var layouts=so.FindProperty("_portraitLayouts");layouts.arraySize=7;
            string[] codes={"MY","FE","DW","SA","AU","DA","UN"};
            float[] heights={910,880,630,950,940,850,930};
            float[] widths={700,660,880,700,700,800,760};
            // 人类以桌后同一地面为参照；矮人小队独立设置，不把横向合影压成长人形。
            Vector2[] baselines={new Vector2(-35,-530),new Vector2(-35,-530),new Vector2(-35,-525),new Vector2(-35,-530),new Vector2(-35,-530),new Vector2(-35,-530),new Vector2(-35,-530)};
            for(int i=0;i<7;i++){
                var entry=layouts.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("addressPrefix").stringValue="Art/CharacterSprites/"+codes[i]+"/";
                entry.FindPropertyRelative("height").floatValue=heights[i];entry.FindPropertyRelative("maxWidth").floatValue=widths[i];
                entry.FindPropertyRelative("baseline").vector2Value=baselines[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            var image=Get<Image>(panel,"_iconImage");image.preserveAspect=true;image.raycastTarget=false;
            var rect=image.rectTransform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.pivot=new Vector2(.5f,0);rect.localScale=Vector3.one;
            rect.anchoredPosition=baselines[0];rect.sizeDelta=new Vector2(570,910);
            var name=Get<Text>(panel,"_speakerNameText");name.color=DialogueHistoryItem.NameColor(Ink);name.fontStyle=FontStyle.Bold;name.raycastTarget=false;
        });
        Edit("Assets/Resources/Panel/DialogueOptionItem.prefab",root=>{
            var item=root.GetComponent<DialogueOptionItem>();var text=Get<Text>(item,"_text");
            text.color=Ink;text.raycastTarget=false;text.lineSpacing=Mathf.Max(text.lineSpacing,1.22f);
            var button=Get<Button>(item,"_button");button.transition=Selectable.Transition.None;
            var image=button.GetComponent<Image>();if(image){image.color=new Color(.35f,.24f,.10f,.09f);image.raycastTarget=true;}
        });
        Edit("Assets/Resources/DialogueHistoryItem.prefab",root=>{
            var text=root.GetComponent<DialogueHistoryItem>().content;text.color=Ink;text.raycastTarget=true;text.supportRichText=true;
        });
        Edit("Assets/Resources/DialogueHistoryEntry.prefab",root=>{
            var entry=root.GetComponent<DialogueHistoryEntry>();var body=Get<Text>(entry,"_contentText");var name=Get<Text>(entry,"_speakerNameText");
            body.raycastTarget=true;name.raycastTarget=true;name.color=DialogueHistoryItem.NameColor(body.color);name.fontStyle=FontStyle.Bold;
        });
        AssetDatabase.SaveAssets();Debug.Log("REFINE_NARRATIVE_CONFIGURED");
    }
    private static T Get<T>(UnityEngine.Object target,string field)where T:UnityEngine.Object
    {return new SerializedObject(target).FindProperty(field).objectReferenceValue as T;}
    private static void Edit(string path,Action<GameObject> action)
    {
        var root=PrefabUtility.LoadPrefabContents(path);
        try{action(root);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
#endif

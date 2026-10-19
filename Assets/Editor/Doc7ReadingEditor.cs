#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>仅调整书宋正文的行距与换行，不移动原框、不改字号或用户原文。</summary>
public static class Doc7ReadingEditor
{
    private static Font font;
    [MenuItem("Tools/说明7/正文阅读间距")]
    public static void Configure()
    {
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Art/Fonts/FangZhengShuSong.ttf");
        Edit("Assets/Resources/Panel/TalkPanel/TalkPanel.prefab",root=>{
            var panel=root.GetComponent<TalkPanel>();var writer=Get<MonoBehaviour>(panel,"_typewriterComponent");
            Readable(Get<Text>(writer,"text"),1.18f);
        });
        Edit("Assets/Resources/Panel/DialogueOptionItem.prefab",root=>Readable(Get<Text>(root.GetComponent<DialogueOptionItem>(),"_text"),1.22f));
        Edit("Assets/Resources/DialogueHistoryItem.prefab",root=>Readable(root.GetComponent<DialogueHistoryItem>().content,1.22f));
        foreach(string path in new[]{"Assets/Resources/Panel/TalkPanel/CharacterArchivePanel.prefab","Assets/Resources/Panel/TalkPanel/TalkPanel.prefab"})
            Edit(path,root=>{
                var archive=root.GetComponentInChildren<CharacterArchivePanel>(true);
                if(archive)Readable(Get<Text>(archive,"_Introduction"),1.2f);
            });
        AssetDatabase.SaveAssets();
    }
    private static void Readable(Text text,float spacing)
    {
        if(!text)return;
        text.font=font;text.lineSpacing=Mathf.Max(text.lineSpacing,spacing);
        text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Overflow;
        text.raycastTarget=false;
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

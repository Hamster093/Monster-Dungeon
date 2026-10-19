#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;

/// <summary>只调整终局选人中的独立等比尺寸；不改接待立绘、头像或其他界面。</summary>
public static class FinalInteractionFixEditor
{
    public static readonly float[] PortraitWidths={225,220,260,228,225,330,240};
    public static readonly float[] ActorPositions={-800,-535,-270,-8,240,530,825};
    public static void Configure()
    {
        const string path="Assets/Resources/Panel/EndingPresentationPanel.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var p=root.GetComponent<EndingPresentationPanel>();
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            var images=(Image[])typeof(EndingPresentationPanel).GetField("_portraits",flags).GetValue(p);
            var buttons=(Button[])typeof(EndingPresentationPanel).GetField("_actorButtons",flags).GetValue(p);
            for(int i=0;i<images.Length;i++)
            {
                var actor=(RectTransform)buttons[i].transform;actor.anchoredPosition=new Vector2(ActorPositions[i],0);
                actor.sizeDelta=new Vector2(Mathf.Max(244,PortraitWidths[i]),655);
                var image=images[i];float width=PortraitWidths[i],height=width*image.sprite.rect.height/image.sprite.rect.width;
                image.preserveAspect=true;image.rectTransform.sizeDelta=new Vector2(width,height);
                image.rectTransform.anchoredPosition=new Vector2(0,-190+height*.5f);
            }
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();Debug.Log("FINAL_INTERACTION_LAYOUT_SAVED");
    }
    public static void ConfigureAndCheck(){Configure();FinalInteractionChecks.RunBatch();}
}
#endif

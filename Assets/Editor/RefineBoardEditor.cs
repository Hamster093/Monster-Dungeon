#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>保存仓库图片适配与棋盘操作提示，购买页的价格分件和等级绑定保持原样。</summary>
public static class RefineBoardEditor
{
    [MenuItem("Tools/交互精修/仓库与棋盘提示")]
    public static void Configure()
    {
        Edit("Assets/Resources/Panel/ShopPanel.prefab",root=>{
            var panel=root.GetComponent<ShopPanel>();
            var icons=new Image[panel._itemSlots.Length];
            for(int i=0;i<icons.Length;i++)
            {
                var so=new SerializedObject(panel._itemSlots[i]);
                icons[i]=so.FindProperty("_itemImage").objectReferenceValue as Image;
                if(icons[i])icons[i].preserveAspect=true;
            }
            var data=new SerializedObject(panel);
            data.FindProperty("_slotGrid").objectReferenceValue=panel._itemSlots[0].GetComponentInParent<GridLayoutGroup>();
            var array=data.FindProperty("_slotIcons");array.arraySize=icons.Length;
            for(int i=0;i<icons.Length;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=icons[i];
            data.FindProperty("_storageCellSize").vector2Value=new Vector2(196,202);
            data.FindProperty("_storageIconSize").vector2Value=new Vector2(176,176);
            data.ApplyModifiedPropertiesWithoutUndo();
        });
        Edit("Assets/Resources/Panel/PreparationPanel.prefab",root=>{
            var board=root.GetComponentInChildren<BackpackBoard>(true);
            if(!board)throw new InvalidOperationException("整备面板未找到原棋盘。");
            var existing=root.transform.Find("BoardInteractionHint");
            var go=existing?existing.gameObject:new GameObject("BoardInteractionHint",typeof(RectTransform),typeof(Text));
            if(!existing){go.layer=5;go.transform.SetParent(root.transform,false);}
            var text=go.GetComponent<Text>();text.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Art/Fonts/FangZhengShuSong.ttf");
            text.text=BackpackBoard.IdleHint;text.fontSize=24;text.color=new Color(.96f,.89f,.75f);
            text.alignment=TextAnchor.MiddleCenter;text.horizontalOverflow=HorizontalWrapMode.Wrap;
            text.verticalOverflow=VerticalWrapMode.Truncate;text.raycastTarget=false;
            var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
            rect.anchoredPosition=new Vector2(0,386);rect.sizeDelta=new Vector2(940,38);
            var data=new SerializedObject(board);data.FindProperty("_editHint").objectReferenceValue=text;data.ApplyModifiedPropertiesWithoutUndo();
        });
        AssetDatabase.SaveAssets();
    }
    private static void Edit(string path,Action<GameObject> action)
    {
        var root=PrefabUtility.LoadPrefabContents(path);
        try{action(root);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
#endif

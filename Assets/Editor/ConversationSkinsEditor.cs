#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>专用检定皮肤与专用操作按钮。选项行不再借用方形底板作为按钮。</summary>
public static class ConversationSkinsEditor
{
    static Sprite button,oldFrame,dice,oldLandscape,lightLandscape;
    static readonly string[] Prefabs={
        "Assets/Resources/Panel/PlayerBuildPanel.prefab",
        "Assets/Resources/Panel/EndingPresentationPanel.prefab","Assets/Resources/Panel/DiceCheckPanel.prefab",
        "Assets/Resources/Panel/HUDPanel.prefab"};
    public static void Configure()
    {
        oldFrame=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Art/UI/Doc6SmallFrame.asset");
        oldLandscape=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Art/UI/Doc6LandscapeFrame.asset");
        lightLandscape=Import("Assets/Resources/Art/UI/Doc6LightLandscapeFrame.png",100);
        dice=Import("Assets/Resources/Art/UI/DiceLightPanel.png",100);
        var source=Import("Assets/Resources/Art/UI/ActionButton.png",500);
        const string path="Assets/Resources/Art/UI/ActionButton.asset";
        button=AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if(!button)
        {
            // 只在Unity内划定透明画布中的按钮区域，原生成图片保留不变。
            button=Sprite.Create(source.texture,new Rect(37,165,2098,411),new Vector2(.5f,.5f),500,0,SpriteMeshType.FullRect,new Vector4(64,64,64,64));
            AssetDatabase.CreateAsset(button,path);
        }
        foreach(var prefab in Prefabs)Edit(prefab,ApplyButtons);
        Edit("Assets/Resources/Panel/DiceCheckPanel.prefab",LightDice);
        AssetDatabase.SaveAssets();Debug.Log("CONVERSATION_DEDICATED_SKINS_CONFIGURED");
    }
    static Sprite Import(string path,int ppu)
    {
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;
        importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=ppu;
        importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=4096;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static void ApplyButtons(GameObject root)
    {
        // 新横框按原生3:2比例显示；仅替换本轮新增面板，旧竖框及存档背景保持原样。
        foreach(var panel in root.GetComponentsInChildren<Image>(true))
            if(panel.sprite==oldLandscape){panel.sprite=lightLandscape;panel.type=Image.Type.Simple;panel.preserveAspect=true;}
        foreach(var b in root.GetComponentsInChildren<Button>(true))
        {
            var image=b.GetComponent<Image>();if(!image||image.sprite!=oldFrame)continue;
            bool choice=b.name.StartsWith("Type")||b.name.StartsWith("Thought")||b.name.StartsWith("Direction");
            if(choice)
            {
                image.sprite=null;image.type=Image.Type.Simple;image.color=new Color(.075f,.055f,.035f,.96f);
                var rule=b.transform.Find("ChoiceBottomRule");
                if(!rule){var line=new GameObject("ChoiceBottomRule",typeof(RectTransform),typeof(Image));line.transform.SetParent(b.transform,false);rule=line.transform;}
                var r=(RectTransform)rule;r.anchorMin=new Vector2(0,0);r.anchorMax=new Vector2(1,0);r.pivot=new Vector2(.5f,0);r.anchoredPosition=new Vector2(0,3);r.sizeDelta=new Vector2(-20,1);
                var lineImage=rule.GetComponent<Image>();lineImage.color=new Color(.6f,.42f,.22f,.55f);lineImage.raycastTarget=false;
                var outline=b.GetComponent<Outline>();if(outline)outline.effectDistance=Vector2.one;
            }
            else
            {
                image.sprite=button;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=1;image.color=Color.white;
                // 专用按钮的原生设计尺寸约420×82；短文字按钮也保留合理横向比例。
                var rect=(RectTransform)b.transform;float width=Mathf.Max(rect.sizeDelta.x,rect.sizeDelta.y*4.2f);
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,width);
                var layout=b.GetComponent<LayoutElement>();if(layout){layout.minWidth=layout.preferredWidth=width;}
                if(b.name=="RecordsUiLatest")rect.anchoredPosition=new Vector2(475,rect.anchoredPosition.y);
            }
        }
    }
    static void LightDice(GameObject root)
    {
        root.GetComponent<Image>().color=new Color(0,0,0,.28f);
        var reveal=root.transform.Find("Reveal");var frame=reveal.Find("Frame").GetComponent<Image>();
        frame.sprite=dice;frame.type=Image.Type.Simple;frame.preserveAspect=true;frame.color=new Color(1,1,1,.78f);
        reveal.Find("FrameBacking").gameObject.SetActive(false);
    }
    static void Edit(string path,Action<GameObject> action)
    {
        var root=PrefabUtility.LoadPrefabContents(path);try{action(root);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
#endif

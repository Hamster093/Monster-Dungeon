#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>音频导入和本轮小幅定位均保存到原预制体/场景，不依赖运行时重排。</summary>
public static class AudioIntegrationEditor
{
    public static void Configure()
    {
        foreach(string file in Directory.GetFiles("Assets/Resources/Audio","*.wav"))
        {
            var importer=AssetImporter.GetAtPath(file.Replace('\\','/')) as AudioImporter;if(!importer)continue;
            var setting=importer.defaultSampleSettings;
            bool streaming=Path.GetFileName(file).StartsWith("BGM_")||Path.GetFileName(file).StartsWith("AMB_");
            setting.loadType=streaming?AudioClipLoadType.Streaming:AudioClipLoadType.DecompressOnLoad;
            setting.compressionFormat=AudioCompressionFormat.Vorbis;setting.quality=.7f;
            setting.preloadAudioData=!streaming;importer.defaultSampleSettings=setting;importer.loadInBackground=streaming;importer.SaveAndReimport();
        }
        LayoutOnly();AssetDatabase.SaveAssets();Debug.Log("AUDIO_INTEGRATION_CONFIGURED");
    }
    public static void LayoutOnly()
    {
        foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Resources/Panel","Assets/Resources/UI"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);var root=PrefabUtility.LoadPrefabContents(path);
            try {PatchRoot(root);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        // 首页的设置是场景内旧实例，同样撤除入口；不改首页和四槽的外观。
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/StartGame.unity");
        foreach(var root in scene.GetRootGameObjects())PatchRoot(root);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }
    public static void PatchRoot(GameObject root)
    {
        foreach(var a in root.GetComponentsInChildren<GuideTopicAnchor>(true))
        {
            if(a.Owner is SettingsPanel||a.Owner is CGGalleryPanel)
            {if(a.help)UnityEngine.Object.DestroyImmediate(a.help.gameObject);UnityEngine.Object.DestroyImmediate(a);continue;}
            if(!a.help)continue;
            var rect=(RectTransform)a.help.transform;
            if(a.Owner is TalkPanel)
            {
                // 保持在原右侧框内：标题文字的视觉中线比几何中线略低。
                rect.anchorMin=rect.anchorMax=new Vector2(1,1);rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(-55,-44);
            }
            if(a.Owner is PreparationPanel)
            {
                var bar=a.help.transform.parent as RectTransform;
                // 问号放到标题条右侧空地，图案与边框间留14像素。
                rect.anchorMin=rect.anchorMax=new Vector2(1,.5f);rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(rect.rect.width*.5f+14,-3);
            }
        }
        foreach(var hud in root.GetComponentsInChildren<HUDPanel>(true))
        {
            var so=new SerializedObject(hud);var b=so.FindProperty("_playerButton").objectReferenceValue as Button;
            if(b)((RectTransform)b.transform).anchoredPosition=new Vector2(530,-68);
        }
        // 只按控件结构绑定交互音，文字文案不参与识别。剧情逐句推进与悬停均不加声音。
        foreach(var button in root.GetComponentsInChildren<Button>(true))
        {
            var emitter=button.GetComponent<UiAudioFeedback>()??button.gameObject.AddComponent<UiAudioFeedback>();
            string n=button.name;
            emitter.cue=new[]{"Back","Return","Close","Cancel","btn_close","btn_close_settings","btn_Close"}.Any(x=>n.IndexOf(x,StringComparison.OrdinalIgnoreCase)>=0)?"back":"click";
            if(n.IndexOf("confirm",StringComparison.OrdinalIgnoreCase)>=0)emitter.cue="confirm";
            // 指路、进化和检定确认的声音随业务结果，不再叠一声通用点击。
            if((button.GetComponentInParent<EvolutionPanel>()||button.GetComponentInParent<DiceCheckPanel>()||button.GetComponentInParent<DescriptionPanel>())&&n.IndexOf("confirm",StringComparison.OrdinalIgnoreCase)>=0)emitter.cue="";
        }
        foreach(var toggle in root.GetComponentsInChildren<Toggle>(true))
        {var emitter=toggle.GetComponent<UiAudioFeedback>()??toggle.gameObject.AddComponent<UiAudioFeedback>();emitter.cue="click";}
        TutorialRevisionEditor.PatchRoot(root);
    }
}
#endif

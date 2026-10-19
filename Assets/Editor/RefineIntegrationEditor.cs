#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>精修配置统一入口；不调用会覆盖旧界面的历史生成器。</summary>
public static class RefineIntegrationEditor
{
    public static void Configure()
    {
        RefinePanelsEditor.Configure();
        RefineNarrativeEditor.Configure();
        RefineBoardEditor.Configure();
        RefineGuideEditor.Configure();
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/PlayerGame.unity");
        if(Camera.main){Camera.main.clearFlags=CameraClearFlags.SolidColor;Camera.main.backgroundColor=Color.black;}
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();Debug.Log("REFINE_ALL_CONFIGURED");
    }
}
#endif

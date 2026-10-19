#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 本轮只调整已有确认框的留白，避免运行时重写编辑器布局。
public static class DecisionLoopIntegration
{
    public static void Configure()
    {
        const string path = "Assets/Resources/Panel/EndingPresentationPanel.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        var frame = root.transform.Find("Confirmation/Frame") as RectTransform;
        frame.sizeDelta = new Vector2(720,475);
        var info = frame.Find("Info") as RectTransform;
        info.sizeDelta = new Vector2(650,315);
        info.anchoredPosition = new Vector2(0,45);
        info.GetComponent<Text>().fontSize=28;
        foreach(var name in new[]{"Confirm","Cancel"})
        {
            var button = frame.Find(name) as RectTransform;
            button.anchoredPosition = new Vector2(button.anchoredPosition.x,-175);
        }
        PrefabUtility.SaveAsPrefabAsset(root,path);
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.SaveAssets();
    }
    public static void BuildWindows()
    {
        Configure();
        var output = System.Environment.GetEnvironmentVariable("MONSTER_DUNGEON_QA_BUILD") ?? "../DecisionLoopPlayer";
        Directory.CreateDirectory(output);
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes=new[]{"Assets/Scenes/StartGame.unity","Assets/Scenes/PlayerGame.unity"},
            locationPathName=Path.Combine(output,"MonsterDungeon.exe"),
            target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development
        });
        if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new System.Exception("验证版构建失败："+report.summary.result);
    }
}
#endif

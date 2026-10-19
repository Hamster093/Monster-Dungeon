#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>本轮只执行说明7配置，避免旧生成器再次覆盖已确认的界面。</summary>
public static class Doc7IntegrationEditor
{
    [MenuItem("Tools/说明7/接入全部本轮内容")]
    public static void Configure()
    {
        Doc7MenuEditor.Configure();
        Doc7ReadingEditor.Configure();
        Doc7GuideEditor.Configure();
        AssetDatabase.SaveAssets();
        Debug.Log("DOC7_ALL_CONFIGURED");
    }
}
#endif

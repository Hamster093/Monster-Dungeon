#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>一次性将从本次对话起新增的界面排版保存进原预制体；不会在运行时重新摆放。</summary>
public static class ConversationUiPolishEditor
{
    [MenuItem("Tools/新增系统UI/应用全对话新增界面打磨")]
    public static void Configure()
    {
        Doc6IntegrationEditor.Configure();
        ConversationChecksUiEditor.Configure();
        // 仅配置用户指定的思维、自身状态、终局、检定；既有界面保持回退原件。
        ConversationPlayerUiEditor.Configure();
        ConversationUtilityUiEditor.Configure();
        ConversationSkinsEditor.Configure();
        AssetDatabase.SaveAssets();Debug.Log("CONVERSATION_UI_POLISH_CONFIGURED");
    }
}
#endif

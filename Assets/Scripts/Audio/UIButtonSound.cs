using UnityEngine;

/// <summary>
/// 挂在需要特殊点击音效的按钮上。如果不挂，则使用全局默认音效。
/// </summary>
public class UIButtonSound : MonoBehaviour
{
    [Tooltip("输入 AudioKeys 里的字符串，比如 SFX_002 (确认) 或 SFX_003 (关闭)")]
    public string customSfxKey;
}
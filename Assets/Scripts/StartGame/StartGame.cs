using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 游戏核心逻辑控制器。
/// </summary>
public class StartGame : MonoBehaviour
{
    public static StartGame Instance;

    [Header("目标游戏场景名称")]
    [SerializeField] private string targetGameSceneName = "";

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    /// <summary>
    /// 跳转到目标游戏场景（在 Inspector 赋值）
    /// </summary>
    public void StartNewGame()
    {
        if (!string.IsNullOrEmpty(targetGameSceneName))
            SceneManager.LoadScene(targetGameSceneName);
        else
            Debug.LogWarning("Game: 未设置目标游戏场景名称！");
    }

    /// <summary>
    /// 继续游戏（跳转至相同的游戏场景）
    /// </summary>
    public void ContinueGame()
    {
        StartNewGame();
    }

    /// <summary>
    /// 保存游戏 (单例接口)
    /// </summary>
    public void SaveSystem()
    {
        Debug.Log(">>> 游戏进度已保存 <<<");
        // TODO: 在此写入 PlayerPrefs 或二进制存档逻辑
    }

    /// <summary>
    /// 读取存档 (单例接口)
    /// </summary>
    public void LoadSystem(int saveSlotId)
    {
        Debug.Log($">>> 正在读取存档槽位 {saveSlotId} <<<");
        // TODO: 在此读取存档并跳转场景
    }
}
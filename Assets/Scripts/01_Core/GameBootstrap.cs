/****************************************************
    文件：GameBootstrap.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/9/25 17:39:31
	功能：游戏入口。负责加载配置、初始化所有系统、管理生命周期。
*****************************************************/
using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    [Header("调试")]
    [Tooltip("勾选后启动时删除本局存档，强制新局")]
    public bool newGameOnStart = false;

    private bool _inited;

    private void Awake()
    {
        // 防止重复挂载
        var existing = FindObjectsOfType<GameBootstrap>();
        if (existing.Length > 1)
        {
            Debug.LogWarning("[GameBootstrap] 场景中存在多个入口，销毁多余的。");
            Destroy(gameObject);
            return;
        }

        InitGame();
    }
    private System.Collections.IEnumerator Start()
    {
        // 等原 UI/棋盘的 Start 完成，再恢复棋盘或显示开场，避免初始化覆盖读档结果。
        yield return null;
        // 3. 27 天剧情启动（story27 为必需资源，Init 缺失时会直接抛错）
        SystemManager.Instance.Narrative.Boot();
    }

    private void OnDestroy()
    {
        if (_inited) SystemManager.Instance.Dispose();
    }

    private void OnApplicationQuit()
    {
        if (_inited)
        {
            SystemManager.Instance.RunStateManager.Save();
            SystemManager.Instance.Dispose();
        }
    }

    /// <summary>初始化游戏</summary>
    private void InitGame()
    {
        // 允许直接从游戏场景调试；正常首页进入时复用已有音频管理器。
        if (AudioManager.Instance == null) new GameObject("AudioManager").AddComponent<AudioManager>();
        Debug.Log("[GameBootstrap] 开始初始化");

        // 1. 可选：强制新局
        if (newGameOnStart)
        {
            SaveAndLoadManager.Delete("run_state");
            Debug.Log("[GameBootstrap] 已删除存档，本次为新局");
        }

        // 2. 初始化所有系统
        SystemManager.Instance.Init();
        _inited = true;

    }

}

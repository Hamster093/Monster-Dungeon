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

    private void OnDestroy()
    {
        if (_inited) SystemManager.Instance.Dispose();
    }

    private void OnApplicationQuit()
    {
        if (_inited) SystemManager.Instance.Dispose();
    }

    /// <summary>初始化游戏</summary>
    private void InitGame()
    {
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

        Debug.Log($"[GameBootstrap] 初始化完成：第 {SystemManager.Instance.RunState.run.currentDay} 天，" +
                  $"阶段 {SystemManager.Instance.RunState.run.currentPhase}，" +
                  $"现金 {SystemManager.Instance.RunState.run.cash}");

        // 3. 生成今日来客（如果开局就在第 1 天，可手动触发一次）
        //    如果你希望开局就刷新第 1 天的来客，取消下面注释
        // NextDayEvent.Trigger();
    }
}

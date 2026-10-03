/****************************************************
    文件：RunStateManager.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/9/24 19:10:50
	功能：运行状态管理器
*****************************************************/

using UnityEngine;

public class RunStateManager
{
    private static RunStateManager _instance;
    /// <summary>
    /// 懒加载单例
    /// </summary>
    public static RunStateManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = new RunStateManager();
            return _instance;
        }
    }
    public const int InitialCash = 2000;
    public const int InitialActionPoint = 5;

    public RunState run;
    /// <summary>
    /// 重置所有运行状态数据，并根据配置表初始化冒险者与怪物信息。
    /// </summary>
    public void NewRun()
    {
        run = new RunState();

        run.currentDay = 0;
        run.currentPhase = 1;
        run.cash = InitialCash;
        run.nutrient = 0;
        run.actionPoint = InitialActionPoint;
        run.remainingDays = 3;
        run.installmentPayment = 325;

        // 假设调查道具 id=1，先给 5 个 TEST
        run.itemInventory[1] = 5;
        Debug.Log("初始化调查道具 道具id=1 数量5");

        foreach (var adv in ConfigDatabase.Instance.Adventurers.Values)
        {
            // 直接调用有参构造函数，自动处理好 configId、Config 和初始属性
            var runtimeData = new AdventurerRuntimeData(adv);
            run.adventurers.Add(adv.id, runtimeData);
        }
        foreach (var mon in ConfigDatabase.Instance.Monsters.Values)
        {
            run.monsters.Add(mon.id, new MonsterState
            {
                id = mon.id,
                alive = true,
                接待过今日 = false,
                今日损耗 = 0
            });
        }

    }
    /// <summary>
    /// 将当前局的运行状态保存到本地
    /// </summary>
    public void Save()
    {
        if (run == null)
        {
            Debug.LogError("当前没有运行状态，无法保存！");
            return;
        }
        SaveAndLoadManager.Save("run_state", run);
        Debug.Log("游戏已保存");
    }
    /// <summary>
    /// 从本地读取运行状态
    /// </summary>
    public void Load()
    {
        RunState loadedState = SaveAndLoadManager.Load<RunState>("run_state");

        // --- 安全检查： SaveAndLoadManager 在没文件时会返回 new RunState()，字典是空的 ---
        if (loadedState == null || loadedState.adventurers == null || loadedState.adventurers.Count == 0)
        {
            Debug.LogWarning("未找到有效存档或存档为空，自动开启新游戏。");
            NewRun();
            return;
        }

        run = loadedState;

        // --- 读档后重新绑定静态配置 ---
        // 序列化时把 AdventurerConfig 标记了 [JsonIgnore] 忽略了，读档后它是 null，必须从全局表重新挂载
        foreach (var kvp in run.adventurers)
        {
            var runtimeData = kvp.Value;
            if (ConfigDatabase.Instance.Adventurers.TryGetValue(runtimeData.configId, out var config))
            {
                runtimeData.Config = config;
            }
            else
            {
                Debug.LogError($"读档时找不到冒险者配置，configId: {runtimeData.configId}");
            }
        }

        Debug.Log("游戏已读取");
    }
}



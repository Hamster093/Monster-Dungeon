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
    /// </summar
    public static RunStateManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = new RunStateManager();
            return _instance;
        }
    }
    public const int InitialCash = 2500;
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

        // 新局提供两枚透镜，足够完整调查一位人物；读档不经过此初始化。
        run.itemInventory[1] = 2;
        // 正式开局补给：一次重投和一次赔礼，旧档不会经过NewRun重新领取。
        run.itemInventory[2] = 1;
        run.itemInventory[5] = 1;

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
                isReception = false,
            });
        }

    }
    /// <summary>
    /// 将当前局的运行状态保存到本地
    /// </summary>
    public void Save()
    {
        SystemManager.Instance.Narrative?.Capture();
        if (run == null)
        {
            Debug.LogError("当前没有运行状态，无法保存！");
            return;
        }
        run.savedUtc = System.DateTime.UtcNow.Ticks;
        SaveAndLoadManager.Save("run_state", run);
        Debug.Log("游戏已保存");
    }
    /// <summary>
    /// 从本地读取运行状态
    /// </summary>
    /// <summary>手动槽与自动续玩共用同一份快照，但互不覆盖。</summary>
    public bool SaveSlot(int id)
    {
        if (run == null) return false;
        try
        {
            SystemManager.Instance.Narrative?.Capture();
            run.savedUtc = System.DateTime.UtcNow.Ticks;
            SaveAndLoadManager.Save(SaveAndLoadManager.SlotKey(id), run);
            return true;
        }
        catch (System.Exception e) { Debug.LogWarning(e.Message); return false; }
    }
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


    //=====外部方法====
    /// <summary>
    /// 尝试花费现金，如果现金不足则返回 false，不会扣除现金。(应该由金币系统调用)
    /// </summary>
    /// <param name="amount"></param>
    /// <returns></returns>
    public bool TrySpendCash(int amount)
    {
        if (run == null || run.cash < amount) {AudioManager.Instance?.PlayCue("denied");return false;}
        if (amount <= 0) return amount == 0;
        run.cash -= amount;
        AudioManager.Instance?.CashChanged(-amount);
        ToastManager.Show("你消耗了" + amount + "金币", System.Guid.NewGuid().ToString());
        TopBarRefreshEvent.Trigger();
        return true;
    }

    /// <summary>
    /// 增加现金，并刷新顶栏 UI。
    /// </summary>
    public void AddCash(int amount)
    {
        if (run == null || amount == 0) return;
        run.cash += amount;
        AudioManager.Instance?.CashChanged(amount);
        ToastManager.Show(amount > 0 ? "你获得了" + amount + "金币" : "你消耗了" + (-amount) + "金币", System.Guid.NewGuid().ToString());
        TopBarRefreshEvent.Trigger();
    }
}



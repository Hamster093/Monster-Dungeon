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

        run.currentDay = 1;
        run.currentPhase = 1;
        run.cash = InitialCash;
        run.nutrient = 0;
        run.actionPoint = InitialActionPoint;

        // 假设调查道具 id=1，先给 5 个 TEST
        run.itemInventory[1] = 5;
        Debug.Log("初始化调查道具 道具id=1 数量5");

        foreach (var adv in ConfigDatabase.Instance.Adventurers.Values)
        {
            run.adventurers.Add(adv.id, new AdventurerState
            {
                id = adv.id,
                health = HealthState.Normal,
                equipment = EquipmentState.Normal,
                relationship = RelationshipState.Neutral,
                revenge = RevengeLevel.None,
                successorStats = new System.Collections.Generic.Dictionary<SuccessorDimension, int>(adv.initialStats)
            });
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
        SaveAndLoadManager.Save("run_state", run);
    }
    /// <summary>
    /// 从本地读取运行状态
    /// </summary>
    public void Load()
    {
        run = SaveAndLoadManager.Load<RunState>("run_state");
    }
}


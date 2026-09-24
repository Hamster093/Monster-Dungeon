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
    public static RunStateManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = new RunStateManager();
            return _instance;
        }
    }
    public RunState run;
    
    public void NewRun()
    {
        run = new RunState();
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

        run.currentDay = 1;
        run.currentPhase = 1;
        run.cash = 0;
        run.nutrient = 0;
        run.actionPoint = 5;
    }
    public void Save()
    {
        SaveAndLoadManager.Save("run_state", run);
    }
    public void Load()
    {
        run = SaveAndLoadManager.Load<RunState>("run_state");
    }
}


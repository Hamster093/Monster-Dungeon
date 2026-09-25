/****************************************************
    文件：ConfigTest.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/9/24 17:06:25
	功能：全流程测试
*****************************************************/
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestBootstrap : MonoBehaviour
{
    private bool _inited;

    void Start()
    {
        StartCoroutine(RunAllTests());
    }

    void OnDestroy()
    {
        if (_inited) SystemManager.Instance.Dispose();
    }

    void OnApplicationQuit()
    {
        if (_inited) SystemManager.Instance.Dispose();
    }

    // ============================================================
    //  主流程
    // ============================================================
    IEnumerator RunAllTests()
    {
        SaveAndLoadManager.Delete("run_state");
       
        Debug.Log("================ 测试开始 ================");

        // 0. 初始化（配置 + 运行时状态 + 所有系统）
        SystemManager.Instance.Init();
        SystemManager.Instance.Economy.AddCash(2000);
        Debug.Log("获得金钱 现有金钱为"+ RunStateManager.Instance.run.cash);
        _inited = true;
        Debug.Log("SystemManager 初始化完成");

        // 1. 配置
        Debug.Log("---------- 1. 配置表 ----------");
        TestConfig();

        // 2. 运行时状态
        Debug.Log("---------- 2. 运行时状态 ----------");
        TestRunState();

        // 3. 事件注册
        Debug.Log("---------- 3. 事件注册 ----------");
        TestEvents();
        yield return null;

        // 4. 行动点
        Debug.Log("---------- 4. 行动点 ----------");
        TestActionPoint();
        yield return null;

        // 5. 模拟一天
        Debug.Log("---------- 5. 推进一天 ----------");
        TestOneDay();
        yield return null;

        // 6. 接待冒险者（真实路线）
        Debug.Log("---------- 6. 接待冒险者（真实路线） ----------");
        TestAdventure(isTruth: true);
        yield return null;

        // 7. 接待冒险者（编造路线）
        Debug.Log("---------- 7. 接待冒险者（编造路线） ----------");
        TestAdventure(isTruth: false);
        yield return null;

        // 8. 阶段结算
        Debug.Log("---------- 8. 阶段结算 ----------");
        TestPhaseEnd();
        yield return null;

        // 9. 存档读档
        Debug.Log("---------- 9. 存档 / 读档 ----------");
        TestSaveLoad();

        Debug.Log("================ 测试结束 ================");
    }

    // ============================================================
    //  1. 配置表
    // ============================================================
    void TestConfig()
    {
        var db = ConfigDatabase.Instance;

        Debug.Log($"[配置] 冒险者:{db.Adventurers.Count} " +
                  $"委托:{db.Contracts.Count} " +
                  $"魔物:{db.Monsters.Count} " +
                  $"分区:{db.Zones.Count} " +
                  $"道具:{db.Items.Count} " +
                  $"对话:{db.Dialogues.Count} " +
                  $"有效信息:{db.ValidInfos.Count} " +
                  $"调查:{db.Investigations.Count} " +
                  $"检定:{db.D20Checks.Count} " +
                  $"日程:{db.Schedules.Count} " +
                  $"记忆:{db.Memories.Count}");

        if (db.Adventurers.TryGetValue(1, out var adv))
        {
            Debug.Log($"[配置] 冒险者 1: name={adv.name}, level={adv.baseLevel}, isParty={adv.isParty}");
            Debug.Log($"[配置] 来访日: {string.Join(",", adv.visitDays)}");
            Debug.Log($"[配置] 背景数: {adv.backgrounds.Count}");
            Debug.Log($"[配置] 主要维度: {adv.mainDimension}, 次要维度: {adv.secondaryDimension}");

            if (adv.initialStats != null)
            {
                foreach (var kv in adv.initialStats)
                    Debug.Log($"  接任数值 {kv.Key} = {kv.Value}");
            }
            else
            {
                Debug.LogError("[配置] initialStats 为 null，Dictionary 反序列化失败");
            }
        }
        else
        {
            Debug.LogError("[配置] 没读到 id=1 的冒险者");
        }
    }

    // ============================================================
    //  2. 运行时状态
    // ============================================================
    void TestRunState()
    {
        var run = SystemManager.Instance.RunState.run;

        Debug.Log($"[状态] 第 {run.currentDay} 天, 阶段 {run.currentPhase}, " +
                  $"现金 {run.cash}, 养分 {run.nutrient}, 行动点 {run.actionPoint}");

        Debug.Log($"[状态] 冒险者状态数: {run.adventurers.Count}");
        foreach (var kv in run.adventurers)
        {
            var s = kv.Value;
            Debug.Log($"  冒险者 {kv.Key}: 健康={s.health}, 装备={s.equipment}, " +
                      $"关系={s.relationship}, 报复={s.revenge}, " +
                      $"接任数值数={s.successorStats.Count}");
        }

        Debug.Log($"[状态] 魔物状态数: {run.monsters.Count}");
        foreach (var kv in run.monsters)
        {
            var m = kv.Value;
            Debug.Log($"  魔物 {kv.Key}: alive={m.alive}, 接待过今日={m.接待过今日}");
        }
    }

    // ============================================================
    //  3. 事件注册
    // ============================================================
    void TestEvents()
    {
        NextDayEvent.Register(OnNextDayTest);
        TodayVisitorsReadyEvent.Register(OnVisitorsReady);
        PhaseEndedEvent.Register(OnPhaseEnded);
        GamePassEvent.Register(OnGamePass);

        Debug.Log("[事件] 监听已注册");
    }

    void OnNextDayTest() { Debug.Log("[事件] NextDayEvent 触发"); }
    void OnVisitorsReady(List<int> ids) { Debug.Log($"[事件] 今日来客: {string.Join(",", ids)}"); }
    void OnPhaseEnded(int phase) { Debug.Log($"[事件] 阶段结束: {phase}"); }
    void OnGamePass() { Debug.Log("[事件] GamePassEvent 触发（游戏结束）"); }

    // ============================================================
    //  4. 行动点
    // ============================================================
    void TestActionPoint()
    {
        var ap = SystemManager.Instance.ActionPoint;
        Debug.Log($"[行动点] 初始: {ap.Current}");

        ap.Spend(1);
        Debug.Log($"[行动点] 消耗 1 后: {ap.Current}");

        ap.Spend(2);
        Debug.Log($"[行动点] 再消耗 2 后: {ap.Current}");

        ap.ResetDaily();
        Debug.Log($"[行动点] 重置后: {ap.Current}");
    }

    // ============================================================
    //  5. 推进一天
    // ============================================================
    void TestOneDay()
    {
        var run = SystemManager.Instance.RunState.run;

        // 测试用：把天数调到 2，触发后到第 3 天，正好冒险者 1 来访
        run.currentDay = 2;
        Debug.Log($"[日程] 手动设为第 {run.currentDay} 天");

        NextDayEvent.Trigger();

        Debug.Log($"[日程] 现在第 {run.currentDay} 天");
        Debug.Log($"[日程] 行动点: {run.actionPoint}");
        Debug.Log($"[日程] 今日来客: {string.Join(",", run.todayVisitors)}");
    }

    // ============================================================
    //  6/7. 接待冒险者
    // ============================================================
    void TestAdventure(bool isTruth)
    {
        var sm = SystemManager.Instance;
        int advId = 1;

        // 让冒险者受损，便于观察带伤成功/撤退
        sm.RunState.run.adventurers[advId].health = HealthState.Damaged;

        int cashBefore = sm.Economy.Cash;
        int nutrientBefore = sm.Economy.Nutrient;

        Debug.Log($"[冒险] 结算前: 现金={cashBefore}, 养分={nutrientBefore}");

        var result = sm.Reception.GiveRoute(advId, isTruth);
        Debug.Log(result.Message);
        bool ok = result.Success;

        Debug.Log($"[冒险] 结算后: 现金={sm.Economy.Cash}, 养分={sm.Economy.Nutrient}");

        var adv = sm.RunState.run.adventurers[advId];
        Debug.Log($"[冒险] 记忆数: {adv.memoryTimeline.Count}, 报复等级: {adv.revenge}");
        foreach (var m in adv.memoryTimeline)
            Debug.Log($"  记忆: {m}");
    }

    // ============================================================
    //  8. 阶段结算
    // ============================================================
    void TestPhaseEnd()
    {
        var sm = SystemManager.Instance;

        // 给点钱避免逾期
        sm.Economy.AddCash(500);
        Debug.Log($"[阶段] 结算前现金: {sm.Economy.Cash}");

        PhaseEndedEvent.Trigger(sm.Schedule.CurrentPhase);

        Debug.Log($"[阶段] 结算后现金: {sm.Economy.Cash}, 当前阶段: {sm.Schedule.CurrentPhase}");
    }

    // ============================================================
    //  9. 存档 / 读档
    // ============================================================
    void TestSaveLoad()
    {
        var sm = SystemManager.Instance;

        sm.RunState.run.cash = 999;
        sm.RunState.run.currentDay = 5;
        sm.RunState.Save();
        Debug.Log("[存档] 已保存");

        // 故意改乱内存值
        sm.RunState.run.cash = 0;
        sm.RunState.run.currentDay = 1;

        sm.RunState.Load();
        Debug.Log($"[存档] 读档后: 现金={sm.RunState.run.cash}, 天数={sm.RunState.run.currentDay}");
    }


}

#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>只导出独立测试数据，不写本地存档槽，不修改正式新局或骰子规则。</summary>
public static class TutorialTestSaves
{
    public static string Out=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../outputs/教程回填与测试存档"));
    static readonly string[] Actors={"MY","FE","DW","SA","AU","DA","UN"};
    public static void Export()
    {
        ConfigDatabase.Load();Directory.CreateDirectory(Path.Combine(Out,"测试存档"));
        Write(true);Write(false);Debug.Log("TWO_TEST_SAVES_EXPORTED");
    }
    static void Write(bool ending)
    {
        RunStateManager.Instance.NewRun();var run=RunStateManager.Instance.run;
        var rules=JsonConvert.DeserializeObject<NarrativeSystem.Rules>(Resources.Load<TextAsset>("Narrative/rules").text);
        var story=new NarrativeRuntime(Resources.Load<TextAsset>("Narrative/story27").text);
        run.currentDay=ending?27:2;run.currentPhase=ending?5:1;run.saveVersion=9;run.savedUtc=DateTime.UtcNow.Ticks;
        run.tradeDay=run.currentDay;run.tradePhase=run.currentPhase;run.cash=2000;run.actionPoint=5;run.correctAnswerCount=0;
        run.player=new PlayerBuildState();run.player.Create(0,1);run.player.thoughtConfirmedPhase=run.currentPhase;
        run.pendingDice=null;run.pendingEvolutionUid=null;run.pendingEvolutionKind="";run.itemInventory.Clear();
        run.todayVisitors=ending?new List<int>():new List<int>{3,2};run.resolvedVisitors.Clear();
        var save=new NarrativeSystem.SaveData{uiVersion=8,stage="playing",audioContext=ending?"uneasy":"reception",storedInstances=new List<MonsterInstance>()};
        save.paidPhases=new HashSet<int>(Enumerable.Range(1,ending?5:0));save.settledDays=new HashSet<int>(Enumerable.Range(1,ending?27:1));
        foreach(var pair in rules.initialStats)
        {
            int id=Array.IndexOf(Actors,pair.Key)+1;var adv=run.adventurers[id];
            for(int i=0;i<7;i++)adv.currentSevenStats[(SevenDimension)i]=pair.Value[i];
            adv.isDead=false;adv.isSeverelyInjured=false;adv.severeInjuryDelay=0;adv.health=HealthState.Normal;
            if(ending)foreach(var condition in rules.successionRules[pair.Key])adv.currentSevenStats[condition.stat]=condition.value;
        }
        story.Set("day",run.currentDay);story.Set("ap",5);
        if(ending)story.Story.ChoosePathString("END_SELECTION");
        else
        {
            story.SetMember("pending_revenge","DW",true);story.SetMember("pending_revenge","FE",true);
            story.SetMember("revenge_severe","DW",false);story.SetMember("revenge_severe","FE",true);
            story.SetMember("injuries","FE",true);
            run.adventurers[3].health=HealthState.Damaged;
            run.adventurers[2].health=HealthState.Critical;run.adventurers[2].isSeverelyInjured=true;run.adventurers[2].severeInjuryDelay=2;
            save.revengeDue[3]=2;save.revengeDue[2]=2;
            foreach(var pair in new[]{(5,3),(6,3),(2,2)})save.inventory.Add(new NarrativeSystem.InventorySlot{id=pair.Item1,count=pair.Item2});
            story.Story.ChoosePathString("VIS_D02_V01");
        }
        save.ink=story.Story.state.ToJson();run.narrativeSave=JsonConvert.SerializeObject(save);
        File.WriteAllText(Path.Combine(Out,"测试存档",ending?"终局测试.json":"报复测试.json"),JsonConvert.SerializeObject(run,Formatting.Indented));
    }
}
#endif

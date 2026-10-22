/****************************************************
    文件：NarrativeSystem.Save.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/22
	功能：27天剧情接线层·存档与迁移：世界快照/恢复、历次uiVersion版本迁移
/*****************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using Ink.Runtime;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 27天剧情的游戏接线层。生命周期由 SystemManager 管理；对话、商店、委托、棋盘收入和结算
/// 均复用原系统，不另建 UI。数值留在 Resources/Narrative/rules.json，原稿 action 标签统一接线。
/// 本类以 partial 拆分为多个文件，同功能代码用 #region 分组。
/// </summary>
public sealed partial class NarrativeSystem
{
    #region 存档快照与写入


    public void Capture()
    {
        if (!Enabled) return;
        BackpackBoard.Instance?.CancelPlacement(); // 存档不保存悬空预摆，先安全恢复已固定布局。
        var archive = UIManager.Instance?.Get<CharacterArchivePanel>();
        _save.topLeftPanel = UIManager.Instance?.CurrentLeftPanelName;
        _save.archiveOpen = archive != null && archive.IsOpen;
        if (archive != null) { _save.archiveActor = archive.CurrentActor; _save.archiveTab = archive.CurrentTab; }
        _save.specialDetailId = 0;
        _save.history = DialogueHistoryModel.Instance?.Lines.ToList() ?? new List<DialogueLine>();
        _save.ink = _runtime.Story.state.ToJson();
        _save.currentVisitor = _systems.VisitSystem.CurrentVisitorId;
        _save.speaker = _runtime.Speaker;
        _save.normal = _systems.DailyCommission.NormalCommissions.ToList();
        _save.special.Clear(); // 旧字段只用于迁移，不再写入任务。
        _save.inventory.Clear();
        var inventory = InventoryModel.Instance;
        if (inventory != null)
            for (int i = 0; i < inventory.Capacity; i++)
            { var slot = inventory.GetSlot(i); if (!slot.IsEmpty) _save.inventory.Add(new InventorySlot { id = slot.data.id, count = slot.quantity }); }
        _save.board.Clear();
        if (BackpackBoard.Instance != null)
            foreach (var view in BackpackBoard.Instance.Items)
                if (view != null && view.Item != null) _save.board.Add(new BoardSlot
                { shape = view.Def.Id, uid = view.Item.Uid, evolved = view.Item.Evolved, usedDay=view.Item.UsedDay, evolutionKind=view.Item.EvolutionKind, x = view.Item.Origin.x, y = view.Item.Origin.y, rotation = view.Item.Rotation });
        _save.storedMonsters = MonsterStorageModel.Instance.Items.Select(m => m.monsterId).ToList();
        _save.storedInstances = MonsterStorageModel.Instance.Items.Select(m => new MonsterInstance(m.monsterId) { uid = m.uid, evolved = m.evolved, usedDay=m.usedDay, evolutionKind=m.evolutionKind }).ToList();
        // 地狱框中的魔物尚未确认分解，不能因本体暂时隐藏而漏存。
        // 读档时取消这次未确认操作，将同一实例退回仓库，不发放分解奖励。
        var pending = UIManager.Instance?.Get<DecomposePanel>()?.PendingItem;
        if (pending != null && int.TryParse(pending.Def.Id, out int pendingId) &&
            !_save.storedInstances.Any(m => m.uid == pending.Uid) && !_save.board.Any(b => b.uid == pending.Uid))
            _save.storedInstances.Add(new MonsterInstance(pendingId) { uid = pending.Uid, evolved = pending.Evolved, usedDay=pending.UsedDay, evolutionKind=pending.EvolutionKind });
        Run.narrativeSave = JsonConvert.SerializeObject(_save);
    }
    public void Save() { _systems.RunStateManager.Save(); }

    #endregion

    #region 世界恢复


    private void RestoreWorld()
    {
        var inventory = InventoryModel.Instance;
        if (inventory != null)
        {
            for (int i = 0; i < inventory.Capacity; i++) inventory.GetSlot(i).Clear();
            foreach (var slot in _save.inventory)
                if (_systems.Config.Items.TryGetValue(slot.id, out var cfg))
                    inventory.AddItem(new ItemStack { data = ItemData.FromConfig(cfg), quantity = slot.count });
        }
        var board = BackpackBoard.Instance;
        if (board != null)
        {
            foreach (var stage in UnityEngine.Object.FindObjectsOfType<StageController>()) stage.ApplyStage(Run.currentPhase);
            foreach (var view in board.Items.ToArray())
                if (view != null) { if (view.Item != null) board.RemoveItem(view.Item); UnityEngine.Object.Destroy(view.gameObject); }
            foreach (var slot in _save.board)
            {
                var def = _systems.Config.ShapeDb.Get(slot.shape);
                if (def != null && board.CanPlace(def, new Vector2Int(slot.x, slot.y), slot.rotation))
                {
                    var go = new GameObject("RestoredMonster", typeof(RectTransform));
                    go.transform.SetParent(board.ItemsRoot, false);
                    var restored = go.AddComponent<ItemView>();
                    restored.Init(def, board, slot.rotation, new Vector2Int(slot.x, slot.y));
                    restored.SetInstanceState(slot.uid, slot.evolved,slot.usedDay,slot.evolutionKind);
                }
            }
        }
        MonsterStorageModel.Instance.Clear();
        if (_save.storedInstances != null)
            foreach (var inst in _save.storedInstances) MonsterStorageModel.Instance.Add(inst.monsterId, inst.uid, inst.evolved,inst.usedDay,inst.evolutionKind);
        else foreach (int id in _save.storedMonsters) MonsterStorageModel.Instance.Add(id);
        BackpackChanged.Trigger();
        if (_save.currentVisitor > 0) _systems.VisitSystem.SelectNarrativeVisitor(_save.currentVisitor);
    }

    #endregion

    #region 版本迁移


    private void UpgradeArchiveAndUI()
    {
        // 上一版存档只保存了调查提示；依据已经揭示的 Ink 状态补回原稿，绝不提前解锁。
        foreach (var pair in ActorIds)
        {
            var adv = Run.adventurers[pair.Value];
            bool Has(string list) => ((InkList)_runtime.Get(list)).Keys.Any(k => k.itemName == "B_" + pair.Key + "3");
            string level = Has("bg_full") ? "full" : Has("bg_partial") ? "partial" : null;
            if (level != null && _runtime.BackgroundTexts.TryGetValue(pair.Key + ":" + level, out var text))
            { adv.investigationArchive.Clear(); adv.investigationArchive.Add(text); _save.backgroundLevels[pair.Key] = level; }
        }
        if (_save.departingVisitor > 0) _save.currentVisitor = ActorIds[Actor];
        if (_save.frame != null)
        {
            _save.displayed.Add(_save.displaySerial + ":line:0");
            if (_runtime.Story.currentChoices.Count > 0) _save.frame.nodes[0].options = _runtime.Read().nodes[0].options;
        }
        string path = _runtime.Story.state.currentPathString ?? "";
        if (path.IndexOf("_commission.", StringComparison.OrdinalIgnoreCase) >= 0 && !path.Contains(".c-"))
        { _save.stage = "commission"; _save.frame = null; }
        _save.uiVersion = 2;
    }

    /// <summary>撤除特殊委托的版本迁移：不回滚已发放金币/七维；未支付任务不再结算。</summary>
    private void MigrateRetiredSpecials()
    {
        if (_save.uiVersion >= 5) return;
        _save.special = _save.special ?? new List<CommissionEntry>();
        bool retiredWait = _save.panel == "special" || _save.pendingRoute?.isSpecial == true || Run.pendingDice?.context == "special";
        bool retiredSettlementDice = Run.pendingDice?.context == "settlement" && _save.special.Any(e => Run.pendingDice.key.Contains(":" + e.adventurerId + ":" + e.contractId + ":"));
        if (retiredWait)
        {
            _save.panel = null; _save.pendingRoute = null; _save.stage = "playing";
            if (Run.pendingDice?.context == "special") Run.pendingDice = null;
        }
        if (retiredSettlementDice) { Run.pendingDice = null; _save.stage = "settlement_checks"; }
        if (_save.settlement != null && !_save.settledDays.Contains(Run.currentDay))
        {
            _save.settlement.totalIncome -= _save.settlement.specialCommissionIncome;
            _save.settlement.specialCommissionIncome = 0;
            _save.settlement.notes?.RemoveAll(n => n.StartsWith("特殊委托"));
        }
        foreach (var e in _save.special.Where(e => !e.statsApplied && !string.IsNullOrEmpty(e.targetUid) && !_save.normal.Any(n => n.targetUid == e.targetUid)))
        {
            foreach (var slot in _save.board.Where(b => b.uid == e.targetUid && b.usedDay == Run.currentDay)) slot.usedDay = -1;
            if (_save.storedInstances != null) foreach (var instance in _save.storedInstances.Where(m => m.uid == e.targetUid && m.usedDay == Run.currentDay)) instance.usedDay = -1;
        }
        _save.special.Clear(); _save.specialDetailId = 0;
        if (_save.topLeftPanel == nameof(SpecialCommissionPanel)) _save.topLeftPanel = null;
        if (_save.frame != null) foreach (var node in _save.frame.nodes) node.options.RemoveAll(o => o.callbackKey == "open_special_commission");
        _save.uiVersion = 5;
    }

    private void MigrateGlobalPlayerRules()
    {
        if(_save.uiVersion>=6)return;
        if(!_save.settledDays.Contains(Run.currentDay))
        {
            foreach(var entry in _save.normal)
            {
                var cfg=_systems.Config.Contracts[entry.contractId];
                int target=entry.targetMonsterId>0?entry.targetMonsterId:cfg.targetMonsterId;
                entry.targetMonsterId=target; entry.requestedMonsterType=DailyCommissionSystem.TargetType(cfg);
                entry.wrongRoute=_systems.Config.Monsters[target].monsterType!=entry.requestedMonsterType;
                entry.rankFactor=DailyCommissionSystem.RankFactor(_systems.Config.Adventurers[entry.adventurerId].baseLevel,_systems.Config.Monsters[target].level);
                entry.completionChecked=true; entry.completionRate=DailyCommissionSystem.RewardRate(entry,cfg);
                if(entry.checksResolved)
                {
                    var lines=(entry.outcomeText??"").Split('\n').Where(s=>!s.StartsWith("报酬：")&&!s.StartsWith("等级系数："));
                    entry.outcomeText=string.Join("\n",lines)+"\n等级系数："+Mathf.RoundToInt(entry.completionRate*100)+"% · 报酬："+Mathf.RoundToInt(cfg.reward*entry.completionRate)+"金币";
                }
            }
            if(_save.settlement!=null)
            {
                int amount=_systems.DailyCommission.ComputeAll().normalIncome;
                _save.settlement.totalIncome+=amount-_save.settlement.legacyCommissionIncome;
                _save.settlement.legacyCommissionIncome=amount;
                _save.settlement.notes=_systems.DailyCommission.ResultNotes();
            }
            if(Run.pendingDice?.context=="settlement"&&Run.pendingDice.key.EndsWith(":income",StringComparison.Ordinal))
            { Run.pendingDice=null; _save.stage="settlement_checks"; }
        }
        if(_save.frame!=null)foreach(var node in _save.frame.nodes)foreach(var line in node.lines)line.content=ExpandText(line.content);
        _save.uiVersion=6;
    }

    private void MigrateTalkAndRevenge()
    {
        string path=_runtime.Story.state.currentPathString??"";
        if(_save.uiVersion<9)
        {
            if(path.StartsWith("REVENGE",StringComparison.Ordinal)&&TryActor(Actor,out var current))_save.activeRevengeActor=current.configId;
            // 礼盒在进入旧检定前已经扣除；迁移只完成礼盒结果，不能再次扣物或赔款。
            if(Run.pendingDice?.playerKind=="revenge_gift"||Run.pendingDice?.checkId=="REVENGE_GIFT.14")
            {Run.pendingDice=null;Run.player.pendingThoughtCheck=null;_save.checkId=null;_save.frame=null;_save.stage="playing";_runtime.Story.ChoosePathString("REVENGE_GIFT_RESOLVED");}
            _save.uiVersion=9;
        }
        foreach(var pair in ActorIds)
            if(_runtime.HasMember("pending_revenge",pair.Key)&&!_save.revengeDue.ContainsKey(pair.Value))
                _save.revengeDue[pair.Value]=_rules.scenes.Where(x=>x.actor==pair.Key&&x.day>=Run.currentDay).Select(x=>x.day).DefaultIfEmpty(Run.currentDay).Min();
    }

    private void MigrateDoc6PlayerRules()
    {
        if (_save.uiVersion >= 8) return;
        if (Run.player == null) Run.player = new PlayerBuildState();
        var player=Run.player;
        // 旧0/1/2依次映射新三思维，类型与三属性保持原值；安全整备时再确认本阶段。
        if (player.thought < -1 || player.thought > 2 || Run.currentDay < 2) player.thought=-1;
        player.thoughtConfirmedPhase=0;
        player.ClearSwitchOpportunity();
        player.pendingThoughtCheck=null;
        player.pendingChoiceCredit=false;
        player.failureStreak=0;
        // 已建骰子及它此前绑定的属性结果不重算，旧熟练度只保留为兼容数据。
        if (string.IsNullOrEmpty(Run.pendingEvolutionUid)) Run.pendingEvolutionKind="";
        _save.uiVersion=8;
        Run.saveVersion=9;
    }
    private void MigrateEndingPresentation()
    {
        if(_save.uiVersion>=7)return;
        // 已完成的旧接任档只显示完成文字，不重新说服、播放剧情或消耗物品。
        if(_save.stage=="ended"&&_save.ending.StartsWith("succession:",StringComparison.Ordinal))_save.stage="ending_complete";
        string path=_runtime.Story.state.currentPathString??"";
        if(new[]{"HIDDEN_BATTLE","HIDDEN_TRANSFER","HIDDEN_SAMPLE","HIDDEN_ABANDON"}.Any(k=>path.StartsWith(k+".",StringComparison.Ordinal))) {
            _runtime.Story.ChoosePathString("HIDDEN_CANCEL");_save.stage="playing";_save.frame=null;
        }
        if(_save.frame!=null)foreach(var n in _save.frame.nodes)foreach(var line in n.lines)line.icon=ResolvePortrait(line);
        _save.uiVersion=7;
    }

    #endregion
}

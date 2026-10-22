/****************************************************
    文件：NarrativeSystem.Items.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/22
	功能：27天剧情接线层·道具与透镜：背包功能窗口、灵魂透镜、道具扣减
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
    #region 功能窗口开关


    public DialogueOptionResult OpenFunction(DialogueOption option)
    {
        if (ModalClosedThisFrame) return new DialogueOptionResult { cancelJump = true };
        if (option.callbackKey == "open_special_commission") return new DialogueOptionResult { cancelJump = true };
        if (option.callbackKey == "open_commission_panel")
        {
            // 先走原稿入口，保留委托前的角色事件，到明确的委托正文边界再打开面板。
            Choose(option);
            return new DialogueOptionResult();
        }
        _save.panel = "backpack";
        _save.specialDetailId = 0;
        _save.stage = "function";
        OpenWaitingPanel();
        Save();
        return new DialogueOptionResult { waitForExternal = true };
    }

    private void OpenWaitingPanel()
    {
        // 关闭回调由原 UIManager 在恢复此前左侧窗口后调用，不触发访客或日期推进。
        if (_save.panel == "backpack")
            UIManager.Instance.Open<BackpackPanel>(new Action(ReturnFromFunction));
    }

    public void ReturnFromFunction()
    {
        if (_save.panel == null) return;
        _save.stage = "playing"; _save.panel = null;
        Save();
        UIManager.Instance.Get<TalkPanel>()?.Dialogue.ResumeCurrentNode();
    }

    #endregion

    #region 灵魂透镜


    /// <summary>由原背包的“使用”按钮调用，扣除和效果仍只执行原稿中的 item 动作一次。</summary>
    public bool UseBagItem(int id)
    {
        if (_save.stage != "function" || _save.panel != "backpack") return false;
        if (id != 1 && id != 4) { ToastManager.Show("该道具尚无可用的接待操作。"); return false; }
        if (ItemCount(id) < 1) return false;
        int phase = Mathf.Clamp(Run.currentPhase, 1, 5);
        if (id == 1 && !CanUseLens()) return false;
        _runtime.Set("ap", Run.actionPoint);
        if (id == 1)
        {
            _runtime.Story.ChoosePathString("LENS_" + Actor);
            _save.returningFromLens = true;
        }
        else
        {
            _runtime.Story.ChoosePathString("ITEMS");
            _save.stage = "playing";
            var items = _runtime.Read();
            _runtime.Choose(items.nodes[0].options.First(o => o.text.Contains("醒神药剂")).param);
        }
        _save.frame = null;
        return true;
    }

    public int LensDailyUses => _save.lensDay == Run.currentDay ? _save.lensDailyUses : 0;
    public bool CanUseLens()
    {
        if (_save.backgroundLevels.TryGetValue(Actor, out var level) && level == "full")
        { ToastManager.Show("这名冒险者的灵魂宫殿貌似已经干干净净了", Guid.NewGuid().ToString()); return false; }
        if (LensDailyUses >= _rules.lensUseLimit[Mathf.Clamp(Run.currentPhase, 1, 5) - 1])
        { ToastManager.Show("灵魂透镜使用达到当前经营日上限", Guid.NewGuid().ToString()); return false; }
        return true;
    }

    #endregion

    #region 道具通用

    private static int RevengeOptionItem(string id)
    {
        switch(id){case "REVENGE_OPTIONS.0.11":return 5;case "REVENGE_OPTIONS.0.17":case "REVENGE_TRAP_CONFIRM.0.9":return 6;default:return 0;}
    }
    public bool IsOptionAvailable(DialogueOption option)
    {int item=RevengeOptionItem(option.choiceId);return item==0||ItemCount(item)>0;}

    private static int RequiredItem(string text)
    {
        if (text.Contains("调查") && text.Contains("往事")) return 1;
        if (text.Contains("醒神药剂")) return 4;
        if (text.Contains("使用致歉礼盒")) return 5;
        if (text.Contains("碎骨") && !text.Contains("不用") && !text.Contains("不使用")) return 6;
        return 0;
    }
    private static int ItemId(string kind)
    {
        switch (kind) { case "lens": return 1; case "stimulant": return 4; case "apology_gift": return 5; case "bone_trap": return 6; default: throw new ArgumentException(kind); }
    }
    private int ItemCount(int id)
    {
        int count = Run.itemInventory.TryGetValue(id, out int old) ? old : 0;
        var inventory = InventoryModel.Instance;
        if (inventory != null)
            for (int i = 0; i < inventory.Capacity; i++)
            { var slot = inventory.GetSlot(i); if (!slot.IsEmpty && slot.data.id == id) count += slot.quantity; }
        return count;
    }
    private bool ConsumeItem(int id)
    {
        var inventory = InventoryModel.Instance;
        if (inventory != null)
            for (int i = 0; i < inventory.Capacity; i++)
            { var slot = inventory.GetSlot(i); if (!slot.IsEmpty && slot.data.id == id) return inventory.RemoveItem(i); }
        if (Run.itemInventory.TryGetValue(id, out int count) && count > 0) { Run.itemInventory[id]--; return true; }
        return false;
    }

    #endregion
}

/****************************************************
    文件：DialogueCallbackBootstrap.cs
    作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/2 19:00:30
    功能：对话回调注册器（挂在场景中，Awake 时注册回调，OnDestroy 时注销回调）
*****************************************************/

using System.Collections.Generic;
using UnityEngine;

public class DialogueCallbackBootstrap : MonoBehaviour
{
    // 功能名 -> 处理函数
    private static readonly Dictionary<string, System.Func<DialogueOption, DialogueOptionResult>> Handlers = new()
    {
        { "add_correct_count", OnAddCorrectCount },
        { "change_attribute",  OnChangeAttribute  },
        { "roll_d20_check",    OnRollD20Check     },
        { "register_info",     OnRegisterInfo     },
        { "enter_talk",        OnEnterTalk        },
        { "accept_quest",      OnAcceptQuest      },
        { "refuse_guest",      OnRefuseGuest      },
        { "guide_check",       OnGuideCheck       },
        { "open_ui",           OnOpenUI },
        { "open_commission_panel", OnOpenCommissionPanel },
    };

    // 每个 callbackKey 属于哪个功能
    private static readonly Dictionary<string, string> KeyToFeature = new()
    {
        { "myd1_intro_mask1",           "add_correct_count" },
        { "myd1_intro_archive_correct", "add_correct_count" },
        { "myd1_talk3_answer3_check",   "add_correct_count" },
        { "myd1_talk4_answer3_check",   "add_correct_count" },

        { "myd1_final_firmness_up",     "change_attribute" },
        { "myd1_final_tolerance_up",    "change_attribute" },

        { "myd1_final_check",           "roll_d20_check" },
        { "myd1_talk1_check",           "roll_d20_check" },
        { "myd1_talk2_check",           "roll_d20_check" },
        { "myd1_talk3_answer1_check",   "roll_d20_check" },
        { "myd1_talk3_answer2_check",   "roll_d20_check" },
        { "myd1_talk4_answer1_check",   "roll_d20_check" },
        { "myd1_talk4_answer2_check",   "roll_d20_check" },

        { "myd1_talk3_begin",           "enter_talk" },
        { "myd1_talk4_begin",           "enter_talk" },

        { "myd1_accept_quest",          "accept_quest" },
        { "myd1_refuse",                "refuse_guest" },
        { "myd1_guide_check",           "guide_check"  },

        { "open_backpack",              "open_ui" },

        { "open_commission_panel", "open_commission_panel" },
    };

    void Awake()
    {
        foreach (var kv in KeyToFeature)
        {
            string key = kv.Key;                    // JSON 里的原名，比如 myd1_refuse
            string feature = kv.Value;              // 功能名，比如 refuse_guest
            if (Handlers.TryGetValue(feature, out var fn))
                DialogueCallbackRegistry.Register(key, fn);
        }
    }

    void OnDestroy()
    {
        foreach (var kv in KeyToFeature)
            DialogueCallbackRegistry.Unregister(kv.Key);
    }

    // ─────────────── 累计正确回答次数 ───────────────
    // 命中 key：myd1_intro_mask1 / myd1_intro_archive_correct
    //           myd1_talk3_answer3_check / myd1_talk4_answer3_check
    private static DialogueOptionResult OnAddCorrectCount(DialogueOption option)
    {
        switch (option.callbackKey)
        {
            case "myd1_intro_mask1":          
            case "myd1_intro_archive_correct":
            case "myd1_talk3_answer3_check":
            case "myd1_talk4_answer3_check": 
                RunStateManager.Instance.run.AddCorrectAnswer(); break;
                
        }
        return new DialogueOptionResult();
    }

    // ─────────────── 改变属性（上升/下降，不掷骰） ───────────────
    // 命中 key：myd1_final_firmness_up / myd1_final_tolerance_up 
    private static DialogueOptionResult OnChangeAttribute(DialogueOption option)
    {
        switch (option.callbackKey)
        {
            case "myd1_final_firmness_up":   RunStateManager.Instance.run.AddAdventurerStat(SevenDimension.Resolve, 3); break;
            case "myd1_final_tolerance_up":  RunStateManager.Instance.run.ReduceAdventurerStat(SevenDimension.Tolerance, 3); break;
        }
        return new DialogueOptionResult();
    }
   


// ─────────────── 执行 D20，并按结果分支 ───────────────
// 命中 key：myd1_final_check
//           myd1_talk1_check / myd1_talk2_check
//           myd1_talk3_answer1_check / myd1_talk3_answer2_check
//           myd1_talk4_answer1_check / myd1_talk4_answer2_check
private static DialogueOptionResult OnRollD20Check(DialogueOption option)
    {
        switch (option.callbackKey)
        {
            case "myd1_talk1_check"://todo 检定
            case "myd1_talk2_check":
                if (!SystemManager.Instance.ActionPoint.Spend(1))
                    return new DialogueOptionResult { cancelJump = true };
                break;

            case "myd1_final_check":
            case "myd1_talk3_answer1_check":
            case "myd1_talk3_answer2_check":
            case "myd1_talk4_answer1_check":
            case "myd1_talk4_answer2_check":
                /* TODO */
                break;
        }
        return new DialogueOptionResult();
    }

    // ─────────────── 登记有效信息 ───────────────
    private static DialogueOptionResult OnRegisterInfo(DialogueOption option)
    {
        // TODO
        return new DialogueOptionResult();
    }

    // ─────────────── 进入搭话、扣行动点、记录话题已选 ───────────────
    // 命中 key：myd1_talk3_begin / myd1_talk4_begin
    private static DialogueOptionResult OnEnterTalk(DialogueOption option)
    {
        switch (option.callbackKey)
        {
            case "myd1_talk3_begin": 
            case "myd1_talk4_begin":
                if (!SystemManager.Instance.ActionPoint.Spend(1))
                    return new DialogueOptionResult { cancelJump = true };
                break;
        }
        return new DialogueOptionResult();
    }

    // ─────────────── 接取委托 ───────────────
    private static DialogueOptionResult OnAcceptQuest(DialogueOption option)
    {
        // 打开委托面板，显示委托信息 传入的是委托号
        Debug.Log($"玩家接取了委托，编号为：1 硬编码");
        UIManager.Instance.Open<CommissionPanel>(1);
        return new DialogueOptionResult();
    }

    // ─────────────── 拒绝接待 ───────────────
    private static DialogueOptionResult OnRefuseGuest(DialogueOption option)
    {
        SystemManager.Instance.VisitSystem.OnRefuseCurrentVisitor();//伤势回复
        //自动推进到下一位访客
        return new DialogueOptionResult();
    }

    // ─────────────── 根据指路结果分流 ───────────────
    private static DialogueOptionResult OnGuideCheck(DialogueOption option)
    {
        // TODO 根据玩家的指路结果，决定跳转到哪个节点
        return new DialogueOptionResult();
    }

    // ─────────────── 打开 UI 面板 ───────────────
    // 命中 key：open_backpack / open_special_commission
    private static DialogueOptionResult OnOpenUI(DialogueOption option)
    {
        if (option.tag == "narrative") return SystemManager.Instance.Narrative.OpenFunction(option);
        switch (option.callbackKey)
        {
            case "open_backpack":
                UIManager.Instance.Open<BackpackPanel>();
                return new DialogueOptionResult { waitForExternal = true };

            case "open_special_commission":
                return new DialogueOptionResult { cancelJump = true }; // 历史回调安全取消。

            case "open_commission_panel":
                return OnOpenCommissionPanel(option);
        }
        return new DialogueOptionResult();
    }

    private static DialogueOptionResult OnOpenCommissionPanel(DialogueOption option)
    {
        if (option.tag == "narrative") return SystemManager.Instance.Narrative.OpenFunction(option);
        int contractId = option.param > 0 ? option.param : 1;

        var talkPanel = UIManager.Instance.Get<TalkPanel>();
        var controller = talkPanel?.Dialogue;

        var panel = UIManager.Instance.Open<CommissionPanel>(contractId);
        if (panel == null)
            return new DialogueOptionResult();

        panel.SetOnAccepted(() => controller?.ContinueFromNode(option.targetNodeId));
        panel.SetOnCancelled(() => controller?.ResumeCurrentNode());

        return new DialogueOptionResult { waitForExternal = true };
    }
}

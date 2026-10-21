/****************************************************
    文件：DialogueCallbackBootstrap.cs
    作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/2 19:00:30
    功能：特殊委托面板（滑动列表 + 详情）
*****************************************************/
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
//SevenDimension 枚举 → 中文名映射
public static class SevenDimensionDisplay
{
    public static string GetName(SevenDimension d)
    {
        switch (d)
        {
            case SevenDimension.Reason: return "理智";
            case SevenDimension.Courage: return "勇气";
            case SevenDimension.Piety: return "虔诚";
            case SevenDimension.Tolerance: return "宽容";
            case SevenDimension.Benevolence: return "仁爱";
            case SevenDimension.Resolve: return "坚定";
            case SevenDimension.Diligence: return "勤勉";
            default: return d.ToString();
        }
    }
}

// 仅保留类名供旧预制体/编辑器工具识别；场景已注销，所有业务已撤除。
public class SpecialCommissionPanel : PanelBase
{
    public int OpenDetailId => 0;
    public int SelectedContractId => 0;
    public void RestoreDetail(int id) { }
    public void CompleteRouteAcceptance() { }
    public override void OnOpen(object data = null) { gameObject.SetActive(false); }
}

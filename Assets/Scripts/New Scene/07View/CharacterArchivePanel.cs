/****************************************************
    文件：CharacterArchivePanel.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/2 12:22:31
	功能： 查看冒险者档案的面板
*****************************************************/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CharacterArchivePanel : PanelBase
{

    [Header("按钮")]
    [SerializeField] private Button _closeButton;
    [SerializeField] private Button _basicInfo; //基本信息
    [SerializeField] private Button _dialogueLog;//对话记录
    [SerializeField] private Button _validInfo;//有效信息
    [SerializeField] private Button _investigationLog;//调查记录

    [Header("雷达图")]
    public RadarChart radarChart;

    [Header("人物信息")]
    [SerializeField] private Text _Introduction; //简要介绍
    [SerializeField] private Text _name; //冒险者名称
    [SerializeField] private Image _iconImage;//冒险者头像

    [Header("七维数值文本 (按顺序拖入: 理智,勇气,虔诚,宽容,仁爱,坚定,勤勉)")]
    public List<Text> statTexts = new List<Text>(7);

    public override bool IsModal => false;

    public override void OnInit()
    {
        _closeButton.onClick.AddListener(OnCloseClick);
    }

    public override void OnDestroy()
    {
        _closeButton.onClick.RemoveListener(OnCloseClick);
    }

    private void OnCloseClick()
    {
        UIManager.Instance.Close(this);
    }

    /// <summary>由外部（TalkPanel）调用：刷新指定冒险者的档案</summary> 图片命名规则  id_portrait
    public void Refresh(int adventurerId, Sprite iconSprite = null)
    {
        // 清空状态
        if (adventurerId < 0)
        {
            ClearBaseInfo();
            UpdateStatsVisuals(null);
            return;
        }

        // 获取静态配置
        if (!ConfigDatabase.Instance.Adventurers.TryGetValue(adventurerId, out var adv))
        {
            Debug.LogWarning($"[CharacterArchivePanel] 找不到冒险者静态配置 id={adventurerId}");
            ClearBaseInfo();
            UpdateStatsVisuals(null);
            return;
        }

        // 更新基础信息
        if (_name) _name.text = adv.name;
        if (_Introduction) _Introduction.text = adv.briefStory;
        if (_iconImage)
        {
            _iconImage.sprite = iconSprite;
            _iconImage.enabled = iconSprite != null;
        }

        // 获取运行时数据并刷新
        var runState = RunStateManager.Instance?.run; 
        if (runState != null && runState.adventurers.TryGetValue(adventurerId, out var runtimeData))
        {
            UpdateStatsVisuals(runtimeData);
        }
        else
        {
            Debug.LogWarning($"[CharacterArchivePanel] 找不到冒险者运行时数据 id={adventurerId}");
            UpdateStatsVisuals(null);
        }
    }

    /// <summary>统一刷新雷达图和七维文本</summary>
    private void UpdateStatsVisuals(AdventurerRuntimeData runtimeData)
    {
        // 数据为空：雷达图归零，文本清空
        if (runtimeData == null || runtimeData.currentSevenStats == null)
        {
            if (radarChart != null) radarChart.Refresh(new Dictionary<SevenDimension, int>());
            foreach (var t in statTexts) if (t) t.text = "0";
            return;
        }

        // 刷新雷达图
        if (radarChart != null) radarChart.Refresh(runtimeData);

        // 遍历更新7个文本
        var stats = runtimeData.currentSevenStats;
        for (int i = 0; i < statTexts.Count && i < 7; i++)
        {
            if (statTexts[i] == null) continue;
            SevenDimension dim = (SevenDimension)i; // 枚举顺序与列表顺序一致
            statTexts[i].text = stats.TryGetValue(dim, out int val) ? val.ToString() : "0";
        }
    }

    /// <summary>清空基础信息</summary>
    private void ClearBaseInfo()
    {
        if (_name) _name.text = "";
        if (_Introduction) _Introduction.text = "";
        if (_iconImage) _iconImage.sprite = null;
    }
}
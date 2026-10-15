/****************************************************
    文件：SpecialCommissionItem.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/16 0:53:06
功能：特殊委托列表中的一条
*****************************************************/
using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class SpecialCommissionItem : MonoBehaviour
{
    [SerializeField] private Text _nameText;
    [SerializeField] private Text _contentText;
    [SerializeField] private Text _rewardText;
    [SerializeField] private Text _influenceText;
    [SerializeField] private Button _viewButton;

    private ContractConfig _config;
    private Action<ContractConfig> _onView;

    public void Bind(ContractConfig cfg, Action<ContractConfig> onView)
    {
        _config = cfg;
        _onView = onView;

        if (_nameText != null) _nameText.text = cfg.name;
        if (_contentText != null) _contentText.text = cfg.description;
        if (_rewardText != null) _rewardText.text = $"报酬：{cfg.reward}";
        if (_influenceText != null) _influenceText.text = BuildInfluenceText(cfg);

        if (_viewButton != null)
        {
            _viewButton.onClick.RemoveAllListeners();
            _viewButton.onClick.AddListener(() => _onView?.Invoke(_config));
        }
    }

    private string BuildInfluenceText(ContractConfig cfg)
    {
        if (cfg.influenceStats == null || cfg.influenceStats.Count == 0)
            return "影响属性：无";

        var sb = new StringBuilder("影响属性：");
        for (int i = 0; i < cfg.influenceStats.Count; i++)
        {
            if (i > 0) sb.Append("、");
            sb.Append(SevenDimensionDisplay.GetName(cfg.influenceStats[i]));
        }
        return sb.ToString();
    }
}

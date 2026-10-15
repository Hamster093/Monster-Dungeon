/****************************************************
    文件：SpecialCommissionDetailPanel.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/16 0:51:48
功能：特殊委托详情（SpecialCommissionPanel 的子节点，SetActive 控制显隐）
*****************************************************/
using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class SpecialCommissionDetailPanel : MonoBehaviour
{
    [SerializeField] private Text _nameText;
    [SerializeField] private Text _rewardText;
    [SerializeField] private Text _influenceText;
    [SerializeField] private Text _descriptionText;

    [SerializeField] private Button _acceptButton;
    [SerializeField] private Button _closeButton;

    private ContractConfig _config;
    private Action<ContractConfig> _onAccept;

    public bool IsShowing => gameObject.activeSelf;

    private void Awake()
    {
        if (_acceptButton != null) _acceptButton.onClick.AddListener(OnAcceptClick);
        if (_closeButton != null) _closeButton.onClick.AddListener(Hide);

        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_acceptButton != null) _acceptButton.onClick.RemoveListener(OnAcceptClick);
        if (_closeButton != null) _closeButton.onClick.RemoveListener(Hide);
    }

    public void Show(ContractConfig cfg, Action<ContractConfig> onAccept)
    {
        _config = cfg;
        _onAccept = onAccept;

        if (_nameText != null) _nameText.text = cfg.name;
        if (_rewardText != null) _rewardText.text = $"报酬：{cfg.reward}";
        if (_influenceText != null) _influenceText.text = BuildInfluenceText(cfg);
        if (_descriptionText != null) _descriptionText.text = cfg.description;

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        _config = null;
        _onAccept = null;
        gameObject.SetActive(false);
    }

    private void OnAcceptClick()
    {
        if (_config == null) return;
        _onAccept?.Invoke(_config);
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

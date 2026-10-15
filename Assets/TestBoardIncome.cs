/****************************************************
    文件：BoardIncomeTestButton.cs
	功能：测试用按钮，点击后计算棋盘总收益并打印
*****************************************************/

using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class BoardIncomeTestButton : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private BackpackBoard _board;
    [SerializeField] private TileAffinityConfig _tileConfig;
    [SerializeField] private MonsterIncomeConfig _incomeConfig;

    [Header("输出（可选，留空就只打印 Console）")]
    [SerializeField] private Text _resultText;

    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(OnClick);

        // 没手动拖棋盘就自动找
        if (_board == null) _board = FindObjectOfType<BackpackBoard>();
    }

    private void OnClick()
    {
        if (_board == null) { Debug.LogError("[IncomeTest] 没有 BackpackBoard"); return; }
        if (_tileConfig == null) { Debug.LogError("[IncomeTest] 没有 TileAffinityConfig"); return; }
        if (_incomeConfig == null) { Debug.LogError("[IncomeTest] 没有 MonsterIncomeConfig"); return; }

        int total = BoardIncomeCalculator.ComputeTotal(_board, _tileConfig, _incomeConfig);

        Debug.Log($"[IncomeTest] 棋盘总收益 = {total}");
        ToastManager.Show($"[IncomeTest] 棋盘总收益 = {total}");

        if (_resultText != null)
            _resultText.text = $"总收益：{total}";
    }
}
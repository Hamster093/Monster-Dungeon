/****************************************************
    文件：StageTileTestButton.cs
    功能：测试按钮 —— 点击应用指定阶段的特殊地块
*****************************************************/

using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class StageTileTestButton : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private BackpackBoard _board;
    [SerializeField] private StageTileLayoutConfig _config;

    [Header("点一下切一个阶段，从 1 到 5 循环")]
    [SerializeField] private int _currentStage = 1;

    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(OnClick);
        if (_board == null) _board = FindObjectOfType<BackpackBoard>();
    }

    private void OnClick()
    {
        if (_board == null || _config == null) { Debug.LogError("[StageTest] 引用未配"); return; }

        StageTileApplier.Apply(_board, _config, _currentStage);
        Debug.Log($"[StageTest] 切换到阶段 {_currentStage}");

        _currentStage = _currentStage % 5 + 1;   // 1→2→3→4→5→1
    }
}
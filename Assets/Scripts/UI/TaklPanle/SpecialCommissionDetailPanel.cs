using System;
using UnityEngine;
// 旧编辑器工具的类型兼容壳，不创建详情，不执行接受回调。
public class SpecialCommissionDetailPanel : MonoBehaviour
{
    public Action OnReturned;
    public bool IsShowing => false;
    public void Show(ContractConfig cfg, Action<ContractConfig> accepted) { gameObject.SetActive(false); }
    public void Hide() { gameObject.SetActive(false); }
}

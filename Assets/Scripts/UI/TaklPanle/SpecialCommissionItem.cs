using System;
using UnityEngine;
// 历史资源兼容壳：正式场景不再注册或实例化特殊委托列表。
public class SpecialCommissionItem : MonoBehaviour
{
    public void Bind(ContractConfig cfg, Action<ContractConfig> viewed) { gameObject.SetActive(false); }
}

/****************************************************
    文件：Commission.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/15 17:13:22
	功能：一条已接取的委托（运行时数据，不存档）
*****************************************************/

[System.Serializable]
public class CommissionEntry
{
    public int contractId;              // 委托配置 ID
    public int adventurerId;            // 接这条委托的冒险者 ID
    [UnityEngine.Range(0f, 1f)]
    public float completionRate = 0.5f;   // ★ 默认改 0.5，等检定覆盖

    [System.NonSerialized]
    public bool statsApplied;             // ★ 防止重复加属性

    public CommissionEntry() { }

    public CommissionEntry(int contractId, int adventurerId, float completionRate = 1f)
    {
        this.contractId = contractId;
        this.adventurerId = adventurerId;
        this.completionRate = completionRate;
    }
}

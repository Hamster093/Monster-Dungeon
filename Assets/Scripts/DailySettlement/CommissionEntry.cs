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
    // 完整剧情按本次来访记录修正与实际指路目标，避免串用其他人的答对次数。
    public int correctAnswerCount = -1;
    public int targetMonsterId;
    public MonsterType? requestedMonsterType;
    public float rankFactor = -1; // 确认指路时固定等级系数；旧档未提供时迁移计算。
    public string targetUid, evolutionKind;
    public bool wrongRoute;
    public string outcomeText;
    public InjuryResult injury = InjuryResult.None;
    public bool completionChecked,combatChecked;
    public bool checksResolved;
    [UnityEngine.Range(0f, 1f)]
    public float completionRate = 1f; // 兼容旧字段；当前收益由类别、生还与等级系数计算。

    public bool statsApplied;             // ★ 防止重复加属性

    public CommissionEntry() { }

    public CommissionEntry(int contractId, int adventurerId, float completionRate = 1f)
    {
        this.contractId = contractId;
        this.adventurerId = adventurerId;
        this.completionRate = completionRate;
    }
}

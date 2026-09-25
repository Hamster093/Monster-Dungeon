/****************************************************
    文件：ConfigEnums.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/9/24 16:30:54
	功能：定义配置所需的枚举类型
*****************************************************/

public enum Level { S = 5, A = 4, B = 3, C = 2, D = 1 }

/// <summary>
/// 性格维度枚举。用于描述角色的性格倾向
/// </summary>
public enum PersonalityAxis
{
    Greed,      // 贪婪
    Caution,    // 谨慎
    Persistence,// 执着
    Vanity,     // 虚荣
    Impulse,    // 冲动
    Suspicion   // 多疑
}
/// <summary>
/// 物品类型枚举。用于区分道具在玩法中的功能分类。
/// </summary>
public enum ItemType
{
    Investigation,      // 调查道具
    CheckManipulation,  // 检定操纵工具
    Trap,               // 陷阱
    ExtraAP             // 额外行动点
}
/// <summary>
/// 货币类型枚举。用于区分不同种类的消耗资源。
/// </summary>
public enum CurrencyType { Cash, Nutrient }
/// <summary>
/// 检定类型枚举。用于区分不同场景下的 D20 检定规则。
/// </summary>
public enum CheckType { Dialogue, Adventure, Revenge }
/// <summary>
/// 记忆触发条件枚举。用于标记在何种事件发生时写入或更新角色的记忆条目。
/// </summary>
public enum MemoryTrigger
{
    Misled,             // 被误导
    Suspicion,          // 怀疑主人
    InjuredInZone,      // 在某分区受伤
    SuccessGetMaterial, // 成功取得材料
    ConfirmedDeceived,  // 确认被欺骗
    HeavyInjuryByLie,   // 因错误指路重伤
    Death               // 死亡
}
/// <summary>
/// 复仇程度枚举。用于量化角色对玩家的敌意或报复等级。
/// </summary>
public enum RevengeLevel { None, Light, Heavy }
/// <summary>
/// 对话阶段枚举。用于区分对话流程的不同阶段
/// </summary>
public enum DialogueStage { Pre, Formal }
/// <summary>
/// 健康状态枚举
/// </summary>
public enum HealthState { Normal, Damaged, Critical }
/// <summary>
/// 装备状态枚举
/// </summary>
public enum EquipmentState { Normal, Damaged, Critical }
/// <summary>
/// 关系状态枚举。
/// </summary>
public enum RelationshipState { Neutral, Suspicious, Hostile }
/// <summary>
/// 继承者维度枚举。用于描述角色在成长或评价时的多维属性。
/// </summary>
public enum SuccessorDimension
{
    Desire,      // 欲望
    Reason,      // 理智
    Trust,       // 信任
    Fear,        // 恐惧
    Vanity,      // 虚荣
    Cooperation  // 合作力
}

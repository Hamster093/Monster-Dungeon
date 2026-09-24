/****************************************************
    文件：ConfigEnums.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/9/24 16:30:54
	功能：定义配置所需的枚举类型
*****************************************************/

public enum Level { S = 5, A = 4, B = 3, C = 2, D = 1 }

public enum PersonalityAxis
{
    Greed,      // 贪婪
    Caution,    // 谨慎
    Persistence,// 执着
    Vanity,     // 虚荣
    Impulse,    // 冲动
    Suspicion   // 多疑
}

public enum ItemType
{
    Investigation,      // 调查道具
    CheckManipulation,  // 检定操纵工具
    Trap,               // 陷阱
    ExtraAP             // 额外行动点
}

public enum CurrencyType { Cash, Nutrient }

public enum CheckType { Dialogue, Adventure, Revenge }

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

public enum RevengeLevel { None, Light, Heavy }

public enum DialogueStage { Pre, Formal }

public enum HealthState { Normal, Damaged, Critical }
public enum EquipmentState { Normal, Damaged, Critical }
public enum RelationshipState { Neutral, Suspicious, Hostile }

public enum SuccessorDimension
{
    Desire,      // 欲望
    Reason,      // 理智
    Trust,       // 信任
    Fear,        // 恐惧
    Vanity,      // 虚荣
    Cooperation  // 合作力
}

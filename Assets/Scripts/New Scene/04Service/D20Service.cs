/****************************************************
    文件：D20Service.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/9/25 0:22:23
	功能：统一 D100 鉴定器（类名保留，兼容现有控制器和回调绑定）
*****************************************************/
/// <summary>
/// 鉴定状态枚举
/// </summary>
public struct CheckResult
{
    public int Roll;
    public bool Success;
    public bool IsCritical;
    public bool IsFumble;
}

public class D20Service
{
    [System.Serializable]
    public class BalanceConfig
    {
        public int criticalMin=96,fumbleMax=5,incomeTarget=52,answerBonus=8,answerBonusCap=24;
        public int levelStep=18,lightPenalty=6,severePenalty=12,severeMax=15,lightMax=35;
        public int informationTarget=65,influenceTarget=60,personalTarget=58,cooperateTarget=62;
        public int dialogueBonus=6,dialogueBonusCap=24,coopStatDivisor=4,coopStatCap=20;
        public int initialTrust=50,gatherBonus=20,injuryTrustPenalty=15,higherLevelTrustPenalty=12,trustGain=4;
        public int revengeLightChance=60,revengeHeavyChance=35,revengeGiftBonus=20;
        public int adventureTarget=52,equipmentDamagedPenalty=8,equipmentCriticalPenalty=16,falseRoutePenalty=10;
        public float criticalIncome=1.25f,successIncome=1,failIncome=.5f,fumbleIncome=.25f;
    }
    private static BalanceConfig _balance;
    public static BalanceConfig Balance
    {
        get{if(_balance==null){var asset=UnityEngine.Resources.Load<UnityEngine.TextAsset>("Config/d100Rules");_balance=asset?Newtonsoft.Json.JsonConvert.DeserializeObject<BalanceConfig>(asset.text):new BalanceConfig();}return _balance;}
    }
    [System.Serializable]
    public class Presentation
    {
        public string key,title,context;
        // 已建立检定保存当时的玩家上下文，读档后不按当前属性重算。
        public string details, playerKind, playerAttribute, checkId;
        public string annotation, storyText, storySpeaker;
        public int playerDay, playerThought, playerLevel;
        public int target,modifier,roll,rawRoll;
        public int sides;
        public bool audioPresented;
        public bool rerolled,charmUsed,confirmed,criticalRules=true;
        public bool guaranteedSuccess; // 达标接任的必成演出；不使用检定道具。
        public bool FixedOutcome => guaranteedSuccess || guaranteedFailure;
        public bool trainingEligible;
        public string trainingReception;
        public bool guaranteedFailure; // 明示必败的终局演出，不消费道具。
        /// <summary>旧档只换算已经掷出的点数，不重投、不再次消耗道具。</summary>
        public void Normalize()
        {
            if(sides==100)return;
            roll=(UnityEngine.Mathf.Clamp(roll,1,20)-1)*5+3;rawRoll=(UnityEngine.Mathf.Clamp(rawRoll,1,20)-1)*5+3;
            target=(target-1)*5+1;modifier*=5;sides=100;
        }
        public CheckResult Result(){Normalize();return FixedOutcome?new CheckResult{Roll=roll,Success=guaranteedSuccess}:Evaluate(roll,target,modifier,criticalRules);}
    }
    public static Presentation CreatePresentation(string key,string title,int target,int modifier=0,string context="",bool criticalRules=true)
    {
        var p=new Presentation{key=key,title=title,target=target,modifier=modifier,context=context,criticalRules=criticalRules,sides=100};RollPresented(p);return p;
    }
    public static void RollPresented(Presentation p)
    {
        p.Normalize();p.charmUsed=false;p.rawRoll=UnityEngine.Random.Range(1,101);p.roll=p.rawRoll;
        if(!p.FixedOutcome&&p.criticalRules&&p.roll<=Balance.fumbleMax&&InventoryModel.Instance?.ConsumeId(3)==true)
        {
            // D100 新规则：解除1～5大失败，重判6～95普通点数；加修正后仍可成功，不生成大成功。
            p.charmUsed=true;p.roll=UnityEngine.Random.Range(Balance.fumbleMax+1,Balance.criticalMin);
        }
    }
    public static void Show(Presentation p,System.Action<CheckResult> completed)
    {
        if(p.playerDay>0&&!string.IsNullOrEmpty(p.annotation)) UIManager.Instance.Get<TalkPanel>()?.ShowCheckAnnotation(p);
        UIManager.Instance.Open<DiceCheckPanel>(new DiceCheckPanel.OpenData{request=p,completed=completed});
    }
    public int Roll() => UnityEngine.Random.Range(1, 101);
    public static CheckResult Evaluate(int roll,int target,int modifier=0,bool criticalRules=true)
    {
        bool critical=criticalRules&&roll>=Balance.criticalMin,fumble=criticalRules&&roll<=Balance.fumbleMax;
        return new CheckResult{Roll=roll,Success=critical||!fumble&&roll+modifier>=target,IsCritical=critical,IsFumble=fumble};
    }
    /// <summary>枚举同一判定函数计算基础单次概率；不掷骰、不使用道具。</summary>
    public static int SuccessPercent(int target, int modifier = 0, bool criticalRules = true)
    {
        int count = 0;
        for (int roll = 1; roll <= 100; roll++) if (Evaluate(roll, target, modifier, criticalRules).Success) count++;
        return count;
    }
    public static float IncomeRate(CheckResult r)=>r.IsCritical?Balance.criticalIncome:r.IsFumble?Balance.fumbleIncome:r.Success?Balance.successIncome:Balance.failIncome;
    public static int AnswerModifier(int answers)=>UnityEngine.Mathf.Clamp(answers*Balance.answerBonus,0,Balance.answerBonusCap);
    /// <summary>
    /// 检定 返回鉴定结果
    /// </summary>
    /// <param name="target"></param>
    /// <param name="modifier"></param>
    /// <returns></returns>
    public CheckResult Check(int target, int modifier = 0)
    {
        return Evaluate(Roll(),target,modifier);
    }
}

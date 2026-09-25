/****************************************************
    文件：D20Service.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/9/25 0:22:23
	功能：D20鉴定器
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
    public int Roll() => UnityEngine.Random.Range(1, 21);
    /// <summary>
    /// 检定 返回鉴定结果
    /// </summary>
    /// <param name="target"></param>
    /// <param name="modifier"></param>
    /// <returns></returns>
    public CheckResult Check(int target, int modifier = 0)
    {
        int roll = Roll();
        return new CheckResult
        {
            Roll = roll,
            Success = roll == 20 || (roll != 1 && roll + modifier >= target),
            IsCritical = roll == 20,
            IsFumble = roll == 1
        };
    }
}
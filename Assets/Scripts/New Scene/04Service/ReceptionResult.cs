/****************************************************
    文件：ReceptionResult.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/9/25 17:55:32
	功能：接待动作统一返回结构
*****************************************************/

public class ReceptionResult
{
    public bool Success;      // 动作本身是否成功（不代表检定成功）
    public string Message;    // 给 UI 显示的文本
    public int SpendAP;       // 本次消耗的行动点
    public bool ShouldEndVisit; // 是否需要结束本次来客（拒绝、指路后）

    /// <summary>
    /// 快速创建一个“失败”的接待结果。
    /// </summary>
    /// <param name="msg">失败原因或提示文本</param>
    /// <returns>
    public static ReceptionResult Fail(string msg)
        => new ReceptionResult { Success = false, Message = msg };

    /// <summary>
    /// 快速创建一个“成功”的接待结果。
    /// </summary>
    /// <param name="msg">成功后的提示文本</param>
    /// <param name="spendAP">本次消耗的行动点，默认为 0</param>
    /// <param name="endVisit">是否需要结束本次来访，默认为 false</param>
    /// <returns>
    public static ReceptionResult Ok(string msg, int spendAP = 0, bool endVisit = false)
        => new ReceptionResult { Success = true, Message = msg, SpendAP = spendAP, ShouldEndVisit = endVisit };
}

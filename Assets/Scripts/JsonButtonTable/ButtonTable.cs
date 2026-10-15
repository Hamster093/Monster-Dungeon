/****************************************************
    文件：ButtonTable.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/12 16:51:02
	功能：按钮字典表，存储按钮的文本和回调键
*****************************************************/

using System.Collections.Generic;

public static class ButtonTable
{
    // key -> (text, callbackKey)
    public static readonly Dictionary<string, (string text, string callbackKey)> All = new()
    {
        { "view_quest",    ("1.-查看委托", null) },
        { "talk",          ("2.-选择搭话", null) },
        { "use_item",      ("3.-使用道具", "open_backpack") },
        { "refuse",        ("4.-直接拒绝", "myd1_refuse") },
        { "special_quest", ("5.-特殊委托", "open_special_commission") },

        { "accept_quest",  ("接取委托", "myd1_accept_quest") },
        { "guide",         ("指路",     "myd1_guide_check") },
        { "back",          ("返回", null) },

        { "talk_1", ("1.-“堂堂A级的冒险者也接这种简单的委托，这不是浪费人力吗？”", "myd1_talk1_check") },
        { "talk_2", ("2.-“我刚才就想问了，你是精灵，还是兽人？”", "myd1_talk2_check") },
        { "talk_3", ("3.-“我们的城主是谁来着？”", "myd1_talk3_begin") },
        { "talk_4", ("4.-“作为一个人类，我真羡慕你的血统。”", "myd1_talk4_begin") },

        { "lord_a1", ("“所以今年是谁？”", "myd1_talk3_answer1_check") },
        { "lord_a2", ("“那这东西是怎么变成一个固定委托的？”", "myd1_talk3_answer2_check") },
        { "lord_a3", ("（开玩笑）“既然如此，说不定哪年就是你呢？”", "myd1_talk3_answer3_check") },

        { "blood_a1", ("“长相。你不觉得你的耳朵很漂亮，牙齿很可爱吗？”", "myd1_talk4_answer1_check") },
        { "blood_a2", ("“见识。我也想多认识点精灵和兽人。”", "myd1_talk4_answer2_check") },
        { "blood_a3", ("“抗污染能力。唉，人类想在哈耳摩尼亚正常生活实在太难了。”", "myd1_talk4_answer3_check") },

        { "archive_shelf",  ("（搜寻身后的架子。）", null) },
        { "archive_drawer", ("（翻找接待桌的抽屉。）", null) },
        { "archive_gear",   ("（穿上你过去的冒险装备。）“稍等！我怀疑那玩意落在下面了，我这就下去找！”", null) },
        { "archive_ask",    ("“不好意思，工作交接得匆忙，能麻烦你再给我一份吗？”", "myd1_intro_archive_correct") },

        { "final_sincere",  ("（真诚）“谢谢，哈耳摩尼亚就应该多一些你这样的冒险者。”", "myd1_final_firmness_up") },
        { "final_flatter",  ("（浮夸）“今天运气真好，竟然能碰上咱们伟大的城镇新星！”", "myd1_final_check") },
        { "final_anxious",  ("（焦虑）“抱歉，我会记住的。”", "myd1_final_tolerance_up") },
    };
}
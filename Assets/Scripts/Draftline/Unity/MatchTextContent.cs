using UnityEngine;

namespace Draftline.Unity
{
    [CreateAssetMenu(menuName="Driftline/Match Text Content")]
    public sealed class MatchTextContent : ScriptableObject
    {
        public string[] FactionNames={"红方 · 赤炉","蓝方 · 苍印","绿方 · 苔庭","黄方 · 金环"};
        [TextArea] public string[] FactionStories={"寻找界钥，重启地下炉城。","封存遗迹知识，阻止机关失控。","守望苔庭，让遗迹不再侵蚀地表。","沿金环指引，寻找失落城邦。"};
        public string[] ItemNames={"穿墙","急停","撞吃","破墙"};
        [TextArea] public string[] ItemDescriptions={"穿墙：本回合穿过升起的机关墙，并忽略传送门。","急停：下一次滑行可停在路径上的指定格，用后失效。","撞吃：本回合碰到敌棋时吃掉它，并停在该格。","破墙：本回合可停在机关墙或中心墙上，离开后墙恢复。"};
        [TextArea] public string FiveVictory="五枚傀儡共鸣，界钥已被唤醒。";
        [TextArea] public string SurvivalVictory="成为最后存活的一方，夺得界钥。";
    }
}

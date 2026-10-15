using UnityEngine;

namespace Draftline.Unity
{
    [CreateAssetMenu(menuName="Driftline/Match Text Content")]
    public sealed class MatchTextContent : ScriptableObject
    {
        public string[] FactionNames={"红方","蓝方","绿方","黄方"};
        [TextArea] public string[] FactionStories={"","","",""};
        public string[] ItemNames={"穿墙","急停","撞吃","破墙"};
        [TextArea] public string[] ItemDescriptions={"穿墙：本回合穿过升起的机关墙，并忽略传送门。","急停：下一次滑行可停在路径上的指定格，用后失效。","撞吃：本回合碰到敌棋时吃掉它，并停在该格。","破墙：本回合可停在机关墙或中心墙上，离开后墙恢复。"};
        [TextArea] public string FiveVictory="五子连线";
        [TextArea] public string SurvivalVictory="最后存活";
    }
}

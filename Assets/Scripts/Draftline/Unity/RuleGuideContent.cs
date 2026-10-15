using UnityEngine;

namespace Draftline.Unity
{
    [System.Serializable]
    public sealed class RuleGuideStep
    {
        public string Title;
        [TextArea(4,10)] public string Body;
        [TextArea(3,8)] public string Example;
        public RuleGuideStep(string title,string body,string example) { Title=title; Body=body; Example=example; }
    }
    [CreateAssetMenu(menuName="Driftline/Rule Guide Content")]
    public sealed class RuleGuideContent : ScriptableObject
    {
        public RuleGuideStep[] Steps = {
            new RuleGuideStep("游戏说明", "让五枚棋子连成线，或者成为最后存活的一方即可。", "●  ●  ●  ●  ●\n\n横、竖、斜均可"),
            new RuleGuideStep("移动", "掷骰后选子，沿直线滑到阻挡前。\n\n骰子决定滑行次数。回合内继续移动同一枚棋子，直到次数用完或无路可走。", "圆形终点 · 滑行\n金色停点 · 急停\n\n右键 / Esc · 取消预览"),
            new RuleGuideStep("生命与补子", "每方 5 点生命，被吃一子扣 1 点，归零出局。\n\n场上最多 8 子。首次滑行前可点出生格 + 补子，不消耗滑行次数。被吃的棋子可以再次部署。", "生命 5 → 4\n\n补子不恢复生命\n只能通过己方阵营门"),
            new RuleGuideStep("传送与围杀", "传送落在配对门朝中央的相邻格；出口被堵时不能通过。\n\n同色棋上下左右连成棋块，共享气。两端门口也连通气；封住最后一口气，可在回合末围杀。", "本次行动不自吃\n\n机关墙在回合末切换\n墙变也可能造成围杀"),
            new RuleGuideStep("道具", "穿墙 · 本回合穿过机关墙，忽略传送。\n\n急停 · 下一次滑行选择路径停点。\n\n撞吃 · 本回合碰到敌棋时吃掉并停下。\n\n破墙 · 本回合可停在机关墙上。", "先选道具，再点目标\n\n确认行动才消耗\n破墙停留后，离开即恢复")
        };
    }
}
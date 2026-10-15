using System.Collections.Generic;
using System.Linq;
using Draftline.Core;

namespace Draftline.Unity
{
    public static class MatchExplanation
    {
        public static string CaptureReason(CaptureCause cause) => cause == CaptureCause.Collision ? "撞吃道具接触敌棋"
            : cause == CaptureCause.Surround ? "回合末新增无气，被围杀"
            : cause == CaptureCause.WallChange ? "机关墙切换后失去最后一口气" : "棋子被吃";

        public static string Describe(ActionInspection inspection)
        {
            if (!inspection.Accepted) return "此动作不可提交：\n" + FriendlyError(inspection.Error);
            var events = inspection.Events;
            var dice=events.FirstOrDefault(e=>e.Type==MatchEventType.DiceRolled);
            var lines = new List<string> { inspection.Kind == MatchCommandKind.Deploy ? "补子 · 不消耗滑行" : dice!=null?"掷骰 · "+dice.Value+" 点":"滑行 · 消耗 1 次" };
            foreach (var item in events.Where(e => e.Type == MatchEventType.ItemActivated)) lines.Add("使用" + MatchLabels.Item(item.Item) + " · 消耗 1 件");
            var portals = events.Count(e => e.Type == MatchEventType.PortalUsed);
            if (portals > 0) lines.Add("经过传送门 " + portals + " 次");
            if (events.Any(e => e.Slide != null && e.Slide.Steps.Any(s => s.BrokeWall))) lines.Add("破墙停留 · 离开后墙恢复");
            foreach (var item in events.Where(e => e.Type == MatchEventType.ItemCollected)) lines.Add("拾取" + MatchLabels.Item(item.Item));
            foreach (var group in events.Where(e => e.Type == MatchEventType.PieceCaptured).GroupBy(e => e.Cause))
            {
                var own=group.Count(e=>e.Player==inspection.Actor);var enemy=group.Count()-own;
                var label=group.Key==CaptureCause.Collision?"撞吃接触":group.Key==CaptureCause.Surround?"回合末新增围杀":group.Key==CaptureCause.WallChange?"墙变后失去生路":"棋子被吃";
                lines.Add(label+"："+(own>0?"己方 "+own+" 枚":"")+(own>0&&enemy>0?"，":"")+(enemy>0?"对方 "+enemy+" 枚":""));
            }
            foreach(var group in events.Where(e=>e.Type==MatchEventType.PieceCaptured).GroupBy(e=>e.Player))
                lines.Add(MatchLabels.Player(group.Key)+" · 生命降至 "+group.Last().Value+"/"+MatchRules.StartingHealth);
            if (inspection.PendingEnemyPieces > 0) lines.Add("回合末围杀：对方 " + inspection.PendingEnemyPieces + " 子");
            if (inspection.RetainedOwnPieces > 0) lines.Add("己方无气 · 本次不自吃");
            if (inspection.ForcedEnd) lines.Add(dice!=null?"无可行动棋子 · 自动结算":"选中棋子无路可走 · 提前结算");
            if (events.Any(e => e.Type == MatchEventType.WallRotated)) lines.Add("机关墙切换");
            if (events.Any(e => e.Type == MatchEventType.Victory)) lines.Add("回合结算 · 达成胜利");
            else if(events.Any(e=>e.Type==MatchEventType.Draw))lines.Add("所有队伍生命耗尽 · 平局");
            return string.Join("\n", lines);
        }

        public static string FriendlyError(string error)
        {
            switch (error)
            {
                case "Direction is blocked.": return "这个方向被墙、棋子或不可通行的阵营门挡住。";
                case "Piece is not selectable.": return "请选择当前玩家可以滑行的棋子。";
                case "Cannot deploy at this spawn.": return "场上最多 8 子；出生格需空闲且新棋有路可走。被吃的棋子可重新部署，首次滑行后不能补子。";
                case "Choose a stop cell for Emergency Stop.": return "急停已选，请点路径上的金色停点。";
                case "Emergency Stop is not active.": return "先选择急停道具，再点路径停点。";
                case "Stop cell is outside this slide.": return "请选择这条路径内的停点。";
                case "Cannot stop on a portal.": return "传送门必须完整通过，不能在门格中间急停。";
                case "Item is unavailable or already active.": return "道具已生效或库存中没有该道具。";
                case "Not waiting for a slide.": return "请先掷骰并选择棋子。";
                default: return error;
            }
        }
    }
}

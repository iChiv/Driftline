using System;

namespace Draftline.Core
{
    public enum MatchCommandKind { ApplyDice, Deploy, Slide, ActivateItem }

    [Serializable]
    public sealed class MatchCommand
    {
        public MatchCommandKind Kind;
        public int ExpectedRevision;
        public int PieceId = -1;
        public SlideDirection Direction;
        public int StopIndex = -1;
        public int Item;
        public int Dice;
    }

    public static class MatchCommandDispatcher
    {
        // A command operates on an independent copy; rejected commands have no effects.
        // ApplyDice is an authority-only operation, never accepted from a network client.
        public static bool TryExecute(MatchRules current, PlayerColor actor, MatchCommand command,
            out MatchRules result, out string error)
        {
            result = null;
            if (current == null || command == null) return Reject("对局尚未开始。", out error);
            if (current.State.CurrentPlayer != actor) return Reject("现在不是你的回合。", out error);
            if (command.ExpectedRevision != current.State.Revision)
                return Reject("棋局已更新，请重新选择目标。", out error);
            if (!Enum.IsDefined(typeof(MatchCommandKind), command.Kind))
                return Reject("未知操作。", out error);
            var copy = current.CloneForSimulation();
            copy.ClearEvents();
            switch (command.Kind)
            {
                case MatchCommandKind.ApplyDice:
                    if (!copy.TryApplyDice(command.Dice, out error)) return false;
                    break;
                case MatchCommandKind.Deploy:
                    if (!copy.TryDeploy(out _, out error)) return false;
                    break;
                case MatchCommandKind.ActivateItem:
                    if (!Enum.IsDefined(typeof(ItemType), command.Item)) return Reject("未知道具。", out error);
                    if (!copy.TryActivateItem((ItemType)command.Item, out error)) return false;
                    break;
                case MatchCommandKind.Slide:
                    if (!Enum.IsDefined(typeof(SlideDirection), command.Direction)) return Reject("未知方向。", out error);
                    if (copy.State.Phase == MatchPhase.WaitSelect)
                    {
                        if (!copy.TrySelectPiece(command.PieceId, out error)) return false;
                    }
                    else if (copy.State.SelectedPieceId != command.PieceId)
                        return Reject("本回合只能继续移动已选棋子。", out error);
                    if (command.Item != 0)
                    {
                        if (!Enum.IsDefined(typeof(ItemType), command.Item)) return Reject("未知道具。", out error);
                        if (!copy.TryActivateItem((ItemType)command.Item, out error)) return false;
                    }
                    if (!copy.TrySlide(command.Direction, command.StopIndex < 0 ? (int?)null : command.StopIndex,
                        out _, out error)) return false;
                    break;
            }
            copy.State.Revision++;
            result = copy;
            error = null;
            return true;
        }

        private static bool Reject(string message, out string error) { error = message; return false; }
    }
}

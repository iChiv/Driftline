using System;
using System.Linq;

namespace Draftline.Core
{
    /// <summary>Read-only inspection of a complete atomic action and its immediate resolution.</summary>
    public sealed class ActionInspection
    {
        public bool Accepted;
        public string Error;
        public MatchCommandKind Kind;
        public PlayerColor Actor;
        public int RemainingSlides;
        public bool EndsTurn, ForcedEnd;
        public int PendingEnemyPieces, RetainedOwnPieces;
        public MatchEvent[] Events = Array.Empty<MatchEvent>();

        public static ActionInspection Evaluate(MatchRules current, MatchCommand command)
        {
            if (current == null || command == null) return new ActionInspection { Error="对局或动作尚未准备。" };
            var result = new ActionInspection { Kind = command.Kind, Actor = current.State.CurrentPlayer };
            result.Accepted = MatchCommandDispatcher.TryExecute(current, result.Actor, command, out var next, out var error);
            result.Error = error;
            if (!result.Accepted) return result;
            // Random replenishment is not public before committing. Never disclose its identity/location.
            result.Events = next.Events.Where(e => e.Type != MatchEventType.ItemSpawned).ToArray();
            result.RemainingSlides = next.State.RemainingSlides;
            result.EndsTurn = next.State.CurrentPlayer != result.Actor || next.State.Phase == MatchPhase.GameOver;
            result.ForcedEnd = result.Events.Any(e => e.Type == MatchEventType.NoMoves);
            var newDead = next.FindDeadBlocks().Where(block => block.Any(p => !current.State.DeadAtTurnStart.Contains(p.Id))).ToArray();
            if (!result.EndsTurn) result.PendingEnemyPieces = newDead.Where(b => b[0].Owner != result.Actor).Sum(b => b.Count);
            result.RetainedOwnPieces = newDead.Where(b => b[0].Owner == result.Actor).Sum(b => b.Count);
            return result;
        }
    }
}

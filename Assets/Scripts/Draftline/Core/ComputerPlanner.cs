using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Draftline.Core
{
    /// <summary>Pure-data beam search. Run on an isolated copy; never uses Unity APIs.</summary>
    public static class ComputerPlanner
    {
        private sealed class Node
        {
            public MatchRules Rules;
            public MatchCommand First;
            public double Score;
            public HashSet<GridPosition> Visited;
        }

        public static IEnumerable<MatchCommand> LegalCommands(MatchRules rules)
        {
            var state = rules.State;
            if (state.Phase == MatchPhase.WaitSelect && rules.CanDeploy(state.CurrentPlayer))
                yield return new MatchCommand { Kind = MatchCommandKind.Deploy, ExpectedRevision = state.Revision };
            if (state.Phase != MatchPhase.WaitSelect && state.Phase != MatchPhase.Moving) yield break;
            var pieces = state.Phase == MatchPhase.Moving
                ? state.Pieces.Where(p => p.Id == state.SelectedPieceId)
                : state.Pieces.Where(p => p.Owner == state.CurrentPlayer && rules.HasLegalSlide(p));
            var items = new[] { 0 }.Concat(state.Players[(int)state.CurrentPlayer].Items.Select(i => (int)i).Distinct());
            foreach (var piece in pieces)
                foreach (var item in items)
                {
                    var preview = rules.CloneForSimulation();
                    if (preview.State.Phase == MatchPhase.WaitSelect && !preview.TrySelectPiece(piece.Id, out _)) continue;
                    if (item != 0 && !preview.TryActivateItem((ItemType)item, out _)) continue;
                    foreach (SlideDirection direction in Enum.GetValues(typeof(SlideDirection)))
                    {
                        var plan = preview.PlanSlide(preview.PieceById(piece.Id), direction);
                        if (!plan.IsLegal) continue;
                        var first = preview.State.EmergencyStopActive ? 0 : plan.Steps.Count - 1;
                        for (var i = first; i < plan.Steps.Count; i++)
                            yield return new MatchCommand { Kind = MatchCommandKind.Slide, ExpectedRevision = state.Revision,
                                PieceId = piece.Id, Direction = direction, Item = item,
                                StopIndex = preview.State.EmergencyStopActive ? i : -1 };
                    }
                }
        }

        public static MatchCommand Choose(MatchRules source, bool easy, CancellationToken cancellation = default)
        {
            var root = source.CloneForSimulation();
            var actor = root.State.CurrentPlayer;
            // Future item randomness is not privileged knowledge: rollouts use a public-state seed.
            root.State.RandomState = unchecked((uint)(root.State.TurnCount * 7919 + root.State.Revision * 397 + 12347));
            if (root.State.RandomState == 0) root.State.RandomState = 1;
            var clock = Stopwatch.StartNew();
            var limit = easy ? 65 : 230;
            var width = easy ? 3 : 8;
            var frontier = new List<Node> { new Node { Rules = root, Visited = new HashSet<GridPosition>() } };
            Node best = null;
            Node bestTerminal = null;
            var initial = new List<Node>();
            for (var depth = 0; depth < 7 && frontier.Count > 0; depth++)
            {
                var next = new List<Node>();
                foreach (var node in frontier)
                {
                    if (node.Rules.State.CurrentPlayer != actor || node.Rules.State.Phase == MatchPhase.GameOver) continue;
                    foreach (var command in LegalCommands(node.Rules))
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if (clock.ElapsedMilliseconds >= limit && best != null) return Pick(bestTerminal ?? best, initial, easy, source);
                        if (!MatchCommandDispatcher.TryExecute(node.Rules, actor, command, out var child, out _)) continue;
                        var visited = new HashSet<GridPosition>(node.Visited);
                        var score = Evaluate(child, actor);
                        if (command.Kind == MatchCommandKind.Slide)
                        {
                            var position = child.PieceById(command.PieceId).Position;
                            if (!visited.Add(position)) score -= 24;
                        }
                        if (command.Item != 0) score -= 3;
                        var candidate = new Node { Rules = child, First = node.First ?? command, Score = score, Visited = visited };
                        if (depth == 0) initial.Add(candidate);
                        if (best == null || score > best.Score) best = candidate;
                        if ((child.State.CurrentPlayer != actor || child.State.Phase == MatchPhase.GameOver) &&
                            (bestTerminal == null || score > bestTerminal.Score)) bestTerminal = candidate;
                        if (score > 900000) return candidate.First;
                        next.Add(candidate);
                    }
                }
                frontier = next.OrderByDescending(n => n.Score).Take(width).ToList();
            }
            return best == null ? null : Pick(bestTerminal ?? best, initial, easy, source);
        }

        private static MatchCommand Pick(Node best, List<Node> initial, bool easy, MatchRules source)
        {
            if (!easy || initial.Count == 0) return best.First;
            var top = initial.OrderByDescending(n => n.Score).Take(3).ToArray();
            return top[(source.State.TurnCount + source.State.Revision) % top.Length].First;
        }

        private static double Evaluate(MatchRules rules, PlayerColor actor)
        {
            var s = rules.State;
            if (s.Winner.HasValue) return s.Winner == actor ? 1000000 : -1000000;
            if (s.Players[(int)actor].Eliminated) return -1000000;
            var score = 0d;
            var dangerous = rules.FindWallKillPreview();
            foreach (var player in s.Players.Where(p => p.Active && !p.Eliminated))
            {
                var mine = player.Color == actor;
                var sign = mine ? 1d : -0.75d;
                var pieces = s.Pieces.Where(p => p.OnBoard && p.Owner == player.Color).ToArray();
                score += sign * (pieces.Length * 25 - player.Consumed * 75 + player.Items.Count * 8);
                score -= sign * pieces.Count(p => dangerous.Contains(p.Id)) * 120;
                foreach (var p in pieces)
                {
                    score += sign * (rules.HasLegalSlide(p, true) ? 3 : -8);
                    score += sign * (18 - Math.Abs(p.Position.Column - 9) - Math.Abs(p.Position.Row - 9)) * 4.0;
                    foreach (var delta in new[] { new[] {1,0}, new[] {0,1}, new[] {1,1}, new[] {1,-1} })
                    {
                        if (rules.PieceAt(p.Position.Offset(-delta[0], -delta[1]))?.Owner == player.Color) continue;
                        var count = 1;
                        var pos = p.Position.Offset(delta[0], delta[1]);
                        while (BoardLayout.Contains(pos) && rules.PieceAt(pos)?.Owner == player.Color)
                        { count++; pos = pos.Offset(delta[0], delta[1]); }
                        var room = count;
                        foreach (var polarity in new[] { -1, 1 })
                        {
                            var next = polarity < 0 ? p.Position.Offset(-delta[0],-delta[1]) : pos;
                            for (var extra = 0; extra < 4 && BoardLayout.Contains(next); extra++)
                            {
                                var cell = rules.Board.CellAt(next);
                                if (cell.Kind == CellKind.FixedWall || (cell.Kind == CellKind.PlayerGate && cell.Owner != player.Color)) break;
                                var occupant = rules.PieceAt(next);
                                if (occupant != null && occupant.Owner != player.Color) break;
                                room++;
                                next = next.Offset(delta[0] * polarity, delta[1] * polarity);
                            }
                        }
                        // A four-run trapped inside a four-cell corner corridor cannot
                        // become five; do not encourage the computer to camp there forever.
                        score += sign * (count >= 5 ? 50000 : room < 5 ? count * 2 : count == 4 ? 1600 : count == 3 ? 180 : count == 2 ? 28 : 0);
                    }
                }
            }
            return score;
        }
    }
}

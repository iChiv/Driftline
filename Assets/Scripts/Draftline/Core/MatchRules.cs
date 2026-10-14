using System;
using System.Collections.Generic;
using System.Linq;

namespace Draftline.Core
{
    /// <summary>
    /// Authoritative, Unity-independent rules. The host or local hot-seat mode sends
    /// commands here; scene objects only render the resulting state and events.
    /// </summary>
    public sealed class MatchRules
    {
        private static readonly SlideDirection[] Directions =
            { SlideDirection.Up, SlideDirection.Down, SlideDirection.Left, SlideDirection.Right };
        private static readonly ItemType[] ItemWeights =
            { ItemType.PassWall, ItemType.PassWall, ItemType.PassWall,
              ItemType.EmergencyStop, ItemType.EmergencyStop, ItemType.EmergencyStop,
              ItemType.CollisionCapture,
              ItemType.BreakWall, ItemType.BreakWall, ItemType.BreakWall };

        private readonly Random random;
        private readonly List<MatchEvent> events = new List<MatchEvent>();

        public BoardLayout Board { get; }
        public MatchState State { get; }
        public IReadOnlyList<MatchEvent> Events => events;

        public MatchRules(IEnumerable<PlayerColor> activeColors, int? randomSeed = null)
        {
            var active = activeColors.Distinct().OrderBy(color => color).ToArray();
            if (active.Length < 2 || active.Length > 4)
                throw new ArgumentException("A match needs two to four distinct colors.", nameof(activeColors));

            Board = new BoardLayout();
            State = new MatchState { Phase = MatchPhase.WaitRoll, CurrentPlayer = active[0] };
            random = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();

            var id = 0;
            for (var color = 0; color < State.Players.Length; color++)
            {
                var playerColor = (PlayerColor)color;
                var player = new PlayerState { Color = playerColor, Active = active.Contains(playerColor) };
                State.Players[color] = player;
                if (!player.Active) continue;
                for (var pieceIndex = 0; pieceIndex < 10; pieceIndex++)
                {
                    State.Pieces.Add(new PieceState
                    {
                        Id = id++, Owner = playerColor,
                        Position = pieceIndex == 0 ? Board.SpawnOf(playerColor) : new GridPosition(-1, -1),
                        OnBoard = pieceIndex == 0
                    });
                }
            }
        }

        public static MatchRules FromSnapshot(MatchState snapshot)
        {
            var active = snapshot.Players.Where(player => player.Active).Select(player => player.Color);
            var rules = new MatchRules(active);
            rules.State.Phase = snapshot.Phase;
            rules.State.CurrentPlayer = snapshot.CurrentPlayer;
            rules.State.TurnCount = snapshot.TurnCount;
            rules.State.DiceValue = snapshot.DiceValue;
            rules.State.RemainingSlides = snapshot.RemainingSlides;
            rules.State.SelectedPieceId = snapshot.SelectedPieceId;
            rules.State.WallPhase = snapshot.WallPhase;
            rules.State.Winner = snapshot.Winner;
            rules.State.PassWallActive = snapshot.PassWallActive;
            rules.State.EmergencyStopActive = snapshot.EmergencyStopActive;
            rules.State.CollisionCaptureActive = snapshot.CollisionCaptureActive;
            rules.State.BreakWallActive = snapshot.BreakWallActive;
            rules.State.NextItemId = snapshot.NextItemId;
            rules.State.Pieces.Clear();
            rules.State.Pieces.AddRange(snapshot.Pieces);
            rules.State.FieldItems.Clear();
            rules.State.FieldItems.AddRange(snapshot.FieldItems);
            for (var index = 0; index < 4; index++)
            {
                var target = rules.State.Players[index];
                var source = snapshot.Players[index];
                target.Active = source.Active;
                target.Eliminated = source.Eliminated;
                target.Consumed = source.Consumed;
                target.Items.Clear();
                target.Items.AddRange(source.Items);
            }
            return rules;
        }

        public void ClearEvents() => events.Clear();

        public int HandCount(PlayerColor player) => State.Pieces.Count(piece =>
            piece.Owner == player && !piece.OnBoard && !piece.Gone);

        public PieceState PieceAt(GridPosition position, int ignoreId = -1) =>
            State.Pieces.Find(piece => piece.OnBoard && piece.Id != ignoreId && piece.Position == position);

        public PieceState PieceById(int id) => State.Pieces.Find(piece => piece.Id == id);

        public bool IsWall(GridPosition position)
        {
            var cell = Board.CellAt(position);
            if (cell.Kind == CellKind.FixedWall)
                return !(position == new GridPosition(9, 9) && PieceAt(position) != null);
            return cell.Kind == CellKind.RotatingWallSlot &&
                   Board.IsScheduledWall(position, State.WallPhase) && PieceAt(position) == null;
        }

        public bool CanDeploy(PlayerColor color)
        {
            var player = State.Players[(int)color];
            if (!player.Active || player.Eliminated || HandCount(color) == 0) return false;
            var spawn = Board.SpawnOf(color);
            if (PieceAt(spawn) != null) return false;
            var candidate = new PieceState { Id = -1, Owner = color, Position = spawn, OnBoard = true };
            return Directions.Any(direction => PlanSlide(candidate, direction, false).IsLegal);
        }

        public bool HasLegalSlide(PieceState piece, bool useActiveEffects = false) =>
            piece != null && piece.OnBoard && Directions.Any(direction =>
                PlanSlide(piece, direction, useActiveEffects).IsLegal);

        public bool HasTurnChoice(PlayerColor color) => CanDeploy(color) || State.Pieces.Any(piece =>
            piece.Owner == color && HasLegalSlide(piece));

        public bool TryApplyDice(int value, out string error)
        {
            if (State.Phase != MatchPhase.WaitRoll) return Reject("Not waiting for a die roll.", out error);
            if (value < 1 || value > 6) return Reject("Die value must be between 1 and 6.", out error);
            State.DiceValue = value;
            State.RemainingSlides = value;
            State.DeadAtTurnStart = DeadPieceIds();
            State.Phase = MatchPhase.WaitSelect;
            AddEvent(MatchEventType.DiceRolled, State.CurrentPlayer, value: value);
            if (!HasTurnChoice(State.CurrentPlayer))
            {
                AddEvent(MatchEventType.NoMoves, State.CurrentPlayer);
                State.RemainingSlides = 0;
                ResolveTurn();
            }
            error = null;
            return true;
        }

        public bool TrySelectPiece(int pieceId, out string error)
        {
            if (State.Phase != MatchPhase.WaitSelect) return Reject("Not waiting for a piece.", out error);
            var piece = PieceById(pieceId);
            if (piece == null || !piece.OnBoard || piece.Owner != State.CurrentPlayer || !HasLegalSlide(piece))
                return Reject("Piece is not selectable.", out error);
            State.SelectedPieceId = pieceId;
            State.Phase = MatchPhase.Moving;
            AddEvent(MatchEventType.PieceSelected, piece.Owner, piece.Id, piece.Position);
            error = null;
            return true;
        }

        public bool TryDeploy(out int pieceId, out string error)
        {
            pieceId = -1;
            if (State.Phase != MatchPhase.WaitSelect || !CanDeploy(State.CurrentPlayer))
                return Reject("Cannot deploy at this spawn.", out error);
            var piece = State.Pieces.Find(candidate => candidate.Owner == State.CurrentPlayer &&
                                                       !candidate.OnBoard && !candidate.Gone);
            piece.Position = Board.SpawnOf(State.CurrentPlayer);
            piece.OnBoard = true;
            pieceId = piece.Id;
            // V0.3：放置不消耗滑行次数，也不强制滑动新棋——留在 WaitSelect 让玩家自由选子。
            AddEvent(MatchEventType.PieceDeployed, piece.Owner, piece.Id, piece.Position);
            error = null;
            return true;
        }

        public bool TryActivateItem(ItemType type, out string error)
        {
            if (State.Phase != MatchPhase.Moving) return Reject("Item can only be used while moving.", out error);
            var player = State.Players[(int)State.CurrentPlayer];
            if (!player.Items.Contains(type) || IsEffectActive(type))
                return Reject("Item is unavailable or already active.", out error);
            player.Items.Remove(type);
            switch (type)
            {
                case ItemType.PassWall: State.PassWallActive = true; break;
                case ItemType.EmergencyStop: State.EmergencyStopActive = true; break;
                case ItemType.CollisionCapture: State.CollisionCaptureActive = true; break;
                case ItemType.BreakWall: State.BreakWallActive = true; break;
            }
            AddEvent(MatchEventType.ItemActivated, State.CurrentPlayer, item: type);
            error = null;
            return true;
        }

        public IReadOnlyList<SlideDirection> LegalDirections()
        {
            var piece = State.SelectedPieceId.HasValue ? PieceById(State.SelectedPieceId.Value) : null;
            if (State.Phase != MatchPhase.Moving || piece == null) return Array.Empty<SlideDirection>();
            return Directions.Where(direction => PlanSlide(piece, direction, true).IsLegal).ToArray();
        }

        public SlidePlan PlanSlide(PieceState piece, SlideDirection direction, bool useActiveEffects = true)
        {
            var plan = new SlidePlan();
            if (piece == null || !piece.OnBoard) return plan;
            var position = piece.Position;
            var visited = new HashSet<GridPosition> { position };
            DirectionDelta(direction, out var columns, out var rows);

            for (var step = 0; step < 200; step++)
            {
                var next = position.Offset(columns, rows);
                if (!BoardLayout.Contains(next)) break;
                var cell = Board.CellAt(next);
                if (!CanEnter(cell, next, piece.Owner, useActiveEffects))
                {
                    var centralWall = cell.Kind == CellKind.RotatingWallSlot ||
                                      (cell.Kind == CellKind.FixedWall && next == new GridPosition(9, 9));
                    if (useActiveEffects && State.BreakWallActive && centralWall && PieceAt(next, piece.Id) == null)
                        plan.Steps.Add(new SlideStep { Position = next, BrokeWall = true });
                    break;
                }

                var occupant = PieceAt(next, piece.Id);
                if (occupant != null)
                {
                    if (useActiveEffects && State.CollisionCaptureActive && occupant.Owner != piece.Owner)
                    {
                        plan.Steps.Add(new SlideStep { Position = next, Hit = true });
                        plan.CapturedPieceId = occupant.Id;
                    }
                    break;
                }

                if (cell.Kind == CellKind.Portal && !(useActiveEffects && State.PassWallActive))
                {
                    var exit = Board.PortalExit(next);
                    if (PieceAt(exit, piece.Id) != null || visited.Contains(exit)) break;
                    visited.Add(next);
                    visited.Add(exit);
                    plan.Steps.Add(new SlideStep { Position = next, PortalEntry = true });
                    var exitStep = new SlideStep { Position = exit, PortalExit = true };
                    var exitItem = ItemAt(exit);
                    if (exitItem != null) exitStep.PickupItemId = exitItem.Id;
                    plan.Steps.Add(exitStep);
                    // Official rule: after the portal, step one cell onward in the slide
                    // direction and stop there; only if that cell is blocked does the
                    // piece stay on the exit portal cell.
                    var beyond = exit.Offset(columns, rows);
                    if (BoardLayout.Contains(beyond) &&
                        CanEnter(Board.CellAt(beyond), beyond, piece.Owner, useActiveEffects) &&
                        PieceAt(beyond, piece.Id) == null && !visited.Contains(beyond))
                    {
                        visited.Add(beyond);
                        var beyondStep = new SlideStep { Position = beyond };
                        var beyondItem = ItemAt(beyond);
                        if (beyondItem != null) beyondStep.PickupItemId = beyondItem.Id;
                        plan.Steps.Add(beyondStep);
                    }
                    break;
                }

                position = next;
                visited.Add(position);
                var normalStep = new SlideStep { Position = position };
                var item = ItemAt(position);
                if (item != null) normalStep.PickupItemId = item.Id;
                plan.Steps.Add(normalStep);
                if (item != null) break;
            }

            return plan;
        }

        public bool TrySlide(SlideDirection direction, int? stopIndex, out SlidePlan applied,
            out string error)
        {
            applied = null;
            if (State.Phase != MatchPhase.Moving || !State.SelectedPieceId.HasValue)
                return Reject("Not waiting for a slide.", out error);
            var piece = PieceById(State.SelectedPieceId.Value);
            var planned = PlanSlide(piece, direction);
            if (!planned.IsLegal) return Reject("Direction is blocked.", out error);
            if (State.EmergencyStopActive && !stopIndex.HasValue)
                return Reject("Choose a stop cell for Emergency Stop.", out error);
            if (!State.EmergencyStopActive && stopIndex.HasValue)
                return Reject("Emergency Stop is not active.", out error);
            if (stopIndex.HasValue && (stopIndex.Value < 0 || stopIndex.Value >= planned.Steps.Count))
                return Reject("Stop cell is outside this slide.", out error);

            applied = new SlidePlan();
            var lastIndex = stopIndex ?? planned.Steps.Count - 1;
            for (var index = 0; index <= lastIndex; index++) applied.Steps.Add(planned.Steps[index]);
            if (applied.Steps.Any(step => step.Hit)) applied.CapturedPieceId = planned.CapturedPieceId;
            if (applied.CapturedPieceId >= 0) CapturePiece(PieceById(applied.CapturedPieceId));

            var slideOrigin = piece.Position;
            piece.Position = applied.Destination;
            if (State.EmergencyStopActive) State.EmergencyStopActive = false;
            foreach (var step in applied.Steps)
            {
                if (step.PortalExit) AddEvent(MatchEventType.PortalUsed, piece.Owner, piece.Id, step.Position);
                if (step.PickupItemId >= 0) CollectItem(step.PickupItemId, piece);
            }
            events.Add(new MatchEvent
            {
                Type = MatchEventType.PieceSlid, Player = piece.Owner,
                PieceId = piece.Id, Position = piece.Position, Origin = slideOrigin, Slide = applied
            });

            State.RemainingSlides--;
            if (State.RemainingSlides == 0)
                ResolveTurn();
            else if (!Directions.Any(candidate => PlanSlide(piece, candidate).IsLegal))
            {
                AddEvent(MatchEventType.NoMoves, piece.Owner, piece.Id, piece.Position);
                State.RemainingSlides = 0;
                ResolveTurn();
            }
            error = null;
            return true;
        }

        private void ResolveTurn()
        {
            if (HasFive(State.CurrentPlayer))
            {
                Win(State.CurrentPlayer);
                return;
            }

            CaptureBlocks(NewlyDeadBlocks(State.DeadAtTurnStart, State.CurrentPlayer));
            if (CheckEliminations()) return;

            var beforeWall = DeadPieceIds();
            State.WallPhase ^= 1;
            State.TurnCount++;
            AddEvent(MatchEventType.WallRotated, State.CurrentPlayer, value: State.WallPhase);
            CaptureBlocks(NewlyDeadBlocks(beforeWall, null));
            if (CheckEliminations()) return;
            // 道具公平性：被埋的道具不占名额；每次换人后若场上没有可见道具则立即补刷。
            var visibleItems = State.FieldItems.Count(item => !IsWall(item.Position));
            if (visibleItems == 0 || (State.TurnCount % 2 == 0 && visibleItems < 2))
                SpawnItem();

            var next = (int)State.CurrentPlayer;
            do next = (next + 1) % 4;
            while (!State.Players[next].Active || State.Players[next].Eliminated);
            State.CurrentPlayer = (PlayerColor)next;
            State.DiceValue = null;
            State.RemainingSlides = 0;
            State.SelectedPieceId = null;
            State.PassWallActive = false;
            State.EmergencyStopActive = false;
            State.CollisionCaptureActive = false;
            State.BreakWallActive = false;
            State.DeadAtTurnStart.Clear();
            State.Phase = MatchPhase.WaitRoll;
            AddEvent(MatchEventType.TurnChanged, State.CurrentPlayer);
        }

        private bool CanEnter(CellDefinition cell, GridPosition position, PlayerColor owner, bool effects)
        {
            if (cell.Kind == CellKind.FixedWall)
                return position == new GridPosition(9, 9) && PieceAt(position) != null;
            if (cell.Kind == CellKind.RotatingWallSlot)
                return !IsWall(position) || (effects && State.PassWallActive);
            if (cell.Kind == CellKind.PlayerGate) return cell.Owner == owner;
            return true;
        }

        private static void DirectionDelta(SlideDirection direction, out int columns, out int rows)
        {
            columns = direction == SlideDirection.Left ? -1 : direction == SlideDirection.Right ? 1 : 0;
            rows = direction == SlideDirection.Up ? -1 : direction == SlideDirection.Down ? 1 : 0;
        }

        private bool IsEffectActive(ItemType type)
        {
            switch (type)
            {
                case ItemType.PassWall: return State.PassWallActive;
                case ItemType.EmergencyStop: return State.EmergencyStopActive;
                case ItemType.CollisionCapture: return State.CollisionCaptureActive;
                case ItemType.BreakWall: return State.BreakWallActive;
                default: return false;
            }
        }

        private FieldItem ItemAt(GridPosition position) => State.FieldItems.Find(item =>
            item.Position == position && !IsWall(position));

        private void CollectItem(int itemId, PieceState piece)
        {
            var item = State.FieldItems.Find(candidate => candidate.Id == itemId);
            if (item == null) return;
            State.FieldItems.Remove(item);
            var inventory = State.Players[(int)piece.Owner].Items;
            if (inventory.Count == 2) return;
            inventory.Add(item.Type);
            AddEvent(MatchEventType.ItemCollected, piece.Owner, piece.Id, piece.Position, item: item.Type);
        }

        private void SpawnItem()
        {
            // 名额只数可见（未被埋）的道具；被埋的道具不占位。
            if (State.FieldItems.Count(item => !IsWall(item.Position)) >= 2) return;
            var candidates = Board.RotatingSlots.Where(position =>
                !Board.IsScheduledWall(position, State.WallPhase) && PieceAt(position) == null &&
                State.FieldItems.All(item => item.Position != position)).ToArray();
            if (candidates.Length == 0) return;
            var item = new FieldItem
            {
                Id = State.NextItemId++,
                Type = ItemWeights[random.Next(ItemWeights.Length)],
                Position = candidates[random.Next(candidates.Length)]
            };
            State.FieldItems.Add(item);
            AddEvent(MatchEventType.ItemSpawned, State.CurrentPlayer, position: item.Position, item: item.Type);
        }

        private bool GivesLiberty(GridPosition position)
        {
            var cell = Board.CellAt(position);
            return cell.Kind == CellKind.Floor || cell.Kind == CellKind.Spawn ||
                   (cell.Kind == CellKind.RotatingWallSlot && !IsWall(position));
        }

        public List<List<PieceState>> FindDeadBlocks()
        {
            var visited = new HashSet<int>();
            var dead = new List<List<PieceState>>();
            foreach (var start in State.Pieces)
            {
                if (!start.OnBoard || !visited.Add(start.Id)) continue;
                var block = new List<PieceState>();
                var pending = new Stack<PieceState>();
                pending.Push(start);
                while (pending.Count > 0)
                {
                    var piece = pending.Pop();
                    block.Add(piece);
                    foreach (var direction in Directions)
                    {
                        DirectionDelta(direction, out var columns, out var rows);
                        var adjacent = piece.Position.Offset(columns, rows);
                        var neighbor = BoardLayout.Contains(adjacent) ? PieceAt(adjacent) : null;
                        if (neighbor != null && neighbor.Owner == piece.Owner && visited.Add(neighbor.Id))
                            pending.Push(neighbor);
                    }
                }

                var liberties = new HashSet<GridPosition>();
                foreach (var piece in block)
                    foreach (var direction in Directions)
                    {
                        DirectionDelta(direction, out var columns, out var rows);
                        var adjacent = piece.Position.Offset(columns, rows);
                        if (BoardLayout.Contains(adjacent) && PieceAt(adjacent) == null && GivesLiberty(adjacent))
                            liberties.Add(adjacent);
                    }
                if (liberties.Count == 0) dead.Add(block);
            }
            return dead;
        }

        private HashSet<int> DeadPieceIds() => new HashSet<int>(FindDeadBlocks()
            .SelectMany(block => block).Select(piece => piece.Id));

        private List<List<PieceState>> NewlyDeadBlocks(HashSet<int> before, PlayerColor? actor) =>
            FindDeadBlocks().Where(block =>
                (!actor.HasValue || block[0].Owner != actor.Value) &&
                block.Any(piece => !before.Contains(piece.Id))).ToList();

        private void CaptureBlocks(IEnumerable<List<PieceState>> blocks)
        {
            foreach (var block in blocks)
                foreach (var piece in block)
                    CapturePiece(piece);
        }

        private void CapturePiece(PieceState piece)
        {
            if (piece == null || !piece.OnBoard) return;
            piece.OnBoard = false;
            piece.Gone = true;
            State.Players[(int)piece.Owner].Consumed++;
            AddEvent(MatchEventType.PieceCaptured, piece.Owner, piece.Id, piece.Position);
        }

        private bool CheckEliminations()
        {
            foreach (var player in State.Players)
            {
                if (!player.Active || player.Eliminated || player.Consumed < 10) continue;
                player.Eliminated = true;
                foreach (var piece in State.Pieces.Where(piece => piece.Owner == player.Color && piece.OnBoard))
                    piece.OnBoard = false;
                AddEvent(MatchEventType.PlayerEliminated, player.Color);
            }
            var survivors = State.Players.Where(player => player.Active && !player.Eliminated).ToArray();
            if (survivors.Length != 1) return false;
            Win(survivors[0].Color);
            return true;
        }

        private bool HasFive(PlayerColor color)
        {
            var directions = new[] { new[] { 1, 0 }, new[] { 0, 1 },
                                     new[] { 1, 1 }, new[] { 1, -1 } };
            foreach (var piece in State.Pieces.Where(piece => piece.OnBoard && piece.Owner == color))
                foreach (var direction in directions)
                {
                    var count = 1;
                    foreach (var sign in new[] { -1, 1 })
                    {
                        var next = piece.Position.Offset(direction[0] * sign, direction[1] * sign);
                        while (BoardLayout.Contains(next) && PieceAt(next)?.Owner == color)
                        {
                            count++;
                            next = next.Offset(direction[0] * sign, direction[1] * sign);
                        }
                    }
                    if (count >= 5) return true;
                }
            return false;
        }

        // Four-run warning (V0.3 §7): pieces in exactly-4 contiguous lines.
        // Five or more already wins, so only exact fours remain on the board.
        public HashSet<int> FindFourRuns()
        {
            var ids = new HashSet<int>();
            var directions = new[] { new[] { 1, 0 }, new[] { 0, 1 },
                                     new[] { 1, 1 }, new[] { 1, -1 } };
            foreach (var color in Enum.GetValues(typeof(PlayerColor)).Cast<PlayerColor>())
            {
                var player = State.Players[(int)color];
                if (!player.Active || player.Eliminated) continue;
                var mine = State.Pieces
                    .Where(piece => piece.OnBoard && piece.Owner == color)
                    .ToDictionary(piece => piece.Position, piece => piece);
                foreach (var pair in mine)
                    foreach (var direction in directions)
                    {
                        if (mine.ContainsKey(pair.Key.Offset(-direction[0], -direction[1])))
                            continue; // Only count from the start of each run.
                        var run = new List<PieceState> { pair.Value };
                        var next = pair.Key.Offset(direction[0], direction[1]);
                        while (mine.TryGetValue(next, out var chained))
                        {
                            run.Add(chained);
                            next = next.Offset(direction[0], direction[1]);
                        }
                        if (run.Count == 4)
                            foreach (var piece in run) ids.Add(piece.Id);
                    }
            }
            return ids;
        }

        // Wall-kill preview (V0.3 §7): blocks alive under the current wall phase that
        // would lose their last liberty if the phase flipped. Blocks already dead now
        // are excluded — they are handled by the normal capture passes.
        public HashSet<int> FindWallKillPreview()
        {
            var nowDead = new HashSet<int>(FindDeadBlocks()
                .SelectMany(block => block).Select(piece => piece.Id));
            State.WallPhase ^= 1;
            var nextDead = new HashSet<int>(FindDeadBlocks()
                .SelectMany(block => block).Select(piece => piece.Id));
            State.WallPhase ^= 1;
            nextDead.ExceptWith(nowDead);
            return nextDead;
        }

        private void Win(PlayerColor color)
        {
            State.Winner = color;
            State.Phase = MatchPhase.GameOver;
            AddEvent(MatchEventType.Victory, color);
        }

        private void AddEvent(MatchEventType type, PlayerColor player, int pieceId = -1,
            GridPosition position = default, int value = 0, ItemType item = default) =>
            events.Add(new MatchEvent
            {
                Type = type, Player = player, PieceId = pieceId,
                Position = position, Value = value, Item = item
            });

        private static bool Reject(string message, out string error)
        {
            error = message;
            return false;
        }
    }
}

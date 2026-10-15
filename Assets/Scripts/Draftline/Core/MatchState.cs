using System.Collections.Generic;

namespace Draftline.Core
{
    public enum MatchPhase { WaitRoll, WaitSelect, Moving, GameOver }
    public enum SlideDirection { Up, Down, Left, Right }
    public enum ItemType { PassWall = 1, EmergencyStop = 2, CollisionCapture = 3, BreakWall = 4 }
    public enum CaptureCause { Unknown, Collision, Surround, WallChange }
    public enum MatchEventType
    {
        DiceRolled, PieceSelected, PieceDeployed, PieceSlid, PortalUsed,
        ItemCollected, ItemActivated, PieceCaptured, NoMoves, WallRotated,
        ItemSpawned, PlayerEliminated, TurnChanged, Victory
    }

    public sealed class PieceState
    {
        public int Id;
        public PlayerColor Owner;
        public GridPosition Position;
        public bool OnBoard;
        public bool Gone;
    }

    public sealed class PlayerState
    {
        public PlayerColor Color;
        public bool Active;
        public bool Eliminated;
        public int Consumed;
        public readonly List<ItemType> Items = new List<ItemType>(2);
    }

    public sealed class FieldItem
    {
        public int Id;
        public ItemType Type;
        public GridPosition Position;
    }

    public sealed class MatchEvent
    {
        public MatchEventType Type;
        public PlayerColor Player;
        public int PieceId = -1;
        public GridPosition Position;
        public GridPosition Origin; // PieceSlid: cell the piece started this slide from.
        public int Value;
        public ItemType Item;
        public SlidePlan Slide;
        public CaptureCause Cause;
    }

    public sealed class SlideStep
    {
        public GridPosition Position;
        public bool PortalEntry;
        public bool PortalExit;
        public bool Hit;
        public bool BrokeWall;
        public int PickupItemId = -1;
    }

    public sealed class SlidePlan
    {
        public readonly List<SlideStep> Steps = new List<SlideStep>();
        public int CapturedPieceId = -1;
        public bool IsLegal => Steps.Count > 0;
        public GridPosition Destination => Steps[Steps.Count - 1].Position;
    }

    public sealed class MatchState
    {
        public int Revision;
        public uint RandomState;
        public MatchPhase Phase;
        public PlayerColor CurrentPlayer;
        public int TurnCount;
        public int? DiceValue;
        public int RemainingSlides;
        public int? SelectedPieceId;
        public int WallPhase;
        public PlayerColor? Winner;
        public bool PassWallActive;
        public bool EmergencyStopActive;
        public bool CollisionCaptureActive;
        public bool BreakWallActive;
        public int NextItemId = 1;
        public readonly PlayerState[] Players = new PlayerState[4];
        public readonly List<PieceState> Pieces = new List<PieceState>(40);
        public readonly List<FieldItem> FieldItems = new List<FieldItem>(2);
        public HashSet<int> DeadAtTurnStart = new HashSet<int>();

        public MatchState DeepClone()
        {
            var copy = new MatchState
            {
                Revision = Revision, RandomState = RandomState, Phase = Phase,
                CurrentPlayer = CurrentPlayer, TurnCount = TurnCount, DiceValue = DiceValue,
                RemainingSlides = RemainingSlides, SelectedPieceId = SelectedPieceId,
                WallPhase = WallPhase, Winner = Winner, NextItemId = NextItemId,
                PassWallActive = PassWallActive, EmergencyStopActive = EmergencyStopActive,
                CollisionCaptureActive = CollisionCaptureActive, BreakWallActive = BreakWallActive,
                DeadAtTurnStart = new HashSet<int>(DeadAtTurnStart)
            };
            for (var i = 0; i < Players.Length; i++)
            {
                var p = Players[i];
                if (p == null) continue;
                copy.Players[i] = new PlayerState
                    { Color = p.Color, Active = p.Active, Eliminated = p.Eliminated, Consumed = p.Consumed };
                copy.Players[i].Items.AddRange(p.Items);
            }
            foreach (var p in Pieces)
                copy.Pieces.Add(new PieceState
                    { Id = p.Id, Owner = p.Owner, Position = p.Position, OnBoard = p.OnBoard, Gone = p.Gone });
            foreach (var item in FieldItems)
                copy.FieldItems.Add(new FieldItem { Id = item.Id, Type = item.Type, Position = item.Position });
            return copy;
        }
    }
}

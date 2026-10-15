using System;
using System.Linq;
using Draftline.Core;
using UnityEngine;

namespace Draftline.Unity
{
    [Serializable]
    public sealed class MatchSnapshot
    {
        public int phase, current, turn, dice, remaining, selected, wall, winner, nextItem;
        public int revision;
        public uint randomState;
        public int[] deadAtTurnStart;
        public bool passWall, emergencyStop, collisionCapture, breakWall;
        public PlayerPacket[] players;
        public PiecePacket[] pieces;
        public ItemPacket[] items;

        public static MatchSnapshot Capture(MatchState state) => new MatchSnapshot
        {
            revision = state.Revision,
            randomState = state.RandomState,
            deadAtTurnStart = state.DeadAtTurnStart.ToArray(),
            phase = (int)state.Phase,
            current = (int)state.CurrentPlayer,
            turn = state.TurnCount,
            dice = state.DiceValue ?? -1,
            remaining = state.RemainingSlides,
            selected = state.SelectedPieceId ?? -1,
            wall = state.WallPhase,
            winner = state.Winner.HasValue ? (int)state.Winner.Value : -1,
            nextItem = state.NextItemId,
            passWall = state.PassWallActive,
            emergencyStop = state.EmergencyStopActive,
            collisionCapture = state.CollisionCaptureActive,
            breakWall = state.BreakWallActive,
            players = state.Players.Select(player => new PlayerPacket
            {
                color = (int)player.Color, active = player.Active,
                eliminated = player.Eliminated, consumed = player.Consumed,
                inventory = player.Items.Select(item => (int)item).ToArray()
            }).ToArray(),
            pieces = state.Pieces.Select(piece => new PiecePacket
            {
                id = piece.Id, owner = (int)piece.Owner,
                column = piece.Position.Column, row = piece.Position.Row,
                onBoard = piece.OnBoard, gone = piece.Gone
            }).ToArray(),
            items = state.FieldItems.Select(item => new ItemPacket
            {
                id = item.Id, type = (int)item.Type,
                column = item.Position.Column, row = item.Position.Row
            }).ToArray()
        };

        public MatchState Restore()
        {
            var state = new MatchState
            {
                Revision = revision,
                RandomState = randomState,
                Phase = (MatchPhase)phase,
                CurrentPlayer = (PlayerColor)current,
                TurnCount = turn,
                DiceValue = dice < 0 ? (int?)null : dice,
                RemainingSlides = remaining,
                SelectedPieceId = selected < 0 ? (int?)null : selected,
                WallPhase = wall,
                Winner = winner < 0 ? (PlayerColor?)null : (PlayerColor)winner,
                NextItemId = nextItem,
                PassWallActive = passWall,
                EmergencyStopActive = emergencyStop,
                CollisionCaptureActive = collisionCapture,
                BreakWallActive = breakWall
            };
            if (deadAtTurnStart != null)
                state.DeadAtTurnStart.UnionWith(deadAtTurnStart);
            foreach (var packet in players)
            {
                var player = new PlayerState
                {
                    Color = (PlayerColor)packet.color, Active = packet.active,
                    Eliminated = packet.eliminated, Consumed = packet.consumed
                };
                if (packet.inventory != null)
                    foreach (var item in packet.inventory) player.Items.Add((ItemType)item);
                state.Players[packet.color] = player;
            }
            foreach (var packet in pieces)
                state.Pieces.Add(new PieceState
                {
                    Id = packet.id, Owner = (PlayerColor)packet.owner,
                    Position = new GridPosition(packet.column, packet.row),
                    OnBoard = packet.onBoard, Gone = packet.gone
                });
            foreach (var packet in items)
                state.FieldItems.Add(new FieldItem
                {
                    Id = packet.id, Type = (ItemType)packet.type,
                    Position = new GridPosition(packet.column, packet.row)
                });
            return state;
        }
    }

    [Serializable]
    public sealed class RoomStatePacket
    {
        public string sessionId, matchId;
        public string themeId;
        public int themeVersion;
        public RoomMemberPacket[] members;
        public int[] controls;
        public bool fillComputers;
        public int sequence;
        public int seatsRequested;
        public bool started;
        public string seats;
        public string snapshot;
        public string slide;
        public MatchEventPacket[] events;
    }

    [Serializable]
    public sealed class RoomMemberPacket
    {
        public ulong clientId;
        public int color;
        public bool connected, ready;
    }

    [Serializable]
    public sealed class MatchEventPacket
    {
        public int type, player, piece, column, row, originColumn, originRow, value, item, cause;
        public SlidePacket slide;
        public static MatchEventPacket Capture(MatchEvent e) => new MatchEventPacket
        {
            type = (int)e.Type, player = (int)e.Player, piece = e.PieceId, column = e.Position.Column,
            row = e.Position.Row, originColumn = e.Origin.Column, originRow = e.Origin.Row,
            value = e.Value, item = (int)e.Item, cause = (int)e.Cause, slide = e.Slide == null ? null : SlidePacket.Capture(e)
        };
        public MatchEvent Restore() => new MatchEvent
        {
            Type = (MatchEventType)type, Player = (PlayerColor)player, PieceId = piece,
            Position = new GridPosition(column,row), Origin = new GridPosition(originColumn,originRow),
            Value = value, Item = (ItemType)item, Cause = (CaptureCause)cause, Slide = slide?.Restore()
        };
    }

    [Serializable]
    public sealed class PlayerPacket
    {
        public int color, consumed;
        public bool active, eliminated;
        public int[] inventory;
    }

    [Serializable]
    public sealed class PiecePacket
    {
        public int id, owner, column, row;
        public bool onBoard, gone;
    }

    [Serializable]
    public sealed class ItemPacket
    {
        public int id, type, column, row;
    }

    [Serializable]
    public sealed class SlidePacket
    {
        public int pieceId;
        public int capturedId;
        public SlideStepPacket[] steps;

        public static SlidePacket Capture(MatchEvent item) => new SlidePacket
        {
            pieceId = item.PieceId,
            capturedId = item.Slide.CapturedPieceId,
            steps = item.Slide.Steps.Select(step => new SlideStepPacket
            {
                column = step.Position.Column, row = step.Position.Row,
                portalEntry = step.PortalEntry, portalExit = step.PortalExit,
                hit = step.Hit, brokeWall = step.BrokeWall,
                pickupId = step.PickupItemId
            }).ToArray()
        };

        public SlidePlan Restore()
        {
            var plan = new SlidePlan { CapturedPieceId = capturedId };
            foreach (var step in steps)
                plan.Steps.Add(new SlideStep
                {
                    Position = new GridPosition(step.column, step.row),
                    PortalEntry = step.portalEntry, PortalExit = step.portalExit,
                    Hit = step.hit, BrokeWall = step.brokeWall,
                    PickupItemId = step.pickupId
                });
            return plan;
        }
    }

    [Serializable]
    public sealed class SlideStepPacket
    {
        public int column, row, pickupId;
        public bool portalEntry, portalExit, hit, brokeWall;
    }
}

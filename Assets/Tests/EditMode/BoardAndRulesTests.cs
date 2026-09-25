using System.Linq;
using Draftline.Core;
using Draftline.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Draftline.Tests
{
    public sealed class BoardAndRulesTests
    {
        [Test]
        public void ExistingSceneCoordinatesMatchRuleAnchors()
        {
            var board = new BoardLayout();
            Assert.AreEqual(new GridPosition(1, 1), board.SpawnOf(PlayerColor.Red));
            Assert.AreEqual(new Vector3(-8, 0.6f, -8), BoardCoordinates.ToWorld(board.SpawnOf(PlayerColor.Red)));
            Assert.AreEqual(new Vector3(-8, 0.6f, 8), BoardCoordinates.ToWorld(board.SpawnOf(PlayerColor.Blue)));
            Assert.AreEqual(new GridPosition(7, 14), board.PortalExit(new GridPosition(7, 4)));
            Assert.AreEqual(8, board.RotatingSlots.Count);
            Assert.IsTrue(BoardCoordinates.TryFromWorld(new Vector3(5, 1, -2), out var portal));
            Assert.AreEqual(new GridPosition(7, 14), portal);
        }

        [Test]
        public void RotatingWallAndPortalChangeTheSlideDestination()
        {
            var rules = new MatchRules(new[] { PlayerColor.Red, PlayerColor.Blue }, 1);
            var red = rules.State.Pieces.First(piece => piece.Owner == PlayerColor.Red);
            red.Position = new GridPosition(7, 6);

            Assert.AreEqual(new GridPosition(7, 7),
                rules.PlanSlide(red, SlideDirection.Down).Destination);
            rules.State.WallPhase = 1;
            var throughPortal = rules.PlanSlide(red, SlideDirection.Down);
            Assert.AreEqual(new GridPosition(7, 4), throughPortal.Destination);
            Assert.IsTrue(throughPortal.Steps.Any(step => step.PortalExit));
        }

        [Test]
        public void FullyBlockedPieceIsNotConsumedAndTurnPasses()
        {
            var rules = new MatchRules(new[] { PlayerColor.Red, PlayerColor.Blue }, 1);
            ClearBoard(rules);
            var red = Place(rules, PlayerColor.Red, 7, 7);
            Place(rules, PlayerColor.Blue, 6, 7);
            Place(rules, PlayerColor.Blue, 8, 7);
            Place(rules, PlayerColor.Blue, 7, 6);
            Place(rules, PlayerColor.Blue, 7, 8);
            foreach (var piece in rules.State.Pieces.Where(piece => piece.Owner == PlayerColor.Red && !piece.OnBoard))
                piece.Gone = true;

            Assert.IsFalse(rules.HasLegalSlide(red));
            Assert.IsTrue(rules.TryApplyDice(3, out var error), error);
            Assert.IsTrue(red.OnBoard);
            Assert.AreEqual(0, rules.State.Players[(int)PlayerColor.Red].Consumed);
            Assert.AreEqual(PlayerColor.Blue, rules.State.CurrentPlayer);
            Assert.AreEqual(MatchPhase.WaitRoll, rules.State.Phase);
        }

        [Test]
        public void RaisedWallCapturesImmediatelyAfterTheTurn()
        {
            var rules = new MatchRules(new[] { PlayerColor.Red, PlayerColor.Blue }, 1);
            ClearBoard(rules);
            var mover = Place(rules, PlayerColor.Red, 1, 1);
            Place(rules, PlayerColor.Red, 6, 7);
            Place(rules, PlayerColor.Red, 8, 7);
            Place(rules, PlayerColor.Red, 7, 6);
            var blue = Place(rules, PlayerColor.Blue, 7, 7);
            rules.State.WallPhase = 1;
            Assert.IsFalse(rules.FindDeadBlocks().Any(block => block.Contains(blue)));

            Assert.IsTrue(rules.TryApplyDice(1, out var error), error);
            Assert.IsTrue(rules.TrySelectPiece(mover.Id, out error), error);
            Assert.IsTrue(rules.TrySlide(SlideDirection.Right, null, out var path, out error), error);
            Assert.IsFalse(blue.OnBoard);
            Assert.AreEqual(1, rules.State.Players[(int)PlayerColor.Blue].Consumed);
            Assert.IsTrue(rules.Events.Any(item => item.Type == MatchEventType.WallRotated));
        }

        [Test]
        public void CenterWallReturnsWhenBreakerLeaves()
        {
            var rules = new MatchRules(new[] { PlayerColor.Red, PlayerColor.Blue }, 1);
            ClearBoard(rules);
            var red = Place(rules, PlayerColor.Red, 9, 8);
            rules.State.Players[(int)PlayerColor.Red].Items.Add(ItemType.BreakWall);

            Assert.IsTrue(rules.TryApplyDice(2, out var error), error);
            Assert.IsTrue(rules.TrySelectPiece(red.Id, out error), error);
            Assert.IsTrue(rules.TryActivateItem(ItemType.BreakWall, out error), error);
            Assert.IsTrue(rules.TrySlide(SlideDirection.Down, null, out var first, out error), error);
            Assert.AreEqual(new GridPosition(9, 9), red.Position);
            Assert.IsFalse(rules.IsWall(red.Position));
            Assert.IsTrue(rules.TrySlide(SlideDirection.Up, null, out var second, out error), error);
            Assert.IsTrue(rules.IsWall(new GridPosition(9, 9)));
        }

        [Test]
        public void NewEnemyZeroLibertyIsCapturedButOldZeroLibertyIsNot()
        {
            var rules = new MatchRules(new[] { PlayerColor.Red, PlayerColor.Blue }, 2);
            ClearBoard(rules);
            var mover = Place(rules, PlayerColor.Red, 1, 1);
            Place(rules, PlayerColor.Red, 6, 7);
            Place(rules, PlayerColor.Red, 8, 7);
            Place(rules, PlayerColor.Red, 7, 6);
            var victim = Place(rules, PlayerColor.Blue, 7, 7);
            rules.State.WallPhase = 1; // (7,8) is open before the red piece seals it.
            Assert.IsFalse(rules.FindDeadBlocks().Any(block => block.Contains(victim)));

            Assert.IsTrue(rules.TryApplyDice(1, out var error), error);
            var blocker = rules.State.Pieces.First(piece => piece.Owner == PlayerColor.Red && !piece.OnBoard);
            blocker.OnBoard = true;
            blocker.Position = new GridPosition(7, 8);
            Assert.IsTrue(rules.TrySelectPiece(mover.Id, out error), error);
            Assert.IsTrue(rules.TrySlide(SlideDirection.Right, null, out _, out error), error);
            Assert.IsFalse(victim.OnBoard);
            Assert.AreEqual(1, rules.State.Players[(int)PlayerColor.Blue].Consumed);

            var oldZero = Place(rules, PlayerColor.Blue, 7, 7);
            rules.State.CurrentPlayer = PlayerColor.Red;
            rules.State.Phase = MatchPhase.WaitRoll;
            Assert.IsTrue(rules.TryApplyDice(1, out error), error);
            Assert.IsTrue(rules.TrySelectPiece(mover.Id, out error), error);
            Assert.IsTrue(rules.TrySlide(SlideDirection.Left, null, out _, out error), error);
            Assert.IsTrue(oldZero.OnBoard, "A pre-existing zero-liberty block is not recaptured by polling.");
        }

        [Test]
        public void FiveInARowWinsBeforeCapture()
        {
            var rules = new MatchRules(new[] { PlayerColor.Red, PlayerColor.Blue }, 3);
            ClearBoard(rules);
            for (var column = 5; column <= 8; column++) Place(rules, PlayerColor.Red, column, 8);
            var completingPiece = Place(rules, PlayerColor.Red, 9, 7);
            Place(rules, PlayerColor.Blue, 12, 9);

            Assert.IsTrue(rules.TryApplyDice(1, out var error), error);
            Assert.IsTrue(rules.TrySelectPiece(completingPiece.Id, out error), error);
            Assert.IsTrue(rules.TrySlide(SlideDirection.Down, null, out _, out error), error);
            Assert.AreEqual(new GridPosition(9, 8), completingPiece.Position);
            Assert.AreEqual(MatchPhase.GameOver, rules.State.Phase);
            Assert.AreEqual(PlayerColor.Red, rules.State.Winner);
        }

        [Test]
        public void CollisionItemConsumesOpponentOnContact()
        {
            var rules = new MatchRules(new[] { PlayerColor.Red, PlayerColor.Blue }, 4);
            ClearBoard(rules);
            var red = Place(rules, PlayerColor.Red, 5, 7);
            var blue = Place(rules, PlayerColor.Blue, 8, 7);
            rules.State.Players[(int)PlayerColor.Red].Items.Add(ItemType.CollisionCapture);

            Assert.IsTrue(rules.TryApplyDice(2, out var error), error);
            Assert.IsTrue(rules.TrySelectPiece(red.Id, out error), error);
            Assert.IsTrue(rules.TryActivateItem(ItemType.CollisionCapture, out error), error);
            Assert.IsTrue(rules.TrySlide(SlideDirection.Right, null, out _, out error), error);
            Assert.AreEqual(new GridPosition(8, 7), red.Position);
            Assert.IsFalse(blue.OnBoard);
            Assert.IsTrue(blue.Gone);
            Assert.AreEqual(1, rules.State.Players[(int)PlayerColor.Blue].Consumed);
        }

        [Test]
        public void NetworkSnapshotPreservesFourPlayerRuleState()
        {
            var rules = new MatchRules(new[] { PlayerColor.Red, PlayerColor.Blue,
                PlayerColor.Green, PlayerColor.Yellow }, 5);
            Assert.IsTrue(rules.TryApplyDice(4, out var error), error);
            var red = rules.State.Pieces.First(piece => piece.Owner == PlayerColor.Red && piece.OnBoard);
            Assert.IsTrue(rules.TrySelectPiece(red.Id, out error), error);
            var json = JsonUtility.ToJson(MatchSnapshot.Capture(rules.State));
            var restored = JsonUtility.FromJson<MatchSnapshot>(json).Restore();
            var copy = MatchRules.FromSnapshot(restored);
            Assert.AreEqual(rules.State.Phase, copy.State.Phase);
            Assert.AreEqual(rules.State.CurrentPlayer, copy.State.CurrentPlayer);
            Assert.AreEqual(4, copy.State.DiceValue);
            Assert.AreEqual(red.Id, copy.State.SelectedPieceId);
            Assert.AreEqual(40, copy.State.Pieces.Count);
            Assert.AreEqual(rules.State.Pieces.Count(piece => piece.OnBoard),
                copy.State.Pieces.Count(piece => piece.OnBoard));
        }

        private static void ClearBoard(MatchRules rules)
        {
            foreach (var piece in rules.State.Pieces)
            {
                piece.OnBoard = false;
                piece.Gone = false;
                piece.Position = new GridPosition(-1, -1);
            }
        }

        private static PieceState Place(MatchRules rules, PlayerColor color, int column, int row)
        {
            var piece = rules.State.Pieces.First(candidate => candidate.Owner == color && !candidate.OnBoard);
            piece.OnBoard = true;
            piece.Position = new GridPosition(column, row);
            return piece;
        }
    }
}

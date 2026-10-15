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
            // 传送门出口 (7,4) 沿滑行方向再走出相邻一格 (7,5) 停下。
            Assert.AreEqual(new GridPosition(7, 5), throughPortal.Destination);
            Assert.IsTrue(throughPortal.Steps.Any(step => step.PortalExit));
        }

        [Test]
        public void BlockedPortalMouthStopsBeforeTheEntrance()
        {
            var rules = new MatchRules(new[] { PlayerColor.Red, PlayerColor.Blue }, 14);
            ClearBoard(rules);
            var red = Place(rules, PlayerColor.Red, 7, 6);
            rules.State.WallPhase = 1;
            Place(rules, PlayerColor.Blue, 7, 5); // 出口 (7,4) 沿下滑方向的相邻格被占。
            var plan = rules.PlanSlide(red, SlideDirection.Down);
            Assert.AreEqual(new GridPosition(7, 13), plan.Destination,
                "A blocked landing mouth prevents the entire teleport.");
            Assert.IsFalse(plan.Steps.Any(step => step.PortalExit));
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
            Assert.IsFalse(blue.Gone,"Captured pieces return to the deployable pool.");
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
            Assert.AreEqual(32, copy.State.Pieces.Count);
            Assert.AreEqual(rules.State.Pieces.Count(piece => piece.OnBoard),
                copy.State.Pieces.Count(piece => piece.OnBoard));
        }

        [Test]
        public void SelfCaptureIsForbiddenEvenWhenOwnBlockLosesLastLiberty()
        {
            // V0.3 §4 禁止自吃：当前玩家的行动使自己棋块零气，棋块仍保留。
            var rules = new MatchRules(new[] { PlayerColor.Red, PlayerColor.Blue }, 6);
            ClearBoard(rules);
            // 红棋块 (2,1)(3,1) 唯一的气是出生点 (1,1)；蓝棋封住其余出口。
            var blockA = Place(rules, PlayerColor.Red, 2, 1);
            var blockB = Place(rules, PlayerColor.Red, 3, 1);
            Place(rules, PlayerColor.Blue, 2, 2);
            Place(rules, PlayerColor.Blue, 3, 2);
            Place(rules, PlayerColor.Blue, 1, 2);
            var mover = Place(rules, PlayerColor.Red, 5, 7);

            Assert.IsTrue(rules.TryApplyDice(1, out var error), error);
            // 红方自己的行动（此处直接落子模拟）使己方棋块失去最后一口气。
            var ownSeal = rules.State.Pieces.First(piece => piece.Owner == PlayerColor.Red && !piece.OnBoard);
            ownSeal.OnBoard = true;
            ownSeal.Position = new GridPosition(1, 1);
            Assert.IsTrue(rules.FindDeadBlocks().Any(block => block.Contains(blockA)),
                "Setup should make the red block zero-liberty.");

            Assert.IsTrue(rules.TrySelectPiece(mover.Id, out error), error);
            Assert.IsTrue(rules.TrySlide(SlideDirection.Right, null, out _, out error), error);
            Assert.IsTrue(blockA.OnBoard && blockB.OnBoard && ownSeal.OnBoard,
                "The current player's own zero-liberty block must survive (no self-capture).");
            Assert.AreEqual(0, rules.State.Players[(int)PlayerColor.Red].Consumed);
        }

        [Test]
        public void NoMovesSkipStillRotatesWallsAndCountsTurn()
        {
            // V0.3 §3.7：无人可行动自动跳过时，旋转墙仍切换、回合计数仍推进、棋子不消耗。
            var rules = new MatchRules(new[] { PlayerColor.Red, PlayerColor.Blue }, 7);
            ClearBoard(rules);
            Place(rules, PlayerColor.Red, 7, 7);
            Place(rules, PlayerColor.Blue, 6, 7);
            Place(rules, PlayerColor.Blue, 8, 7);
            Place(rules, PlayerColor.Blue, 7, 6);
            Place(rules, PlayerColor.Blue, 7, 8);
            foreach (var piece in rules.State.Pieces.Where(piece => piece.Owner == PlayerColor.Red && !piece.OnBoard))
                piece.Gone = true;
            Assert.AreEqual(0, rules.State.WallPhase);
            Assert.AreEqual(0, rules.State.TurnCount);

            Assert.IsTrue(rules.TryApplyDice(4, out var error), error);
            Assert.AreEqual(1, rules.State.WallPhase, "A skipped turn still flips the rotating walls.");
            Assert.AreEqual(1, rules.State.TurnCount, "A skipped turn still advances the turn counter.");
            Assert.AreEqual(0, rules.State.Players[(int)PlayerColor.Red].Consumed);
            Assert.AreEqual(PlayerColor.Blue, rules.State.CurrentPlayer);
        }

        [Test]
        public void InactiveCornerGatesBlockEveryoneInTwoPlayerMatch()
        {
            // V0.3 §2：未参战角落的门仍保留，参战棋子均不能通过。
            var rules = new MatchRules(new[] { PlayerColor.Red, PlayerColor.Blue }, 8);
            ClearBoard(rules);
            var blue = Place(rules, PlayerColor.Blue, 13, 14);
            Assert.IsFalse(rules.PlanSlide(blue, SlideDirection.Down).IsLegal,
                "Green is not playing; its gate at (13,15) must block blue as well.");
        }

        [Test]
        public void FirstPlayerIsTheEarliestActiveCorner()
        {
            // V0.3 §1：首手是参战角落中按红、蓝、绿、黄顺序最靠前的一方。
            Assert.AreEqual(PlayerColor.Blue,
                new MatchRules(new[] { PlayerColor.Green, PlayerColor.Blue }, 9).State.CurrentPlayer);
            Assert.AreEqual(PlayerColor.Red,
                new MatchRules(new[] { PlayerColor.Yellow, PlayerColor.Red }, 9).State.CurrentPlayer);
            Assert.AreEqual(PlayerColor.Green,
                new MatchRules(new[] { PlayerColor.Yellow, PlayerColor.Green }, 9).State.CurrentPlayer);
        }

        [Test]
        public void EmergencyStopAppliesToOnlyOneSlide()
        {
            // V0.3 §5：急停只对下一次滑行生效，用过一次即失效。
            var rules = new MatchRules(new[] { PlayerColor.Red, PlayerColor.Blue }, 10);
            ClearBoard(rules);
            var red = Place(rules, PlayerColor.Red, 7, 9);
            rules.State.WallPhase = 1; // (7,8) 槽位为空。
            rules.State.Players[(int)PlayerColor.Red].Items.Add(ItemType.EmergencyStop);

            Assert.IsTrue(rules.TryApplyDice(3, out var error), error);
            Assert.IsTrue(rules.TrySelectPiece(red.Id, out error), error);
            Assert.IsTrue(rules.TryActivateItem(ItemType.EmergencyStop, out error), error);
            Assert.IsTrue(rules.TrySlide(SlideDirection.Up, 0, out _, out error), error);
            Assert.AreEqual(new GridPosition(7, 8), red.Position);
            Assert.IsFalse(rules.State.EmergencyStopActive, "Emergency Stop is consumed by the first slide.");

            Assert.IsFalse(rules.TrySlide(SlideDirection.Up, 1, out _, out error),
                "A stop index is rejected once Emergency Stop is spent.");
            Assert.IsTrue(rules.TrySlide(SlideDirection.Up, null, out _, out error), error);
            Assert.AreEqual(new GridPosition(7, 13), red.Position,
                "The following slide runs the full course: through the portal and onto the cell adjacent to the exit.");
        }

        [Test]
        public void CollisionCaptureIsImmediateBeforeTurnResolution()
        {
            // V0.3 §6：撞吃在碰撞时立即移除被撞棋，不等待回合末的气判定。
            var rules = new MatchRules(new[] { PlayerColor.Red, PlayerColor.Blue }, 11);
            ClearBoard(rules);
            var red = Place(rules, PlayerColor.Red, 5, 7);
            var blue = Place(rules, PlayerColor.Blue, 8, 7);
            rules.State.Players[(int)PlayerColor.Red].Items.Add(ItemType.CollisionCapture);

            Assert.IsTrue(rules.TryApplyDice(2, out var error), error);
            Assert.IsTrue(rules.TrySelectPiece(red.Id, out error), error);
            Assert.IsTrue(rules.TryActivateItem(ItemType.CollisionCapture, out error), error);
            Assert.IsTrue(rules.TrySlide(SlideDirection.Right, null, out _, out error), error);
            Assert.IsTrue(!blue.Gone && !blue.OnBoard, "The hit piece leaves the board and returns to its pool.");
            Assert.AreEqual(MatchPhase.Moving, rules.State.Phase);
            Assert.AreEqual(1, rules.State.RemainingSlides);
            Assert.AreEqual(PlayerColor.Red, rules.State.CurrentPlayer,
                "The turn is not resolved yet; capture did not wait for the liberty phase.");
        }

        [Test]
        public void FourRunWarningFindsOnlyExactFours()
        {
            var rules = new MatchRules(new[] { PlayerColor.Red, PlayerColor.Blue }, 12);
            ClearBoard(rules);
            for (var column = 7; column <= 10; column++) Place(rules, PlayerColor.Red, column, 9);
            Assert.AreEqual(4, rules.FindFourRuns().Count);
            ClearBoard(rules);
            Place(rules, PlayerColor.Red, 7, 9);
            Place(rules, PlayerColor.Red, 8, 9);
            Place(rules, PlayerColor.Red, 10, 9);
            Place(rules, PlayerColor.Red, 12, 9);
            Assert.AreEqual(0, rules.FindFourRuns().Count, "Disconnected fours are not a run.");
            ClearBoard(rules);
            for (var column = 7; column <= 11; column++) Place(rules, PlayerColor.Red, column, 9);
            Assert.AreEqual(0, rules.FindFourRuns().Count, "Five in a row wins; it is not a four-run warning.");
        }

        [Test]
        public void WallKillPreviewFlagsBlocksDyingOnFlipOnly()
        {
            var rules = new MatchRules(new[] { PlayerColor.Red, PlayerColor.Blue }, 13);
            ClearBoard(rules);
            var blue = Place(rules, PlayerColor.Blue, 7, 7);
            Place(rules, PlayerColor.Red, 6, 7);
            Place(rules, PlayerColor.Red, 8, 7);
            Place(rules, PlayerColor.Red, 7, 6);
            rules.State.WallPhase = 1; // (7,8) open: blue is alive now, dies when the phase flips.
            var preview = rules.FindWallKillPreview();
            Assert.IsTrue(preview.Contains(blue.Id));
            Assert.AreEqual(1, rules.State.WallPhase, "Preview must not change the live wall phase.");

            // A block already dead in the current phase is not a wall-kill preview.
            ClearBoard(rules);
            Place(rules, PlayerColor.Blue, 9, 9);
            Place(rules, PlayerColor.Red, 9, 8);
            Place(rules, PlayerColor.Red, 9, 10);
            Place(rules, PlayerColor.Red, 8, 9);
            Place(rules, PlayerColor.Red, 10, 9);
            Assert.AreEqual(0, rules.FindWallKillPreview().Count);
        }

        [Test]
        public void BuriedItemTriggersRespawnOfAVisibleOne()
        {
            // 道具公平性：墙翻转把场上道具全部埋掉后，立即补刷一个当前可见的道具。
            var rules = new MatchRules(new[] { PlayerColor.Red, PlayerColor.Blue }, 15);
            ClearBoard(rules);
            var mover = Place(rules, PlayerColor.Red, 1, 1);
            rules.State.WallPhase = 0;
            // (10,7) 内圈 idx1：相位 0 时空槽（道具可见），翻转到相位 1 后升墙（被埋）。
            rules.State.FieldItems.Add(new FieldItem
                { Id = 99, Type = ItemType.PassWall, Position = new GridPosition(10, 7) });

            Assert.IsTrue(rules.TryApplyDice(1, out var error), error);
            Assert.IsTrue(rules.TrySelectPiece(mover.Id, out error), error);
            Assert.IsTrue(rules.TrySlide(SlideDirection.Right, null, out _, out error), error);
            Assert.AreEqual(1, rules.State.WallPhase);
            Assert.IsTrue(rules.State.FieldItems.Any(item => !rules.IsWall(item.Position)),
                "After the flip buried every field item, a visible one must respawn immediately.");
        }

        [Test]
        public void DeployIsFreeAndDoesNotForceTheNewPiece()
        {
            // V0.3：放置新棋不消耗滑行次数，也不强制滑动它——放置后留在选子阶段。
            var rules = new MatchRules(new[] { PlayerColor.Red, PlayerColor.Blue }, 1);
            ClearBoard(rules);
            var existing = Place(rules, PlayerColor.Red, 7, 7);
            Assert.IsTrue(rules.TryApplyDice(4, out var error), error);
            Assert.AreEqual(4, rules.State.RemainingSlides);

            Assert.IsTrue(rules.TryDeploy(out var deployedId, out error), error);
            Assert.AreEqual(MatchPhase.WaitSelect, rules.State.Phase, "放置后应留在选子阶段");
            Assert.AreEqual(4, rules.State.RemainingSlides, "放置不消耗滑行次数");
            Assert.IsFalse(rules.State.SelectedPieceId.HasValue, "放置后不强制选中新棋");
            Assert.IsFalse(rules.CanDeploy(PlayerColor.Red), "出生点被占后不能再出子");
            Assert.IsFalse(rules.TryDeploy(out _, out error));

            // 可以改选场上的旧棋滑行。
            Assert.IsTrue(rules.TrySelectPiece(existing.Id, out error), error);
            Assert.IsTrue(rules.TrySlide(SlideDirection.Right, null, out _, out error), error);
            Assert.AreEqual(3, rules.State.RemainingSlides);

            var deployed = rules.PieceById(deployedId);
            Assert.IsTrue(deployed.OnBoard);
            Assert.AreEqual(rules.Board.SpawnOf(PlayerColor.Red), deployed.Position);
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

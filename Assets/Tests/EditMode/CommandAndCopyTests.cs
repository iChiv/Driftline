using System.Linq;
using Draftline.Core;
using Draftline.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Draftline.Tests
{
    public sealed class CommandAndCopyTests
    {
        private static MatchRules NewMatch() => new MatchRules(new[] { PlayerColor.Red, PlayerColor.Blue }, 27);

        [Test]
        public void SimulationOwnsAllMutableObjectsAndRandomState()
        {
            var live = NewMatch();
            live.State.Players[0].Items.Add(ItemType.PassWall);
            live.State.FieldItems.Add(new FieldItem { Id = 6, Type = ItemType.BreakWall, Position = new GridPosition(10, 7) });
            live.State.DeadAtTurnStart.Add(9);
            var copy = live.CloneForSimulation();
            copy.State.Pieces[0].Position = new GridPosition(5, 5);
            copy.State.Players[0].Items.Clear();
            copy.State.FieldItems[0].Type = ItemType.EmergencyStop;
            copy.State.DeadAtTurnStart.Clear();
            copy.State.RandomState++;
            Assert.AreEqual(live.Board.SpawnOf(PlayerColor.Red), live.State.Pieces[0].Position);
            Assert.AreEqual(1, live.State.Players[0].Items.Count);
            Assert.AreEqual(ItemType.BreakWall, live.State.FieldItems[0].Type);
            Assert.IsTrue(live.State.DeadAtTurnStart.Contains(9));
            Assert.AreNotEqual(live.State.RandomState, copy.State.RandomState);
        }

        [Test]
        public void SnapshotPreservesTurnBaselineRevisionAndRandomStream()
        {
            var live = NewMatch();
            live.State.Revision = 13;
            live.State.DeadAtTurnStart.Add(3);
            var restored = JsonUtility.FromJson<MatchSnapshot>(JsonUtility.ToJson(MatchSnapshot.Capture(live.State))).Restore();
            Assert.AreEqual(13, restored.Revision);
            Assert.AreEqual(live.State.RandomState, restored.RandomState);
            CollectionAssert.AreEquivalent(live.State.DeadAtTurnStart, restored.DeadAtTurnStart);
        }

        [Test]
        public void IllegalCombinedSlideDoesNotConsumeItemOrCommitSelection()
        {
            var live = NewMatch();
            live.TryApplyDice(2, out _);
            live.State.Players[0].Items.Add(ItemType.EmergencyStop);
            var command = new MatchCommand { Kind = MatchCommandKind.Slide, PieceId = 0,
                Direction = SlideDirection.Right, Item = (int)ItemType.EmergencyStop, StopIndex = 999 };
            Assert.IsFalse(MatchCommandDispatcher.TryExecute(live, PlayerColor.Red, command, out var result, out _));
            Assert.IsNull(result);
            Assert.AreEqual(MatchPhase.WaitSelect, live.State.Phase);
            Assert.IsNull(live.State.SelectedPieceId);
            Assert.AreEqual(2, live.State.RemainingSlides);
            Assert.AreEqual(1, live.State.Players[0].Items.Count);
        }

        [Test]
        public void AcceptedCommandAdvancesRevisionAndRejectsReplayOrWrongActor()
        {
            var live = NewMatch();
            var command = new MatchCommand { Kind = MatchCommandKind.ApplyDice, Dice = 2 };
            Assert.IsTrue(MatchCommandDispatcher.TryExecute(live, PlayerColor.Red, command, out var next, out var error), error);
            Assert.AreEqual(0, live.State.Revision);
            Assert.AreEqual(1, next.State.Revision);
            Assert.IsFalse(MatchCommandDispatcher.TryExecute(next, PlayerColor.Red, command, out _, out _));
            command.ExpectedRevision = 1;
            Assert.IsFalse(MatchCommandDispatcher.TryExecute(next, PlayerColor.Blue, command, out _, out _));
        }

        [Test]
        public void CopiedRulesProduceIdenticalItemSpawnsWithoutAdvancingLiveRandom()
        {
            var live = NewMatch();
            live.TryApplyDice(1, out _);
            var command = new MatchCommand { Kind = MatchCommandKind.Slide, PieceId = 0, Direction = SlideDirection.Right };
            var originalRandom = live.State.RandomState;
            Assert.IsTrue(MatchCommandDispatcher.TryExecute(live, PlayerColor.Red, command, out var a, out var error), error);
            Assert.IsTrue(MatchCommandDispatcher.TryExecute(live, PlayerColor.Red, command, out var b, out error), error);
            Assert.AreEqual(originalRandom, live.State.RandomState);
            Assert.AreEqual(a.State.RandomState, b.State.RandomState);
            CollectionAssert.AreEqual(a.State.FieldItems.Select(i => i.Position), b.State.FieldItems.Select(i => i.Position));
            CollectionAssert.AreEqual(a.State.FieldItems.Select(i => i.Type), b.State.FieldItems.Select(i => i.Type));
        }
    }
}

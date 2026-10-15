using System.Linq;
using Draftline.Core;
using Draftline.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Draftline.Tests
{
    public sealed class HealthAndPortalTests
    {
        static MatchRules Empty()
        {
            var rules=new MatchRules(new[]{PlayerColor.Red,PlayerColor.Blue},15);
            foreach(var p in rules.State.Pieces){p.OnBoard=false;p.Gone=false;}
            return rules;
        }
        static PieceState Place(MatchRules rules,PlayerColor color,int column,int row)
        {
            var p=rules.State.Pieces.First(v=>v.Owner==color&&!v.OnBoard&&!v.Gone);
            p.OnBoard=true;p.Position=new GridPosition(column,row);return p;
        }
        static void Hit(MatchRules rules,PieceState mover)
        {
            rules.State.Players[(int)mover.Owner].Items.Add(ItemType.CollisionCapture);
            Assert.IsTrue(rules.TryApplyDice(3,out var error),error);
            Assert.IsTrue(rules.TrySelectPiece(mover.Id,out error),error);
            Assert.IsTrue(rules.TryActivateItem(ItemType.CollisionCapture,out error),error);
            Assert.IsTrue(rules.TrySlide(SlideDirection.Right,null,out _,out error),error);
        }

        [Test]
        public void PlayersStartWithFiveHealthAndAnEightPiecePool()
        {
            var rules=new MatchRules(new[]{PlayerColor.Red,PlayerColor.Blue},1);
            foreach(var color in new[]{PlayerColor.Red,PlayerColor.Blue})
            {
                Assert.AreEqual(5,rules.State.Players[(int)color].Health);
                Assert.AreEqual(8,rules.State.Pieces.Count(p=>p.Owner==color));
                Assert.AreEqual(7,rules.HandCount(color));
            }
        }
        [Test]
        public void CaptureCostsOneHealthAndTheSamePieceCanBeRedeployed()
        {
            var rules=Empty();var red=Place(rules,PlayerColor.Red,5,7);var blue=Place(rules,PlayerColor.Blue,8,7);
            Hit(rules,red);
            Assert.AreEqual(4,rules.State.Players[1].Health);
            Assert.IsFalse(blue.OnBoard);Assert.IsFalse(blue.Gone);
            Assert.AreEqual(8,rules.HandCount(PlayerColor.Blue));
            rules.State.CurrentPlayer=PlayerColor.Blue;rules.State.Phase=MatchPhase.WaitSelect;
            Assert.IsTrue(rules.TryDeploy(out var id,out var error),error);
            Assert.AreEqual(blue.Id,id);Assert.AreEqual(rules.Board.SpawnOf(PlayerColor.Blue),blue.Position);
            Assert.AreEqual(4,rules.State.Players[1].Health,"Deploying never restores health.");
        }
        [Test]
        public void ZeroHealthEliminatesImmediatelyEvenWithMorePiecesAndSlides()
        {
            var rules=Empty();var red=Place(rules,PlayerColor.Red,5,7);Place(rules,PlayerColor.Blue,8,7);
            Place(rules,PlayerColor.Blue,11,8);rules.State.Players[1].Health=1;
            Hit(rules,red);
            Assert.AreEqual(0,rules.State.Players[1].Health);Assert.IsTrue(rules.State.Players[1].Eliminated);
            Assert.IsFalse(rules.State.Pieces.Any(p=>p.Owner==PlayerColor.Blue&&p.OnBoard));
            Assert.AreEqual(2,rules.State.RemainingSlides);Assert.AreEqual(MatchPhase.GameOver,rules.State.Phase);
            Assert.AreEqual(PlayerColor.Red,rules.State.Winner);Assert.IsFalse(rules.CanDeploy(PlayerColor.Blue));
        }
        [Test]
        public void EightOnBoardBlocksAReplacementEvenIfAnExtraReserveExists()
        {
            var rules=Empty();
            for(int c=5;c<13;c++)Place(rules,PlayerColor.Red,c,0);
            rules.State.Pieces.Add(new PieceState{Id=999,Owner=PlayerColor.Red,OnBoard=false});
            rules.State.Phase=MatchPhase.WaitSelect;
            Assert.IsFalse(rules.CanDeploy(PlayerColor.Red));Assert.IsFalse(rules.TryDeploy(out _,out _));
        }
        [Test]
        public void HealthSurvivesClonesAndNetworkSnapshotsWithoutAliasing()
        {
            var rules=Empty();rules.State.Players[0].Health=2;rules.State.Players[1].Health=4;
            var clone=rules.CloneForSimulation();clone.State.Players[0].Health=1;
            Assert.AreEqual(2,rules.State.Players[0].Health);
            var copy=JsonUtility.FromJson<MatchSnapshot>(JsonUtility.ToJson(MatchSnapshot.Capture(rules.State))).Restore();
            Assert.AreEqual(2,copy.Players[0].Health);Assert.AreEqual(4,copy.Players[1].Health);
        }
        [Test]
        public void SimultaneousLastHealthLossEndsAsDrawInsteadOfSearchingForASurvivorForever()
        {
            var rules=Empty();var mover=Place(rules,PlayerColor.Red,1,1);
            Place(rules,PlayerColor.Blue,7,7);Place(rules,PlayerColor.Red,6,7);Place(rules,PlayerColor.Red,8,7);Place(rules,PlayerColor.Red,7,6);
            Place(rules,PlayerColor.Red,11,9);Place(rules,PlayerColor.Blue,10,9);Place(rules,PlayerColor.Blue,12,9);Place(rules,PlayerColor.Blue,11,8);
            rules.State.Players[0].Health=rules.State.Players[1].Health=1;rules.State.WallPhase=1;
            Assert.IsFalse(rules.FindDeadBlocks().Any());
            Assert.IsTrue(rules.TryApplyDice(1,out var error),error);Assert.IsTrue(rules.TrySelectPiece(mover.Id,out error),error);
            Assert.IsTrue(rules.TrySlide(SlideDirection.Right,null,out _,out error),error);
            Assert.AreEqual(MatchPhase.GameOver,rules.State.Phase);Assert.IsNull(rules.State.Winner);
            Assert.IsTrue(rules.Events.Any(e=>e.Type==MatchEventType.Draw));
        }

        [TestCase(7,4)][TestCase(11,4)][TestCase(7,14)][TestCase(11,14)]
        [TestCase(4,7)][TestCase(4,11)][TestCase(14,7)][TestCase(14,11)]
        public void AllPortalEndsLandAtThePairedInwardMouthAndRejectAnOccupiedMouth(int column,int row)
        {
            var rules=Empty();var entry=new GridPosition(column,row);var start=rules.Board.PortalMouth(entry);
            var landing=rules.Board.PortalMouth(rules.Board.PortalExit(entry));
            var red=Place(rules,PlayerColor.Red,start.Column,start.Row);
            var direction=entry.Column<start.Column?SlideDirection.Left:entry.Column>start.Column?SlideDirection.Right:entry.Row<start.Row?SlideDirection.Up:SlideDirection.Down;
            var plan=rules.PlanSlide(red,direction);
            Assert.IsTrue(plan.IsLegal);Assert.AreEqual(landing,plan.Destination);
            Assert.IsTrue(plan.Steps.All(s=>BoardLayout.Contains(s.Position)));
            Place(rules,PlayerColor.Blue,landing.Column,landing.Row);
            Assert.IsFalse(rules.PlanSlide(red,direction).IsLegal,"An enemy on the landing mouth blocks the whole passage.");
            rules.State.CollisionCaptureActive=true;
            Assert.IsFalse(rules.PlanSlide(red,direction).IsLegal,"Collision capture cannot bypass a blocked portal landing.");
        }
        [Test]
        public void IncomingDirectionCannotSendAPieceOutOfTheBackOfThePortal()
        {
            var rules=Empty();var red=Place(rules,PlayerColor.Red,7,3);
            var plan=rules.PlanSlide(red,SlideDirection.Down);
            Assert.AreEqual(new GridPosition(7,13),plan.Destination);
            red.Position=plan.Destination;
            var next=rules.PlanSlide(red,SlideDirection.Down);
            Assert.AreEqual(new GridPosition(7,5),next.Destination,"Moving down into the bottom portal returns inward, never onto row 18.");
        }
        [Test]
        public void BlockedExitPortalTileAlsoPreventsThePassage()
        {
            var rules=Empty();var red=Place(rules,PlayerColor.Red,7,5);Place(rules,PlayerColor.Blue,7,14);
            Assert.IsFalse(rules.PlanSlide(red,SlideDirection.Up).IsLegal);
        }
        [Test]
        public void FriendlyPiecesShareTheirBlockAndLibertiesAcrossTheTwoMouths()
        {
            var rules=Empty();var a=Place(rules,PlayerColor.Blue,7,5);var b=Place(rules,PlayerColor.Blue,7,13);
            Place(rules,PlayerColor.Red,6,13);Place(rules,PlayerColor.Red,8,13);Place(rules,PlayerColor.Red,7,12);
            Assert.IsFalse(rules.FindDeadBlocks().Any(v=>v.Contains(b)),"The far mouth's open neighbors keep both friendly pieces alive.");
            Place(rules,PlayerColor.Red,6,5);Place(rules,PlayerColor.Red,8,5);Place(rules,PlayerColor.Red,7,6);
            var block=rules.FindDeadBlocks().Single(v=>v.Contains(b));
            CollectionAssert.AreEquivalent(new[]{a.Id,b.Id},block.Select(v=>v.Id));
        }
        [Test]
        public void SealingTheRemoteMouthCapturesTheNewlySurroundedPieceAndCostsHealth()
        {
            var rules=Empty();var mover=Place(rules,PlayerColor.Red,1,1);var victim=Place(rules,PlayerColor.Blue,7,13);
            Place(rules,PlayerColor.Red,6,13);Place(rules,PlayerColor.Red,8,13);Place(rules,PlayerColor.Red,7,12);
            Assert.IsFalse(rules.FindDeadBlocks().Any(v=>v.Contains(victim)),"The empty remote mouth is the remaining liberty.");
            Assert.IsTrue(rules.TryApplyDice(1,out var error),error);
            Place(rules,PlayerColor.Red,7,5);
            Assert.IsTrue(rules.TrySelectPiece(mover.Id,out error),error);
            Assert.IsTrue(rules.TrySlide(SlideDirection.Right,null,out _,out error),error);
            Assert.IsFalse(victim.OnBoard);Assert.AreEqual(4,rules.State.Players[1].Health);
            Assert.IsTrue(rules.Events.Any(e=>e.Type==MatchEventType.PieceCaptured&&e.PieceId==victim.Id&&e.Cause==CaptureCause.Surround));
        }
        [Test]
        public void EmergencyStopCannotLeaveAPieceInsideThePortalPassage()
        {
            var rules=Empty();var red=Place(rules,PlayerColor.Red,7,5);
            rules.State.Players[0].Items.Add(ItemType.EmergencyStop);
            Assert.IsTrue(rules.TryApplyDice(3,out var error),error);Assert.IsTrue(rules.TrySelectPiece(red.Id,out error),error);
            Assert.IsTrue(rules.TryActivateItem(ItemType.EmergencyStop,out error),error);
            var before=JsonUtility.ToJson(MatchSnapshot.Capture(rules.State));
            Assert.IsFalse(rules.TrySlide(SlideDirection.Up,0,out _,out _));
            Assert.IsFalse(rules.TrySlide(SlideDirection.Up,1,out _,out _));
            Assert.AreEqual(before,JsonUtility.ToJson(MatchSnapshot.Capture(rules.State)));
            Assert.IsTrue(rules.TrySlide(SlideDirection.Up,2,out _,out error),error);Assert.AreEqual(new GridPosition(7,13),red.Position);
        }
    }
}

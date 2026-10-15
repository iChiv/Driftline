using System.Linq;
using Draftline.Core;
using Draftline.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Draftline.Tests
{
    public sealed class ActionInspectionTests
    {
        private static MatchRules Board()
        {
            var rules=new MatchRules(new[]{PlayerColor.Red,PlayerColor.Blue},73);
            foreach(var piece in rules.State.Pieces){piece.OnBoard=false;piece.Gone=false;piece.Position=new GridPosition(-1,-1);}
            return rules;
        }
        private static PieceState Place(MatchRules r,PlayerColor c,int x,int y)
        {var p=r.State.Pieces.First(p=>p.Owner==c&&!p.OnBoard);p.OnBoard=true;p.Position=new GridPosition(x,y);return p;}
        private static MatchCommand Slide(MatchRules r,PieceState p,SlideDirection d,int item=0) => new MatchCommand
        {Kind=MatchCommandKind.Slide,ExpectedRevision=r.State.Revision,PieceId=p.Id,Direction=d,Item=item};

        [Test]
        public void CollisionInspectionExplainsContactWithoutSpendingLiveInventoryOrRandom()
        {
            var r=Board();var red=Place(r,PlayerColor.Red,5,7);var blue=Place(r,PlayerColor.Blue,8,7);
            r.State.Players[0].Items.Add(ItemType.CollisionCapture);r.TryApplyDice(2,out _);
            var before=JsonUtility.ToJson(MatchSnapshot.Capture(r.State));var events=r.Events.Count;
            var result=ActionInspection.Evaluate(r,Slide(r,red,SlideDirection.Right,(int)ItemType.CollisionCapture));
            Assert.IsTrue(result.Accepted);Assert.AreEqual(1,result.RemainingSlides);Assert.IsFalse(result.EndsTurn);
            Assert.AreEqual(CaptureCause.Collision,result.Events.Single(e=>e.Type==MatchEventType.PieceCaptured&&e.PieceId==blue.Id).Cause);
            Assert.AreEqual(before,JsonUtility.ToJson(MatchSnapshot.Capture(r.State)));Assert.AreEqual(events,r.Events.Count);
        }

        [Test]
        public void LastSlideExplainsWallDeathAndDoesNotDiscloseRandomReplenishment()
        {
            var r=Board();var red=Place(r,PlayerColor.Red,1,1);var blue=Place(r,PlayerColor.Blue,7,7);
            Place(r,PlayerColor.Red,6,7);Place(r,PlayerColor.Red,8,7);Place(r,PlayerColor.Red,7,6);
            r.State.WallPhase=1;r.TryApplyDice(1,out _);var rng=r.State.RandomState;
            var result=ActionInspection.Evaluate(r,Slide(r,red,SlideDirection.Right));
            Assert.IsTrue(result.EndsTurn);
            Assert.AreEqual(CaptureCause.WallChange,result.Events.Single(e=>e.Type==MatchEventType.PieceCaptured&&e.PieceId==blue.Id).Cause);
            Assert.IsFalse(result.Events.Any(e=>e.Type==MatchEventType.ItemSpawned));
            Assert.AreEqual(rng,r.State.RandomState);Assert.IsTrue(blue.OnBoard);
        }

        [TestCase(1)] [TestCase(2)]
        public void NewEnemyZeroIsConditionalUntilTurnResolution(int dice)
        {
            var r=Board();var red=Place(r,PlayerColor.Red,7,10);var blue=Place(r,PlayerColor.Blue,7,7);
            Place(r,PlayerColor.Red,6,7);Place(r,PlayerColor.Red,8,7);Place(r,PlayerColor.Red,7,6);
            r.State.WallPhase=1;r.TryApplyDice(dice,out _);
            var result=ActionInspection.Evaluate(r,Slide(r,red,SlideDirection.Up));
            Assert.IsTrue(result.Accepted);
            if(dice==1) Assert.AreEqual(CaptureCause.Surround,result.Events.Single(e=>e.Type==MatchEventType.PieceCaptured&&e.PieceId==blue.Id).Cause);
            else {Assert.AreEqual(1,result.PendingEnemyPieces);Assert.IsFalse(result.Events.Any(e=>e.Type==MatchEventType.PieceCaptured));}
        }

        [TestCase(CaptureCause.Collision)] [TestCase(CaptureCause.Surround)] [TestCase(CaptureCause.WallChange)]
        public void NetworkEventRetainsCaptureCause(CaptureCause cause)
        {
            var e=new MatchEvent{Type=MatchEventType.PieceCaptured,Cause=cause,Player=PlayerColor.Blue,PieceId=17,Position=new GridPosition(7,7)};
            var restored=JsonUtility.FromJson<MatchEventPacket>(JsonUtility.ToJson(MatchEventPacket.Capture(e))).Restore();
            Assert.AreEqual(cause,restored.Cause);Assert.AreEqual(e.Position,restored.Position);Assert.AreEqual(17,restored.PieceId);
        }
    }
}

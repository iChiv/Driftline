using System.Linq;
using Draftline.Core;
using NUnit.Framework;

namespace Draftline.Tests
{
    public sealed class ComputerPlannerTests
    {
        [Test]
        public void ComputerFindsAnAvailableImmediateFiveWithoutChangingTheSource()
        {
            var rules = new MatchRules(new[] { PlayerColor.Red, PlayerColor.Blue },21);
            foreach (var p in rules.State.Pieces) { p.OnBoard=false; p.Gone=false; }
            var red = rules.State.Pieces.Where(p=>p.Owner==PlayerColor.Red).ToArray();
            for(var i=0;i<4;i++){red[i].OnBoard=true;red[i].Position=new GridPosition(5+i,8);}
            red[4].OnBoard=true;red[4].Position=new GridPosition(9,7);
            rules.TryApplyDice(1,out _);
            var rng=rules.State.RandomState;
            var chosen=ComputerPlanner.Choose(rules,false);
            Assert.IsNotNull(chosen);
            Assert.IsTrue(MatchCommandDispatcher.TryExecute(rules,PlayerColor.Red,chosen,out var next,out var error),error);
            Assert.AreEqual(PlayerColor.Red,next.State.Winner);
            Assert.AreEqual(MatchPhase.WaitSelect,rules.State.Phase);
            Assert.AreEqual(new GridPosition(9,7),red[4].Position);
            Assert.AreEqual(rng,rules.State.RandomState);
        }

        [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void ComputerProducesALegalActionForEveryPlayerCount(int count)
        {
            var rules=new MatchRules(Enumerable.Range(0,count).Select(i=>(PlayerColor)i),12);
            rules.TryApplyDice(3,out _);
            var command=ComputerPlanner.Choose(rules,false);
            Assert.IsNotNull(command);
            Assert.IsTrue(MatchCommandDispatcher.TryExecute(rules,rules.State.CurrentPlayer,command,out _,out var error),error);
            Assert.AreEqual(0,rules.State.Revision);
        }
    }
}

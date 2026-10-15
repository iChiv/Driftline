using System.Linq;
using System.Net;
using Draftline.Core;
using Draftline.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Draftline.Tests
{
    public sealed class EnvironmentAndSettingsTests
    {
        [TestCase("fortress")][TestCase("dungeon")][TestCase("hangar")][TestCase("forest")][TestCase("harbor")]
        public void DressingKeepsWallMountsOutsidePlayableCentersAndAddsNoColliders(string id)
        {
            var theme=DraftlineThemeApplier.Load(id);Assert.IsNotNull(theme.BackgroundMaterial);
            Assert.AreEqual("Driftline/ThemeGradient",theme.BackgroundMaterial.shader.name);
            var inventory=theme.PresentationPrefab.GetComponentInChildren<ThemeInfillDressing>(true);
            Assert.IsNotNull(inventory);Assert.AreEqual(16,inventory.GroundDetails.Length);Assert.AreEqual(8,inventory.WallProps.Length);Assert.AreEqual(12,inventory.RimProps.Length);
            Assert.AreEqual(0,inventory.GetComponentsInChildren<Collider>(true).Length);
            var board=new BoardLayout();
            foreach(var prop in inventory.WallProps)
            {
                var b=Bounds(prop);Assert.IsTrue(BoardCoordinates.TryFromWorld(b.center,out var p));
                Assert.AreEqual(CellKind.FixedWall,board.CellAt(p).Kind);
                Assert.LessOrEqual(b.size.x,.66f);Assert.LessOrEqual(b.size.z,.66f);Assert.LessOrEqual(b.size.y,.56f);
            }
            foreach(var prop in inventory.RimProps)
            {
                var b=Bounds(prop);Assert.IsTrue(b.min.x>9.5f||b.max.x< -9.5f||b.min.z>9.5f||b.max.z< -9.5f);
            }
            foreach(var prop in inventory.GroundDetails)
            {
                var b=Bounds(prop);Assert.LessOrEqual(b.max.y,.626f);
                for(int row=0;row<19;row++)for(int col=0;col<19;col++)
                {
                    if(board.CellAt(new GridPosition(col,row)).Kind==CellKind.FixedWall)continue;
                    var center=BoardCoordinates.ToWorld(new GridPosition(col,row));
                    float x=Mathf.Max(b.min.x-center.x,0,center.x-b.max.x),z=Mathf.Max(b.min.z-center.z,0,center.z-b.max.z);
                    Assert.GreaterOrEqual(Mathf.Sqrt(x*x+z*z),.35f,"A detail intrudes into a playable cell center.");
                }
            }
        }
        static Bounds Bounds(GameObject prop){var renderers=prop.GetComponentsInChildren<Renderer>(true);var b=renderers[0].bounds;foreach(var r in renderers.Skip(1))b.Encapsulate(r.bounds);return b;}

        [TestCase("192.168.1.20",true)][TestCase("10.0.0.8",true)][TestCase("127.0.0.1",false)]
        [TestCase("169.254.1.2",false)][TestCase("::1",false)][TestCase("0.0.0.0",false)]
        public void LanChoicesExcludeLoopbackAndUnusableAddresses(string address,bool expected)
            => Assert.AreEqual(expected,LanAddressUtility.IsUsable(IPAddress.Parse(address)));

        [Test]
        public void CopiedHostEndpointCanBePastedWithoutManuallySplittingThePort()
        {
            Assert.IsTrue(LanAddressUtility.TryReadEndpoint(" 192.168.1.42:7788 ","7777",out var address,out var port,out var error),error);
            Assert.AreEqual("192.168.1.42",address);Assert.AreEqual(7788,port);
        }
        [TestCase("192.168.1.20","7777",true)][TestCase("127.0.0.1","7777",true)]
        [TestCase("192.168.1.20:0","7777",false)][TestCase("invalid","7777",false)]
        public void EndpointValidationAcceptsIpv4AndRejectsInvalidPorts(string address,string port,bool expected)
            => Assert.AreEqual(expected,LanAddressUtility.TryReadEndpoint(address,port,out _,out _,out _));

        [Test]
        public void PassWallStillReachesTheOuterPocketThroughAPortal()
        {
            var rules=new MatchRules(new[]{PlayerColor.Red,PlayerColor.Blue});
            var p=rules.State.Pieces.First(piece=>piece.OnBoard&&piece.Owner==PlayerColor.Red);
            p.Position=new GridPosition(7,5);rules.State.PassWallActive=true;
            var plan=rules.PlanSlide(p,SlideDirection.Up);
            Assert.AreEqual(new GridPosition(7,0),plan.Destination);Assert.AreEqual(5,plan.Steps.Count);
        }
    }
}

using System.Linq;
using Draftline.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Draftline.Tests
{
    public sealed class BoardThemeTests
    {
        [Test]
        public void CatalogueContainsOnlyTheFiveCompleteScenes()
        {
            var themes=DraftlineThemeApplier.Available;
            Assert.AreEqual(5,themes.Length);Assert.AreEqual(5,themes.Select(t=>t.ThemeId).Distinct().Count());
            Assert.IsFalse(themes.Any(t=>t.ThemeId=="tabletop"));
            foreach(var theme in themes.Where(t=>t.ThemeId!="tabletop")) {
                Assert.IsNotNull(theme.PresentationPrefab,theme.ThemeId);Assert.IsNotNull(theme.PiecePrefab,theme.ThemeId);
                Assert.AreEqual(9,theme.PresentationPrefab.GetComponentsInChildren<ThemeMovingWall>(true).Length);
                var hotspots=theme.PresentationPrefab.GetComponentsInChildren<ThemeSceneryInteraction>(true);
                Assert.AreEqual(4,hotspots.Length,theme.ThemeId);
                foreach(var collider in theme.PresentationPrefab.GetComponentsInChildren<Collider>(true)){
                    Assert.IsTrue(collider.isTrigger);Assert.AreEqual(2,collider.gameObject.layer);
                    Assert.IsNotNull(collider.GetComponent<ThemeSceneryInteraction>());
                    var box=(BoxCollider)collider;var b=new Bounds(box.transform.TransformPoint(box.center),Vector3.Scale(box.size,box.transform.lossyScale));Assert.IsTrue(b.min.x>9.5f||b.max.x< -9.5f||b.min.z>9.5f||b.max.z< -9.5f,"Scenery triggers must stay outside the core: "+collider.name);
                }
                var batch=theme.PresentationPrefab.GetComponent<ThemeStaticGeometry>();
                Assert.IsTrue(batch.StaticParts.All(part=>part.GetComponentsInChildren<ThemeSceneryInteraction>(true).Length==0));
                Assert.IsTrue(batch.StaticParts.All(part=>part.GetComponentsInChildren<ThemeMovingWall>(true).Length==0));
                Assert.IsNotNull(theme.PresentationPrefab.GetComponent<ThemeEnvironment>());
                Assert.AreEqual(2,theme.EnvironmentNames.Length);
                Assert.IsNotEmpty(theme.Description);
                foreach(var r in theme.PresentationPrefab.GetComponentsInChildren<Renderer>(true).Concat(theme.PiecePrefab.GetComponentsInChildren<Renderer>(true)))
                    foreach(var m in r.sharedMaterials) { Assert.IsNotNull(m,theme.ThemeId+" "+r.name);Assert.IsFalse(m.shader.name.StartsWith("Standard"),m.name); }
            }
        }
        [Test]
        public void RoomPacketRetainsThemeIdentityAndVersion()
        {
            var packet=new RoomStatePacket{themeId="harbor",themeVersion=3,environmentVariant=1,environmentSeed=12345,sessionId="room",sequence=4};
            var restored=JsonUtility.FromJson<RoomStatePacket>(JsonUtility.ToJson(packet));
            Assert.AreEqual("harbor",restored.themeId);Assert.AreEqual(3,restored.themeVersion);
            Assert.AreEqual(1,restored.environmentVariant);Assert.AreEqual(12345,restored.environmentSeed);
        }
        [TestCase(0,"Isolated")][TestCase(1,"End")][TestCase(5,"Straight")][TestCase(10,"Straight")]
        [TestCase(3,"Corner")][TestCase(6,"Corner")][TestCase(9,"Corner")][TestCase(12,"Corner")]
        [TestCase(7,"T")][TestCase(15,"Cross")]
        public void ConnectionMasksDistinguishBendsFromStraightSegments(int mask,string expected)
        {
            Assert.AreEqual(expected,BoardWallTopology.Shape(mask));
            var neighbors=new[]{new[]{-1,0},new[]{0,1},new[]{1,0},new[]{0,-1}};
            Assert.AreEqual(mask,BoardWallTopology.Mask((r,c)=>Enumerable.Range(0,4).Any(i=>(mask&(1<<i))!=0&&r==neighbors[i][0]&&c==neighbors[i][1]),0,0));
        }
    }
}

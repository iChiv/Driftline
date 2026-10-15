using System.Linq;
using Draftline.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Draftline.Tests
{
    public sealed class BoardThemeTests
    {
        [Test]
        public void CatalogueHasFiveCompleteScenesAndTheOriginalTabletop()
        {
            var themes=DraftlineThemeApplier.Available;
            Assert.AreEqual(6,themes.Length);Assert.AreEqual(6,themes.Select(t=>t.ThemeId).Distinct().Count());
            foreach(var theme in themes.Where(t=>t.ThemeId!="tabletop")) {
                Assert.IsNotNull(theme.PresentationPrefab,theme.ThemeId);Assert.IsNotNull(theme.PiecePrefab,theme.ThemeId);
                Assert.AreEqual(9,theme.PresentationPrefab.GetComponentsInChildren<ThemeMovingWall>(true).Length);
                Assert.AreEqual(0,theme.PresentationPrefab.GetComponentsInChildren<Collider>(true).Length,"Decoration cannot intercept board interaction.");
                Assert.IsNotEmpty(theme.Description);
                foreach(var r in theme.PresentationPrefab.GetComponentsInChildren<Renderer>(true).Concat(theme.PiecePrefab.GetComponentsInChildren<Renderer>(true)))
                    foreach(var m in r.sharedMaterials) { Assert.IsNotNull(m,theme.ThemeId+" "+r.name);Assert.IsFalse(m.shader.name.StartsWith("Standard"),m.name); }
            }
        }
        [Test]
        public void RoomPacketRetainsThemeIdentityAndVersion()
        {
            var packet=new RoomStatePacket{themeId="harbor",themeVersion=3,sessionId="room",sequence=4};
            var restored=JsonUtility.FromJson<RoomStatePacket>(JsonUtility.ToJson(packet));
            Assert.AreEqual("harbor",restored.themeId);Assert.AreEqual(3,restored.themeVersion);
        }
    }
}

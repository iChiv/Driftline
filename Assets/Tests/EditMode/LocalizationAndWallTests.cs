using System.Linq;
using Draftline.Core;
using Draftline.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Draftline.Tests
{
    public sealed class LocalizationAndWallTests
    {
        [TestCase("fortress")][TestCase("dungeon")][TestCase("hangar")][TestCase("forest")][TestCase("harbor")]
        public void EveryLiftAssemblyIsCenteredOnItsOwnCell(string id)
        {
            var walls=DraftlineThemeApplier.Load(id).PresentationPrefab.GetComponentsInChildren<ThemeMovingWall>(true);
            Assert.AreEqual(9,walls.Length);
            foreach(var wall in walls)
            {
                Assert.AreEqual(1,wall.GeometryVersion);
                var expected=BoardCoordinates.ToWorld(new GridPosition(wall.Column,wall.Row));
                var shapes=wall.Body.GetComponentsInChildren<Renderer>(true);var bounds=shapes[0].bounds;
                foreach(var shape in shapes.Skip(1))bounds.Encapsulate(shape.bounds);
                Assert.Less(Mathf.Abs(bounds.center.x-expected.x),.04f);Assert.Less(Mathf.Abs(bounds.center.z-expected.z),.04f);
                Assert.GreaterOrEqual(bounds.min.y,.49f);Assert.LessOrEqual(bounds.size.x,1.02f);Assert.LessOrEqual(bounds.size.z,1.02f);
            }
        }
        [TestCase("红方\n生命 3/5   场上 4/8","Red\nHP 3/5   Pieces 4/8")]
        [TestCase("保留显示设置？ 9 秒","Keep display settings? 9s")]
        [TestCase("棋盘主题：前线要塞 · 黄昏战损","Board: Frontline Fortress · Dusk aftermath")]
        [TestCase("红方获胜\n五子连线\n共 12 回合","Red wins\nFive in a row\n12 turns")]
        public void DynamicTextTranslatesAndRoundTripsWithoutChangingValues(string source,string expected)
        {
            var original=GameLocalization.Language;
            try{GameLocalization.SetLanguage(GameLanguage.English,false);var english=GameLocalization.Translate(source);Assert.AreEqual(expected,english);GameLocalization.SetLanguage(GameLanguage.Chinese,false);Assert.AreEqual(source,GameLocalization.Translate(english));}
            finally{GameLocalization.SetLanguage(original,false);}
        }
        [Test]
        public void AllGuideAndItemDescriptionsHaveCompleteEnglishTranslations()
        {
            var original=GameLocalization.Language;
            try
            {
                GameLocalization.SetLanguage(GameLanguage.English,false);
                var guide=Resources.FindObjectsOfTypeAll<RuleGuideContent>().First();
                foreach(var step in guide.Steps)foreach(var text in new[]{step.Title,step.Body,step.Example})
                    Assert.IsFalse(GameLocalization.ContainsChinese(GameLocalization.Translate(text)),text);
                var words=Resources.FindObjectsOfTypeAll<MatchTextContent>().First();
                foreach(var text in words.ItemDescriptions)Assert.IsFalse(GameLocalization.ContainsChinese(GameLocalization.Translate(text)),text);
            }
            finally{GameLocalization.SetLanguage(original,false);}
        }
    }
}

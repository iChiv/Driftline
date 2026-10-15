using System.Collections;
using System.Linq;
using System.Reflection;
using Draftline.Core;
using Draftline.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Draftline.Tests
{
    public sealed class BoardPointerTests
    {
        private Mouse mouse;
        private Mouse[] otherMice;
        private DraftlineGameController controller;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return null;
            otherMice=InputSystem.devices.OfType<Mouse>().Where(device=>device.enabled).ToArray();
            foreach(var device in otherMice)InputSystem.DisableDevice(device);
            mouse=InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            controller=Object.FindObjectOfType<DraftlineGameController>();
            var ui=Object.FindObjectOfType<DraftlineCanvasUI>();
            ui.LocalGame.onClick.Invoke();
            ui.SeatControls[1].value=1;
            ui.StartMatch.onClick.Invoke();
            typeof(DraftlineGameController).GetMethod("ExecuteAuthorityCommand",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(controller,new object[]{new MatchCommand{Kind=MatchCommandKind.ApplyDice,Dice=3,ExpectedRevision=0}});
            var readyDeadline=Time.realtimeSinceStartup+5f;
            while(controller.IsAnimating&&Time.realtimeSinceStartup<readyDeadline)yield return null;
            Assert.IsFalse(controller.IsAnimating,"Initial wall presentation must finish before pointer input.");
            yield return new WaitForSeconds(.5f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            controller?.ReturnToMenu();
            if(mouse!=null)InputSystem.RemoveDevice(mouse);
            if(otherMice!=null)foreach(var device in otherMice)InputSystem.EnableDevice(device);
            yield return null;
        }

        private IEnumerator Click(Vector3 world)
        {
            var point=Camera.main.WorldToScreenPoint(world);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(point.x,point.y),buttons=0});
            yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(point.x,point.y),buttons=1});
            yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(point.x,point.y),buttons=0});
            yield return null;
        }

        [UnityTest]
        public IEnumerator EveryAuthoredThemeKeepsModelsAndSupportsBoardPointerActions()
        {
            var ui=controller.CanvasUI;ui.ResultMenu.onClick.Invoke();
            var themes=DraftlineThemeApplier.Available;
            Assert.AreEqual(6,themes.Length);
            var originalPreference=DraftlineThemeApplier.PreferredId;
            foreach(var theme in themes.Where(t=>t.PresentationPrefab!=null))
            {
                ui.ThemeSelector.value=System.Array.IndexOf(themes,theme);
                Assert.AreEqual(theme.ThemeId,DraftlineThemeApplier.CurrentId);
                Assert.AreEqual(theme.ThemeId,DraftlineThemeApplier.PreferredId);
                ui.LocalGame.onClick.Invoke();ui.StartMatch.onClick.Invoke();
                typeof(DraftlineGameController).GetMethod("ExecuteAuthorityCommand",BindingFlags.Instance|BindingFlags.NonPublic)
                    .Invoke(controller,new object[]{new MatchCommand{Kind=MatchCommandKind.ApplyDice,Dice=2,ExpectedRevision=0}});
                yield return new WaitForSeconds(.6f);
                ui.Refresh();Assert.IsFalse(ui.ThemeSelector.interactable);
                var before=JsonUtility.ToJson(MatchSnapshot.Capture(controller.Rules.State));
                ui.ThemeSelector.onValueChanged.Invoke(0);
                Assert.AreEqual(theme.ThemeId,DraftlineThemeApplier.CurrentId,"An active match cannot change theme.");
                Assert.AreEqual(before,JsonUtility.ToJson(MatchSnapshot.Capture(controller.Rules.State)));
                var piece=controller.Rules.State.Pieces.First(p=>p.Owner==PlayerColor.Red&&p.OnBoard);
                var view=controller.PieceViews[piece.Id];var appearance=view.GetComponent<ThemePieceAppearance>();
                Assert.IsNotNull(appearance,theme.ThemeId);
                Assert.IsTrue(view.GetComponentsInChildren<Renderer>().Any(r=>!appearance.FactionRenderers.Contains(r)&&r.sharedMaterial!=appearance.FactionRenderers[0].sharedMaterial),"Imported palette must be retained.");
                yield return Click(view.transform.position+Vector3.up*.25f);
                Assert.AreEqual(piece.Id,controller.BoardInteraction.SelectedPieceId,theme.ThemeId+" piece collider selection");
                var target=Object.FindObjectsOfType<BoardTarget>().First(t=>!t.Deploy&&!t.DirectionChoice&&t.StopIndex==-1);
                yield return Click(target.transform.position);
                var deadline=Time.realtimeSinceStartup+4;
                while(controller.IsAnimating&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.Less(controller.Rules.State.RemainingSlides,2,theme.ThemeId+" endpoint executes");
                var scene=Object.FindObjectOfType<BoardThemeScene>();var active=scene.Entries.Single(e=>e.Root.activeSelf);
                Assert.AreEqual(theme,active.Theme);
                Assert.IsTrue(active.Root.transform.Find("Architecture — static/BoardSurface").GetComponentsInChildren<MeshRenderer>().Any(r=>r.isPartOfStaticBatch),"Static board geometry should be batched.");
                foreach(var wall in active.Root.GetComponentsInChildren<ThemeMovingWall>(true)) {
                    Assert.IsNotNull(wall.Driver);
                    Assert.AreEqual(controller.Rules.IsWall(new GridPosition(wall.Column,wall.Row)),wall.Body.gameObject.activeSelf,theme.ThemeId+" wall binding");
                }
                ui.ReturnMenu.onClick.Invoke();yield return null;
            }
            DraftlineThemeApplier.SelectLocal(originalPreference);
        }

        [UnityTest]
        public IEnumerator SceneryClicksWorkDuringRemoteTurnsWithoutChangingSelectionOrRules()
        {
            var ui=controller.CanvasUI;ui.ResultMenu.onClick.Invoke();
            foreach(var theme in DraftlineThemeApplier.Available.Where(t=>t.PresentationPrefab!=null)){
                ui.ThemeSelector.value=System.Array.IndexOf(DraftlineThemeApplier.Available,theme);
                ui.LocalGame.onClick.Invoke();ui.SeatControls[1].value=1;ui.StartMatch.onClick.Invoke();
                typeof(DraftlineGameController).GetMethod("ExecuteAuthorityCommand",BindingFlags.Instance|BindingFlags.NonPublic)
                    .Invoke(controller,new object[]{new MatchCommand{Kind=MatchCommandKind.ApplyDice,Dice=3,ExpectedRevision=0}});
                yield return new WaitForSeconds(.8f);
                var piece=controller.Rules.State.Pieces.First(p=>p.Owner==PlayerColor.Red&&p.OnBoard);
                yield return Click(controller.PieceViews[piece.Id].transform.position+Vector3.up*.25f);
                Assert.AreEqual(piece.Id,controller.BoardInteraction.SelectedPieceId);
                var before=JsonUtility.ToJson(MatchSnapshot.Capture(controller.Rules.State));
                var props=Object.FindObjectsOfType<ThemeSceneryInteraction>();Assert.AreEqual(4,props.Length);
                foreach(var prop in props){
                    yield return Click(prop.transform.position);
                    Assert.Greater(prop.Activations,0,theme.ThemeId+" "+prop.Label+" actual pointer hit");
                    Assert.AreEqual(piece.Id,controller.BoardInteraction.SelectedPieceId,"Decorative clicks preserve the preview.");
                    Assert.AreEqual(before,JsonUtility.ToJson(MatchSnapshot.Capture(controller.Rules.State)));
                }
                yield return new WaitForSecondsRealtime(2);
                var toggle=props.First(p=>p.Toggle);Assert.IsTrue(toggle.IsOn);
                yield return Click(toggle.transform.position);yield return new WaitForSecondsRealtime(2);Assert.IsFalse(toggle.IsOn);
                foreach(var motion in toggle.Motions)Assert.Less(Vector3.Distance(motion.Target.localPosition,motion.Position),.001f);
                // Authority gate deliberately closed: the same direct scenery click still works.
                controller.Session.Seat(controller.Rules.State.CurrentPlayer).Control=SeatControl.RemoteHuman;
                Assert.IsFalse(controller.CanControl);var count=toggle.Activations;
                yield return Click(toggle.transform.position);Assert.AreEqual(count+1,toggle.Activations);
                ui.GuideHud.onClick.Invoke();Assert.IsTrue(ui.EnvironmentInputBlocked);
                count=toggle.Activations;yield return Click(toggle.transform.position);Assert.AreEqual(count,toggle.Activations);
                ui.GuideClose.onClick.Invoke();
                ui.ReturnMenu.onClick.Invoke();yield return new WaitForSecondsRealtime(.5f);
                var visibleProp=Object.FindObjectsOfType<ThemeSceneryInteraction>().First(p=>p.transform.position.z>10&&!p.Busy);
                count=visibleProp.Activations;yield return Click(visibleProp.transform.position);Assert.AreEqual(count+1,visibleProp.Activations,"Main menu preview interaction");
            }
        }

        [UnityTest]
        public IEnumerator ThemedDiceStationCanBePressedDirectly()
        {
            var ui=controller.CanvasUI;ui.ResultMenu.onClick.Invoke();ui.ThemeSelector.value=4;
            ui.LocalGame.onClick.Invoke();ui.StartMatch.onClick.Invoke();yield return new WaitForSeconds(.6f);
            var dice=Object.FindObjectOfType<DraftlineRollDice>();Assert.IsNotNull(dice);
            Assert.IsTrue(dice.GetComponentsInChildren<Renderer>().All(r=>!r.forceRenderingOff));
            yield return Click(dice.transform.position);yield return null;
            Assert.IsTrue(controller.IsDiceRolling,"Actual pointer click on the foreground dice starts the roll.");
        }

        [UnityTest]
        public IEnumerator EnvironmentVariantsAreDeterministicAndLockedDuringMatches()
        {
            var ui=controller.CanvasUI;ui.ResultMenu.onClick.Invoke();
            ui.ThemeSelector.value=3;ui.EnvironmentSelector.value=1;yield return null;
            Assert.AreEqual(1,DraftlineThemeApplier.CurrentVariant);
            var scene=Object.FindObjectOfType<BoardThemeScene>();var environment=scene.Entries.Single(e=>e.Root.activeSelf).Root.GetComponent<ThemeEnvironment>();
            DraftlineThemeApplier.ApplyEnvironment(1,19);var first=environment.SeededDetails.Select(g=>g.activeSelf).ToArray();
            DraftlineThemeApplier.ApplyEnvironment(0,72);DraftlineThemeApplier.ApplyEnvironment(1,19);
            CollectionAssert.AreEqual(first,environment.SeededDetails.Select(g=>g.activeSelf).ToArray());
            Assert.IsTrue(environment.Alternate.activeSelf);Assert.IsFalse(environment.Standard.activeSelf);
            ui.LocalGame.onClick.Invoke();ui.StartMatch.onClick.Invoke();yield return new WaitForSeconds(.5f);
            Assert.IsFalse(ui.EnvironmentSelector.interactable);ui.EnvironmentSelector.onValueChanged.Invoke(0);Assert.AreEqual(1,DraftlineThemeApplier.CurrentVariant);
            var dice=Object.FindObjectOfType<DraftlineRollDice>();Assert.IsNotNull(dice);
            Assert.IsTrue(dice.GetComponentsInChildren<Renderer>().All(r=>!r.forceRenderingOff));
            ui.ReturnMenu.onClick.Invoke();ui.EnvironmentSelector.value=0;
        }

        [UnityTest]
        public IEnumerator LocalGameAllowsMixedSeatsAndRestoresThemAfterLanNavigation()
        {
            var ui=Object.FindObjectOfType<DraftlineCanvasUI>();
            ui.ResultMenu.onClick.Invoke();
            ui.LocalGame.onClick.Invoke();
            ui.SeatControls[2].value=2;
            Assert.IsTrue(controller.Session.Validate(out var error),error);
            ui.SetupBack.onClick.Invoke();
            ui.Lan.onClick.Invoke();
            ui.LeaveRoom.onClick.Invoke();
            ui.LocalGame.onClick.Invoke();
            Assert.AreEqual(GameMode.Local,controller.Session.Mode);
            Assert.AreEqual(SeatControl.LocalHuman,controller.Session.Seats[0].Control);
            Assert.AreEqual(SeatControl.LocalHuman,controller.Session.Seats[1].Control);
            Assert.AreEqual(SeatControl.Computer,controller.Session.Seats[2].Control);
            Assert.AreEqual(SeatControl.Closed,controller.Session.Seats[3].Control);
            ui.StartMatch.onClick.Invoke();
            yield return null;
            Assert.AreEqual(3,controller.Rules.State.Players.Count(player=>player.Active));
        }

        [UnityTest]
        public IEnumerator HoveringEndpointExplainsTheActionWithoutChangingTheMatch()
        {
            var piece=controller.Rules.State.Pieces.First(p=>p.Owner==PlayerColor.Red&&p.OnBoard);
            yield return Click(controller.PieceViewPosition(piece.Id).Value+Vector3.up*.25f);
            var before=JsonUtility.ToJson(MatchSnapshot.Capture(controller.Rules.State));
            var target=Object.FindObjectsOfType<BoardTarget>().First(t=>!t.Deploy&&!t.DirectionChoice&&t.StopIndex==-1);
            var point=Camera.main.WorldToScreenPoint(target.transform.position);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(point.x,point.y)});
            yield return null;yield return null;
            var ui=controller.CanvasUI;ui.Refresh();
            Assert.AreEqual("行动预览",ui.ActionTitle.text);
            Assert.IsTrue(ui.ActionText.text.Contains("消耗 1 次"));
            Assert.AreEqual(before,JsonUtility.ToJson(MatchSnapshot.Capture(controller.Rules.State)));
        }

        [UnityTest]
        public IEnumerator OptionalGuidePreservesMatchAndRestoresThePreviousPauseState()
        {
            var ui=controller.CanvasUI;var before=JsonUtility.ToJson(MatchSnapshot.Capture(controller.Rules.State));
            ui.GuideHud.onClick.Invoke();Assert.IsTrue(ui.GuidePanel.activeSelf);Assert.IsTrue(controller.IsPaused);
            ui.GuideNext.onClick.Invoke();Assert.IsNotEmpty(ui.GuideBody.text);
            ui.GuideClose.onClick.Invoke();Assert.IsFalse(controller.IsPaused);
            ui.HudSettings.onClick.Invoke();ui.GuideSettings.onClick.Invoke();ui.GuideClose.onClick.Invoke();
            Assert.IsTrue(controller.IsPaused,"Closing the guide opened from settings must retain settings pause.");
            ui.CloseSettings.onClick.Invoke();Assert.IsFalse(controller.IsPaused);
            Assert.AreEqual(before,JsonUtility.ToJson(MatchSnapshot.Capture(controller.Rules.State)));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PointingAtPieceThenEndpointExecutesSlide()
        {
            var piece=controller.Rules.State.Pieces.First(p=>p.Owner==PlayerColor.Red&&p.OnBoard);
            yield return Click(controller.PieceViewPosition(piece.Id).Value+Vector3.up*.25f);
            Assert.AreEqual(piece.Id,controller.BoardInteraction.SelectedPieceId);
            Assert.AreEqual(MatchPhase.WaitSelect,controller.Rules.State.Phase,"Preview must not commit rule selection.");
            var target=Object.FindObjectsOfType<BoardTarget>().First(t=>!t.Deploy&&!t.DirectionChoice&&t.StopIndex==-1);
            var destination=target.Cell;
            yield return Click(target.transform.position);
            yield return new WaitForSeconds(.8f);
            Assert.AreEqual(destination,controller.Rules.PieceById(piece.Id).Position);
            Assert.AreEqual(2,controller.Rules.State.RemainingSlides);
            Assert.AreEqual(2,controller.Rules.State.Revision);
        }

        [UnityTest]
        public IEnumerator EmergencyStopUsesTheClickedBoardStep()
        {
            var piece=controller.Rules.State.Pieces.First(p=>p.Owner==PlayerColor.Red&&p.OnBoard);
            controller.Rules.State.Players[0].Items.Add(ItemType.EmergencyStop);
            yield return Click(controller.PieceViewPosition(piece.Id).Value+Vector3.up*.25f);
            controller.UseItemFromUI(ItemType.EmergencyStop);
            yield return null;
            var target=Object.FindObjectsOfType<BoardTarget>().First(t=>!t.Deploy&&!t.DirectionChoice&&t.StopIndex==0&&t.Direction==SlideDirection.Right);
            var cell=target.Cell;
            yield return Click(target.transform.position);
            yield return new WaitForSeconds(.7f);
            Assert.AreEqual(cell,controller.Rules.PieceById(piece.Id).Position);
            Assert.IsFalse(controller.Rules.State.Players[0].Items.Contains(ItemType.EmergencyStop));
            Assert.AreEqual(2,controller.Rules.State.RemainingSlides);
        }

        [UnityTest]
        public IEnumerator ClickingHighlightedSpawnDeploysWithoutUsingASlide()
        {
            var piece=controller.Rules.State.Pieces.First(p=>p.Owner==PlayerColor.Red&&p.OnBoard);
            piece.Position=new GridPosition(7,7);
            controller.BoardInteraction.StateChanged();
            yield return null;
            var spawn=Object.FindObjectsOfType<BoardTarget>().First(t=>t.Deploy);
            yield return Click(spawn.transform.position);
            yield return new WaitForSeconds(.5f);
            Assert.AreEqual(2,controller.Rules.State.Pieces.Count(p=>p.Owner==PlayerColor.Red&&p.OnBoard));
            Assert.AreEqual(3,controller.Rules.State.RemainingSlides);
        }

        [UnityTest]
        public IEnumerator CancellingPreviewPreservesSelectionRulesAndInventory()
        {
            var piece=controller.Rules.State.Pieces.First(p=>p.Owner==PlayerColor.Red&&p.OnBoard);
            controller.Rules.State.Players[0].Items.Add(ItemType.EmergencyStop);
            yield return Click(controller.PieceViewPosition(piece.Id).Value+Vector3.up*.25f);
            controller.UseItemFromUI(ItemType.EmergencyStop);
            var point=Camera.main.WorldToScreenPoint(controller.PieceViewPosition(piece.Id).Value);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(point.x,point.y),buttons=2});
            yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(point.x,point.y),buttons=0});
            yield return null;
            Assert.IsFalse(controller.BoardInteraction.SelectedPieceId.HasValue);
            Assert.IsFalse(controller.BoardInteraction.PendingItem.HasValue);
            Assert.AreEqual(MatchPhase.WaitSelect,controller.Rules.State.Phase);
            Assert.AreEqual(1,controller.Rules.State.Revision);
            Assert.Contains(ItemType.EmergencyStop,controller.Rules.State.Players[0].Items);
        }

        [UnityTest]
        public IEnumerator RestartDuringSlideDiscardsThePreviousMatchAnimation()
        {
            var piece=controller.Rules.State.Pieces.First(p=>p.Owner==PlayerColor.Red&&p.OnBoard);
            yield return Click(controller.PieceViewPosition(piece.Id).Value+Vector3.up*.25f);
            var target=Object.FindObjectsOfType<BoardTarget>().First(t=>!t.Deploy&&!t.DirectionChoice&&t.StopIndex==-1);
            yield return Click(target.transform.position);
            Assert.IsTrue(controller.IsAnimating);
            controller.RestartMatch();
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(0,controller.Rules.State.Revision);
            Assert.AreEqual(MatchPhase.WaitRoll,controller.Rules.State.Phase);
            Assert.IsFalse(controller.IsAnimating);
            Assert.IsFalse(controller.IsDiceRolling);
            Assert.AreEqual(controller.Rules.Board.SpawnOf(PlayerColor.Red),controller.Rules.PieceById(piece.Id).Position);
            Assert.AreEqual(2,controller.PieceViews.Values.Count(view=>view.gameObject.activeSelf));
        }
    }
}

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
        private DraftlineGameController controller;
        private InputActionAsset uiActions;
        private InputTestFixture inputFixture;
        private InputDevice[] originalDevices;
        private bool[] originalEnabled;
        private Mouse originalMouse;
        private GameLanguage originalLanguage;

        [OneTimeSetUp]
        public void RememberEditorInput()
        {
            originalDevices=InputSystem.devices.ToArray();
            originalEnabled=originalDevices.Select(d=>d.enabled).ToArray();
            originalMouse=Mouse.current;
            originalLanguage=GameLocalization.Language;
        }

        [OneTimeTearDown]
        public void VerifyEditorInputWasRestored()
        {
            GameLocalization.SetLanguage(originalLanguage,false);
            Assert.That(InputSystem.devices.ToArray(),Is.EquivalentTo(originalDevices),"Pointer tests must not leave virtual devices in the Editor.");
            for(var i=0;i<originalDevices.Length;i++)
                Assert.AreEqual(originalEnabled[i],originalDevices[i].enabled,"Device enable state leaked: "+originalDevices[i].name);
            Assert.AreSame(originalMouse,Mouse.current,"The original mouse must regain ownership after the suite.");
        }

        [UnitySetUp]
        public IEnumerator SetUpScene()
        {
            GameLocalization.SetLanguage(GameLanguage.Chinese,false);
            // Enter isolation before loading the scene: UnitySetUp precedes NUnit
            // SetUp in this runner, so an inherited fixture would run too late.
            inputFixture=new InputTestFixture();
            inputFixture.Setup();
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            mouse=InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return null;
            // Scene sub-assets can retain actions resolved against the saved native
            // devices. Give the isolated UI module its own fresh action state.
            var inputModule=Object.FindObjectOfType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            inputModule.enabled=false;
            uiActions=InputActionAsset.FromJson(inputModule.actionsAsset.ToJson());
            uiActions.devices=new InputDevice[]{mouse};
            inputModule.actionsAsset=uiActions;
            inputModule.enabled=true;
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
        public IEnumerator TearDownScene()
        {
            try
            {
                controller?.ReturnToMenu();
                var inputModule=Object.FindObjectOfType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                if(inputModule!=null)inputModule.enabled=false;
                if(uiActions!=null)Object.Destroy(uiActions);
                yield return null;
            }
            finally
            {
                inputFixture?.TearDown();
                inputFixture=null;mouse=null;uiActions=null;controller=null;
            }
        }

        private IEnumerator Click(Vector3 world)
        {
            var point=Camera.main.WorldToScreenPoint(world);
            yield return ClickScreen(new Vector2(point.x,point.y));
        }

        private IEnumerator ClickScreen(Vector2 position)
        {
            InputSystem.QueueStateEvent(mouse,new MouseState{position=position,buttons=0});
            yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=position,buttons=1});
            yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=position,buttons=0});
            yield return null;
        }

        [UnityTest]
        public IEnumerator LiftWallsMatchBothPhasesAcrossAllThemesAndAfterRestart()
        {
            var ui=controller.CanvasUI;ui.ReturnMenu.onClick.Invoke();
            foreach(var theme in DraftlineThemeApplier.Available)
            {
                ui.ThemeSelector.value=System.Array.FindIndex(DraftlineThemeApplier.Available,t=>t.ThemeId==theme.ThemeId);
                ui.LocalGame.onClick.Invoke();ui.StartMatch.onClick.Invoke();yield return new WaitForSeconds(.4f);
                for(int phase=0;phase<4;phase++)
                {
                    controller.Rules.State.WallPhase=phase;controller.Rules.State.Revision++;
                    typeof(DraftlineGameController).GetMethod("SyncViews",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(controller,null);
                    yield return new WaitForSeconds(.04f);
                    DraftlineThemeApplier.Apply(theme.ThemeId); // Rebinding during the lift must keep a stable rest pose.
                    yield return new WaitForSeconds(.4f);
                    AssertWallsFollowRules();
                }
                controller.RestartMatch();yield return new WaitForSeconds(.45f);AssertWallsFollowRules();
                ui.ReturnMenu.onClick.Invoke();
            }
        }
        private void AssertWallsFollowRules()
        {
            var walls=Object.FindObjectsOfType<ThemeMovingWall>();Assert.AreEqual(9,walls.Length);
            foreach(var wall in walls)
            {
                var cell=new GridPosition(wall.Column,wall.Row);bool up=controller.Rules.IsWall(cell);
                Assert.AreEqual(up,wall.Body.gameObject.activeSelf,cell.ToString());
                Assert.Less(Vector3.Distance(Vector3.one,wall.Body.localScale),.01f,"A transient driver scale must not become the rest scale.");
                if(!up)continue;
                var renderers=wall.Body.GetComponentsInChildren<Renderer>();Assert.IsNotEmpty(renderers);
                var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
                var point=BoardCoordinates.ToWorld(cell);
                Assert.Less(Mathf.Abs(bounds.center.x-point.x),.04f);Assert.Less(Mathf.Abs(bounds.center.z-point.z),.04f);
                Assert.Greater(bounds.max.y,.9f);Assert.IsTrue(renderers.Any(r=>r.enabled&&!r.forceRenderingOff));
                Assert.IsFalse(renderers.Any(r=>r.isPartOfStaticBatch),"Lift geometry must never enter a static batch.");
            }
        }

        [UnityTest]
        public IEnumerator LanguageSelectorWorksThroughPointerAndDynamicHudStaysTranslated()
        {
            bool had=PlayerPrefs.HasKey(GameLocalization.PreferenceKey);int saved=PlayerPrefs.GetInt(GameLocalization.PreferenceKey);
            try
            {
                var ui=controller.CanvasUI;ui.ReturnMenu.onClick.Invoke();yield return null;
                var locale=ui.GetComponent<LocalizationPresenter>();var rect=(RectTransform)locale.LanguageSelector.transform;
                yield return ClickScreen(RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)));
                var option=locale.LanguageSelector.GetComponentsInChildren<UnityEngine.UI.Toggle>().First(t=>t.GetComponentInChildren<UnityEngine.UI.Text>().text=="English");
                rect=(RectTransform)option.transform;yield return ClickScreen(RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)));
                Assert.AreEqual(GameLanguage.English,GameLocalization.Language);Assert.AreEqual(1,PlayerPrefs.GetInt(GameLocalization.PreferenceKey));
                ui.LocalGame.onClick.Invoke();ui.StartMatch.onClick.Invoke();yield return new WaitForSeconds(.6f);
                ui.Refresh();Assert.IsFalse(GameLocalization.ContainsChinese(ui.TurnText.text));
                yield return new WaitForSeconds(.3f);Assert.IsFalse(GameLocalization.ContainsChinese(ui.TurnText.text),"Periodic HUD refresh must not restore Chinese text.");
                Assert.IsFalse(GameLocalization.ContainsChinese(ui.PlayerTexts[0].text));
                var before=JsonUtility.ToJson(MatchSnapshot.Capture(controller.Rules.State));
                ui.GuideHud.onClick.Invoke();yield return null;
                foreach(var step in ui.GuideContent.Steps){Assert.IsFalse(GameLocalization.ContainsChinese(ui.GuideBody.text));ui.GuideNext.onClick.Invoke();yield return null;}
                ui.GuideClose.onClick.Invoke();
                GameLocalization.SetLanguage(GameLanguage.Chinese,false);ui.Refresh();Assert.AreEqual("红方",ui.TurnText.text);
                Assert.AreEqual(before,JsonUtility.ToJson(MatchSnapshot.Capture(controller.Rules.State)));
            }
            finally{GameLocalization.SetLanguage(GameLanguage.Chinese,false);if(had)PlayerPrefs.SetInt(GameLocalization.PreferenceKey,saved);else PlayerPrefs.DeleteKey(GameLocalization.PreferenceKey);PlayerPrefs.Save();}
        }

        [UnityTest]
        public IEnumerator DisplayChangesRequireConfirmationAndTimeoutRestoresThePreviousMode()
        {
            var display=controller.CanvasUI.GetComponent<DisplaySettingsPanel>();
            var keys=new[]{DisplaySettingsPanel.WidthKey,DisplaySettingsPanel.HeightKey,DisplaySettingsPanel.ModeKey};
            var had=keys.Select(PlayerPrefs.HasKey).ToArray();var values=keys.Select(k=>PlayerPrefs.GetInt(k)).ToArray();
            try
            {
                controller.CanvasUI.HudSettings.onClick.Invoke();
                var before=display.RequestedSize;var mode=display.RequestedMode;
                display.Resolution.value=3;display.Mode.value=1;display.ConfirmationSeconds=.25f;
                display.Apply.onClick.Invoke();Assert.IsTrue(display.Pending);
                Assert.AreEqual(new Vector2Int(3840,2160),display.RequestedSize);
                Assert.AreEqual(FullScreenMode.FullScreenWindow,display.RequestedMode);
                yield return new WaitForSecondsRealtime(.4f);
                Assert.IsFalse(display.Pending);Assert.AreEqual(before,display.RequestedSize);Assert.AreEqual(mode,display.RequestedMode);
                display.Resolution.value=1;display.Mode.value=0;display.Apply.onClick.Invoke();display.Keep.onClick.Invoke();
                Assert.AreEqual(1920,PlayerPrefs.GetInt(DisplaySettingsPanel.WidthKey));Assert.IsFalse(display.Pending);
                display.Resolution.value=2;display.Apply.onClick.Invoke();controller.CanvasUI.CloseSettings.onClick.Invoke();
                yield return null;Assert.IsFalse(display.Pending);Assert.AreEqual(new Vector2Int(1920,1080),display.RequestedSize);
            }
            finally{for(int i=0;i<keys.Length;i++){if(had[i])PlayerPrefs.SetInt(keys[i],values[i]);else PlayerPrefs.DeleteKey(keys[i]);}PlayerPrefs.Save();}
        }

        [UnityTest]
        public IEnumerator LanHelpAndAddressValidationWorkWithoutStartingAConnection()
        {
            var ui=controller.CanvasUI;ui.ReturnMenu.onClick.Invoke();ui.Lan.onClick.Invoke();
            Assert.IsEmpty(ui.Address.text);Assert.IsFalse(ui.ConnectionInstructions.activeSelf);
            var rect=(RectTransform)ui.ConnectionHelp.transform;
            yield return ClickScreen(RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)));
            Assert.IsTrue(ui.ConnectionInstructions.activeSelf);
            StringAssert.Contains("127.0.0.1",ui.ConnectionInstructions.GetComponentInChildren<UnityEngine.UI.Text>().text);
            ui.Join.onClick.Invoke();Assert.IsFalse(controller.LanRoom.IsNetworked);StringAssert.Contains("房主",controller.SetupError);
            ui.Address.text="not-an-ip";ui.Join.onClick.Invoke();Assert.IsFalse(controller.LanRoom.IsNetworked);StringAssert.Contains("IPv4",controller.SetupError);
        }

        [UnityTest]
        public IEnumerator BackgroundChangesBlendAndDoNotChangeTheMatch()
        {
            var backdrop=Object.FindObjectOfType<ThemeBackdrop>();
            var before=JsonUtility.ToJson(MatchSnapshot.Capture(controller.Rules.State));
            var theme=DraftlineThemeApplier.Load("harbor");var original=backdrop.CurrentLow;
            backdrop.Apply(theme);Assert.AreEqual(original,backdrop.CurrentLow);
            yield return new WaitForSecondsRealtime(.35f);
            Assert.AreNotEqual(original,backdrop.CurrentLow);Assert.IsTrue(backdrop.IsTransitioning);
            backdrop.Apply(DraftlineThemeApplier.Load("dungeon"));
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.IsFalse(backdrop.IsTransitioning);Assert.AreEqual(CameraClearFlags.Skybox,Camera.main.clearFlags);
            Assert.AreEqual(before,JsonUtility.ToJson(MatchSnapshot.Capture(controller.Rules.State)));
        }

        [UnityTest]
        public IEnumerator MenuTourCyclesPausesForTheGuideAndYieldsToGameplay()
        {
            var ui=controller.CanvasUI;
            var director=controller.GetComponent<DraftlineCameraDirector>();
            director.MenuHoldSeconds=.5f;director.MenuBlendSeconds=.1f;
            ui.ReturnMenu.onClick.Invoke();
            InputSystem.QueueStateEvent(mouse,new MouseState{position=Vector2.zero});
            yield return null;
            Assert.IsTrue(director.IsMenuTourActive);
            var first=director.CurrentMenuShot;
            yield return new WaitForSecondsRealtime(.7f);
            Assert.AreNotEqual(first,director.CurrentMenuShot,"The saved menu viewpoints should cycle.");
            ui.GuideMenu.onClick.Invoke();
            var pausedShot=director.CurrentMenuShot;
            Assert.IsFalse(director.IsMenuTourActive);
            yield return new WaitForSecondsRealtime(.8f);
            Assert.AreEqual(pausedShot,director.CurrentMenuShot,"Reading the guide must pause the tour clock.");
            ui.GuideClose.onClick.Invoke();
            ui.LocalGame.onClick.Invoke();ui.StartMatch.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.6f);
            Assert.IsFalse(director.IsMenuTourActive);
            var cameras=(Cinemachine.CinemachineVirtualCamera[])typeof(DraftlineCameraDirector)
                .GetField("menuCameras",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(director);
            Assert.IsTrue(cameras.All(c=>c.Priority==0),"No menu camera may compete with gameplay framing.");
            Assert.AreEqual(26f,director.CurrentDistance);
            var rotation=Camera.main.transform.rotation;
            var before=JsonUtility.ToJson(MatchSnapshot.Capture(controller.Rules.State));
            yield return new WaitForSecondsRealtime(.7f);
            Assert.Less(Quaternion.Angle(rotation,Camera.main.transform.rotation),.05f,"Gameplay must stay stable.");
            Assert.AreEqual(before,JsonUtility.ToJson(MatchSnapshot.Capture(controller.Rules.State)));
        }

        [UnityTest]
        public IEnumerator ThemeDropdownCanBeOpenedAndSelectedWithThePointer()
        {
            var ui=controller.CanvasUI;
            ui.ReturnMenu.onClick.Invoke();yield return null;
            var original=DraftlineThemeApplier.PreferredId;
            try
            {
                var desired=DraftlineThemeApplier.Available.First(t=>t.ThemeId!=DraftlineThemeApplier.CurrentId);
                var selector=(RectTransform)ui.ThemeSelector.transform;
                yield return ClickScreen(RectTransformUtility.WorldToScreenPoint(null,selector.TransformPoint(selector.rect.center)));
                var option=ui.ThemeSelector.GetComponentsInChildren<UnityEngine.UI.Toggle>()
                    .First(t=>t.GetComponentInChildren<UnityEngine.UI.Text>().text==desired.ThemeName);
                var rect=(RectTransform)option.transform;
                yield return ClickScreen(RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)));
                Assert.AreEqual(desired.ThemeId,DraftlineThemeApplier.CurrentId);
                Assert.AreEqual(desired.ThemeId,DraftlineThemeApplier.PreferredId);
            }
            finally{PlayerPrefs.SetString(DraftlineThemeApplier.PrefKey,original);}
        }

        [UnityTest]
        public IEnumerator MenuAndStartButtonsRespondThroughTheUiInputModule()
        {
            var ui=controller.CanvasUI;
            ui.ReturnMenu.onClick.Invoke();
            yield return null;
            var local=(RectTransform)ui.LocalGame.transform;
            var point=RectTransformUtility.WorldToScreenPoint(null,local.TransformPoint(local.rect.center));
            yield return ClickScreen(point);
            var events=UnityEngine.EventSystems.EventSystem.current;
            var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            events.RaycastAll(new UnityEngine.EventSystems.PointerEventData(events){position=point},hits);
            var module=events.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            Assert.IsTrue(ui.Setup.activeSelf,"UI input diagnostics: point="+point+", device="+mouse.position.ReadValue()+", action="+module.point.action.ReadValue<Vector2>()+", testMouse="+mouse.deviceId+", controls="+string.Join(",",module.point.action.controls.Select(c=>c.path+"#"+c.device.deviceId))+", active="+module.point.action.activeControl?.device.deviceId+", processor="+InputSystem.TryGetProcessor("AutoWindowSpace")+", focused="+events.isFocused+", hits="+string.Join(",",hits.Select(h=>h.gameObject.name)));
            var start=(RectTransform)ui.StartMatch.transform;
            yield return ClickScreen(RectTransformUtility.WorldToScreenPoint(null,start.TransformPoint(start.rect.center)));
            Assert.IsNotNull(controller.Rules,"A pointer click must start the game through the saved UI input bindings.");
            Assert.IsTrue(ui.Hud.activeSelf);
        }

        [UnityTest]
        public IEnumerator PortalBlockerPreventsTheMoveAndClearingItKeepsViewAndRuleLandingTogether()
        {
            var red=controller.Rules.State.Pieces.First(p=>p.Owner==PlayerColor.Red&&p.OnBoard);
            var blue=controller.Rules.State.Pieces.First(p=>p.Owner==PlayerColor.Blue&&p.OnBoard);
            red.Position=new GridPosition(7,3);blue.Position=new GridPosition(7,13);
            typeof(DraftlineGameController).GetMethod("SyncViews",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(controller,null);
            controller.BoardInteraction.StateChanged();yield return null;
            yield return Click(controller.PieceViewPosition(red.Id).Value+Vector3.up*.25f);
            Assert.IsFalse(Object.FindObjectsOfType<BoardTarget>().Any(t=>!t.Deploy&&t.Direction==SlideDirection.Down));
            var before=JsonUtility.ToJson(MatchSnapshot.Capture(controller.Rules.State));
            Assert.IsFalse(controller.SubmitCommand(new MatchCommand{Kind=MatchCommandKind.Slide,PieceId=red.Id,Direction=SlideDirection.Down,ExpectedRevision=controller.Rules.State.Revision}));
            Assert.AreEqual(before,JsonUtility.ToJson(MatchSnapshot.Capture(controller.Rules.State)));
            blue.Position=new GridPosition(8,13);
            typeof(DraftlineGameController).GetMethod("SyncViews",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(controller,null);
            controller.BoardInteraction.StateChanged();controller.ShowError(null);yield return null;
            yield return Click(controller.PieceViewPosition(red.Id).Value+Vector3.up*.25f);
            var target=Object.FindObjectsOfType<BoardTarget>().First(t=>!t.DirectionChoice&&!t.Deploy&&t.Direction==SlideDirection.Down);
            Assert.AreEqual(new GridPosition(7,13),target.Cell);yield return Click(target.transform.position);
            var deadline=Time.realtimeSinceStartup+4;
            while(controller.IsAnimating&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.AreEqual(new GridPosition(7,13),controller.Rules.PieceById(red.Id).Position);
            Assert.Less(Vector3.Distance(BoardCoordinates.ToWorld(new GridPosition(7,13)),controller.PieceViewPosition(red.Id).Value),.01f);
            Assert.IsTrue(controller.Rules.State.Pieces.Where(p=>p.OnBoard).All(p=>BoardLayout.Contains(p.Position)));
        }

        [UnityTest]
        public IEnumerator BoardWheelZoomPreservesTheMatchAndDoesNotConsumeUiScrolling()
        {
            var director=controller.GetComponent<DraftlineCameraDirector>();
            Assert.AreEqual(26f,director.CurrentDistance);
            var before=JsonUtility.ToJson(MatchSnapshot.Capture(controller.Rules.State));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(Screen.width*.5f,Screen.height*.5f),scroll=new Vector2(0,120)});
            yield return null;yield return null;
            Assert.Less(director.CurrentDistance,26f);
            var zoomed=director.CurrentDistance;var button=(RectTransform)controller.CanvasUI.Roll.transform;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=RectTransformUtility.WorldToScreenPoint(null,button.TransformPoint(button.rect.center)),scroll=new Vector2(0,120)});
            yield return null;yield return null;
            Assert.AreEqual(zoomed,director.CurrentDistance,"A wheel over UI must not zoom the board.");
            Assert.AreEqual(before,JsonUtility.ToJson(MatchSnapshot.Capture(controller.Rules.State)));
            controller.Rules.State.Players[0].Health=3;controller.CanvasUI.Refresh();
            StringAssert.Contains("生命 3/5",controller.CanvasUI.PlayerTexts[0].text);
            StringAssert.Contains("场上 1/8",controller.CanvasUI.PlayerTexts[0].text);
        }

        [UnityTest]
        public IEnumerator EmptyHudCardsDoNotBlockTheSceneAndInventoryAppearsWhenNeeded()
        {
            controller.RestartMatch();
            yield return new WaitForSeconds(.6f);
            var ui=controller.CanvasUI; ui.Refresh();
            Assert.IsFalse(ui.InventoryPanel.activeSelf,"An empty inventory must not leave a blocking card.");
            Assert.IsFalse(ui.ActionPanel.activeSelf,"No stale action from the previous match.");
            var gap=new Vector2(Screen.width*175f/1920f,Screen.height*(1080f-650f)/1080f);
            Assert.IsFalse(controller.BoardInteraction.IsPointerOverUI(gap),"The former full-height sidebar must let scene clicks through its empty area.");
            controller.Rules.State.Players[0].Items.Add(ItemType.EmergencyStop);
            ui.Refresh();
            Assert.IsTrue(ui.InventoryPanel.activeSelf);
            controller.Rules.State.Players[0].Items.Clear();
            ui.Refresh();
            Assert.IsFalse(ui.InventoryPanel.activeSelf);
        }

        [UnityTest]
        public IEnumerator EveryAuthoredThemeKeepsModelsAndSupportsBoardPointerActions()
        {
            var ui=controller.CanvasUI;ui.ResultMenu.onClick.Invoke();
            var themes=DraftlineThemeApplier.Available;
            Assert.AreEqual(5,themes.Length);
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
            ui.ThemeSelector.value=3;yield return null;
            Assert.IsFalse(ui.EnvironmentSelector.gameObject.activeInHierarchy,"Environment is automatic, not a player-facing selector.");
            Assert.AreEqual(2,DraftlineThemeApplier.PreferredEnvironment);
            var scene=Object.FindObjectOfType<BoardThemeScene>();var environment=scene.Entries.Single(e=>e.Root.activeSelf).Root.GetComponent<ThemeEnvironment>();
            DraftlineThemeApplier.ApplyEnvironment(1,19);var first=environment.SeededDetails.Select(g=>g.activeSelf).ToArray();
            DraftlineThemeApplier.ApplyEnvironment(0,72);DraftlineThemeApplier.ApplyEnvironment(1,19);
            CollectionAssert.AreEqual(first,environment.SeededDetails.Select(g=>g.activeSelf).ToArray());
            Assert.IsTrue(environment.Alternate.activeSelf);Assert.IsFalse(environment.Standard.activeSelf);
            ui.LocalGame.onClick.Invoke();ui.StartMatch.onClick.Invoke();yield return new WaitForSeconds(.5f);
            var variant=DraftlineThemeApplier.CurrentVariant;var seed=DraftlineThemeApplier.CurrentSeed;
            Assert.IsTrue(variant==0||variant==1);
            Assert.IsFalse(ui.EnvironmentSelector.interactable);ui.EnvironmentSelector.onValueChanged.Invoke(0);
            Assert.AreEqual(variant,DraftlineThemeApplier.CurrentVariant);Assert.AreEqual(seed,DraftlineThemeApplier.CurrentSeed);
            var dice=Object.FindObjectOfType<DraftlineRollDice>();Assert.IsNotNull(dice);
            Assert.IsTrue(dice.GetComponentsInChildren<Renderer>().All(r=>!r.forceRenderingOff));
            ui.ReturnMenu.onClick.Invoke();
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
            var restingScale=target.transform.localScale;
            var point=Camera.main.WorldToScreenPoint(target.transform.position);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(point.x,point.y)});
            yield return null;yield return null;
            var ui=controller.CanvasUI;ui.Refresh();
            Assert.AreEqual("行动预览",ui.ActionTitle.text);
            Assert.IsTrue(ui.ActionText.text.Contains("消耗 1 次"));
            Assert.Greater(target.transform.localScale.x,restingScale.x,"Hovered targets visibly acknowledge the pointer.");
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
            Assert.AreEqual(controller.BoardInteraction.Style.StopTargetMesh,target.GetComponent<MeshFilter>().sharedMesh,"Stop points use their own shape as well as color.");
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

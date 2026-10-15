using System.Collections;
using Cinemachine;
using Draftline.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Draftline.Unity
{
    /// <summary>
    /// Local-only views; match state never depends on camera position.
    /// Gameplay stays stable through slides, captures, portals and turn changes.
    /// Menu observation shots, dice rolls and victory have separate framing.
    /// Gameplay projection keeps rule "up" at screen top and
    /// rule "right" at screen right (yaw -90, pitch varies).
    /// </summary>
    public sealed class DraftlineCameraDirector : MonoBehaviour
    {
        [Header("Local board view — editable in the scene")]
        public float GameplayDistance = 26f;
        public float MinimumDistance = 20f;
        public float MaximumDistance = 36f;
        public float ScrollSensitivity = .012f;
        public float GameplayPitch = 72f;
        public Vector3 GameplayTarget = new Vector3(.6f,0,-.8f);
        [System.Serializable]
        public sealed class MenuShot
        {
            public string Name;
            public float Yaw, Pitch, Distance;
            public MenuShot(string name,float yaw,float pitch,float distance)
            { Name=name;Yaw=yaw;Pitch=pitch;Distance=distance; }
        }
        [Header("Main menu observation shots")]
        public MenuShot[] MenuShots = {
            new MenuShot("Overview",-110f,63f,36f),
            new MenuShot("Across the board",-60f,56f,38f),
            new MenuShot("High flank",-145f,68f,35f)
        };
        public float MenuHoldSeconds=9f;
        public float MenuBlendSeconds=3.5f;
        public float MenuDriftDegrees=2f;
        public float MenuBoardOffset=4.5f;
        public int CurrentMenuShot { get; private set; }
        public bool IsMenuTourActive => controller != null && controller.Rules == null &&
            controller.CanvasUI != null && controller.CanvasUI.MainMenu.activeInHierarchy &&
            !controller.CanvasUI.EnvironmentInputBlocked;
        public float CurrentDistance { get; private set; }
        private Vector3 boardTarget;
        private float boardPitch;
        private DraftlineGameController controller;
        private const float DefaultBlendTime = 0.35f;
        private const float BoardBlendTime = 0.45f;

        private CinemachineVirtualCamera overview;
        private CinemachineVirtualCamera dice;
        private CinemachineVirtualCamera topDown;
        private CinemachineVirtualCamera victory;
        private CinemachineBrain brain;
        private CinemachineImpulseSource impulse;
        private PhysicalDiceRoller diceRoller;

        private bool manualTopDown;
        private bool victoryPlaying;
        private Vector3 victoryFocus;
        private float victoryAngle;
        private Vector3 diceVelocity;
        private CinemachineVirtualCamera[] menuCameras;
        private float menuElapsed;
        private bool menuWasVisible;

        private void Awake()
        {
            controller = GetComponent<DraftlineGameController>();
            var main = Camera.main;
            if (main == null)
            {
                Debug.LogWarning("Draftline camera director needs a Main Camera.");
                return;
            }
            brain = main.GetComponent<CinemachineBrain>();
            if (brain == null) brain = main.gameObject.AddComponent<CinemachineBrain>();
            brain.m_DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Style.EaseInOut, DefaultBlendTime);

            diceRoller = GetComponent<PhysicalDiceRoller>();

            // Default: high 72° view — pieces are never occluded by walls, the whole
            // 19x19 board and all four corners stay framed, shifted right of the HUD.
            overview = CreateCamera("Board Overview", new Vector3(0, 0, -2f), 72f, 27f, 54f, 10);
            // Dice: wide framing over the low tray, lazily follows the die.
            dice = CreateCamera("Dice Lazy Follow", new Vector3(0, 1.9f, 0), 50f, 13f, 48f, 0);
            // Manual pure top-down for precise cell reading.
            topDown = CreateCamera("Board Top Down", new Vector3(0, 0, -2f), 89.99f, 27.5f, 55f, 0);
            ApplyThemeFraming(DraftlineThemeApplier.Load(DraftlineThemeApplier.CurrentId??DraftlineThemeApplier.PreferredId));
            // Victory is the only cut away from the board: slow push-in and orbit.
            victory = CreateCamera("Victory Orbit", Vector3.zero, 55f, 10f, 50f, 0);
            menuCameras=new CinemachineVirtualCamera[MenuShots.Length];
            for(int i=0;i<MenuShots.Length;i++)
            {
                menuCameras[i]=CreateCamera("Menu / "+MenuShots[i].Name,Vector3.zero,MenuShots[i].Pitch,MenuShots[i].Distance,54f,0);
                FrameMenuShot(i,0);
            }

            // Cinemachine 2.10: the impulse source is a plain MonoBehaviour; listeners
            // are vcam extensions.
            impulse = gameObject.AddComponent<CinemachineImpulseSource>();
            impulse.m_ImpulseDefinition.m_AmplitudeGain = 0.35f;
            impulse.m_ImpulseDefinition.m_TimeEnvelope.m_SustainTime = 0.15f;
            overview.gameObject.AddComponent<CinemachineImpulseListener>();
            dice.gameObject.AddComponent<CinemachineImpulseListener>();
        }

        private static Quaternion BoardRotation(float pitch) => Quaternion.Euler(pitch, -90f, 0f);

        private static Vector3 FramedPosition(Vector3 lookAt, float pitch, float distance) =>
            lookAt - BoardRotation(pitch) * Vector3.forward * distance;

        private CinemachineVirtualCamera CreateCamera(string name, Vector3 lookAt,
            float pitch, float distance, float fieldOfView, int priority)
        {
            var cameraObject = new GameObject(name);
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.position = FramedPosition(lookAt, pitch, distance);
            cameraObject.transform.rotation = BoardRotation(pitch);
            var virtualCamera = cameraObject.AddComponent<CinemachineVirtualCamera>();
            var lens = virtualCamera.m_Lens;
            lens.FieldOfView = fieldOfView;
            virtualCamera.m_Lens = lens;
            virtualCamera.Priority = priority;
            return virtualCamera;
        }

        private void SetBlend(float time)
        {
            if (brain != null) brain.m_DefaultBlend.m_Time = time;
        }

        private void LateUpdate()
        {
            UpdateMenuTour();
            var mouse = Mouse.current;
            if (mouse != null && !victoryPlaying && controller != null && controller.Rules != null &&
                !controller.IsPaused && !controller.IsDiceRolling &&
                !controller.CanvasUI.EnvironmentInputBlocked &&
                !controller.BoardInteraction.IsPointerOverUI(mouse.position.ReadValue()))
            {
                var scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > .01f) Zoom(scroll);
            }
            // Lazy dice follow: heavy damping, the frame is wide so the die never
            // leaves it; the camera only drifts after the die.
            if (dice != null && dice.Priority > 0 && diceRoller != null && diceRoller.IsRolling)
            {
                var die = diceRoller.DieTransform;
                if (die != null)
                {
                    var desired = FramedPosition(die.position, 50f, 13f);
                    dice.transform.position = Vector3.SmoothDamp(
                        dice.transform.position, desired, ref diceVelocity, 0.4f);
                    dice.transform.rotation = BoardRotation(50f);
                }
            }
            if (victoryPlaying && victory != null)
            {
                victoryAngle += Time.deltaTime * 22f;
                var radius = Mathf.Lerp(10f, 7f, Mathf.Clamp01(victoryAngle / 60f));
                var radians = victoryAngle * Mathf.Deg2Rad;
                var offset = new Vector3(Mathf.Cos(radians) * radius, 7f, Mathf.Sin(radians) * radius);
                victory.transform.position = victoryFocus + offset;
                victory.transform.rotation = Quaternion.LookRotation(
                    victoryFocus + Vector3.up * 0.5f - victory.transform.position);
            }
        }

        private void FrameMenuShot(int index,float drift)
        {
            var shot=MenuShots[index];
            var rotation=Quaternion.Euler(shot.Pitch,shot.Yaw+drift,0);
            var lookAt=rotation*Vector3.left*MenuBoardOffset;
            menuCameras[index].transform.SetPositionAndRotation(lookAt-rotation*Vector3.forward*shot.Distance,rotation);
        }

        private void UpdateMenuTour()
        {
            if(menuCameras==null||menuCameras.Length==0)return;
            if(controller.Rules!=null)
            {
                foreach(var camera in menuCameras)camera.Priority=0;
                menuWasVisible=false;
                return;
            }
            // Freeze on configuration pages and over scenery hotspots, so targets
            // do not move underneath a player who is editing or exploring.
            if(!IsMenuTourActive || ThemeSceneryInteraction.Hovered!=null)return;
            if(!menuWasVisible)
            {
                CurrentMenuShot=0;menuElapsed=0;menuWasVisible=true;
                SetBlend(MenuBlendSeconds);
                for(int i=0;i<menuCameras.Length;i++)menuCameras[i].Priority=i==0?20:0;
            }
            menuElapsed+=Time.unscaledDeltaTime;
            var duration=Mathf.Max(.5f,MenuHoldSeconds)+Mathf.Max(.1f,MenuBlendSeconds);
            if(menuElapsed>=duration)
            {
                menuElapsed-=duration;
                menuCameras[CurrentMenuShot].Priority=0;
                CurrentMenuShot=(CurrentMenuShot+1)%menuCameras.Length;
                menuCameras[CurrentMenuShot].Priority=20;
                SetBlend(MenuBlendSeconds);
            }
            var drift=Mathf.SmoothStep(-MenuDriftDegrees,MenuDriftDegrees,Mathf.Clamp01(menuElapsed/duration));
            FrameMenuShot(CurrentMenuShot,drift);
        }

        public void ShowDice()
        {
            if (manualTopDown || dice == null || victoryPlaying) return;
            SetBlend(0.3f);
            dice.Priority = 30;
        }

        public void ShowBoard()
        {
            if (victoryPlaying) return;
            SetBlend(BoardBlendTime);
            if (dice != null) dice.Priority = 0;
        }

        public void ToggleManualTopDown()
        {
            manualTopDown = !manualTopDown;
            if (topDown != null) topDown.Priority = manualTopDown ? 40 : 0;
            if (!manualTopDown) ShowBoard();
        }

        // Light shake for captures / wall events — the camera itself does not move.
        public void Pulse(float strength = 0.35f)
        {
            if (GetComponent<DraftlineGameController>().SimpleEffects) return;
            impulse?.GenerateImpulse(strength);
        }

        // The one allowed cut: game over, slow push-in and orbit on the winning cell.
        public void PlayVictory(Vector3 focus)
        {
            if (manualTopDown) return;
            StartCoroutine(VictorySequence(focus));
        }

        private IEnumerator VictorySequence(Vector3 focus)
        {
            victoryPlaying = true;
            if (dice != null) dice.Priority = 0;
            victoryFocus = focus;
            victoryAngle = 0f;
            SetBlend(0.6f);
            victory.transform.position = FramedPosition(focus, 55f, 10f);
            victory.transform.rotation = BoardRotation(55f);
            victory.Priority = 40;
            yield return new WaitForSeconds(2.3f);
            // Hold the victory framing; the result screen appears on top.
        }

        public bool IsManualTopDown => manualTopDown;
        public void ApplyThemeFraming(BoardTheme theme)
        {
            if(theme==null)return;
            var playing=GetComponent<DraftlineGameController>()?.Rules!=null;
            CurrentDistance=playing?GameplayDistance:theme.OverviewDistance;
            boardTarget=playing?GameplayTarget:theme.FramingTarget;
            boardPitch=playing?GameplayPitch:theme.OverviewPitch;
            FrameBoard();
        }
        public void Zoom(float scroll)
        {
            CurrentDistance=Mathf.Clamp(CurrentDistance-scroll*ScrollSensitivity,MinimumDistance,MaximumDistance);
            FrameBoard();
        }
        private void FrameBoard()
        {
            if(overview!=null){overview.transform.position=FramedPosition(boardTarget,boardPitch,CurrentDistance);overview.transform.rotation=BoardRotation(boardPitch);}
            if(topDown!=null)topDown.transform.position=FramedPosition(boardTarget,89.99f,CurrentDistance+.5f);
        }
        public void ResetForMatch()
        {
            StopAllCoroutines();
            SetBlend(BoardBlendTime);
            victoryPlaying = false;
            manualTopDown = false;
            victoryAngle = 0f;
            if (dice != null) dice.Priority = 0;
            if (victory != null) victory.Priority = 0;
            if (topDown != null) topDown.Priority = 0;
            if (overview != null) overview.Priority = 10;
            if(menuCameras!=null)foreach(var camera in menuCameras)camera.Priority=0;
            menuWasVisible=false;
            ApplyThemeFraming(DraftlineThemeApplier.Load(DraftlineThemeApplier.CurrentId??DraftlineThemeApplier.PreferredId));
        }
    }
}

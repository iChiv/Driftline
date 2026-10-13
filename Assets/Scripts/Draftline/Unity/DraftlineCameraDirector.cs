using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using Draftline.Core;
using UnityEngine;

namespace Draftline.Unity
{
    /// <summary>
    /// Local-only views; match state never depends on camera position.
    /// All views keep the calibrated projection: rule "up" (row-1) at screen top,
    /// rule "right" (column+1) at screen right — same yaw (-90), only pitch varies.
    /// </summary>
    public sealed class DraftlineCameraDirector : MonoBehaviour
    {
        private const float DefaultBlendTime = 0.35f;
        private const float EventBlendTime = 0.2f;
        private const float BoardBlendTime = 0.45f;

        private CinemachineVirtualCamera overview;
        private CinemachineVirtualCamera dice;
        private CinemachineVirtualCamera follow;
        private CinemachineVirtualCamera topDown;
        private CinemachineVirtualCamera turnCorner;
        private CinemachineVirtualCamera eventCam;
        private CinemachineBrain brain;
        private CinemachineImpulseSource impulse;

        private Transform followTarget;
        private Vector3 followVelocity;
        private bool manualTopDown;

        private readonly Queue<Vector3> eventQueue = new Queue<Vector3>();
        private bool eventPlaying;
        private bool victoryPlaying;
        private Vector3 victoryFocus;
        private float victoryAngle;

        private void Awake()
        {
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

            // Angled overview keeps the whole 19x19 board and all four corners framed.
            overview = CreateCamera("Board Overview", Vector3.zero, 58f, 27f, 50f, 10);
            // Dice tray sits above the board center (floor y=5.25); frame it from the side.
            dice = CreateCamera("Dice Tray View", new Vector3(0, 5.5f, 0), 45f, 21f, 50f, 0);
            follow = CreateCamera("Moving Piece Follow", Vector3.zero, 55f, 7.5f, 55f, 0);
            // Pure top-down stays available for precise cell reading (manual toggle).
            topDown = CreateTopDown("Board Top Down", new Vector3(0, 23, 0), 49f, 0);
            turnCorner = CreateCamera("Next Player Corner", Vector3.zero, 50f, 10f, 50f, 0);
            eventCam = CreateCamera("Event Cam", Vector3.zero, 45f, 9f, 50f, 0);
            // Cinemachine 2.10: impulse source is a plain MonoBehaviour, while the listener
            // is a vcam extension — both belong on virtual cameras, not on the brain.
            impulse = follow.gameObject.AddComponent<CinemachineImpulseSource>();
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
            virtualCamera.gameObject.AddComponent<CinemachineImpulseListener>();
            return virtualCamera;
        }

        private CinemachineVirtualCamera CreateTopDown(string name, Vector3 position,
            float fieldOfView, int priority)
        {
            var cameraObject = new GameObject(name);
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.position = position;
            cameraObject.transform.rotation = Quaternion.Euler(90, -90, 0);
            var virtualCamera = cameraObject.AddComponent<CinemachineVirtualCamera>();
            var lens = virtualCamera.m_Lens;
            lens.FieldOfView = fieldOfView;
            virtualCamera.m_Lens = lens;
            virtualCamera.Priority = priority;
            virtualCamera.gameObject.AddComponent<CinemachineImpulseListener>();
            return virtualCamera;
        }

        private void SetBlend(float time)
        {
            if (brain != null) brain.m_DefaultBlend.m_Time = time;
        }

        private void LateUpdate()
        {
            if (followTarget != null && follow != null)
            {
                var desired = FramedPosition(followTarget.position, 55f, 7.5f);
                follow.transform.position = Vector3.SmoothDamp(
                    follow.transform.position, desired, ref followVelocity, 0.1f);
                follow.transform.rotation = BoardRotation(55f);
            }
            if (victoryPlaying && eventCam != null)
            {
                victoryAngle += Time.deltaTime * 24f;
                var radius = Mathf.Lerp(9f, 6.5f, Mathf.Clamp01(victoryAngle / 55f));
                var radians = victoryAngle * Mathf.Deg2Rad;
                var offset = new Vector3(Mathf.Cos(radians) * radius, 6f, Mathf.Sin(radians) * radius);
                eventCam.transform.position = victoryFocus + offset;
                eventCam.transform.rotation =
                    Quaternion.LookRotation(victoryFocus + Vector3.up * 0.5f - eventCam.transform.position);
            }
        }

        public void ShowDice()
        {
            if (manualTopDown || dice == null || victoryPlaying) return;
            SetBlend(0.3f);
            dice.Priority = 30;
            if (follow != null) follow.Priority = 0;
            if (eventCam != null) eventCam.Priority = 0;
        }

        public void FollowPiece(Transform piece)
        {
            if (manualTopDown || follow == null || victoryPlaying) return;
            SetBlend(0.15f); // Snap to the moving piece quickly so short slides stay visible.
            followTarget = piece;
            follow.transform.position = FramedPosition(piece.position, 55f, 7.5f);
            follow.transform.rotation = BoardRotation(55f);
            follow.Priority = 25;
            if (dice != null) dice.Priority = 0;
        }

        public void ShowBoard()
        {
            if (victoryPlaying) return;
            SetBlend(BoardBlendTime);
            followTarget = null;
            if (follow != null) follow.Priority = 0;
            if (dice != null) dice.Priority = 0;
        }

        public void ShowNextPlayer(PlayerColor player)
        {
            if (manualTopDown || turnCorner == null || victoryPlaying) return;
            var position = (int)player == 0 ? new GridPosition(1, 1)
                : (int)player == 1 ? new GridPosition(17, 1)
                : (int)player == 2 ? new GridPosition(17, 17)
                : new GridPosition(1, 17);
            var corner = BoardCoordinates.ToWorld(position, 0.6f);
            turnCorner.transform.position = FramedPosition(corner, 50f, 10f);
            turnCorner.transform.rotation = BoardRotation(50f);
            turnCorner.Priority = 20;
        }

        public void EndTurnView()
        {
            if (turnCorner != null) turnCorner.Priority = 0;
        }

        public void ToggleManualTopDown()
        {
            manualTopDown = !manualTopDown;
            if (topDown != null) topDown.Priority = manualTopDown ? 40 : 0;
            if (!manualTopDown) ShowBoard();
        }

        // Light screen shake for captures and wall events.
        public void Pulse(float strength = 0.35f)
        {
            impulse?.GenerateImpulse(strength);
        }

        // Short angled cut to a board event (portal hop, capture, wall-kill).
        // Bounded queue: at most two pending events, extra ones are dropped to keep tempo.
        public void PlayEventAt(Vector3 worldPosition)
        {
            if (manualTopDown || victoryPlaying) return;
            if (eventQueue.Count >= 2) return;
            eventQueue.Enqueue(worldPosition);
            if (!eventPlaying) StartCoroutine(RunEventQueue());
        }

        private IEnumerator RunEventQueue()
        {
            eventPlaying = true;
            while (eventQueue.Count > 0)
            {
                var focus = eventQueue.Dequeue();
                if (victoryPlaying) break;
                SetBlend(EventBlendTime);
                eventCam.transform.position = FramedPosition(focus, 45f, 9f);
                eventCam.transform.rotation = BoardRotation(45f);
                eventCam.Priority = 35;
                if (follow != null) follow.Priority = 0;
                Pulse(0.4f);
                yield return new WaitForSeconds(0.75f);
                eventCam.Priority = 0;
                yield return new WaitForSeconds(0.15f);
            }
            eventPlaying = false;
            ShowBoard();
        }

        // Slow push-in and partial orbit on the winning five, then the result screen.
        public void PlayVictory(Vector3 focus)
        {
            if (manualTopDown) return;
            StartCoroutine(VictorySequence(focus));
        }

        private IEnumerator VictorySequence(Vector3 focus)
        {
            victoryPlaying = true;
            eventQueue.Clear();
            if (follow != null) follow.Priority = 0;
            if (dice != null) dice.Priority = 0;
            followTarget = null;
            victoryFocus = focus;
            victoryAngle = 0f;
            SetBlend(0.6f);
            eventCam.transform.position = FramedPosition(focus, 50f, 10f);
            eventCam.transform.rotation = BoardRotation(50f);
            eventCam.Priority = 40;
            yield return new WaitForSeconds(2.3f);
            // Hold the victory framing; the result screen appears on top.
        }

        public bool IsManualTopDown => manualTopDown;
    }
}

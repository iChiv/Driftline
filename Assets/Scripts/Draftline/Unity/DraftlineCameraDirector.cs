using System.Collections;
using Cinemachine;
using Draftline.Core;
using UnityEngine;

namespace Draftline.Unity
{
    /// <summary>
    /// Local-only views; match state never depends on camera position.
    /// Doctrine: the camera never leaves the full-board view — no cuts for slides,
    /// captures, portals or turn changes. Only the dice roll (lazy-follow) and the
    /// victory moment move the camera. Projection keeps rule "up" at screen top and
    /// rule "right" at screen right (yaw -90, pitch varies).
    /// </summary>
    public sealed class DraftlineCameraDirector : MonoBehaviour
    {
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

            diceRoller = GetComponent<PhysicalDiceRoller>();

            // Default: high 72° view — pieces are never occluded by walls, the whole
            // 19x19 board and all four corners stay framed, shifted right of the HUD.
            overview = CreateCamera("Board Overview", new Vector3(0, 0, -2f), 72f, 27f, 54f, 10);
            // Dice: wide framing over the low tray, lazily follows the die.
            dice = CreateCamera("Dice Lazy Follow", new Vector3(0, 1.9f, 0), 50f, 13f, 48f, 0);
            // Manual pure top-down for precise cell reading.
            topDown = CreateCamera("Board Top Down", new Vector3(0, 0, -2f), 89.99f, 27.5f, 55f, 0);
            // Victory is the only cut away from the board: slow push-in and orbit.
            victory = CreateCamera("Victory Orbit", Vector3.zero, 55f, 10f, 50f, 0);

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
        public void ResetForMatch()
        {
            StopAllCoroutines();
            victoryPlaying = false;
            manualTopDown = false;
            victoryAngle = 0f;
            if (dice != null) dice.Priority = 0;
            if (victory != null) victory.Priority = 0;
            if (topDown != null) topDown.Priority = 0;
            if (overview != null) overview.Priority = 10;
        }
    }
}

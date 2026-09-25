using Cinemachine;
using Draftline.Core;
using UnityEngine;

namespace Draftline.Unity
{
    /// <summary>Local-only views; match state never depends on camera position.</summary>
    public sealed class DraftlineCameraDirector : MonoBehaviour
    {
        private CinemachineVirtualCamera overview;
        private CinemachineVirtualCamera dice;
        private CinemachineVirtualCamera follow;
        private CinemachineVirtualCamera topDown;
        private CinemachineVirtualCamera turnCorner;
        private Transform followTarget;
        private bool manualTopDown;

        private void Awake()
        {
            var main = Camera.main;
            if (main == null)
            {
                Debug.LogWarning("Draftline camera director needs a Main Camera.");
                return;
            }
            var brain = main.GetComponent<CinemachineBrain>();
            if (brain == null) brain = main.gameObject.AddComponent<CinemachineBrain>();
            brain.m_DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Style.EaseInOut, 0.35f);

            overview = CreateCamera("Board Overview", new Vector3(0, 20, 0), 60f, 10);
            // Fit the whole 19x19 tray so an energetic throw never leaves the frame.
            dice = CreateCamera("Dice Tray View", new Vector3(0, 23, 0), 65f, 0);
            follow = CreateCamera("Moving Piece Follow", new Vector3(0, 9, 0), 57f, 0);
            topDown = CreateCamera("Board Top Down", new Vector3(0, 23, 0), 49f, 0);
            turnCorner = CreateCamera("Next Player Corner", new Vector3(0, 16, 0), 58f, 0);
        }

        private CinemachineVirtualCamera CreateCamera(string name, Vector3 position,
            float fieldOfView, int priority)
        {
            var cameraObject = new GameObject(name);
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.position = position;
            // Rule up is row - 1 -> world -X. Keep it at screen top in every view.
            // Rule right is column + 1 -> world +Z, at screen right.
            cameraObject.transform.rotation = Quaternion.Euler(90, -90, 0);
            var virtualCamera = cameraObject.AddComponent<CinemachineVirtualCamera>();
            var lens = virtualCamera.m_Lens;
            lens.FieldOfView = fieldOfView;
            virtualCamera.m_Lens = lens;
            virtualCamera.Priority = priority;
            return virtualCamera;
        }

        private void LateUpdate()
        {
            if (followTarget != null && follow != null)
                follow.transform.position = followTarget.position + Vector3.up * 9f;
        }

        public void ShowDice()
        {
            if (manualTopDown || dice == null) return;
            dice.Priority = 30;
            if (follow != null) follow.Priority = 0;
        }

        public void FollowPiece(Transform piece)
        {
            if (manualTopDown || follow == null) return;
            followTarget = piece;
            follow.transform.position = piece.position + Vector3.up * 9f;
            follow.Priority = 25;
            if (dice != null) dice.Priority = 0;
        }

        public void ShowBoard()
        {
            followTarget = null;
            if (follow != null) follow.Priority = 0;
            if (dice != null) dice.Priority = 0;
        }

        public void ShowNextPlayer(PlayerColor player)
        {
            if (manualTopDown || turnCorner == null) return;
            var position = (int)player == 0 ? new GridPosition(1, 1)
                : (int)player == 1 ? new GridPosition(17, 1)
                : (int)player == 2 ? new GridPosition(17, 17)
                : new GridPosition(1, 17);
            turnCorner.transform.position = BoardCoordinates.ToWorld(position, 16f);
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

        public bool IsManualTopDown => manualTopDown;
    }
}

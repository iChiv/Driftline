using DG.Tweening;
using Draftline.Core;
using UnityEngine;

namespace Draftline.Unity
{
    /// <summary>
    /// Physical roll button on the table edge (Themes/Tabletop/Roll Button). Clickable
    /// only while the match waits for a dice roll; the cap sinks and greys out otherwise.
    /// Visuals live in the scene so they can be repositioned/restyled without code changes.
    /// </summary>
    public sealed class DraftlineRollButton : MonoBehaviour
    {
        [SerializeField] private Transform cap;
        [SerializeField] private Renderer capRenderer;

        private DraftlineGameController controller;
        private Vector3 capUpLocal;
        private Color readyColor = new Color(0.85f, 0.25f, 0.2f);
        private Color idleColor = new Color(0.45f, 0.45f, 0.48f);
        private bool ready;
        private bool pressAnimating;

        public bool IsReady => ready;

        private void Start()
        {
            controller = FindObjectOfType<DraftlineGameController>();
            if (cap == null) cap = transform.Find("Cap");
            if (capRenderer == null && cap != null) capRenderer = cap.GetComponent<Renderer>();
            if (cap != null) capUpLocal = cap.localPosition;
            if (capRenderer != null)
            {
                capRenderer.material = new Material(capRenderer.sharedMaterial);
                if (capRenderer.material.HasProperty("_BaseColor"))
                    readyColor = capRenderer.material.GetColor("_BaseColor");
                else if (capRenderer.material.HasProperty("_Color"))
                    readyColor = capRenderer.material.GetColor("_Color");
            }
            ApplyState(false, true);
        }

        private void Update()
        {
            var canRoll = controller != null && controller.Rules != null &&
                          controller.Rules.State.Phase == MatchPhase.WaitRoll &&
                          !controller.IsAnimating && !controller.IsDiceRolling;
            if (canRoll != ready) ApplyState(canRoll, false);
        }

        /// <summary>Called by DraftlineTabletopInteraction when the button is clicked.</summary>
        public void TryPress()
        {
            if (!ready || pressAnimating || controller == null) return;
            pressAnimating = true;
            if (cap != null)
            {
                cap.DOKill();
                cap.DOLocalMoveY(capUpLocal.y - 0.07f, 0.06f).SetEase(Ease.InQuad)
                    .OnComplete(() =>
                    {
                        cap.DOLocalMoveY(capUpLocal.y, 0.16f).SetEase(Ease.OutBack);
                        pressAnimating = false;
                    });
            }
            controller.RollFromUI();
        }

        private void ApplyState(bool isReady, bool instant)
        {
            ready = isReady;
            if (cap != null)
            {
                cap.DOKill();
                var targetY = isReady ? capUpLocal.y : capUpLocal.y - 0.07f;
                if (instant) cap.localPosition = new Vector3(capUpLocal.x, targetY, capUpLocal.z);
                else cap.DOLocalMoveY(targetY, 0.18f).SetEase(Ease.OutQuad);
            }
            if (capRenderer != null)
            {
                var color = isReady ? readyColor : idleColor;
                if (capRenderer.material.HasProperty("_BaseColor"))
                    capRenderer.material.DOColor(color, "_BaseColor", instant ? 0f : 0.18f);
                else if (capRenderer.material.HasProperty("_Color"))
                    capRenderer.material.DOColor(color, "_Color", instant ? 0f : 0.18f);
            }
        }
    }
}

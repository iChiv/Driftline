using DG.Tweening;
using Draftline.Core;
using UnityEngine;

namespace Draftline.Unity
{
    /// <summary>
    /// Clickable dice resting on the table edge (Themes/Tabletop/Roll Dice) — click it to
    /// roll. While the match waits for a roll the dice glows and slowly tumbles; at other
    /// times it sits still and dimmed. Visuals live in the scene for free restyling.
    /// </summary>
    public sealed class DraftlineRollDice : MonoBehaviour
    {
        private DraftlineGameController controller;
        private Renderer[] renderers;
        private Material[] instances;
        private Color[] readyColors;
        private readonly Color idleTint = new Color(0.42f, 0.42f, 0.46f);
        private Vector3 restLocal;
        private bool ready;
        private bool pressAnimating;
        private Tween idleSpin;
        private Tween idleBob;

        private void Start()
        {
            controller = FindObjectOfType<DraftlineGameController>();
            restLocal = transform.localPosition;
            renderers = GetComponentsInChildren<Renderer>();
            instances = new Material[renderers.Length];
            readyColors = new Color[renderers.Length];
            for (var i = 0; i < renderers.Length; i++)
            {
                instances[i] = renderers[i].material;
                readyColors[i] = instances[i].HasProperty("_BaseColor")
                    ? instances[i].GetColor("_BaseColor")
                    : instances[i].HasProperty("_Color")
                        ? instances[i].GetColor("_Color")
                        : Color.white;
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

        /// <summary>Called by DraftlineTabletopInteraction when the dice is clicked.</summary>
        public void TryPress()
        {
            if (!ready || pressAnimating || controller == null) return;
            pressAnimating = true;
            StopIdle();
            transform.DOKill();
            // 原地弹跳一下再出手。
            transform.DOLocalJump(restLocal + Vector3.up * 0.35f, 0.35f, 1, 0.35f)
                .SetEase(Ease.OutQuad);
            transform.DORotate(new Vector3(0f, 180f, 0f), 0.35f, RotateMode.LocalAxisAdd)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    pressAnimating = false;
                    if (ready) StartIdle();
                });
            controller.RollFromUI();
        }

        private void ApplyState(bool isReady, bool instant)
        {
            ready = isReady;
            if (isReady) StartIdle();
            else StopIdle();
            for (var i = 0; i < instances.Length; i++)
            {
                var material = instances[i];
                if (material == null) continue;
                var color = isReady ? readyColors[i] : readyColors[i] * idleTint;
                var prop = material.HasProperty("_BaseColor") ? "_BaseColor" :
                           material.HasProperty("_Color") ? "_Color" : null;
                if (prop == null) continue;
                if (instant) material.SetColor(prop, color);
                else material.DOColor(color, prop, 0.25f);
            }
        }

        private void StartIdle()
        {
            if (idleSpin != null) return;
            idleSpin = transform.DORotate(new Vector3(0f, 360f, 0f), 6f, RotateMode.LocalAxisAdd)
                .SetEase(Ease.Linear).SetLoops(-1);
            idleBob = transform.DOLocalMoveY(restLocal.y + 0.08f, 1.1f)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
        }

        private void StopIdle()
        {
            idleSpin?.Kill();
            idleSpin = null;
            idleBob?.Kill();
            idleBob = null;
            if (!pressAnimating)
            {
                transform.DOKill();
                transform.DOLocalMoveY(restLocal.y, 0.25f).SetEase(Ease.OutQuad);
            }
        }

        private void OnDestroy()
        {
            StopIdle();
            transform.DOKill();
        }
    }
}

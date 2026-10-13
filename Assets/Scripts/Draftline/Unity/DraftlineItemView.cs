using DG.Tweening;
using Draftline.Core;
using UnityEngine;

namespace Draftline.Unity
{
    /// <summary>
    /// Floating, spinning marker for one FieldItem on the board. Buried items (under a
    /// risen rotating wall) sink and dim until the wall recedes.
    /// </summary>
    public sealed class DraftlineItemView : MonoBehaviour
    {
        public int ItemId;
        public ItemType Type;
        public GridPosition Position { get; private set; }

        private float baseY;
        private Tween spin;
        private Tween bob;
        private bool buried;

        public static DraftlineItemView Create(Transform parent, FieldItem item, Material material)
        {
            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = $"Item {item.Type} {item.Id}";
            Destroy(visual.GetComponent<Collider>());
            visual.transform.SetParent(parent, false);
            var view = visual.AddComponent<DraftlineItemView>();
            view.ItemId = item.Id;
            view.Type = item.Type;
            view.Position = item.Position;
            view.baseY = 1.1f;
            visual.transform.position = BoardCoordinates.ToWorld(item.Position, view.baseY);
            visual.transform.localScale = Vector3.one * 0.42f;
            visual.GetComponent<Renderer>().sharedMaterial = material;
            view.SpawnPop();
            view.StartLoops();
            return view;
        }

        private void StartLoops()
        {
            StopLoops();
            spin = transform.DORotate(new Vector3(0f, 360f, 0f), 2.4f, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear).SetLoops(-1);
            bob = transform.DOMoveY(baseY + 0.12f, 0.9f)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
        }

        private void StopLoops()
        {
            spin?.Kill();
            bob?.Kill();
        }

        private void SpawnPop()
        {
            var target = transform.localScale;
            transform.localScale = Vector3.zero;
            transform.DOScale(target, 0.22f).SetEase(Ease.OutBack);
        }

        public void SetBuried(bool isBuried)
        {
            if (buried == isBuried) return;
            buried = isBuried;
            if (isBuried)
            {
                StopLoops();
                transform.DOScale(Vector3.one * 0.2f, 0.2f).SetEase(Ease.InQuad);
                transform.DOMoveY(baseY - 0.45f, 0.2f).SetEase(Ease.InQuad);
            }
            else
            {
                transform.DOScale(Vector3.one * 0.42f, 0.22f).SetEase(Ease.OutBack);
                transform.DOMoveY(baseY, 0.22f).SetEase(Ease.OutQuad)
                    .OnComplete(StartLoops);
            }
        }

        // Fly to the collecting piece, then vanish.
        public Tween Collect(Vector3 pieceWorld)
        {
            StopLoops();
            var sequence = DOTween.Sequence();
            sequence.Append(transform.DOMove(pieceWorld + Vector3.up * 0.4f, 0.22f).SetEase(Ease.InQuad));
            sequence.Join(transform.DOScale(Vector3.zero, 0.22f).SetEase(Ease.InQuad));
            sequence.OnComplete(() =>
            {
                if (this != null) Destroy(gameObject);
            });
            return sequence;
        }

        private void OnDestroy()
        {
            StopLoops();
            transform.DOKill();
        }
    }
}

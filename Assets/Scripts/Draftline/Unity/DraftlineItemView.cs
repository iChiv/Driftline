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
        private Vector3 baseScale;
        private Tween spin;
        private Tween bob;
        private bool buried;
        private static Transform templateRoot;
        private static bool templateSearched;

        public static DraftlineItemView Create(Transform parent, FieldItem item, Material material)
        {
            // 优先克隆场景里的形状模板（Themes/Tabletop/Item Templates，inactive），没有则退回小球。
            // GameObject.Find 找不到 inactive 对象，所以用 FindObjectsOfType(true) 并缓存。
            GameObject visual = null;
            if (!templateSearched)
            {
                templateSearched = true;
                foreach (var transform in Object.FindObjectsOfType<Transform>(true))
                    if (transform.name == "Item Templates") { templateRoot = transform; break; }
            }
            if (templateRoot != null)
            {
                var template = templateRoot.Find("Template " + item.Type);
                if (template != null)
                {
                    visual = Object.Instantiate(template.gameObject);
                    visual.SetActive(true);
                }
            }
            if (visual == null)
            {
                visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                visual.transform.localScale = Vector3.one * 0.42f;
                visual.GetComponent<Renderer>().sharedMaterial = material;
            }
            visual.name = $"Item {item.Type} {item.Id}";
            foreach (var collider in visual.GetComponentsInChildren<Collider>())
                Object.Destroy(collider);
            visual.transform.SetParent(parent, false);
            var view = visual.AddComponent<DraftlineItemView>();
            view.ItemId = item.Id;
            view.Type = item.Type;
            view.Position = item.Position;
            view.baseY = 1.1f;
            visual.transform.position = BoardCoordinates.ToWorld(item.Position, view.baseY);
            if (visual.transform.localScale == Vector3.one)
                visual.transform.localScale = Vector3.one * 0.42f;
            view.baseScale = visual.transform.localScale;
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
                transform.DOScale(baseScale * 0.5f, 0.2f).SetEase(Ease.InQuad);
                transform.DOMoveY(baseY - 0.45f, 0.2f).SetEase(Ease.InQuad);
            }
            else
            {
                transform.DOScale(baseScale, 0.22f).SetEase(Ease.OutBack);
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

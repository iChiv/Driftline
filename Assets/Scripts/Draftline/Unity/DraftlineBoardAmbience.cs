using System.Linq;
using DG.Tweening;
using Draftline.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Draftline.Unity
{
    /// <summary>
    /// Board-surface ambience: clicking an empty floor cell (no piece, no item, no wall)
    /// spawns a small ripple ring on that cell. Piece/prop/button clicks are left to their
    /// own handlers; off-board table clicks are handled by DraftlineTabletopInteraction.
    /// </summary>
    public sealed class DraftlineBoardAmbience : MonoBehaviour
    {
        private DraftlineGameController controller;
        private Material rippleMaterial;

        private void Start()
        {
            controller = GetComponent<DraftlineGameController>();
            if (controller == null) controller = FindObjectOfType<DraftlineGameController>();
        }

        private void Update()
        {
            if (controller == null || controller.Rules == null ||
                controller.Rules.State.Phase == MatchPhase.GameOver) return;
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || Camera.main == null) return;
            if (!Physics.Raycast(Camera.main.ScreenPointToRay(mouse.position.ReadValue()), out var hit))
                return;

            // 各自有交互的对象不涟漪。
            if (hit.collider.GetComponentInParent<DraftlinePieceView>() != null) return;
            if (hit.collider.GetComponentInParent<DraftlineItemView>() != null) return;
            if (hit.collider.GetComponentInParent<DraftlineToyProp>() != null) return;
            if (hit.collider.GetComponentInParent<DraftlineRollButton>() != null) return;
            // 命中墙体（含升起状态的旋转墙，点在墙侧面会投影到相邻地板格）不涟漪。
            for (var t = hit.collider.transform; t != null; t = t.parent)
                if (t.name == "Walls" || t.name == "Border") return;

            // 棋盘外交给 TabletopInteraction。
            if (!BoardCoordinates.TryFromWorld(hit.point, out var cell)) return;

            var rules = controller.Rules;
            var kind = rules.Board.CellAt(cell).Kind;
            if (kind == CellKind.FixedWall) return;
            // IsScheduledWall 只对旋转墙槽位合法，其他格子会抛异常。
            if (kind == CellKind.RotatingWallSlot &&
                rules.Board.IsScheduledWall(cell, rules.State.WallPhase)) return;
            if (rules.State.Pieces.Any(piece => piece.OnBoard && piece.Position == cell)) return;
            if (rules.State.FieldItems.Any(item => item.Position == cell)) return;

            SpawnRipple(cell, hit.point.y);
        }

        private void SpawnRipple(GridPosition cell, float hitY)
        {
            if (rippleMaterial == null)
                rippleMaterial = Resources.Load<Material>("Materials/TableRipple");
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Cell Ripple";
            Destroy(ring.GetComponent<Collider>());
            ring.transform.position = BoardCoordinates.ToWorld(cell, hitY + 0.03f);
            ring.transform.localScale = new Vector3(0.25f, 0.01f, 0.25f);
            if (rippleMaterial != null)
                ring.GetComponent<Renderer>().sharedMaterial = rippleMaterial;
            ring.transform.DOScale(new Vector3(1.1f, 0.01f, 1.1f), 0.4f).SetEase(Ease.OutQuad);
            ring.transform.DOScale(Vector3.zero, 0.18f).SetDelay(0.38f).SetEase(Ease.InQuad)
                .OnComplete(() => Destroy(ring));
        }
    }
}

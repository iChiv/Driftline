using DG.Tweening;
using Draftline.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Draftline.Unity
{
    /// <summary>
    /// Desktop click interactions only. All visuals (materials, table, props) are baked
    /// into the scene by DraftlineSceneBake — this component just plays the feedback:
    /// prop bounce on prop clicks, a ripple ring on empty-table clicks during a match.
    /// </summary>
    public sealed class DraftlineTabletopInteraction : MonoBehaviour
    {
        private Material rippleMaterial;

        private void Update()
        {
            var controller = GetComponent<DraftlineGameController>();
            if (controller == null || controller.Rules == null ||
                controller.Rules.State.Phase == MatchPhase.GameOver) return;
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || Camera.main == null) return;
            if (!Physics.Raycast(Camera.main.ScreenPointToRay(mouse.position.ReadValue()), out var hit))
                return;
            var rollDice = hit.collider.GetComponentInParent<DraftlineRollDice>();
            if (rollDice != null)
            {
                rollDice.TryPress();
                return;
            }
            var prop = hit.collider.GetComponentInParent<DraftlineToyProp>();
            if (prop != null)
            {
                prop.Play();
                return;
            }
            var point = hit.point;
            if (Mathf.Abs(point.x) > 10.2f || Mathf.Abs(point.z) > 10.2f)
                SpawnRipple(point);
        }

        private void SpawnRipple(Vector3 point)
        {
            if (rippleMaterial == null)
                rippleMaterial = Resources.Load<Material>("Materials/TableRipple");
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Table Ripple";
            Destroy(ring.GetComponent<Collider>());
            ring.transform.position = new Vector3(point.x, 0.05f, point.z);
            ring.transform.localScale = new Vector3(0.3f, 0.01f, 0.3f);
            if (rippleMaterial != null)
                ring.GetComponent<Renderer>().sharedMaterial = rippleMaterial;
            ring.transform.DOScale(new Vector3(2.2f, 0.01f, 2.2f), 0.55f).SetEase(Ease.OutQuad);
            ring.transform.DOScale(Vector3.zero, 0.2f).SetDelay(0.5f).SetEase(Ease.InQuad)
                .OnComplete(() => Destroy(ring));
        }
    }
}

using UnityEngine;

namespace Draftline.Unity
{
    [CreateAssetMenu(menuName = "Driftline/Board Interaction Style")]
    public sealed class BoardInteractionStyle : ScriptableObject
    {
        public GameObject TargetPrefab;
        public GameObject SelectionPrefab;
        public GameObject ContextMenuPrefab;
        public Mesh StopTargetMesh;
        public Vector3 ContextOffset = new Vector3(0,1.9f,0);
        public Material PathMaterial;
        public Color Normal = new Color(0.28f, 0.90f, 0.70f);
        public Color Stop = new Color(1f, 0.73f, 0.25f);
        public Color Selected = new Color(1f, 0.87f, 0.47f);
        public Color Hover = Color.white;
        public Color Danger = new Color(1f,.32f,.25f);
        public float TargetHeight = 0.70f;
        public float PathHeight = 0.72f;
        public float PathWidth = 0.07f;
        public Vector3 TargetScale = new Vector3(0.68f, 0.06f, 0.68f);
        public float PointerHitRadius = 0.5f;
    }
}

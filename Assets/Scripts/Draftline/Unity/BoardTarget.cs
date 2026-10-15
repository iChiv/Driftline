using Draftline.Core;
using UnityEngine;

namespace Draftline.Unity
{
    public sealed class BoardTarget : MonoBehaviour
    {
        public int PieceId, Revision, StopIndex = -1;
        public SlideDirection Direction;
        public bool Deploy, DirectionChoice;
        public GridPosition Cell;
        private Vector3 restingScale;
        private Color restingColor;
        private Renderer surface;
        private MaterialPropertyBlock tint;

        public void SetAppearance(Color color)
        {
            restingScale = transform.localScale;
            restingColor = color;
            surface = GetComponent<Renderer>();
            tint = new MaterialPropertyBlock();
            SetHovered(false);
        }

        public void SetHovered(bool value)
        {
            if (surface == null) return;
            transform.localScale = restingScale * (value ? 1.13f : 1f);
            var color = value ? Color.Lerp(restingColor, Color.white, .28f) : restingColor;
            tint.SetColor("_BaseColor", color);
            tint.SetColor("_Color", color);
            surface.SetPropertyBlock(tint);
        }
    }
}

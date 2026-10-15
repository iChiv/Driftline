using UnityEngine;

namespace Draftline.Unity
{
    /// <summary>Only faction bases receive faction tint; imported model palettes remain intact.</summary>
    public sealed class ThemePieceAppearance : MonoBehaviour
    {
        public Renderer[] FactionRenderers;
    }
}

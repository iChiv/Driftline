using DG.Tweening;
using Draftline.Core;
using UnityEngine;

namespace Draftline.Unity
{
    /// <summary>Slow spinning glow pad for a portal cell. Presentation only.</summary>
    public sealed class DraftlinePortalSpin : MonoBehaviour
    {
        private void Update()
        {
            transform.Rotate(0f, 28f * Time.deltaTime, 0f, Space.Self);
        }
    }
}

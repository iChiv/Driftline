using DG.Tweening;
using UnityEngine;

namespace Draftline.Unity
{
    /// <summary>Clickable toy prop on the table; plays a small action when clicked.</summary>
    public sealed class DraftlineToyProp : MonoBehaviour
    {
        public void Play()
        {
            transform.DOKill();
            transform.DOPunchScale(Vector3.one * 0.2f, 0.4f, 6, 0.5f);
            transform.DORotate(new Vector3(0, 360f, 0), 0.7f, RotateMode.LocalAxisAdd)
                .SetEase(Ease.OutQuad);
        }
    }
}

using UnityEngine;

namespace Draftline.Unity
{
    [CreateAssetMenu(menuName="Driftline/Match Feedback Profile")]
    public sealed class MatchFeedbackProfile : ScriptableObject
    {
        public GameObject Capture, Portal, Victory;
        public float CaptureLifetime=1.8f, PortalLifetime=1.2f, VictoryLifetime=3f;
        public float CaptureScale=.55f, PortalScale=.35f, VictoryScale=.6f;
        public int MaxConcurrentEffects=6;
    }
}

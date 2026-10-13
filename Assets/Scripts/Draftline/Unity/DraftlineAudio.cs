using System.Collections.Generic;
using UnityEngine;

namespace Draftline.Unity
{
    public enum SfxType
    {
        DiceRoll, DiceLand, Slide, Portal, ItemPickup, ItemActivate,
        Capture, WallRise, WallFall, Victory, TurnChange
    }

    /// <summary>
    /// Central sound hooks. Clips are optional: drop assets into Assets/Resources/Sfx/
    /// named after SfxType entries and they play; until then every call is a silent no-op.
    /// Presentation only — no rule state lives here.
    /// </summary>
    public sealed class DraftlineAudio : MonoBehaviour
    {
        private readonly Dictionary<SfxType, AudioClip> clips = new Dictionary<SfxType, AudioClip>();
        private AudioSource source;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            foreach (SfxType type in System.Enum.GetValues(typeof(SfxType)))
            {
                var clip = Resources.Load<AudioClip>($"Sfx/{type}");
                if (clip != null) clips[type] = clip;
            }
        }

        public void Play(SfxType type, float volume = 1f)
        {
            if (source == null) return;
            if (clips.TryGetValue(type, out var clip) && clip != null)
                source.PlayOneShot(clip, volume);
        }
    }
}

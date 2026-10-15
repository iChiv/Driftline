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
        [System.Serializable]
        public sealed class ClipBinding { public SfxType Event; public AudioClip Clip; }
        public ClipBinding[] Overrides;
        public AudioSource BackgroundMusic;
        private readonly Dictionary<SfxType, AudioClip> clips = new Dictionary<SfxType, AudioClip>();
        private AudioSource source;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            if (source == null) { Debug.LogError("AudioSource must be saved on Draftline Game."); return; }
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            foreach (SfxType type in System.Enum.GetValues(typeof(SfxType)))
            {
                var clip = Resources.Load<AudioClip>($"Sfx/{type}");
                if (clip != null) clips[type] = clip;
            }
            if (Overrides != null)
                foreach (var binding in Overrides)
                    if (binding != null && binding.Clip != null) clips[binding.Event] = binding.Clip;
            if (BackgroundMusic != null && BackgroundMusic.clip != null) BackgroundMusic.Play();
        }

        public void Play(SfxType type, float volume = 1f)
        {
            if (source == null) return;
            if (clips.TryGetValue(type, out var clip) && clip != null)
                source.PlayOneShot(clip, volume);
        }
    }
}

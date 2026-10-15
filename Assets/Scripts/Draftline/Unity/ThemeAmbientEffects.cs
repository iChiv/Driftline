using UnityEngine;

namespace Draftline.Unity
{
    public sealed class ThemeAmbientEffects : MonoBehaviour
    {
        ParticleSystem[] loops;
        bool reduced;
        void OnEnable(){loops=System.Array.FindAll(GetComponentsInChildren<ParticleSystem>(true),p=>p.main.loop);Apply();}
        void Update(){if(reduced!=(PlayerPrefs.GetInt("Driftline.SimpleEffects",0)==1))Apply();}
        public void Apply(){if(loops==null)return;reduced=PlayerPrefs.GetInt("Driftline.SimpleEffects",0)==1;foreach(var p in loops){if(reduced)p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);else if(p.gameObject.activeInHierarchy)p.Play();}}
    }
}

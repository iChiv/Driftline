using System.Collections.Generic;
using UnityEngine;

namespace Draftline.Unity
{
    /// <summary>Four distinct discoveries light the local scene's centrepiece, with no rule reward.</summary>
    public sealed class ThemeSceneryEnsemble : MonoBehaviour
    {
        public ParticleSystem Discovery;
        public Light Beacon;
        readonly HashSet<ThemeSceneryInteraction> touched=new HashSet<ThemeSceneryInteraction>();
        float until;
        public int Discoveries {get;private set;}
        public void Touched(ThemeSceneryInteraction prop)
        {
            if(!touched.Add(prop) || touched.Count<4)return;
            touched.Clear();Discoveries++;until=Time.unscaledTime+3;
            if(Discovery!=null && PlayerPrefs.GetInt("Driftline.SimpleEffects",0)==0)Discovery.Play();
        }
        void Update(){if(Beacon!=null)Beacon.intensity=Time.unscaledTime<until?3.5f:0;}
        void OnDisable(){touched.Clear();if(Beacon!=null)Beacon.intensity=0;}
    }
}

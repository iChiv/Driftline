using System;
using UnityEngine;

namespace Draftline.Unity
{
    /// <summary>Authored visual states only. This RNG never participates in the match simulation.</summary>
    public sealed class ThemeEnvironment : MonoBehaviour
    {
        public GameObject Standard, Alternate;
        public GameObject[] SeededDetails = Array.Empty<GameObject>();
        public Light KeyLight;
        public Color StandardLight = Color.white, AlternateLight = new Color(1,.65f,.4f);
        public Color StandardAmbient = Color.gray, AlternateAmbient = Color.gray;
        public Color StandardBackground, AlternateBackground;
        public float StandardIntensity = 1.1f, AlternateIntensity = .75f;
        public int Variant { get; private set; }
        public int Seed { get; private set; }

        public void Apply(int variant, int seed)
        {
            Variant = Mathf.Clamp(variant,0,1); Seed = seed;
            if(Standard!=null)Standard.SetActive(Variant==0);
            if(Alternate!=null)Alternate.SetActive(Variant==1);
            var random=new System.Random(seed);
            foreach(var detail in SeededDetails)if(detail!=null)detail.SetActive(random.NextDouble()>.35);
            if(KeyLight!=null){KeyLight.color=Variant==0?StandardLight:AlternateLight;KeyLight.intensity=Variant==0?StandardIntensity:AlternateIntensity;}
            RenderSettings.ambientLight=Variant==0?StandardAmbient:AlternateAmbient;
            var backdrop=UnityEngine.Object.FindObjectOfType<ThemeBackdrop>();
            if(backdrop!=null)backdrop.SetEnvironment(Variant==0?StandardBackground:AlternateBackground);
            else if(Camera.main!=null)Camera.main.backgroundColor=Variant==0?StandardBackground:AlternateBackground;
            GetComponent<ThemeAmbientEffects>()?.Apply();
        }
    }
}

using UnityEngine;

namespace Draftline.Unity
{
    /// <summary>Local presentation only; interpolates a shared sky shader without relighting the board.</summary>
    public sealed class ThemeBackdrop : MonoBehaviour
    {
        public float TransitionSeconds=1f;
        public bool UseGradient=true;
        public string ThemeId {get;private set;}
        public bool IsTransitioning => elapsed<TransitionSeconds;
        public Color CurrentLow {get;private set;}
        public Color CurrentHigh {get;private set;}
        Material instance,previousSky;
        BoardTheme theme;
        Color fromLow,fromHigh,fromHaze,toLow,toHigh,toHaze,currentHaze;
        float elapsed;
        bool initialized;
        public void Apply(BoardTheme value)
        {
            if(value==null)return;
            theme=value;ThemeId=value.ThemeId;
            if(Application.isPlaying&&instance==null&&value.BackgroundMaterial!=null)
            {previousSky=RenderSettings.skybox;instance=new Material(value.BackgroundMaterial){name="Runtime theme atmosphere"};}
            SetEnvironment(value.BackgroundColor);
        }
        public void SetEnvironment(Color low)
        {
            if(theme==null)return;
            fromLow=CurrentLow;fromHigh=CurrentHigh;fromHaze=currentHaze;
            toLow=Color.Lerp(theme.BackgroundColor,low,.35f);
            toHigh=Color.Lerp(theme.BackgroundTop,low,.15f);
            toHaze=Color.Lerp(theme.BackgroundHaze,low,.2f);
            elapsed=0;
            if(!initialized||!Application.isPlaying){CurrentLow=toLow;CurrentHigh=toHigh;currentHaze=toHaze;elapsed=TransitionSeconds;initialized=true;}
            Render();
        }
        void Update()
        {
            if(theme==null)return;
            elapsed+=Time.unscaledDeltaTime;
            float t=TransitionSeconds<=0?1:Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/TransitionSeconds));
            CurrentLow=Color.Lerp(fromLow,toLow,t);CurrentHigh=Color.Lerp(fromHigh,toHigh,t);currentHaze=Color.Lerp(fromHaze,toHaze,t);
            Render();
        }
        void Render()
        {
            var camera=Camera.main;if(camera==null)return;
            camera.backgroundColor=CurrentLow;
            var material=Application.isPlaying?instance:theme.BackgroundMaterial;
            if(UseGradient&&material!=null)
            {
                material.SetColor("_Low",CurrentLow);material.SetColor("_High",CurrentHigh);material.SetColor("_Haze",currentHaze);
                RenderSettings.skybox=material;camera.clearFlags=CameraClearFlags.Skybox;
            }
            else camera.clearFlags=CameraClearFlags.SolidColor;
        }
        void OnDestroy(){if(instance!=null){if(RenderSettings.skybox==instance)RenderSettings.skybox=previousSky;if(Application.isPlaying)Destroy(instance);else DestroyImmediate(instance);}}
    }
}

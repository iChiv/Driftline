using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Draftline.Unity
{
    /// <summary>Explicit trigger hotspot, isolated from gameplay physics and rule state.</summary>
    public sealed class ThemeSceneryInteraction : MonoBehaviour
    {
        [Serializable] public sealed class Motion
        {
            public Transform Target;
            public Vector3 Translation, Rotation, Scale;
            [NonSerialized] public Vector3 Position;
            [NonSerialized] public Quaternion Orientation;
            [NonSerialized] public Vector3 OriginalScale;
        }
        public string Label;
        [TextArea] public string Hint;
        public bool Toggle;
        public float Duration=1.8f, Cooldown=.25f;
        public Motion[] Motions=Array.Empty<Motion>();
        public ParticleSystem Effect;
        public Light ResponseLight;
        public Renderer Indicator;
        public AudioSource Sound;
        public GameObject ActiveDecoration;
        public bool DecorationRequiresEffects;
        public int Activations {get;private set;}
        public bool IsOn {get;private set;}
        public bool Busy {get;private set;}
        float nextTime, baseIntensity;
        Coroutine routine;
        MaterialPropertyBlock block;
        void Awake()
        {
            foreach(var m in Motions)if(m.Target!=null){m.Position=m.Target.localPosition;m.Orientation=m.Target.localRotation;m.OriginalScale=m.Target.localScale;}
            if(ResponseLight!=null)baseIntensity=ResponseLight.intensity;
            block=new MaterialPropertyBlock();
        }
        public bool Activate()
        {
            if(Busy || Time.unscaledTime<nextTime)return false;
            nextTime=Time.unscaledTime+Cooldown;Activations++;IsOn=Toggle?!IsOn:true;
            if(Sound!=null)Sound.Play();
            if(Effect!=null && PlayerPrefs.GetInt("Driftline.SimpleEffects",0)==0)Effect.Play();
            routine=StartCoroutine(Animate(IsOn?1:0));
            if(ActiveDecoration!=null)ActiveDecoration.SetActive(IsOn&&(!DecorationRequiresEffects||PlayerPrefs.GetInt("Driftline.SimpleEffects",0)==0));
            var ensemble=GetComponentInParent<ThemeSceneryEnsemble>();if(ensemble!=null)ensemble.Touched(this);
            return true;
        }
        IEnumerator Animate(float destination)
        {
            Busy=true;float from=Toggle?1-destination:0;
            for(float elapsed=0;elapsed<Duration;elapsed+=Time.unscaledDeltaTime)
            {
                var t=Mathf.Clamp01(elapsed/Duration);
                var amount=Toggle?Mathf.SmoothStep(from,destination,t):Mathf.Sin(t*Mathf.PI);
                foreach(var m in Motions)if(m.Target!=null){m.Target.localPosition=m.Position+m.Translation*amount;m.Target.localRotation=m.Orientation*Quaternion.Euler(m.Rotation*amount);m.Target.localScale=m.OriginalScale+m.Scale*amount;}
                if(ResponseLight!=null)ResponseLight.intensity=baseIntensity+amount*2;
                yield return null;
            }
            foreach(var m in Motions)if(m.Target!=null){m.Target.localPosition=m.Position+m.Translation*(Toggle?destination:0);m.Target.localRotation=m.Orientation*Quaternion.Euler(m.Rotation*(Toggle?destination:0));m.Target.localScale=m.OriginalScale+m.Scale*(Toggle?destination:0);}
            if(ResponseLight!=null)ResponseLight.intensity=baseIntensity+(Toggle?destination:0)*2;
            if(!Toggle)IsOn=false;Busy=false;routine=null;
        }
        public void Hover(bool hovered)
        {
            if(Indicator==null)return;
            if(block==null)block=new MaterialPropertyBlock();
            block.SetColor("_EmissionColor",hovered?new Color(.4f,.8f,1)*2:Color.black);
            Indicator.SetPropertyBlock(block);
        }
        void OnDisable()
        {
            if(routine!=null)StopCoroutine(routine);routine=null;
            foreach(var m in Motions)if(m.Target!=null){m.Target.localPosition=m.Position;m.Target.localRotation=m.Orientation;m.Target.localScale=m.OriginalScale;}
            if(ResponseLight!=null)ResponseLight.intensity=baseIntensity;
            if(Effect!=null)Effect.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(Sound!=null)Sound.Stop();Busy=IsOn=false;Hover(false);
            if(ActiveDecoration!=null)ActiveDecoration.SetActive(false);
        }

        public static ThemeSceneryInteraction Hovered {get;private set;}
        void Update(){if(ActiveDecoration!=null&&DecorationRequiresEffects)ActiveDecoration.SetActive(IsOn&&PlayerPrefs.GetInt("Driftline.SimpleEffects",0)==0);}
        /// <summary>Only called once by BoardInteractionController, before its authority gate.</summary>
        public static bool ProcessPointer(BoardInteractionController board)
        {
            var mouse=Mouse.current;var camera=Camera.main;
            ThemeSceneryInteraction hitProp=null;
            var ui=board.Controller.CanvasUI;
            if(mouse!=null && camera!=null && (ui==null || !ui.EnvironmentInputBlocked) && !board.IsPointerOverUI(mouse.position.ReadValue()))
            {
                var ray=camera.ScreenPointToRay(mouse.position.ReadValue());
                // No scenery hotspot may steal a click whose ray lands inside the playable core.
                var plane=new Plane(Vector3.up,new Vector3(0,.5f,0));
                if(plane.Raycast(ray,out var distance))
                {
                    var p=ray.GetPoint(distance);
                    if(Mathf.Abs(p.x)>9.65f || Mathf.Abs(p.z)>9.65f)
                    {
                        var hits=Physics.RaycastAll(ray,100,1<<2,QueryTriggerInteraction.Collide);
                        float closest=float.MaxValue;
                        foreach(var hit in hits){var prop=hit.collider.GetComponent<ThemeSceneryInteraction>();if(prop!=null && hit.distance<closest){closest=hit.distance;hitProp=prop;}}
                    }
                }
            }
            if(Hovered!=hitProp){if(Hovered!=null)Hovered.Hover(false);Hovered=hitProp;if(hitProp!=null)hitProp.Hover(true);}
            if(hitProp==null || !mouse.leftButton.wasPressedThisFrame)return false;
            hitProp.Activate();return true;
        }
    }
}

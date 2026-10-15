using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Draftline.Unity;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Draftline.EditorTools
{
    /// <summary>Opt-in captures of the actual Game View, including overlay UI. Never included in players.</summary>
    public static class BattlefieldReviewCapture
    {
        static readonly Queue<Action> jobs=new Queue<Action>();
        static double next;
        static DraftlineCanvasUI ui;
        public static bool Busy=>jobs.Count>0||video;
        static bool video;
        static int frame,videoTheme;
        static double videoStart,lastFrame;
        static readonly string[] ids={"fortress","dungeon","hangar","forest","harbor"};
        public static string CaptureStills()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Capture in Play Mode.");
            ui=Object.FindObjectOfType<DraftlineCanvasUI>();jobs.Clear();
            Directory.CreateDirectory("Docs/Review/BattlefieldsV2");
            foreach(var id in ids){var selected=id;
                jobs.Enqueue(()=>Open(selected,0));jobs.Enqueue(()=>{Capture(selected+"-standard.png");CaptureCorner(selected);});
                jobs.Enqueue(()=>DraftlineThemeApplier.ApplyEnvironment(1,74019));jobs.Enqueue(()=>Capture(selected+"-alternate.png"));
            }
            jobs.Enqueue(()=>{ui.ReturnMenu.onClick.Invoke();ui.ThemeSelector.value=1;ui.EnvironmentSelector.value=0;});jobs.Enqueue(()=>Capture("main-menu.png"));
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;next=EditorApplication.timeSinceStartup+.3;return "Queued 11 native Game View screenshots";
        }
        static void Open(string id,int variant)
        {
            ui.ReturnMenu.onClick.Invoke();ui.ThemeSelector.value=Array.FindIndex(DraftlineThemeApplier.Available,t=>t.ThemeId==id);DraftlineThemeApplier.ApplyEnvironment(variant,74019);
            ui.LocalGame.onClick.Invoke();ui.StartMatch.onClick.Invoke();
        }
        static void Capture(string name){ScreenCapture.CaptureScreenshot("Docs/Review/BattlefieldsV2/"+name);}
        static void CaptureCorner(string id)
        {
            var go=new GameObject("Review detail camera",typeof(Camera));var camera=go.GetComponent<Camera>();camera.CopyFrom(Camera.main);camera.enabled=false;
            var target=new Vector3(11.4f,.45f,11.4f);camera.transform.position=target+new Vector3(-5.4f,5.8f,-6.5f);camera.transform.LookAt(target);camera.fieldOfView=43;
            var rt=RenderTexture.GetTemporary(1280,900,24);var old=RenderTexture.active;camera.targetTexture=rt;camera.aspect=1280f/900;
            try{camera.Render();RenderTexture.active=rt;var image=new Texture2D(1280,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,900),0,0);image.Apply();File.WriteAllBytes("Docs/Review/BattlefieldsV2/"+id+"-corner.png",image.EncodeToPNG());Object.DestroyImmediate(image);}finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(go);}
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying){jobs.Clear();EditorApplication.update-=Tick;return;}
            if(EditorApplication.timeSinceStartup<next)return;
            if(jobs.Count==0){EditorApplication.update-=Tick;return;}
            jobs.Dequeue()();next=EditorApplication.timeSinceStartup+1.0;
        }
        public static string CaptureMotion()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Capture in Play Mode.");
            Directory.CreateDirectory("Temp/BattlefieldMotion");ui=Object.FindObjectOfType<DraftlineCanvasUI>();
            video=true;frame=0;videoTheme=-1;videoStart=EditorApplication.timeSinceStartup;lastFrame=0;
            EditorApplication.update-=VideoTick;EditorApplication.update+=VideoTick;return "Capturing 25 seconds / five themes, all four interactions";
        }
        static void VideoTick()
        {
            var now=EditorApplication.timeSinceStartup;var time=now-videoStart;
            if(!EditorApplication.isPlaying||time>=25){video=false;EditorApplication.update-=VideoTick;File.WriteAllText("Temp/BattlefieldMotion/capture.json",JsonUtility.ToJson(new Record{frames=frame,seconds=(float)time}));return;}
            int index=(int)(time/5);
            if(index!=videoTheme){videoTheme=index;Open(ids[index],1);}
            if(time%5>1.1 && time%5<1.5)foreach(var prop in Object.FindObjectsOfType<ThemeSceneryInteraction>())if(prop.Activations==0||(!prop.Busy&&!prop.IsOn))prop.Activate();
            if(now-lastFrame<.1)return;lastFrame=now;
            ScreenCapture.CaptureScreenshot("Temp/BattlefieldMotion/frame-"+(frame++).ToString("D4")+".png");
        }
        [Serializable] class Record{public int frames;public float seconds;}
    }
}

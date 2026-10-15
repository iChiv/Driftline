#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Draftline.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Draftline.Unity
{
    /// <summary>Opt-in native-player checks, including preferences across two separate launches.</summary>
    public sealed class PresentationSmokeRunner : MonoBehaviour
    {
        [Serializable] sealed class Preferences { public bool hadLanguage,hadTheme;public int language;public string theme; }
        [Serializable] sealed class Entry { public string check,theme,details;public int phase;public bool passed; }
        string report,backup;
        int failures;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartIfRequested()
        {if(Environment.GetCommandLineArgs().Contains("--driftline-presentation-smoke"))new GameObject("Presentation integration probe").AddComponent<PresentationSmokeRunner>();}
        static string Argument(string name,string fallback)
        {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
        void Record(string check,bool passed,string details="",int phase=-1)
        {
            if(!passed)failures++;
            File.AppendAllText(report,JsonUtility.ToJson(new Entry{check=check,theme=DraftlineThemeApplier.CurrentId,phase=phase,passed=passed,details=details})+"\n");
        }
        void CheckWalls(DraftlineGameController controller,int phase)
        {
            var walls=FindObjectsOfType<ThemeMovingWall>();Record("nine-lifts",walls.Length==9,"",phase);
            foreach(var wall in walls)
            {
                var cell=new GridPosition(wall.Column,wall.Row);bool up=controller.Rules.IsWall(cell);
                var renderers=wall.Body.GetComponentsInChildren<Renderer>(true);var bounds=renderers[0].bounds;
                foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
                var point=BoardCoordinates.ToWorld(cell);
                bool geometry=Mathf.Abs(bounds.center.x-point.x)<.04f&&Mathf.Abs(bounds.center.z-point.z)<.04f;
                bool visible=!up||renderers.Any(r=>r.enabled&&!r.forceRenderingOff&&r.gameObject.activeInHierarchy);
                Record("lift-"+wall.Column+"-"+wall.Row,wall.Body.gameObject.activeSelf==up&&Vector3.Distance(wall.Body.localScale,Vector3.one)<.01f&&geometry&&visible&&!renderers.Any(r=>r.isPartOfStaticBatch),"up="+up+" bounds="+bounds,phase);
            }
        }
        void CaptureFrame(DraftlineCanvasUI ui)
        {
            // Hidden test windows do not present a backbuffer. Render explicitly,
            // including the saved Canvas, rather than saving a black screenshot.
            var camera=Camera.main;var canvas=ui.GetComponent<Canvas>();
            var mode=canvas.renderMode;var worldCamera=canvas.worldCamera;var plane=canvas.planeDistance;
            var target=camera.targetTexture;var active=RenderTexture.active;
            var frame=RenderTexture.GetTemporary(1366,768,24);var pixels=new Texture2D(1366,768,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=frame;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=frame;
                pixels.ReadPixels(new Rect(0,0,1366,768),0,0);pixels.Apply();
                File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(report),"native-walls-en.png"),pixels.EncodeToPNG());
                Record("render-has-visible-pixels",pixels.GetPixels32().Where((p,i)=>i%997==0).Select(p=>p.r+256*p.g+65536*p.b).Distinct().Count()>16);
            }
            finally
            {
                camera.targetTexture=target;RenderTexture.active=active;canvas.renderMode=mode;canvas.worldCamera=worldCamera;canvas.planeDistance=plane;
                Canvas.ForceUpdateCanvases();RenderTexture.ReleaseTemporary(frame);Destroy(pixels);
            }
        }
        IEnumerator Start()
        {
            report=Path.GetFullPath(Argument("--report",Path.Combine(Application.temporaryCachePath,"presentation.jsonl")));
            backup=Path.GetFullPath(Argument("--backup",report+".preferences.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(report));File.WriteAllText(report,"");
            yield return null;yield return null;
            var ui=FindObjectOfType<DraftlineCanvasUI>();var controller=FindObjectOfType<DraftlineGameController>();
            if(Argument("--phase","write")=="read")
            {
                var original=JsonUtility.FromJson<Preferences>(File.ReadAllText(backup));
                try
                {
                    Record("language-survives-process-restart",GameLocalization.Language==GameLanguage.English&&ui.GetComponent<LocalizationPresenter>().LanguageSelector.value==1);
                    ui.Refresh();yield return null;
                    Record("english-menu-after-restart",FindObjectsOfType<Text>().All(t=>!GameLocalization.ContainsChinese(t.text)));
                }
                finally
                {
                    if(original.hadLanguage)PlayerPrefs.SetInt(GameLocalization.PreferenceKey,original.language);else PlayerPrefs.DeleteKey(GameLocalization.PreferenceKey);
                    if(original.hadTheme)PlayerPrefs.SetString(DraftlineThemeApplier.PrefKey,original.theme);else PlayerPrefs.DeleteKey(DraftlineThemeApplier.PrefKey);
                    PlayerPrefs.Save();
                }
            }
            else
            {
                File.WriteAllText(backup,JsonUtility.ToJson(new Preferences{hadLanguage=PlayerPrefs.HasKey(GameLocalization.PreferenceKey),language=PlayerPrefs.GetInt(GameLocalization.PreferenceKey),hadTheme=PlayerPrefs.HasKey(DraftlineThemeApplier.PrefKey),theme=PlayerPrefs.GetString(DraftlineThemeApplier.PrefKey)}));
                GameLocalization.SetLanguage(GameLanguage.English);yield return null;
                var themes=DraftlineThemeApplier.Available;
                for(int theme=0;theme<themes.Length;theme++)
                {
                    ui.ThemeSelector.value=theme;ui.LocalGame.onClick.Invoke();ui.StartMatch.onClick.Invoke();yield return new WaitForSecondsRealtime(.5f);
                    for(int phase=0;phase<2;phase++)
                    {
                        controller.Rules.State.WallPhase=phase;controller.Rules.State.Revision++;
                        typeof(DraftlineGameController).GetMethod("SyncViews",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(controller,null);
                        yield return new WaitForSecondsRealtime(.04f);DraftlineThemeApplier.Apply(themes[theme].ThemeId);
                        yield return new WaitForSecondsRealtime(.5f);CheckWalls(controller,phase);
                    }
                    Record("english-dynamic-hud",FindObjectsOfType<Text>().All(t=>!GameLocalization.ContainsChinese(t.text)));
                    if(theme==0)CaptureFrame(ui);
                    controller.RestartMatch();yield return new WaitForSecondsRealtime(.5f);CheckWalls(controller,0);
                    ui.ReturnMenu.onClick.Invoke();yield return null;
                }
                Record("language-saved",PlayerPrefs.GetInt(GameLocalization.PreferenceKey,-1)==1);
            }
            Record("complete",failures==0);Application.Quit(failures==0?0:1);
        }
    }
}
#endif

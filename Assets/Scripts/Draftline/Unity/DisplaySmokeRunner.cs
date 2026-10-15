#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Draftline.Unity
{
    /// <summary>Opt-in Windows-player display verification; preserves the user's preferences.</summary>
    public sealed class DisplaySmokeRunner : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartIfRequested()
        {if(Environment.GetCommandLineArgs().Contains("--driftline-display-smoke"))new GameObject("Display integration probe").AddComponent<DisplaySmokeRunner>();}
        [Serializable] sealed class Entry
        {
            public string check,mode;
            public int requestedWidth,requestedHeight,actualWidth,actualHeight;
            public bool passed;
        }
        string report;
        int failures;
        void Record(string check,DisplaySettingsPanel panel,bool passed)
        {
            if(!passed)failures++;
            File.AppendAllText(report,JsonUtility.ToJson(new Entry{check=check,mode=Screen.fullScreenMode.ToString(),requestedWidth=panel.RequestedSize.x,requestedHeight=panel.RequestedSize.y,actualWidth=Screen.width,actualHeight=Screen.height,passed=passed})+"\n");
        }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int arg=Array.IndexOf(args,"--report");
            report=arg>=0&&arg+1<args.Length?args[arg+1]:Path.Combine(Application.temporaryCachePath,"display-smoke.jsonl");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report)));File.WriteAllText(report,"");
            yield return null;yield return null;
            var ui=FindObjectOfType<DraftlineCanvasUI>();var panel=ui.GetComponent<DisplaySettingsPanel>();
            var keys=new[]{DisplaySettingsPanel.WidthKey,DisplaySettingsPanel.HeightKey,DisplaySettingsPanel.ModeKey};
            var had=keys.Select(PlayerPrefs.HasKey).ToArray();var values=keys.Select(k=>PlayerPrefs.GetInt(k)).ToArray();
            var originalSize=new Vector2Int(Screen.width,Screen.height);var originalMode=Screen.fullScreenMode;
            try
            {
                ui.OpenSettings.onClick.Invoke();
                for(int mode=0;mode<2;mode++)for(int i=0;i<DisplaySettingsPanel.Presets.Length;i++)
                {
                    panel.Resolution.value=i;panel.Mode.value=mode;panel.Apply.onClick.Invoke();
                    yield return new WaitForSecondsRealtime(1f);
                    var size=DisplaySettingsPanel.Presets[i];var expected=mode==0?FullScreenMode.Windowed:FullScreenMode.FullScreenWindow;
                    Record("apply",panel,panel.Pending&&Screen.width==size.x&&Screen.height==size.y&&Screen.fullScreenMode==expected);
                    panel.Keep.onClick.Invoke();
                    Record("persist",panel,!panel.Pending&&PlayerPrefs.GetInt(DisplaySettingsPanel.WidthKey)==size.x&&PlayerPrefs.GetInt(DisplaySettingsPanel.HeightKey)==size.y&&PlayerPrefs.GetInt(DisplaySettingsPanel.ModeKey)==mode);
                }
                var prior=panel.RequestedSize;var priorMode=panel.RequestedMode;
                panel.ConfirmationSeconds=.3f;panel.Resolution.value=0;panel.Mode.value=0;panel.Apply.onClick.Invoke();
                yield return new WaitForSecondsRealtime(.8f);
                Record("timeout-revert",panel,!panel.Pending&&panel.RequestedSize==prior&&Screen.width==prior.x&&Screen.height==prior.y&&Screen.fullScreenMode==priorMode);
                panel.Resolution.value=1;panel.Mode.value=0;panel.Apply.onClick.Invoke();ui.CloseSettings.onClick.Invoke();
                yield return new WaitForSecondsRealtime(.6f);
                Record("close-revert",panel,!panel.Pending&&panel.RequestedSize==prior&&Screen.fullScreenMode==priorMode);
                Record("complete",panel,failures==0);
            }
            finally
            {
                panel.Cancel();
                for(int i=0;i<keys.Length;i++){if(had[i])PlayerPrefs.SetInt(keys[i],values[i]);else PlayerPrefs.DeleteKey(keys[i]);}
                PlayerPrefs.Save();Screen.SetResolution(originalSize.x,originalSize.y,originalMode);
            }
            Application.Quit(failures==0?0:1);
        }
    }
}
#endif

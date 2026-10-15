#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Draftline.Core;
using UnityEngine;

namespace Draftline.Unity
{
    /// <summary>Opt-in development-player integration probe; dormant in normal play.</summary>
    public sealed class LanSmokeRunner : MonoBehaviour
    {
        private string report, role;
        private DraftlineGameController controller;
        private bool reconnecting, reconnected;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartIfRequested()
        {
            var args=Environment.GetCommandLineArgs();
            if(!args.Contains("--driftline-lan-smoke"))return;
            new GameObject("LAN Integration Probe").AddComponent<LanSmokeRunner>();
        }

        [Serializable] private sealed class Entry
        {
            public string role,phase,digest,status,error,theme,preferredTheme;
            public int revision,turn,current,variant,environmentSeed;
            public bool complete,reconnected;
        }

        private IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();
            role=Value(args,"--role","host");
            report=Value(args,"--report",Path.Combine(Application.temporaryCachePath,"driftline-lan-"+role+".jsonl"));
            var port=ushort.Parse(Value(args,"--port","7788"));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report)));
            File.WriteAllText(report,"");
            yield return null;yield return null;yield return null;
            controller=UnityEngine.Object.FindObjectOfType<DraftlineGameController>();
            var requestedTheme=Value(args,"--theme",null);
            if(requestedTheme!=null)DraftlineThemeApplier.SelectLocal(requestedTheme);
            DraftlineThemeApplier.ApplyEnvironment(int.Parse(Value(args,"--variant","0")),int.Parse(Value(args,"--environment-seed","12345")));
            controller.SimpleEffects=true;
            var room=controller.LanRoom;
            if(role=="host") {room.FillWithComputers=true;room.Host(4,port);}
            else room.Join("127.0.0.1",port);
            var started=Time.realtimeSinceStartup;
            var lastRevision=-1;
            var nextAction=0f;
            while(Time.realtimeSinceStartup-started<110f)
            {
                if(role=="client"&&!room.MatchStarted&&room.LocalColor.HasValue&&!room.LocalReady)room.ToggleReady();
                if(role=="host"&&!room.MatchStarted&&room.PlayerCount>=2&&room.CanStart)room.StartHostedMatch();
                var state=controller.Rules?.State;
                if(state!=null&&state.Revision!=lastRevision)
                {
                    lastRevision=state.Revision;
                    Write(new Entry{role=role,revision=state.Revision,turn=state.TurnCount,current=(int)state.CurrentPlayer,
                        phase=state.Phase.ToString(),digest=Digest(state),status=room.Status,reconnected=reconnected});
                    if(state.Revision>=24&&(!reconnecting||reconnected))
                    {
                        yield return new WaitForSecondsRealtime(1.5f);
                        Write(new Entry{role=role,revision=state.Revision,complete=true,reconnected=reconnected,digest=Digest(state)});
                        Application.Quit(0);yield break;
                    }
                }
                if(role=="client"&&!reconnecting&&state!=null&&state.Revision>=6)
                {
                    reconnecting=true;
                    room.Network.Shutdown();
                    yield return new WaitForSecondsRealtime(1.5f);
                    room.Reconnect();
                }
                if(reconnecting&&room.IsClientOnly&&room.Network.IsConnectedClient&&room.LocalColor.HasValue)reconnected=true;
                if(state!=null&&!controller.IsAnimating&&controller.CanControl&&Time.unscaledTime>=nextAction)
                {
                    if(state.Phase==MatchPhase.WaitRoll)controller.RollFromUI();
                    else if(state.Phase!=MatchPhase.GameOver)
                    {
                        var command=ComputerPlanner.Choose(controller.Rules,true);
                        if(command!=null)controller.SubmitCommand(command);
                    }
                    nextAction=Time.unscaledTime+.2f;
                }
                yield return null;
            }
            Write(new Entry{role=role,error="Timed out",status=room.Status});
            Application.Quit(2);
        }

        private void Write(Entry entry){entry.theme=DraftlineThemeApplier.CurrentId;entry.preferredTheme=DraftlineThemeApplier.PreferredId;entry.variant=DraftlineThemeApplier.CurrentVariant;entry.environmentSeed=DraftlineThemeApplier.CurrentSeed;File.AppendAllText(report,JsonUtility.ToJson(entry)+Environment.NewLine);}
        private static string Value(string[] args,string key,string fallback)
        {var i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
        private static string Digest(MatchState state)
        {
            var json=JsonUtility.ToJson(MatchSnapshot.Capture(state));
            using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(json))).Replace("-","");
        }
    }
}
#endif

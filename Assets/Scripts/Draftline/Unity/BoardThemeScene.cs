using System;
using System.Linq;
using Draftline.Core;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Draftline.Unity
{
    /// <summary>Saved scene instances, toggled without rebuilding layout or changing rule colliders.</summary>
    public sealed class BoardThemeScene : MonoBehaviour
    {
        [Serializable] public sealed class Entry { public BoardTheme Theme; public GameObject Root; }
        public Entry[] Entries;
        public Renderer[] LegacyRenderers;
        public Transform LegacyWalls;
        public Light LegacyLight;
        public string PreviewThemeId="fortress";
        public BoardTheme ActiveTheme {get;private set;}
        public BoardTheme Apply(BoardTheme theme)
        {
            if(theme==null)return null;
            var entry=Entries?.FirstOrDefault(e=>e.Theme==theme);
            if(theme.ThemeId!="tabletop" && (entry==null || entry.Root==null)) return null;
            foreach(var e in Entries??Array.Empty<Entry>())if(e.Root!=null)e.Root.SetActive(e==entry);
            var original=theme.ThemeId=="tabletop";
            foreach(var r in LegacyRenderers??Array.Empty<Renderer>())if(r!=null)r.forceRenderingOff=!original&&r.GetComponentInParent<DraftlineRollDice>()==null;
            var tableDice=Object.FindObjectOfType<DraftlineRollDice>();if(tableDice!=null)tableDice.SetThemeStation(!original);
            if(LegacyLight!=null)LegacyLight.enabled=original;
            if(entry!=null && LegacyWalls!=null)
            {
                var drivers=LegacyWalls.Cast<Transform>().Select(t=>t.GetComponent<Renderer>()).Where(r=>r!=null).ToArray();
                var controller=Object.FindObjectOfType<DraftlineGameController>();
                if(controller==null || controller.Rules==null) {
                    var previewRules=new MatchRules(new[]{PlayerColor.Red,PlayerColor.Blue});
                    foreach(var driver in drivers)if(BoardCoordinates.TryFromWorld(driver.transform.position,out var p))driver.enabled=previewRules.IsWall(p);
                }
                foreach(var wall in entry.Root.GetComponentsInChildren<ThemeMovingWall>(true))
                {
                    var driver=drivers.FirstOrDefault(r=>BoardCoordinates.TryFromWorld(r.transform.position,out var p)&&p==new GridPosition(wall.Column,wall.Row));
                    if(driver!=null)wall.Bind(driver,controller!=null?controller.WallRestPosition(driver):driver.transform.position,controller!=null?controller.WallRestScale(driver):driver.transform.localScale);
                }
            }
            ActiveTheme=theme;
            Object.FindObjectOfType<DraftlineCameraDirector>()?.ApplyThemeFraming(theme);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight=theme.AmbientColor;
            RenderSettings.fog=false;
            var backdrop=GetComponent<ThemeBackdrop>();
            if(backdrop!=null)backdrop.Apply(theme);
            else if(Camera.main!=null){Camera.main.clearFlags=CameraClearFlags.SolidColor;Camera.main.backgroundColor=theme.BackgroundColor;}
            return theme;
        }
        [ContextMenu("Preview Selected Theme")]
        public void Preview() { DraftlineThemeApplier.Apply(PreviewThemeId); }
        public void ApplyEnvironment(int variant,int seed)
        {
            var entry=Entries?.FirstOrDefault(e=>e.Theme==ActiveTheme);
            if(entry?.Root!=null)entry.Root.GetComponent<ThemeEnvironment>()?.Apply(variant,seed);
        }
    }
}

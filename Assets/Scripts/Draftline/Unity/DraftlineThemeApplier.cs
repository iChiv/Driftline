using UnityEngine;
using System.Linq;
using Draftline.Core;

namespace Draftline.Unity
{
    /// <summary>
    /// Applies a BoardTheme at runtime: activates the matching scene layer under the
    /// Themes root and swaps the materials the theme defines. Selection is stored in
    /// PlayerPrefs["boardTheme"]; unimplemented themes fall back to Tabletop.
    /// </summary>
    public static class DraftlineThemeApplier
    {
        public const string PrefKey = "boardTheme";
        public const string DefaultId = "fortress";

        public static string CurrentId { get; private set; }
        public static int CurrentVariant {get;private set;}
        public static int CurrentSeed {get;private set;}
        public static int PreferredEnvironment => 2;
        public static string PreferredId { get { var id=PlayerPrefs.GetString(PrefKey,DefaultId);return Available.Any(t=>t.ThemeId==id)?id:DefaultId; } }
        public static BoardTheme[] Available => Resources.LoadAll<BoardTheme>("Themes").Where(t=>t.Implemented&&t.PresentationPrefab!=null).OrderBy(t=>t.SortOrder).ThenBy(t=>t.ThemeId).ToArray();

        public static void SelectLocal(string themeId)
        {
            var theme=Apply(themeId);
            if(theme==null)return;
            PlayerPrefs.SetString(PrefKey,theme.ThemeId);PlayerPrefs.Save();
            SelectEnvironment(PreferredEnvironment);
        }

        public static void SelectEnvironment(int choice)
        {
            choice=Mathf.Clamp(choice,0,2);
            var seed=System.Environment.TickCount & int.MaxValue;
            ApplyEnvironment(choice==2?new System.Random(seed).Next(2):choice,seed);
        }
        public static void ApplyEnvironment(int variant,int seed)
        {
            CurrentVariant=Mathf.Clamp(variant,0,1);CurrentSeed=seed;
            var scene=Object.FindObjectOfType<BoardThemeScene>();
            if(scene!=null)scene.ApplyEnvironment(CurrentVariant,CurrentSeed);
        }

        public static BoardTheme Load(string themeId)
        {
            return Resources.LoadAll<BoardTheme>("Themes").FirstOrDefault(t=>t.ThemeId==themeId);
        }

        /// <summary>Activates the theme's scene layer and applies its materials. Returns the applied theme.</summary>
        public static BoardTheme Apply(string themeId)
        {
            var theme = Load(themeId);
            if (theme == null || !theme.Implemented || theme.PresentationPrefab==null)
            {
                if (theme != null)
                    Debug.Log($"[Theme] {theme.ThemeName} 不在当前主题目录，使用前线要塞。");
                theme = Load(DefaultId);
            }
            if (theme == null)
            {
                Debug.LogWarning("[Theme] Default theme asset missing from Resources/Themes.");
                return null;
            }

            if(CurrentId==null){var choice=PreferredEnvironment;CurrentSeed=System.Environment.TickCount & int.MaxValue;CurrentVariant=choice==2?new System.Random(CurrentSeed).Next(2):choice;}
            var scene=Object.FindObjectOfType<BoardThemeScene>();
            if(scene!=null)
            {
                var applied=scene.Apply(theme);
                if(applied==null) { theme=Load(DefaultId);scene.Apply(theme); }
                CurrentId=theme.ThemeId;
                scene.ApplyEnvironment(CurrentVariant,CurrentSeed);
                if(theme.PresentationPrefab!=null)return theme;
            }
            CurrentId=theme.ThemeId;

            var themesRoot = GameObject.Find("Themes");
            if (themesRoot != null)
            {
                foreach (Transform layer in themesRoot.transform)
                    layer.gameObject.SetActive(layer.name == theme.SceneLayerName);
            }

            AssignMaterial("Wooden Table", theme.TableMaterial);
            AssignMaterial("Table Rim", theme.RimMaterial);
            var walls = GameObject.Find("Walls");
            var layout = new BoardLayout();
            if (walls != null && theme.WallMaterial != null)
                foreach (var renderer in walls.GetComponentsInChildren<Renderer>(true))
                {
                    // 皮肤化的 DungeonWall 子件用自己的模型材质，不上主题色。
                    var skinned = false;
                    for (var t = renderer.transform; t != null; t = t.parent)
                        if (t.name == "DungeonWall") { skinned = true; break; }
                    if (!skinned)
                    {
                        var rotating=BoardCoordinates.TryFromWorld(renderer.transform.position,out var cell)&&layout.CellAt(cell).Kind==CellKind.RotatingWallSlot;
                        renderer.sharedMaterial=rotating&&theme.RotatingWallMaterial!=null?theme.RotatingWallMaterial:theme.WallMaterial;
                    }
                }
            return theme;
        }

        private static void AssignMaterial(string objectName, Material material)
        {
            if (material == null) return;
            var target = GameObject.Find(objectName);
            if (target == null) return;
            foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterial = material;
        }
    }
}

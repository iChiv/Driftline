using UnityEngine;

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
        public const string DefaultId = "tabletop";

        public static string CurrentId => PlayerPrefs.GetString(PrefKey, DefaultId);

        public static BoardTheme Load(string themeId)
        {
            var name = themeId == "magic" ? "MagicTheme" : "TabletopTheme";
            return Resources.Load<BoardTheme>("Themes/" + name);
        }

        /// <summary>Activates the theme's scene layer and applies its materials. Returns the applied theme.</summary>
        public static BoardTheme Apply(string themeId)
        {
            var theme = Load(themeId);
            if (theme == null || !theme.Implemented)
            {
                if (theme != null)
                    Debug.Log($"[Theme] {theme.ThemeName} 建设中，本局使用桌面玩具主题。");
                theme = Load(DefaultId);
            }
            if (theme == null)
            {
                Debug.LogWarning("[Theme] TabletopTheme asset missing from Resources/Themes.");
                return null;
            }

            var themesRoot = GameObject.Find("Themes");
            if (themesRoot != null)
            {
                foreach (Transform layer in themesRoot.transform)
                    layer.gameObject.SetActive(layer.name == theme.SceneLayerName);
            }

            AssignMaterial("Wooden Table", theme.TableMaterial);
            AssignMaterial("Table Rim", theme.RimMaterial);
            var walls = GameObject.Find("Walls");
            if (walls != null && theme.WallMaterial != null)
                foreach (var renderer in walls.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterial = theme.WallMaterial;
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

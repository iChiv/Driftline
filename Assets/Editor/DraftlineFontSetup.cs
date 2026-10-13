using UnityEditor;
using UnityEngine;

namespace Draftline.EditorTools
{
    /// <summary>
    /// Creates the dynamic CJK font asset referenced by DraftlineTheme.uss so the
    /// standalone build renders Chinese text (editor falls back to OS fonts, builds don't).
    /// </summary>
    public static class DraftlineFontSetup
    {
        private const string FontPath = "Assets/Resources/DraftlineFont.asset";

        [MenuItem("Draftline/Create CJK Font Asset")]
        public static void CreateCjkFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (existing != null)
            {
                Debug.Log($"[DraftlineFont] already exists: {FontPath} (fontNames: {string.Join(",", existing.fontNames)})");
                return;
            }
            var font = new Font
            {
                name = "DraftlineCJK",
                hideFlags = HideFlags.None
            };
            // Dynamic OS font: rasterized at runtime, includes full CJK coverage on Windows.
            font = new Font("Microsoft YaHei");
            AssetDatabase.CreateAsset(font, FontPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DraftlineFont] created {FontPath} (fontNames: {string.Join(",", font.fontNames)})");
        }
    }
}

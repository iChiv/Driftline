using UnityEngine;

namespace Draftline.Unity
{
    /// <summary>
    /// One board theme: material references plus the decorative props that belong to it.
    /// Themes are scene layers under Themes/&lt;name&gt; — a theme is edited by enabling its
    /// layer in the editor; the menu (phase 2) picks which layer is active at runtime.
    /// The Magic theme is a placeholder until its models arrive.
    /// </summary>
    [CreateAssetMenu(menuName = "Draftline/Board Theme", fileName = "NewBoardTheme")]
    public sealed class BoardTheme : ScriptableObject
    {
        [Tooltip("菜单与 PlayerPrefs 使用的标识，如 tabletop / magic")]
        public string ThemeId = "tabletop";
        public string ThemeName = "桌面玩具";
        [Tooltip("场景里 Themes 根下的图层对象名")]
        public string SceneLayerName = "Tabletop";
        [Tooltip("未实现的主题选择后回退到桌面玩具")]
        public bool Implemented = true;
        [Header("墙体与桌面")]
        public Material WallMaterial;
        public Material RotatingWallMaterial;
        public Material TableMaterial;
        public Material RimMaterial;
        [Header("传送门（A/B/C/D 四对）")]
        public Material PortalA;
        public Material PortalB;
        public Material PortalC;
        public Material PortalD;
        [Header("阵营出生角（红蓝绿黄）")]
        public Material[] FactionMaterials = new Material[4];
        [Header("摆件（场景主题层内的装饰根对象）")]
        public GameObject[] Props = new GameObject[0];
    }
}

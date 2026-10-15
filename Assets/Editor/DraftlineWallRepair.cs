using Draftline.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Draftline.EditorTools
{
    public static class DraftlineWallRepair
    {
        [MenuItem("Driftline/Repair Lift Wall Geometry")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Repair in Edit Mode.");
            foreach(var theme in DraftlineThemeApplier.Available)
            {
                var path=AssetDatabase.GetAssetPath(theme.PresentationPrefab);var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach(var wall in root.GetComponentsInChildren<ThemeMovingWall>(true))
                    {
                        if(wall.GeometryVersion>=1)continue;
                        var offset=wall.transform.position;
                        foreach(Transform child in wall.transform)
                        {
                            if(child==wall.Body){foreach(Transform shape in child)shape.localPosition+=offset;}
                            else child.localPosition+=offset;
                        }
                        wall.GeometryVersion=1;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Draftline.Core;
using Draftline.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Draftline.EditorTools
{
    /// <summary>Authored tabletop identity. Does not change the discrete rule layout.</summary>
    public static class DriftlineTabletopIdentity
    {
        private const string ModelRoot="Assets/KayKit/Packs/KayKit - Dungeon Remastered Pack (for Unity)/Models/";
        private const string ArtRoot="Assets/Themes/Driftline";
        private static Font font;
        private static readonly Dictionary<Material,Material> palette=new Dictionary<Material,Material>();

        [MenuItem("Driftline/Author Tabletop Identity")]
        public static void Author()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Author in Edit Mode.");
            if(GameObject.Find("Tabletop Identity")!=null){Debug.Log("Tabletop identity already authored. Edit scene and assets directly.");return;}
            var game=Object.FindObjectOfType<DraftlineGameController>();
            if(game==null)throw new InvalidOperationException("Open SampleScene.");
            Folder(ArtRoot);Folder(ArtRoot+"/Materials");
            font=AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/SourceHanSansCN-Regular.otf");
            var identity=new GameObject("Tabletop Identity").transform;
            Undo.RegisterCreatedObjectUndo(identity.gameObject,"Author tabletop identity");
            var theme=AssetDatabase.LoadAssetAtPath<BoardTheme>("Assets/Resources/Themes/TabletopTheme.asset");
            var fixedMaterial=MaterialAsset("SlateWalls",new Color(.27f,.32f,.34f));
            var rotatingMaterial=MaterialAsset("MechanismBrass",new Color(.58f,.41f,.20f));
            var tableMaterial=MaterialAsset("WalnutTable",new Color(.22f,.13f,.085f));
            if(theme.TableMaterial!=null&&theme.TableMaterial.mainTexture!=null)tableMaterial.mainTexture=theme.TableMaterial.mainTexture;
            var rimMaterial=MaterialAsset("BoardRim",new Color(.30f,.20f,.11f));
            theme.WallMaterial=fixedMaterial;theme.RotatingWallMaterial=rotatingMaterial;theme.TableMaterial=tableMaterial;theme.RimMaterial=rimMaterial;
            EditorUtility.SetDirty(theme);
            var layout=new BoardLayout();
            var walls=GameObject.Find("Walls");
            foreach(var renderer in walls.GetComponentsInChildren<Renderer>(true).Concat(GameObject.Find("Border").GetComponentsInChildren<Renderer>(true)))
            {
                var transform=renderer.transform;
                var position=transform.position;
                var scale=transform.localScale;
                // Keep X/Z footprint and colliders. Reduce the sculpted wall's visual height.
                if(scale.y>.35f){position.y-=scale.y*.20f;scale.y*=.60f;transform.position=position;transform.localScale=scale;}
                var rotating=BoardCoordinates.TryFromWorld(position,out var cell)&&layout.CellAt(cell).Kind==CellKind.RotatingWallSlot;
                renderer.sharedMaterial=rotating?rotatingMaterial:fixedMaterial;
            }
            // Four small faction banners outside the board establish identity without hiding cells.
            var names=new[]{"red","blue","green","yellow"};
            for(var i=0;i<4;i++)
            {
                var color=(PlayerColor)i;
                var corner=BoardCoordinates.ToWorld(layout.SpawnOf(color),.52f);
                var position=new Vector3(Mathf.Sign(corner.x)*10.5f,.5f,Mathf.Sign(corner.z)*10.5f);
                PlaceModel("banner_patternA_"+names[i],"Banner "+color,identity,position,1.7f);
                var baseGo=GameObject.CreatePrimitive(PrimitiveType.Cylinder);baseGo.name="Faction Seal "+color;
                Object.DestroyImmediate(baseGo.GetComponent<Collider>());baseGo.transform.SetParent(identity,false);
                baseGo.transform.position=BoardCoordinates.ToWorld(layout.SpawnOf(color),.52f);baseGo.transform.localScale=new Vector3(.88f,.02f,.88f);
                baseGo.GetComponent<Renderer>().sharedMaterial=MaterialAsset("Seal"+color,MatchLabels.Color(color));
                FloorText(identity,"Faction Mark "+color,new[]{"炉","印","庭","环"}[i],new Vector3(position.x,.54f,position.z-1.1f),.16f,MatchLabels.Color(color));
            }
            // The same glyph on both ends makes portal pairing legible beyond color alone.
            var symbols=new Dictionary<char,string>{{'O',"○"},{'C',"◇"},{'M',"△"},{'R',"□"}};
            for(var row=0;row<BoardLayout.Size;row++)for(var col=0;col<BoardLayout.Size;col++)
            {
                var pos=new GridPosition(col,row);var definition=layout.CellAt(pos);
                if(definition.Kind==CellKind.Portal)FloorText(identity,"Portal Pair "+definition.PortalKey+" "+pos,symbols[definition.PortalKey],BoardCoordinates.ToWorld(pos,.57f),.14f,Color.white);
                if(definition.Kind==CellKind.PlayerGate)
                    FloorText(identity,"Gate Seal "+pos,"◆",BoardCoordinates.ToWorld(pos,.58f),.10f,MatchLabels.Color(definition.Owner));
            }
            // Three small environmental clues, all off the functional grid.
            PlaceModel("chest","Sealed Supply Chest",identity,new Vector3(11.3f,.45f,3.5f),1.2f);
            PlaceModel("barrel_large","Explorers Supplies",identity,new Vector3(-11.2f,.45f,3f),1.1f);
            PlaceModel("torch_lit","Watchfire",identity,new Vector3(-11.1f,.45f,-3.2f),1.3f);
            FloorText(identity,"Inscription","五印共鸣 · 界钥苏醒",new Vector3(10.4f,.53f,-3.8f),.07f,new Color(.80f,.70f,.50f));
            FloorText(identity,"Survey Note","门阵同印相连",new Vector3(-10.8f,.53f,5f),.07f,new Color(.80f,.70f,.50f));
            FloorText(identity,"Old Warning","墙起之时，留一道生路",new Vector3(-10.8f,.53f,-5f),.065f,new Color(.80f,.70f,.50f));
            foreach(var toy in new[]{"Toy Car","Toy Block Tower","Toy Spinning Top","Toy Hourglass"})
            {var go=GameObject.Find(toy);if(go!=null)go.SetActive(false);}
            var light=GameObject.Find("Directional Light")?.GetComponent<Light>();
            if(light!=null){light.color=new Color(1f,.94f,.84f);light.intensity=1.15f;}
            RenderSettings.ambientLight=new Color(.40f,.45f,.50f);
            var camera=Camera.main;if(camera!=null){camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.07f,.09f);}
            var feedback=ScriptableObject.CreateInstance<MatchFeedbackProfile>();
            feedback.Capture=Resources.Load<GameObject>("FX/CaptureExplosion");feedback.Portal=Resources.Load<GameObject>("FX/PortalAura");feedback.Victory=Resources.Load<GameObject>("FX/VictoryFirework");
            AssetDatabase.CreateAsset(feedback,ArtRoot+"/MatchFeedback.asset");
            game.Feedback=feedback;
            DraftlineThemeApplier.Apply("tabletop");
            EditorUtility.SetDirty(game);AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
        }
        private static void FloorText(Transform parent,string name,string copy,Vector3 position,float size,Color color)
        {
            var go=new GameObject(name,typeof(TextMesh));go.transform.SetParent(parent,false);go.transform.position=position;go.transform.rotation=Quaternion.Euler(90,-90,0);
            var text=go.GetComponent<TextMesh>();text.font=font;text.text=copy;text.fontSize=40;text.characterSize=size;text.anchor=TextAnchor.MiddleCenter;text.color=color;
            go.GetComponent<Renderer>().sharedMaterial=font.material;
        }
        private static void PlaceModel(string file,string name,Transform parent,Vector3 position,float maxSize)
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(ModelRoot+file+".fbx");
            if(model==null){Debug.LogWarning("Optional tabletop model not found: "+file);return;}
            var go=(GameObject)PrefabUtility.InstantiatePrefab(model);go.name=name;go.transform.SetParent(parent,false);
            if(file.StartsWith("banner"))go.transform.localRotation=Quaternion.Euler(0,90,0);
            var renderers=go.GetComponentsInChildren<Renderer>();if(renderers.Length==0)return;
            var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            var max=Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);go.transform.localScale*=max>0?maxSize/max:1;
            bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            go.transform.position=position+Vector3.up*(-bounds.min.y);
            foreach(var r in renderers)
            {
                var materials=r.sharedMaterials;
                for(var i=0;i<materials.Length;i++)
                {
                    var old=materials[i];if(old==null||old.shader.name.StartsWith("Universal Render"))continue;
                    if(!palette.TryGetValue(old,out var converted))
                    {
                        converted=MaterialAsset("Palette"+palette.Count,old.color);converted.mainTexture=old.mainTexture;palette[old]=converted;
                    }
                    materials[i]=converted;
                }
                r.sharedMaterials=materials;
            }
            foreach(var collider in go.GetComponentsInChildren<Collider>())Object.DestroyImmediate(collider);
        }
        private static Material MaterialAsset(string name,Color color)
        {
            var path=ArtRoot+"/Materials/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material!=null)return material;
            material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=color;material.SetFloat("_Smoothness",.18f);AssetDatabase.CreateAsset(material,path);return material;
        }
        private static void Folder(string path)
        {var parts=path.Split('/');var current=parts[0];for(var i=1;i<parts.Length;i++){var next=current+"/"+parts[i];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,parts[i]);current=next;}}
    }
}

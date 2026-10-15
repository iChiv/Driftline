using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Draftline.Core;
using Draftline.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Draftline.EditorTools
{
    /// <summary>One-time authoring, never called by the player. Completed prefabs stay artist-editable.</summary>
    public static class DriftlineThemeAuthoring
    {
        private const string Root="Assets/Themes/Battlefields";
        private const string U="Assets/polyperfect/Low Poly Ultimate Pack/_T/Prefabs_T/";
        private const string K="Assets/KayKit/Packs/KayKit - Dungeon Remastered Pack (for Unity)/Prefabs/";
        private const string P="Assets/Off Axis Studios/Polyworks/Prefabs/Atlased/";
        private static readonly Color[] Factions={new Color(.96f,.24f,.20f),new Color(.18f,.53f,1),new Color(.20f,.88f,.45f),new Color(1,.79f,.14f)};
        private static readonly Color[] PortalColors={new Color(1,.52f,.15f),new Color(1,.25f,.29f),new Color(.26f,.75f,1),new Color(.91f,.28f,.97f)};
        private static readonly string[] PortalGlyphs={"○","◇","△","□"};
        private sealed class Spec
        {
            public string Id,Name,Story,Floor,Wall,Pawn;
            public Color FloorColor,Structure,Trim,Back,Ambient,Sun;
            public float SunIntensity;
            public int Order;
        }
        private static Spec Get(string id)
        {
            switch(id)
            {
                case "fortress":return new Spec{Id=id,Order=1,Name="前线要塞",Story="废弃要塞的演习庭院重新亮起信标。四支队伍穿过沙袋掩体，在轮转的装甲闸板间争夺中枢。",Floor=K+"floor_tile_small.prefab",Wall=U+"WW2_T/Sandbags_Straight.prefab",Pawn=U+"People_T/Man_Soldier.prefab",FloorColor=new Color(.69f,.68f,.57f),Structure=new Color(.24f,.28f,.24f),Trim=new Color(.56f,.40f,.20f),Back=new Color(.15f,.19f,.21f),Ambient=new Color(.56f,.59f,.61f),Sun=new Color(1,.94f,.79f),SunIntensity=1.15f};
                case "dungeon":return new Spec{Id=id,Order=2,Name="符文地城",Story="封印大厅深处，界钥令古老石阵再次转动。火把照亮探索者的营地，成对符文指向另一端的出口。",Floor=K+"floor_tile_small.prefab",Wall=K+"wall_half.prefab",Pawn=U+"People_T/Man_Knight.prefab",FloorColor=new Color(.48f,.52f,.57f),Structure=new Color(.24f,.29f,.36f),Trim=new Color(.35f,.62f,.68f),Back=new Color(.035f,.065f,.11f),Ambient=new Color(.48f,.57f,.68f),Sun=new Color(.83f,.91f,1),SunIntensity=1.1f};
                case "hangar":return new Spec{Id=id,Order=3,Name="星舰机库",Story="导航核心将训练甲板改造成相位战场。检修区与货运舱围绕棋阵运转，权限灯标示四队的部署通道。",Floor=U+"Scifi_T/Scifi_Floor.prefab",Wall=U+"Scifi_T/Scifi_Box_Divided.prefab",Pawn=U+"People_T/Man_Astronaut.prefab",FloorColor=new Color(.52f,.60f,.66f),Structure=new Color(.16f,.23f,.31f),Trim=new Color(.90f,.57f,.20f),Back=new Color(.025f,.045f,.085f),Ambient=new Color(.52f,.62f,.73f),Sun=new Color(.82f,.92f,1),SunIntensity=1.05f};
                case "forest":return new Spec{Id=id,Order=4,Name="暮林祭坛",Story="暮色落在被森林收回的古代石庭。符石与树根仍守着界钥，四处营火之间，旧封印按照原有节律升起。",Floor=K+"floor_tile_small.prefab",Wall=U+"Medieval_T/Stone_Brick.prefab",Pawn=U+"People_T/Man_Shaman_Tribal.prefab",FloorColor=new Color(.57f,.62f,.48f),Structure=new Color(.25f,.32f,.23f),Trim=new Color(.66f,.55f,.29f),Back=new Color(.055f,.11f,.10f),Ambient=new Color(.53f,.61f,.51f),Sun=new Color(1,.83f,.59f),SunIntensity=1.15f};
                case "harbor":return new Spec{Id=id,Order=5,Name="潮汐港口",Story="泊船、仓屋与吊机环抱石质集散场。四支船队沿货运信标争夺界钥，升降防水闸依旧决定每一条生路。",Floor=K+"floor_tile_small.prefab",Wall=K+"chest_large.prefab",Pawn=U+"People_T/Man_Pirate.prefab",FloorColor=new Color(.69f,.70f,.63f),Structure=new Color(.26f,.24f,.19f),Trim=new Color(.48f,.31f,.17f),Back=new Color(.055f,.20f,.27f),Ambient=new Color(.56f,.66f,.72f),Sun=new Color(1,.94f,.78f),SunIntensity=1.2f};
                default:throw new ArgumentException("Unknown theme: "+id);
            }
        }
        [MenuItem("Driftline/Themes/1 Author Frontier Fortress")] public static void Fortress()=>Produce("fortress");
        [MenuItem("Driftline/Themes/2 Author Rune Dungeon")] public static void Dungeon()=>Produce("dungeon");
        [MenuItem("Driftline/Themes/3 Author Starship Hangar")] public static void Hangar()=>Produce("hangar");
        [MenuItem("Driftline/Themes/4 Author Forest Shrine")] public static void Forest()=>Produce("forest");
        [MenuItem("Driftline/Themes/5 Author Tidal Harbor")] public static void Harbor()=>Produce("harbor");
        public static string Produce(string id)
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Author in Edit Mode.");
            var s=Get(id);Folder(Root+"/"+id);Folder(Root+"/Adapters");Folder(Root+"/Materials");
            var path=Root+"/"+id+"/"+id+".prefab";
            if(File.Exists(path))return "Already authored; edit the saved prefab directly: "+path;
            var root=new GameObject(s.Name+" — "+id);
            try
            {
                var surface=Group(root.transform,"BoardSurface");var walls=Group(root.transform,"FixedWalls");var moving=Group(root.transform,"MovingWalls");
                var gates=Group(root.transform,"FactionGates");var portals=Group(root.transform,"PairedPortals");var near=Group(root.transform,"NearSet");var landmarks=Group(root.transform,"Landmarks");
                var stone=Mat(id+"_Structure",s.Structure);var trim=Mat(id+"_Trim",s.Trim);var floor=Mat(id+"_Paving",s.FloorColor);var seam=Mat(id+"_Seams",s.FloorColor*.53f);
                var metal=Mat(id+"_Mechanism",Color.Lerp(s.Trim,s.Structure,.3f));
                var terrain=Mat(id+"_Surround",id=="harbor"?new Color(.075f,.29f,.36f):Color.Lerp(s.Structure,s.FloorColor,.35f));
                Cube(surface,"Foundation",new Vector3(0,.18f,0),new Vector3(19.6f,.5f,19.6f),stone);
                Cube(surface,"Grid shadow bed",new Vector3(0,.45f,0),new Vector3(19,.06f,19),seam);
                Cube(near,id=="harbor"?"Harbor water":"Surrounding terrain",new Vector3(0,-.22f,0),new Vector3(38,.18f,38),terrain);
                if(id=="harbor")Waterlines(near,Mat("Harbor_Foam",new Color(.19f,.43f,.49f)));
                var layout=new BoardLayout();
                for(var row=0;row<19;row++)for(var col=0;col<19;col++)
                {
                    var p=new GridPosition(col,row);var cell=layout.CellAt(p);var w=BoardCoordinates.ToWorld(p,.5f);
                    var tile=Place(surface,s.Floor,"Paving "+p,w-Vector3.up*.10f,.985f,0,true);
                    tile.transform.position+=Vector3.up*(.50f-Bounds(tile).max.y);
                    foreach(var r in tile.GetComponentsInChildren<Renderer>()) {r.sharedMaterial=floor;r.shadowCastingMode=ShadowCastingMode.Off;}
                    // Rule-owned walls occupy their whole square even where the sculpted model is narrower.
                    if(cell.Kind==CellKind.FixedWall && p!=new GridPosition(9,9))
                    {
                        Cube(walls,"Occupied footprint "+p,w+Vector3.up*.035f,new Vector3(.985f,.07f,.985f),stone);
                        var rotation=(col>0&&layout.CellAt(p.Offset(-1,0)).Kind==CellKind.FixedWall)||(col<18&&layout.CellAt(p.Offset(1,0)).Kind==CellKind.FixedWall)?90:0;
                        Place(walls,s.Wall,"Low cover "+p,w+Vector3.up*.07f,id=="dungeon"||id=="hangar"?.62f:.89f,rotation);
                    }
                    if(cell.Kind==CellKind.RotatingWallSlot || p==new GridPosition(9,9))
                    {
                        var slot=Group(moving,"Mechanism "+p);slot.position=w;
                        Cube(slot,"Socket rim",new Vector3(0,.008f,0),new Vector3(.99f,.018f,.99f),stone,true);
                        var body=Group(slot,"Lift body");
                        Cube(body,"Armor or seal",new Vector3(0,.26f,0),new Vector3(.88f,.50f,.88f),metal,true);
                        Cube(body,"Cap",new Vector3(0,.52f,0),new Vector3(.94f,.05f,.94f),trim,true);
                        for(var k=0;k<3;k++)Cube(body,"Warning inlay "+k,new Vector3(-.28f+k*.28f,.549f,0),new Vector3(.09f,.015f,.55f),floor,true);
                        var binding=slot.gameObject.AddComponent<ThemeMovingWall>();binding.Column=col;binding.Row=row;binding.Body=body;
                        body.gameObject.SetActive(p==new GridPosition(9,9)||layout.IsScheduledWall(p,0));
                    }
                    if(cell.Kind==CellKind.Spawn) {
                        var faction=Mat("Faction"+(int)cell.Owner,Factions[(int)cell.Owner],.25f);
                        Cylinder(gates,"Deployment "+cell.Owner,w+Vector3.up*.02f,new Vector3(.91f,.025f,.91f),faction);
                        FloorText(gates,"Deployment ID "+cell.Owner,new[]{"Ⅰ","Ⅱ","Ⅲ","Ⅳ"}[(int)cell.Owner],w+Vector3.up*.055f,.11f,Color.white);
                    }
                    if(cell.Kind==CellKind.PlayerGate) {
                        var faction=Mat("Faction"+(int)cell.Owner,Factions[(int)cell.Owner],.25f);
                        Cube(gates,"Permission lane "+p,w+Vector3.up*.015f,new Vector3(.83f,.02f,.83f),stone);
                        foreach(var sign in new[]{-1,1})Cube(gates,"Gate light "+p,w+new Vector3(sign*.36f,.055f,0),new Vector3(.09f,.09f,.78f),faction);
                        FloorText(gates,"Permission "+p,"◆",w+Vector3.up*.033f,.08f,Factions[(int)cell.Owner]);
                    }
                    if(cell.Kind==CellKind.Portal) {
                        var i="OCMR".IndexOf(cell.PortalKey);var light=Mat("Portal"+i,PortalColors[i],.55f);
                        Cylinder(portals,"Transfer pad "+p,w+Vector3.up*.025f,new Vector3(.91f,.025f,.91f),stone);
                        var ring=new GameObject("Pair ring "+cell.PortalKey,typeof(MeshFilter),typeof(MeshRenderer));ring.transform.SetParent(portals,false);ring.transform.position=w+Vector3.up*.057f;
                        ring.GetComponent<MeshFilter>().sharedMesh=RingMesh();ring.GetComponent<Renderer>().sharedMaterial=light;
                        FloorText(portals,"Pair "+cell.PortalKey+" "+p,PortalGlyphs[i],w+Vector3.up*.062f,.12f,PortalColors[i]);
                        foreach(var sign in new[]{-1,1})Cube(portals,"Projector",w+new Vector3(sign*.38f,.13f,0),new Vector3(.10f,.24f,.16f),trim);
                    }
                }
                // Four-sided architectural edge. The front side stays low for the playable camera.
                foreach(var sign in new[]{-1,1}) {
                    Cube(near,"Perimeter coping",new Vector3(sign*9.75f,.51f,0),new Vector3(.28f,.22f,19.8f),trim);
                    Cube(near,"Perimeter coping",new Vector3(0,.51f,sign*9.75f),new Vector3(19.8f,.22f,.28f),trim);
                }
                for(var i=0;i<4;i++) {
                    var p=BoardCoordinates.ToWorld(layout.SpawnOf((PlayerColor)i));var corner=new Vector3(Mathf.Sign(p.x)*10.6f,.05f,Mathf.Sign(p.z)*10.5f);
                    Place(near,K+"banner_patternA_"+new[]{"red","blue","green","yellow"}[i]+".prefab","Faction banner "+i,corner,1.8f,90);
                    Place(near,id=="hangar"?U+"Scifi_T/Scifi_Box.prefab":id=="fortress"?U+"WW2_T/Ammo_Crate_Pak.prefab":K+"barrel_small_stack.prefab","Camp supply "+i,corner+new Vector3(1.0f,0,.25f),.95f);
                }
                Dress(s,near,landmarks,stone,trim);
                var lights=Group(root.transform,"Lighting");var sun=new GameObject("Theme key light",typeof(Light));sun.transform.SetParent(lights,false);sun.transform.rotation=Quaternion.Euler(55,-35,0);var key=sun.GetComponent<Light>();key.type=LightType.Directional;key.color=s.Sun;key.intensity=s.SunIntensity;key.shadows=LightShadows.Soft;key.shadowStrength=.65f;
                // Presentation-only dice station; the existing, verified physical tray remains unchanged.
                Cube(near,"Dice dispatch console",new Vector3(-11,.18f,6),new Vector3(1.6f,.7f,2.3f),stone);
                FloorText(near,"Station label",id=="fortress"?"COMMAND":id=="dungeon"?"界钥封印":id=="hangar"?"NAV / 04":id=="forest"?"暮林遗迹":"HARBOR",new Vector3(-11,.55f,6),.065f,s.FloorColor);
                var pawn=MakePawn(s);
                var theme=ScriptableObject.CreateInstance<BoardTheme>();theme.ThemeId=id;theme.ThemeName=s.Name;theme.Description=s.Story;theme.SceneLayerName=id;theme.Implemented=true;theme.SortOrder=s.Order;theme.BackgroundColor=s.Back;theme.AmbientColor=s.Ambient;theme.PiecePrefab=pawn;
                theme.PresentationPrefab=PrefabUtility.SaveAsPrefabAsset(root,path);
                AssetDatabase.CreateAsset(theme,"Assets/Resources/Themes/"+id+"Theme.asset");
                AssetDatabase.SaveAssets();return path;
            }
            finally {Object.DestroyImmediate(root);}
        }
        private static void Dress(Spec s,Transform near,Transform landmarks,Material stone,Material trim)
        {
            switch(s.Id)
            {
                case "fortress":
                    Place(landmarks,U+"Military_T/Military_Headquarters.prefab","Rear command headquarters",new Vector3(-12.8f,-.1f,-1),4.5f,90);
                    Place(landmarks,U+"Military_T/Military_Barracks.prefab","Rear barracks",new Vector3(-12.5f,-.1f,7),3.8f,90);
                    Place(landmarks,U+"WW2_T/Bunker_Medium.prefab","East bunker",new Vector3(-5,-.1f,13),4.2f);
                    Place(landmarks,U+"Military_T/Military_Tent.prefab","West supply tent",new Vector3(-4,-.1f,-12),3.4f);
                    Place(near,U+"Military_T/Military_Truck.prefab","Supply truck",new Vector3(2,-.1f,-12),3.1f,90);
                    Place(near,U+"WW2_T/Jeep_Willys.prefab","Patrol jeep",new Vector3(2,-.1f,12),2.6f);
                    for(var i=0;i<5;i++)Place(near,U+"WW2_T/Sandbags_Straight.prefab","Forward sandbags "+i,new Vector3(11,-.1f,-6+i*2.7f),1.7f,90);
                    Place(landmarks,U+"Military_T/Military_Gate.prefab","Vehicle gate",new Vector3(-14,-.1f,-8),3.4f);
                    break;
                case "dungeon":
                    Place(landmarks,P+"Dungeon/Prop_Furniture_Church_Altar_01_Atlased.prefab","Rear seal altar",new Vector3(-12,-.1f,0),3.2f,90);
                    Place(landmarks,K+"wall_arched.prefab","Rear vault arch",new Vector3(-14,-.1f,0),5.0f,90);
                    for(var i=0;i<4;i++) {
                        Place(landmarks,K+"pillar_decorated.prefab","Hall pillar "+i,new Vector3(-12+i*4,-.1f,-12),2.5f);
                        Place(landmarks,K+"pillar.prefab","Hall pillar "+(i+4),new Vector3(-12+i*4,-.1f,12),2.5f);
                        Place(near,K+"torch_lit.prefab","Torch niche "+i,new Vector3(-10+i*5,-.1f,i%2==0?-11:11),1.2f,90);
                    }
                    for(var i=0;i<6;i++)Place(landmarks,K+"wall_half.prefab","Side gallery "+i,new Vector3(-12+i*4,-.1f,14),3.6f);
                    Place(near,K+"stairs_wide.prefab","Broken approach steps",new Vector3(11,-.35f,0),2.8f,90);
                    Place(near,K+"chest_gold.prefab","Seal supplies",new Vector3(-11,-.1f,-5),1.2f);
                    break;
                case "hangar":
                    Place(landmarks,U+"Scifi_T/Scifi_Ship_Hangar.prefab","Rear docking bay",new Vector3(-14,-.1f,0),6f,90);
                    Place(landmarks,U+"Scifi_T/Scifi_Door_Metal.prefab","Bulkhead door",new Vector3(-12,-.1f,6),3.2f,90);
                    Place(landmarks,P+"SciFi/SciFi_Modular_Computer_Console_Control_Grey_01_Atlased.prefab","West maintenance console",new Vector3(-5,-.1f,-11.7f),2.1f);
                    Place(landmarks,U+"Scifi_T/Scifi_Generator_Double_Small.prefab","Maintenance generator",new Vector3(0,-.1f,-12),3f);
                    Place(landmarks,U+"Scifi_T/Scifi_Cargo_Big.prefab","East cargo container",new Vector3(-5,-.1f,12),3.7f);
                    Place(landmarks,U+"Scifi_T/Belt_Moving_Straight.prefab","Cargo conveyor",new Vector3(1,-.1f,12),3f);
                    for(var i=0;i<5;i++) {
                        Cube(near,"Deck safety rail",new Vector3(11,.20f,-7+i*3.5f),new Vector3(.13f,.35f,2.7f),stone);
                        Cube(near,"Amber rail light",new Vector3(11,.40f,-7+i*3.5f),new Vector3(.15f,.04f,2.7f),trim);
                        Place(near,U+"Scifi_T/Scifi_Pipe_Corridor.prefab","Deck conduit",new Vector3(7,-.1f,-11.5f+i*.20f),1.8f,90);
                    }
                    for(var i=0;i<4;i++)Place(landmarks,U+"Scifi_T/Scifi_Wall_Windows.prefab","Outer observation bay "+i,new Vector3(-14,-.1f,-9+i*6),3.1f,90);
                    break;
                case "forest":
                    Place(landmarks,P+"Druid Home/Rock_Prop_Tribal_Stone_Altar_Rune_01_Atlased.prefab","Rear druid altar",new Vector3(-12,-.1f,0),3.5f);
                    Place(landmarks,P+"Druid Home/Rock_Large_Arch_Cave_Stone_01_Atlased.prefab","Rear ruined arch",new Vector3(-14,-.1f,0),5f,90);
                    for(var i=0;i<12;i++) {
                        var x=i<4?-14:-10+(i%4)*4.5f;var z=i<4?-12+i*8:i<8?-13:13;
                        Place(landmarks,U+"Nature_T/Trees_T/Tree_Beech.prefab","Forest boundary "+i,new Vector3(x,-.2f,z),i<4?4.3f:2.6f,i*53);
                    }
                    for(var i=0;i<7;i++)Place(near,K+"floor_dirt_small_weeds.prefab","Moss approach "+i,new Vector3(11,-.1f,-8+i*2.5f),1.8f,i*37);
                    Place(near,K+"stairs_wide.prefab","Overgrown stone stair",new Vector3(11,-.45f,0),2.2f,90);
                    break;
                case "harbor":
                    Place(landmarks,U+"Pirates_T/Ship_Colonial.prefab","Rear moored sailing ship",new Vector3(-15,-.2f,1.5f),6.7f,90);
                    Place(landmarks,U+"Pirates_T/House_Pirate_Medium.prefab","East harbor warehouse",new Vector3(-6,-.1f,13),4.2f,180);
                    Place(landmarks,U+"Pirates_T/Shack.prefab","Harbormaster shack",new Vector3(1,-.1f,13),2.6f);
                    Place(landmarks,U+"Pirates_T/Crane_Wood.prefab","West cargo crane",new Vector3(-5,-.1f,-12),3.7f,90);
                    for(var i=0;i<5;i++)Place(near,U+"Pirates_T/Dock_Wood.prefab","Dock extension "+i,new Vector3(-10+i*5,-.23f,i%2==0?-11.5f:11.5f),3.0f,i%2==0?90:0);
                    for(var i=0;i<5;i++) {
                        Place(near,K+"barrel_small_stack.prefab","Harbor cargo "+i,new Vector3(10.8f,-.1f,-7+i*3.5f),.85f);
                        Cylinder(near,"Mooring bollard",new Vector3(11,.1f,-5+i*3),new Vector3(.22f,.25f,.22f),trim);
                    }
                    break;
            }
        }
        private static GameObject MakePawn(Spec s)
        {
            var go=new GameObject(s.Name+" Pawn");
            try {
                var baseGo=Cylinder(go.transform,"Faction base",new Vector3(0,-.015f,0),new Vector3(.78f,.055f,.78f),Mat("FactionBase",Color.white,.1f),true);
                Place(go.transform,s.Pawn,"Miniature",new Vector3(0,.04f,0),.82f,90);
                var collider=go.AddComponent<BoxCollider>();collider.center=new Vector3(0,.35f,0);collider.size=new Vector3(.82f,.9f,.82f);
                go.AddComponent<ThemePieceAppearance>().FactionRenderers=new[]{baseGo.GetComponent<Renderer>()};
                return PrefabUtility.SaveAsPrefabAsset(go,Root+"/"+s.Id+"/Pawn.prefab");
            } finally{Object.DestroyImmediate(go);}
        }
        internal static GameObject Place(Transform parent,string source,string name,Vector3 position,float size,float yaw=0,bool horizontal=false)
        {
            var prefab=Adapter(source);var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);go.name=name;go.transform.SetParent(parent,false);go.transform.localRotation=Quaternion.Euler(0,yaw,0);
            var bounds=Bounds(go);var max=horizontal?Mathf.Max(bounds.size.x,bounds.size.z):Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
            go.transform.localScale*=size/Mathf.Max(.001f,max);bounds=Bounds(go);
            go.transform.position+=position-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            foreach(var r in go.GetComponentsInChildren<Renderer>())r.receiveShadows=true;
            return go;
        }
        private static GameObject Adapter(string source)
        {
            var path=Root+"/Adapters/"+Path.GetFileNameWithoutExtension(source)+"_"+AssetDatabase.AssetPathToGUID(source).Substring(0,8)+".prefab";
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(existing!=null)return existing;
            var original=AssetDatabase.LoadAssetAtPath<GameObject>(source);if(original==null)throw new FileNotFoundException("Missing art model",source);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(original);
            try {
                foreach(var c in go.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(c);
                foreach(var c in go.GetComponentsInChildren<Animator>(true))Object.DestroyImmediate(c);
                foreach(var c in go.GetComponentsInChildren<Rigidbody>(true))Object.DestroyImmediate(c);
                foreach(var r in go.GetComponentsInChildren<Renderer>(true))r.sharedMaterials=r.sharedMaterials.Select(ConvertMaterial).ToArray();
                return PrefabUtility.SaveAsPrefabAsset(go,path);
            } finally{Object.DestroyImmediate(go);}
        }
        private static Material ConvertMaterial(Material old)
        {
            if(old==null)throw new InvalidOperationException("Source contains an empty material slot");
            if(old.shader.name.StartsWith("Universal Render Pipeline/"))return old;
            var path=Root+"/Materials/Adapted_"+old.name.Replace('/','_')+"_"+AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(old)).Substring(0,8)+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material!=null)return material;
            material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if(old.HasProperty("_Color"))material.SetColor("_BaseColor",old.GetColor("_Color"));
            if(old.HasProperty("_MainTex")) {material.SetTexture("_BaseMap",old.GetTexture("_MainTex"));material.SetTextureScale("_BaseMap",old.GetTextureScale("_MainTex"));material.SetTextureOffset("_BaseMap",old.GetTextureOffset("_MainTex"));}
            if(old.HasProperty("_Glossiness"))material.SetFloat("_Smoothness",old.GetFloat("_Glossiness"));
            if(old.HasProperty("_Metallic"))material.SetFloat("_Metallic",old.GetFloat("_Metallic"));
            if(old.HasProperty("_SpecColor")){material.SetFloat("_WorkflowMode",0);material.SetColor("_SpecColor",old.GetColor("_SpecColor"));material.EnableKeyword("_SPECULAR_SETUP");}
            foreach(var pair in new[]{new[]{"_BumpMap","_BumpMap","_NORMALMAP"},new[]{"_OcclusionMap","_OcclusionMap",""},new[]{"_MetallicGlossMap","_MetallicGlossMap","_METALLICSPECGLOSSMAP"},new[]{"_SpecGlossMap","_SpecGlossMap","_METALLICSPECGLOSSMAP"},new[]{"_EmissionMap","_EmissionMap","_EMISSION"}})
                if(old.HasProperty(pair[0]) && old.GetTexture(pair[0])!=null){material.SetTexture(pair[1],old.GetTexture(pair[0]));if(pair[2]!="")material.EnableKeyword(pair[2]);}
            if(old.HasProperty("_EmissionColor"))material.SetColor("_EmissionColor",old.GetColor("_EmissionColor"));
            if(old.IsKeywordEnabled("_ALPHATEST_ON")){material.SetFloat("_AlphaClip",1);material.EnableKeyword("_ALPHATEST_ON");material.SetFloat("_Cutoff",old.GetFloat("_Cutoff"));material.renderQueue=2450;}
            if(old.HasProperty("_Cull"))material.SetFloat("_Cull",old.GetFloat("_Cull"));
            material.enableInstancing=true;AssetDatabase.CreateAsset(material,path);return material;
        }
        internal static Bounds Bounds(GameObject go){var rs=go.GetComponentsInChildren<Renderer>();if(rs.Length==0)throw new InvalidOperationException("Model contains no renderer: "+go.name);var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
        private static Transform Group(Transform parent,string name){var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
        private static GameObject Cube(Transform parent,string name,Vector3 position,Vector3 scale,Material material,bool local=false)=>Primitive(PrimitiveType.Cube,parent,name,position,scale,material,local);
        private static GameObject Cylinder(Transform parent,string name,Vector3 position,Vector3 scale,Material material,bool local=false)=>Primitive(PrimitiveType.Cylinder,parent,name,position,scale,material,local);
        private static GameObject Primitive(PrimitiveType type,Transform parent,string name,Vector3 position,Vector3 scale,Material material,bool local)
        {var go=GameObject.CreatePrimitive(type);go.name=name;Object.DestroyImmediate(go.GetComponent<Collider>());go.transform.SetParent(parent,false);if(local)go.transform.localPosition=position;else go.transform.position=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;return go;}
        private static Material Mat(string name,Color color,float emission=0)
        {var path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m!=null)return m;m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.18f);m.enableInstancing=true;if(emission>0){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*emission);}AssetDatabase.CreateAsset(m,path);return m;}
        private static void FloorText(Transform parent,string name,string copy,Vector3 position,float size,Color color)
        {var go=new GameObject(name,typeof(TextMesh));go.transform.SetParent(parent,false);go.transform.position=position;go.transform.rotation=Quaternion.Euler(90,-90,0);var t=go.GetComponent<TextMesh>();t.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/SourceHanSansCN-Regular.otf");t.fontSize=48;t.characterSize=size;t.anchor=TextAnchor.MiddleCenter;t.text=copy;t.color=color;go.GetComponent<Renderer>().sharedMaterial=t.font.material;}
        private static Mesh RingMesh()
        {
            var path=Root+"/PortalRing.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh!=null)return mesh;
            mesh=new Mesh{name="Transfer ring"};var vertices=new Vector3[96];var triangles=new int[288];
            for(var i=0;i<48;i++){var a=i*Mathf.PI*2/48;vertices[i*2]=new Vector3(Mathf.Cos(a)*.41f,0,Mathf.Sin(a)*.41f);vertices[i*2+1]=new Vector3(Mathf.Cos(a)*.35f,0,Mathf.Sin(a)*.35f);var j=(i+1)%48;var k=i*6;triangles[k]=i*2;triangles[k+1]=j*2;triangles[k+2]=i*2+1;triangles[k+3]=j*2;triangles[k+4]=j*2+1;triangles[k+5]=i*2+1;}
            for(var i=0;i<triangles.Length;i+=3){var t=triangles[i+1];triangles[i+1]=triangles[i+2];triangles[i+2]=t;}
            mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        private static void Waterlines(Transform near,Material material)
        {for(var i=0;i<22;i++){var sign=i%2==0?-1:1;Cube(near,"Water ripple "+i,new Vector3(-16+i*1.5f,-.115f,sign*(11+i%4*1.2f)),new Vector3(.025f,.01f,.8f+i%3*.3f),material);}}
        private static void Folder(string path){var parts=path.Split('/');var p=parts[0];for(var i=1;i<parts.Length;i++){var n=p+"/"+parts[i];if(!AssetDatabase.IsValidFolder(n))AssetDatabase.CreateFolder(p,parts[i]);p=n;}}

        [MenuItem("Driftline/Themes/Install Authored Themes And Menu")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Install in Edit Mode.");
            var scene=Object.FindObjectOfType<BoardThemeScene>();
            if(scene==null) {
                var root=new GameObject("ThemeContainer");scene=root.AddComponent<BoardThemeScene>();
                scene.LegacyWalls=GameObject.Find("Walls").transform;scene.LegacyLight=GameObject.Find("Directional Light").GetComponent<Light>();
                var names=new[]{"Ground","Walls","Border","Lines","Start","Portal","Themes","Tabletop Identity"};
                scene.LegacyRenderers=names.Select(GameObject.Find).Where(g=>g!=null).SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)).ToArray();
            }
            var entries=(scene.Entries??Array.Empty<BoardThemeScene.Entry>()).ToList();
            foreach(var theme in DraftlineThemeApplier.Available.Where(t=>t.PresentationPrefab!=null)) {
                if(entries.Any(e=>e.Theme==theme))continue;
                var go=(GameObject)PrefabUtility.InstantiatePrefab(theme.PresentationPrefab);go.transform.SetParent(scene.transform,false);go.SetActive(false);
                entries.Add(new BoardThemeScene.Entry{Theme=theme,Root=go});
            }
            scene.Entries=entries.ToArray();EditorUtility.SetDirty(scene);
            InstallMenu();
            DraftlineThemeApplier.Apply("fortress");scene.PreviewThemeId="fortress";
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene.gameObject.scene);EditorSceneManager.SaveScene(scene.gameObject.scene);
        }
        private static void InstallMenu()
        {
            var path="Assets/UI/Prefabs/UIRoot.prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try {
                var ui=root.GetComponent<DraftlineCanvasUI>();if(ui.ThemeSelector!=null){ui.ThemeSelector.ClearOptions();ui.ThemeSelector.AddOptions(DraftlineThemeApplier.Available.Select(t=>t.ThemeName).ToList());ui.ThemeSelector.template.sizeDelta=new Vector2(ui.ThemeSelector.template.sizeDelta.x,272);PrefabUtility.SaveAsPrefabAsset(root,path);return;}
                var card=ui.MainMenu.transform.Find("MenuCard");
                var label=Object.Instantiate(card.Find("Subtitle").gameObject,card);label.name="BoardThemeLabel";Position(label.GetComponent<RectTransform>(),new Vector2(35,334),new Vector2(170,35));label.GetComponent<Text>().text="棋盘美术主题";
                var dropdown=Object.Instantiate(ui.Difficulty.gameObject,card);dropdown.name="BoardThemeSelector";Position(dropdown.GetComponent<RectTransform>(),new Vector2(220,327),new Vector2(305,48));ui.ThemeSelector=dropdown.GetComponent<Dropdown>();
                ui.ThemeSelector.ClearOptions();ui.ThemeSelector.AddOptions(DraftlineThemeApplier.Available.Select(t=>t.ThemeName).ToList());DriftlineExperienceSetup.StyleDropdown(ui.ThemeSelector);
                // Keep the authored menu story as the themed description; no runtime geometry is generated.
                ui.ThemeStory=card.Find("Story").GetComponent<Text>();ui.ThemeStory.fontSize=20;
                PrefabUtility.SaveAsPrefabAsset(root,path);
            } finally {PrefabUtility.UnloadPrefabContents(root);}
            // The scene holds an existing prefab instance; apply only the new serialized bindings.
            var live=Object.FindObjectOfType<DraftlineCanvasUI>();var liveCard=live.MainMenu.transform.Find("MenuCard");
            live.ThemeSelector=liveCard.Find("BoardThemeSelector").GetComponent<Dropdown>();live.ThemeStory=liveCard.Find("Story").GetComponent<Text>();EditorUtility.SetDirty(live);
        }
        private static void Position(RectTransform r,Vector2 p,Vector2 size){r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(p.x,-p.y);r.sizeDelta=size;}
        [MenuItem("Driftline/Themes/Polish Authored Theme Readability")]
        public static void Polish()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Polish in Edit Mode.");
            foreach(var id in new[]{"fortress","dungeon","hangar","forest","harbor"}) {
                var path=Root+"/"+id+"/"+id+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
                try {
                    foreach(var cover in root.transform.Find("FixedWalls").Cast<Transform>().Where(t=>t.name.StartsWith("Low cover"))) {
                        var b=Bounds(cover.gameObject);if(b.size.y<=.65f)continue;
                        cover.localScale*=.62f/b.size.y;b=Bounds(cover.gameObject);cover.position+=Vector3.up*(.57f-b.min.y);
                    }
                    if(root.transform.Find("ReadabilityAndSetDress")!=null){PrefabUtility.SaveAsPrefabAsset(root,path);continue;}
                    var s=Get(id);var details=Group(root.transform,"ReadabilityAndSetDress");
                    var seam=Mat(id+"_RuleGrid",Color.Lerp(s.Structure,s.FloorColor,.17f));
                    for(var i=0;i<=19;i++) {
                        Cube(details,"Rule column "+i,new Vector3(i-9.5f,.506f,0),new Vector3(.015f,.008f,19),seam);
                        Cube(details,"Rule row "+i,new Vector3(0,.506f,i-9.5f),new Vector3(19,.008f,.015f),seam);
                    }
                    var footing=Mat(id+"_Walkway",Color.Lerp(s.Structure,s.FloorColor,.6f));
                    if(id=="harbor") {
                        foreach(var c in root.transform.Find("FixedWalls").Cast<Transform>().Where(t=>t.name.StartsWith("Low cover")).ToArray())Object.DestroyImmediate(c.gameObject);
                        var layout=new BoardLayout();for(var y=0;y<19;y++)for(var x=0;x<19;x++){var p=new GridPosition(x,y);if(layout.CellAt(p).Kind==CellKind.FixedWall&&p!=new GridPosition(9,9))Place(root.transform.Find("FixedWalls"),U+"Fantasy_T/Crate_Box.prefab","Low cover "+p,BoardCoordinates.ToWorld(p,.57f),.62f,(x+y)%2*90);}
                        Cube(details,"Warehouse quay",new Vector3(-3,-.31f,12.7f),new Vector3(13,.42f,5.3f),footing);
                        Cube(details,"Crane quay",new Vector3(-5,-.31f,-12),new Vector3(6,.42f,4.5f),footing);
                        Cube(details,"Mooring pier",new Vector3(-11.1f,-.31f,1),new Vector3(3,.42f,18),Mat("Harbor_PierWood",new Color(.34f,.24f,.15f)));
                        var ship=root.transform.Find("Landmarks/Rear moored sailing ship");ship.localScale*=.78f;ship.position+=new Vector3(2.1f,-.15f,-.5f);
                    } else if(id=="fortress") {
                        Cube(details,"Rear command apron",new Vector3(-12,-.18f,1),new Vector3(4,.16f,20),footing);
                        foreach(var sign in new[]{-1,1})Cube(details,"Supply access strip",new Vector3(-3,-.18f,sign*11.7f),new Vector3(16,.16f,2.9f),footing);
                        // Low field markers connect the deployment camps to the supply road.
                        for(var i=0;i<9;i++)Cube(details,"Road dash",new Vector3(-7+i*1.9f,-.086f,-11.7f),new Vector3(.8f,.015f,.08f),Mat("Fortress_RoadPaint",new Color(.75f,.72f,.54f)));
                    } else if(id=="dungeon") {
                        Cube(details,"Hall plinth",new Vector3(0,-.28f,0),new Vector3(29,.30f,29),footing);
                        var originalGround=root.transform.Find("NearSet/Surrounding terrain").GetComponent<Renderer>().sharedMaterial;originalGround.SetColor("_BaseColor",new Color(.13f,.17f,.22f));EditorUtility.SetDirty(originalGround);
                        var arch=root.transform.Find("Landmarks/Rear vault arch");arch.localScale*=.72f;arch.position+=new Vector3(1,0,0);
                        foreach(var side in new[]{-1,1})for(var i=0;i<3;i++){var light=new GameObject("Warm gallery light",typeof(Light));light.transform.SetParent(details,false);light.transform.position=new Vector3(-8+i*7,1.2f,side*10.6f);var l=light.GetComponent<Light>();l.type=LightType.Point;l.color=new Color(1,.54f,.20f);l.intensity=2.8f;l.range=4;l.shadows=LightShadows.None;}
                    } else if(id=="forest") {
                        var originalGround=root.transform.Find("NearSet/Surrounding terrain").GetComponent<Renderer>().sharedMaterial;originalGround.SetColor("_BaseColor",new Color(.21f,.30f,.18f));EditorUtility.SetDirty(originalGround);
                        foreach(var tree in root.transform.Find("Landmarks").Cast<Transform>().Where(t=>t.name.StartsWith("Forest boundary")))if(Bounds(tree.gameObject).size.y>3.3f)tree.localScale*=.75f;
                        for(var i=0;i<9;i++)Place(details,K+"floor_dirt_small_weeds.prefab","Forest floor patch",new Vector3(-10+i*2.5f,-.15f,i%2==0?11:-11),1.5f,i*51);
                    } else if(id=="hangar") {
                        Cube(details,"Hangar outer deck",new Vector3(0,-.28f,0),new Vector3(30,.30f,30),footing);
                        var bay=root.transform.Find("Landmarks/Rear docking bay");bay.localScale*=.78f;bay.position+=new Vector3(1.2f,0,0);
                        foreach(var sign in new[]{-1,1})for(var i=0;i<8;i++)Cube(details,"Deck guide lamp",new Vector3(-12+i*3.5f,-.116f,sign*10.3f),new Vector3(.5f,.035f,.10f),Mat("Hangar_GuideLight",new Color(.28f,.83f,1),.5f));
                    }
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                } finally {PrefabUtility.UnloadPrefabContents(root);}
            }
            AssetDatabase.SaveAssets();
        }
        public static string Capture(string path,int width=1920,int height=1080)
        {
            var camera=Camera.main;var canvas=Object.FindObjectOfType<Canvas>();
            var oldTarget=camera.targetTexture;var oldAspect=camera.aspect;var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;var distance=canvas.planeDistance;
            var rt=RenderTexture.GetTemporary(width,height,24);var oldActive=RenderTexture.active;
            try {
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                camera.targetTexture=rt;camera.aspect=(float)width/height;Canvas.ForceUpdateCanvases();camera.Render();
                RenderTexture.active=rt;var image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,image.EncodeToPNG());Object.DestroyImmediate(image);return path;
            } finally {camera.targetTexture=oldTarget;camera.aspect=oldAspect;canvas.renderMode=mode;canvas.worldCamera=oldCamera;canvas.planeDistance=distance;RenderTexture.active=oldActive;RenderTexture.ReleaseTemporary(rt);}
        }
        [MenuItem("Driftline/Themes/Prepare Static Geometry Batching")]
        public static void PrepareBatching()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Prepare in Edit Mode.");
            Folder(Root+"/Meshes");
            var copies=new Dictionary<Mesh,Mesh>();
            foreach(var id in new[]{"fortress","dungeon","hangar","forest","harbor"}) {
                var path=Root+"/"+id+"/"+id+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
                try {
                    var batch=root.GetComponent<ThemeStaticGeometry>()??root.AddComponent<ThemeStaticGeometry>();
                    batch.StaticParts=new[]{"BoardSurface","FixedWalls","FactionGates","PairedPortals","NearSet","Landmarks","ReadabilityAndSetDress"}.Select(n=>root.transform.Find(n).gameObject).ToArray();
                    foreach(var part in batch.StaticParts)foreach(var filter in part.GetComponentsInChildren<MeshFilter>(true)) {
                        var source=filter.sharedMesh;if(source==null||source.isReadable)continue;
                        if(!copies.TryGetValue(source,out var mesh)) {
                            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out string guid,out long localId);
                            var meshPath=Root+"/Meshes/"+guid+"_"+localId+".asset";
                            mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                            if(mesh==null){
                                mesh=new Mesh{name=source.name+" (batch copy)",indexFormat=source.indexFormat,vertices=source.vertices,normals=source.normals,tangents=source.tangents,uv=source.uv,uv2=source.uv2,uv3=source.uv3,uv4=source.uv4,colors=source.colors,subMeshCount=source.subMeshCount};
                                for(var i=0;i<source.subMeshCount;i++)mesh.SetIndices(source.GetIndices(i),source.GetTopology(i),i,false);
                                mesh.bounds=source.bounds;AssetDatabase.CreateAsset(mesh,meshPath);
                            }
                            copies[source]=mesh;
                        }
                        filter.sharedMesh=mesh;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                } finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            AssetDatabase.SaveAssets();
        }
    }
}

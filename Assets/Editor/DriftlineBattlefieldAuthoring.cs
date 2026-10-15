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
    /// <summary>Offline stage construction. Saved prefabs, pivots, triggers and both states remain editable.</summary>
    public static class DriftlineBattlefieldAuthoring
    {
        const string Root="Assets/Themes/BattlefieldsV2";
        const string U="Assets/polyperfect/Low Poly Ultimate Pack/_T/Prefabs_T/";
        const string K="Assets/KayKit/Packs/KayKit - Dungeon Remastered Pack (for Unity)/Prefabs/";
        const string P="Assets/Off Axis Studios/Polyworks/Prefabs/Atlased/";
        static readonly string[] Ids={"fortress","dungeon","hangar","forest","harbor"};
        static readonly Color[] Factions={new Color(.95f,.24f,.18f),new Color(.16f,.52f,1),new Color(.18f,.83f,.40f),new Color(1,.76f,.12f)};
        static readonly Color[] PortalColors={new Color(1,.49f,.12f),new Color(1,.23f,.28f),new Color(.2f,.75f,1),new Color(.86f,.25f,1)};
        sealed class Palette
        {
            public Color Floor,Wall,Cap,Ground,Edge,Accent;
            public Palette(Color floor,Color wall,Color cap,Color ground,Color edge,Color accent){Floor=floor;Wall=wall;Cap=cap;Ground=ground;Edge=edge;Accent=accent;}
        }
        static Palette Colors(string id)
        {
            switch(id){
                case "fortress":return new Palette(new Color(.69f,.70f,.61f),new Color(.28f,.33f,.28f),new Color(.43f,.46f,.34f),new Color(.29f,.32f,.23f),new Color(.15f,.20f,.18f),new Color(.94f,.66f,.24f));
                case "dungeon":return new Palette(new Color(.32f,.37f,.45f),new Color(.16f,.20f,.29f),new Color(.36f,.43f,.52f),new Color(.12f,.15f,.23f),new Color(.07f,.095f,.15f),new Color(.34f,.87f,.91f));
                case "hangar":return new Palette(new Color(.31f,.40f,.49f),new Color(.105f,.17f,.23f),new Color(.40f,.53f,.62f),new Color(.13f,.20f,.28f),new Color(.055f,.09f,.15f),new Color(.25f,.84f,1));
                case "forest":return new Palette(new Color(.64f,.66f,.51f),new Color(.27f,.32f,.22f),new Color(.43f,.47f,.33f),new Color(.20f,.29f,.14f),new Color(.12f,.17f,.08f),new Color(.74f,.87f,.35f));
                default:return new Palette(new Color(.62f,.62f,.51f),new Color(.31f,.32f,.27f),new Color(.48f,.43f,.32f),new Color(.29f,.27f,.20f),new Color(.15f,.17f,.16f),new Color(.98f,.73f,.28f));
            }
        }
        [MenuItem("Driftline/Battlefields V2/Author All Missing Stages")]
        public static string AuthorAll(){foreach(var id in Ids)Author(id);Install();return "Five V2 stages installed";}
        public static string Author(string id,bool replace=false)
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Author in Edit Mode");
            Folder(Root);Folder(Root+"/Materials");Folder(Root+"/Meshes");Folder(Root+"/"+id);
            var path=Root+"/"+id+"/Stage.prefab";
            if(File.Exists(path)&&!replace)return path+" already exists; preserved artist edits";
            var theme=DraftlineThemeApplier.Load(id);var palette=Colors(id);
            var go=new GameObject(theme.ThemeName+" · 精制战场");
            try{
                var statics=Group(go.transform,"Architecture — static");var surface=Group(statics,"BoardSurface");
                var walls=Group(statics,"ConnectedWalls");var set=Group(statics,"SurroundingSet");
                var moving=Group(go.transform,"MovingWalls");var interactions=Group(go.transform,"FourCornerInteractions");
                var standard=Group(go.transform,"Environment — standard");var alternate=Group(go.transform,"Environment — alternate");
                var p=palette;
                var floor=Mat(id+"_Paving",p.Floor);var wall=Mat(id+"_Wall",p.Wall);var cap=Mat(id+"_Coping",p.Cap);
                var ground=Mat(id+"_Terrain",p.Ground);var edge=Mat(id+"_Foundation",p.Edge);var accent=Mat(id+"_Accent",p.Accent,.35f);
                // A crafted tray with visible thickness, bevelled perimeter and recessed inner paving.
                Slab(surface,id+" board foundation",Vector3.zero,new Vector3(20.5f,.65f,20.5f),.38f,edge,.12f);
                Slab(surface,"Inset board frame",new Vector3(0,.29f,0),new Vector3(20.05f,.2f,20.05f),.23f,cap,.07f);
                Box(surface,"Grid underlay",new Vector3(0,.442f,0),new Vector3(19.03f,.1f,19.03f),Mat(id+"_Seams",p.Floor*.51f));
                // Calm flat squares: no irregular broken tiles or debris in the play area.
                for(int x=0;x<19;x++)for(int z=0;z<19;z++){
                    var shade=((x*17+z*7)%5)*.008f;
                    var material=Mat(id+"_Paving_"+((x*17+z*7)%5),p.Floor*(1-shade));
                    Box(surface,"Cell "+z+","+x,new Vector3(x-9,.491f,z-9),new Vector3(.975f,.018f,.975f),material);
                }
                var board=new BoardLayout();
                MeshWalls(walls,id,board,wall,cap);
                WallDetails(walls,id,board,p,cap,accent);
                Mechanics(moving,id,board,wall,cap,accent);
                Markings(surface,id,board,wall,accent);
                // A continuous narrow service walk wraps the board; diagonal corners are actual joined slabs.
                RingWalk(set,id,ground,edge);
                Stage(go.transform,set,interactions,standard,alternate,id,p,ground,cap,accent);
                var lighting=Group(go.transform,"Lighting");var sun=new GameObject("Key light",typeof(Light));sun.transform.SetParent(lighting,false);
                sun.transform.rotation=Quaternion.Euler(id=="forest"?48:55,-35,0);var light=sun.GetComponent<Light>();light.type=LightType.Directional;light.shadows=LightShadows.Soft;light.shadowStrength=.65f;
                var environment=go.AddComponent<ThemeEnvironment>();environment.Standard=standard.gameObject;environment.Alternate=alternate.gameObject;environment.KeyLight=light;
                environment.StandardLight=id=="dungeon"?new Color(.68f,.79f,1):id=="hangar"?new Color(.77f,.9f,1):new Color(1,.91f,.73f);
                environment.AlternateLight=id=="dungeon"?new Color(.52f,.73f,.87f):id=="hangar"?new Color(.6f,.73f,1):id=="forest"?new Color(.71f,.83f,.87f):new Color(1,.67f,.43f);
                environment.StandardIntensity=id=="dungeon"?.95f:1.12f;environment.AlternateIntensity=.83f;
                environment.StandardAmbient=theme.AmbientColor;environment.AlternateAmbient=Color.Lerp(theme.AmbientColor,new Color(.23f,.32f,.44f),.25f);
                environment.StandardBackground=theme.BackgroundColor;environment.AlternateBackground=Color.Lerp(theme.BackgroundColor,new Color(.025f,.045f,.075f),.6f);
                environment.SeededDetails=alternate.Cast<Transform>().Where(t=>t.name.StartsWith("Detail")).Select(t=>t.gameObject).ToArray();
                light.color=environment.StandardLight;light.intensity=environment.StandardIntensity;alternate.gameObject.SetActive(false);
                go.AddComponent<ThemeStaticGeometry>().StaticParts=new[]{statics.gameObject};go.AddComponent<ThemeAmbientEffects>();
                PrepareMeshes(statics);
                theme.EnvironmentNames=id=="fortress"?new[]{"晴日驻防","黄昏战损"}:id=="dungeon"?new[]{"封印苏醒","湿冷遗迹"}:id=="hangar"?new[]{"正常运转","检修警戒"}:id=="forest"?new[]{"林间日光","雨后薄雾"}:new[]{"白昼装卸","暮色潮雾"};
                theme.Version=2;theme.OverviewDistance=32.5f;theme.FramingTarget=new Vector3(1.7f,0,-1.2f);theme.OverviewPitch=66;theme.PresentationPrefab=PrefabUtility.SaveAsPrefabAsset(go,path);
                EditorUtility.SetDirty(theme);AssetDatabase.SaveAssets();return path;
            }finally{Object.DestroyImmediate(go);}
        }
        static void MeshWalls(Transform parent,string id,BoardLayout board,Material side,Material top)
        {
            var vertices=new List<Vector3>();var sides=new List<int>();var tops=new List<int>();
            Action<Vector3,Vector3,Vector3,Vector3,List<int>> quad=(a,b,c,d,indices)=>{var n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});indices.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});};
            Func<int,int,bool> occupied=(r,c)=>BoardWallTopology.Occupied(board,r,c);
            for(int r=0;r<19;r++)for(int c=0;c<19;c++)if(occupied(r,c)){
                var x=r-9f;var z=c-9f;float lo=.50f,hi=id=="dungeon"?1.13f:id=="forest"?1.02f:1.07f;
                quad(new Vector3(x-.5f,hi,z-.5f),new Vector3(x-.5f,hi,z+.5f),new Vector3(x+.5f,hi,z+.5f),new Vector3(x+.5f,hi,z-.5f),tops);
                if(!occupied(r-1,c))quad(new Vector3(x-.5f,lo,z+.5f),new Vector3(x-.5f,hi,z+.5f),new Vector3(x-.5f,hi,z-.5f),new Vector3(x-.5f,lo,z-.5f),sides);
                if(!occupied(r+1,c))quad(new Vector3(x+.5f,lo,z-.5f),new Vector3(x+.5f,hi,z-.5f),new Vector3(x+.5f,hi,z+.5f),new Vector3(x+.5f,lo,z+.5f),sides);
                if(!occupied(r,c-1))quad(new Vector3(x-.5f,lo,z-.5f),new Vector3(x-.5f,hi,z-.5f),new Vector3(x+.5f,hi,z-.5f),new Vector3(x+.5f,lo,z-.5f),sides);
                if(!occupied(r,c+1))quad(new Vector3(x+.5f,lo,z+.5f),new Vector3(x+.5f,hi,z+.5f),new Vector3(x-.5f,hi,z+.5f),new Vector3(x-.5f,lo,z+.5f),sides);
            }
            var mesh=new Mesh{name=id+" welded wall silhouette"};mesh.SetVertices(vertices);mesh.subMeshCount=2;mesh.SetTriangles(sides,0);mesh.SetTriangles(tops,1);mesh.RecalculateNormals();mesh.RecalculateBounds();
            mesh=SaveMesh(mesh,id+"_ConnectedWalls");var go=new GameObject("Continuous walls — joined corners / ends / junctions",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<Renderer>().sharedMaterials=new[]{side,top};
        }
        static void WallDetails(Transform parent,string id,BoardLayout board,Palette p,Material cap,Material accent)
        {
            Func<int,int,bool> occ=(r,c)=>BoardWallTopology.Occupied(board,r,c);
            for(int r=0;r<19;r++)for(int c=0;c<19;c++)if(occ(r,c)){
                var mask=BoardWallTopology.Mask(occ,r,c);var shape=BoardWallTopology.Shape(mask);var pos=new Vector3(r-9,1.07f,c-9);
                // Junction caps are centred, never a straight model blindly rotated through a bend.
                if(shape=="Corner"||shape=="T"||shape=="Cross"||shape=="End"){
                    if(id=="fortress"){
                        Box(parent,shape+" armored join "+r+","+c,pos+Vector3.up*.035f,new Vector3(.56f,.07f,.56f),cap);
                        for(int k=0;k<4;k++)Cylinder(parent,"Flush bolt",pos+new Vector3(k<2?-.18f:.18f,.078f,k%2==0?-.18f:.18f),new Vector3(.055f,.018f,.055f),Mat("Steel bolts",new Color(.22f,.25f,.25f)));
                    }else if(id=="dungeon"){
                        Model(parent,K+"pillar_decorated.prefab",shape+" stone join",pos-new Vector3(0,.20f,0),.34f);
                    }else if(id=="hangar"){
                        Box(parent,shape+" bulkhead collar",pos+Vector3.up*.04f,new Vector3(.65f,.08f,.65f),cap);
                        Box(parent,"Flush status inset",pos+Vector3.up*.085f,new Vector3(.30f,.012f,.07f),accent);
                    }else if(id=="forest"){
                        Model(parent,U+"Nature_T/Stones_T/Stone_Small_Flat.prefab",shape+" ancient capstone",pos-new Vector3(0,.10f,0),.48f,(r+c)*17);
                    }else{
                        Cylinder(parent,shape+" quay joint",pos,new Vector3(.27f,.12f,.27f),cap);
                    }
                }else if((r+c)%3==0){
                    if(id=="fortress")Model(parent,U+"WW2_T/Sandbags_Straight.prefab","Sandbag crown",pos-Vector3.up*.08f,.73f,mask==5?90:0);
                    else if(id=="dungeon")Box(parent,"Masonry seam",pos+new Vector3(0,.065f,0),new Vector3(.018f,.009f,.98f),Mat("Mortar seams",p.Wall));
                    else if(id=="hangar")Box(parent,"Safety marking",pos+Vector3.up*.01f,new Vector3(.65f,.018f,.08f),Mat("Deck amber",new Color(.95f,.59f,.17f)));
                    else if(id=="forest")Model(parent,U+"Nature_T/Grass_T/Grass.prefab","Moss on connected stone",pos-Vector3.up*.07f,.20f,(r+c)*41);
                    else Model(parent,U+"Fantasy_T/Crate_Box.prefab","Quay stowage",pos,.40f,90);
                }
            }
        }
        static void Mechanics(Transform parent,string id,BoardLayout board,Material wall,Material cap,Material glow)
        {
            for(int r=0;r<19;r++)for(int c=0;c<19;c++){
                var pos=new GridPosition(c,r);if(board.CellAt(pos).Kind!=CellKind.RotatingWallSlot&&pos!=new GridPosition(9,9))continue;
                var slot=Group(parent,"Lift socket "+c+","+r);slot.localPosition=BoardCoordinates.ToWorld(pos,.5f);
                Box(slot,"Recess",new Vector3(0,.007f,0),new Vector3(.98f,.014f,.98f),wall);
                var body=Group(slot,"Animated body");
                if(id=="dungeon"||id=="forest"){
                    Slab(body,"Rising seal stone",new Vector3(0,.27f,0),new Vector3(.91f,.54f,.91f),.10f,cap,.06f);
                    Cylinder(body,"Carved sigil",new Vector3(0,.549f,0),new Vector3(.46f,.016f,.46f),glow);
                    Model(body,id=="dungeon"?K+"pillar_decorated.prefab":U+"Nature_T/Stones_T/Stone_Diamond.prefab","Seal crown",new Vector3(0,.55f,0),.22f);
                }else{
                    Box(body,id=="harbor"?"Floodgate":"Armored lift",new Vector3(0,.26f,0),new Vector3(.87f,.52f,.87f),wall);
                    for(int i=0;i<3;i++)Box(body,"Linked plated top",new Vector3(0,.55f,-.30f+i*.30f),new Vector3(.94f,.065f,.285f),cap);
                    foreach(var sign in new[]{-1,1})Box(body,"Recessed lamp",new Vector3(sign*.35f,.592f,0),new Vector3(.07f,.012f,.64f),glow);
                }
                var binding=slot.gameObject.AddComponent<ThemeMovingWall>();binding.Column=c;binding.Row=r;binding.Body=body;body.gameObject.SetActive(pos==new GridPosition(9,9)||board.IsScheduledWall(pos,0));
            }
        }
        static void Markings(Transform surface,string id,BoardLayout board,Material structure,Material accent)
        {
            for(int r=0;r<19;r++)for(int c=0;c<19;c++){
                var cell=board.CellAt(new GridPosition(c,r));var w=BoardCoordinates.ToWorld(new GridPosition(c,r),.514f);
                if(cell.Kind==CellKind.Spawn||cell.Kind==CellKind.PlayerGate){
                    var material=Mat("Faction_"+(int)cell.Owner,Factions[(int)cell.Owner],.3f);
                    if(cell.Kind==CellKind.Spawn){Cylinder(surface,"Deployment base "+cell.Owner,w,new Vector3(.9f,.025f,.9f),structure);Ring(surface,"Deployment rim",w+Vector3.up*.022f,.41f,.035f,material);FloorText(surface,"队徽",new[]{"Ⅰ","Ⅱ","Ⅲ","Ⅳ"}[(int)cell.Owner],w+Vector3.up*.027f,.10f,Factions[(int)cell.Owner]);}
                    else {var marker=Box(surface,"Permission diamond",w+Vector3.up*.01f,new Vector3(.25f,.018f,.25f),material);marker.transform.rotation=Quaternion.Euler(0,45,0);}
                }
                if(cell.Kind==CellKind.Portal){
                    int i="OCMR".IndexOf(cell.PortalKey);var portal=Mat("Portal_"+i,PortalColors[i],.65f);
                    if(id=="hangar"||id=="fortress"){
                        Slab(surface,"Transfer socket",w,new Vector3(.88f,.035f,.88f),.15f,structure,.01f);
                        foreach(var sign in new[]{-1,1})Box(surface,"Pair light rail",w+new Vector3(sign*.33f,.035f,0),new Vector3(.055f,.025f,.58f),portal);
                    }else{Cylinder(surface,"Transfer stone",w,new Vector3(.88f,.03f,.88f),structure);Ring(surface,"Engraved pair ring",w+Vector3.up*.025f,.38f,.035f,portal);}
                    FloorText(surface,"Portal "+cell.PortalKey,new[]{"○","◇","△","□"}[i],w+Vector3.up*.05f,.115f,PortalColors[i]);
                }
            }
        }
        static void RingWalk(Transform set,string id,Material ground,Material edge)
        {
            RingVolume(set,"Continuous service walk — welded corners",11.7f,10f,.285f,.035f,.20f,ground);
            RingVolume(set,"Continuous inner edge band",10.13f,10.03f,.37f,.29f,.055f,edge);
        }
        static void Stage(Transform root,Transform set,Transform interactions,Transform standard,Transform alternate,string id,Palette p,Material ground,Material cap,Material accent)
        {
            // Four composed corner islands, connected by side architecture. Rear silhouettes can rise; front stays low.
            var centers=new[]{new Vector3(-11.6f,.30f,-11.6f),new Vector3(-11.6f,.30f,11.6f),new Vector3(11.6f,.30f,11.6f),new Vector3(11.6f,.30f,-11.6f)};
            if(id=="forest"){
                RingVolume(set,"Continuous rooted earth — no overlapping terrain tiles",14.7f,9.95f,.28f,-.70f,1.1f,ground);
            }else if(id=="harbor"){
                Slab(set,"Water basin",new Vector3(0,-.65f,0),new Vector3(30,.40f,30),1.8f,Mat("Harbor deep water",new Color(.075f,.30f,.37f)),.2f);
                Slab(set,"Front stone quayside",new Vector3(11.9f,-.05f,0),new Vector3(4.0f,.65f,27),.55f,ground,.16f);
                Slab(set,"Rear mooring shore",new Vector3(-11.1f,-.05f,0),new Vector3(2.4f,.65f,27),.4f,ground,.16f);
                for(int i=0;i<4;i++)Slab(set,"Harbor corner apron",centers[i]-new Vector3(0,.25f,0),new Vector3(5.3f,.40f,5.3f),.45f,ground,.1f);
            }else{
                Slab(set,id=="hangar"?"Cargo deck chassis":id=="dungeon"?"Hall foundation":"Fortress earthen plinth",new Vector3(0,-.35f,0),new Vector3(28.5f,.75f,28.5f),.95f,ground,.18f);
                for(int i=0;i<4;i++)Slab(set,"Corner workshop platform",centers[i]-new Vector3(0,.26f,0),new Vector3(5.8f,.42f,5.8f),.45f,cap,.1f);
            }
            switch(id){
                case "fortress":Fortress(set,interactions,centers,p,cap,accent);break;
                case "dungeon":Dungeon(set,interactions,centers,p,cap,accent);break;
                case "hangar":Hangar(set,interactions,centers,p,cap,accent);break;
                case "forest":Forest(set,interactions,centers,p,cap,accent);break;
                case "harbor":Harbor(set,interactions,centers,p,cap,accent);break;
            }
            FineSetDress(set,id,p,cap,accent);
            Variants(set,standard,alternate,id,p);
            var ensemble=root.gameObject.AddComponent<ThemeSceneryEnsemble>();ensemble.Discovery=Particles(root,"Hidden beacon discovery",new Vector3(-11.4f,1,0),p.Accent,35,false,false);
            ensemble.Beacon=Point(root,"Discovery beacon",new Vector3(-11.6f,2,0),p.Accent,0,5);
        }
        static void Fortress(Transform set,Transform interactive,Vector3[] c,Palette p,Material cap,Material glow)
        {
            Model(set,U+"Military_T/Military_Headquarters.prefab","Command headquarters",new Vector3(-12,.23f,-3),4.2f,90);
            Model(set,U+"WW2_T/Bunker_Medium.prefab","Rear defense bunker",new Vector3(-12,.23f,5),4.4f,90);
            Model(set,U+"Military_T/Military_Tent.prefab","Supply depot tent",c[1]+new Vector3(1,0,1),3.2f,180);
            Model(set,U+"Military_T/Military_Barracks.prefab","Workshop",c[3]+new Vector3(1,0,-1),3.3f,90);
            for(int side=-1;side<=1;side+=2){
                for(int i=0;i<9;i++){
                    Span(set,U+"WW2_T/Sandbags_Straight.prefab","Connected sandbag parapet",new Vector3(-14+i*28f/9,.23f,side*14),new Vector3(-14+(i+1)*28f/9,.23f,side*14));
                    Box(set,"Road centre dash",new Vector3(-8+i*2.5f,.29f,side*11),new Vector3(1.15f,.012f,.10f),Mat("Military road paint",new Color(.72f,.68f,.49f)));
                }
                // Two perpendicular legs with a filled central knot at each defense corner.
                foreach(var end in new[]{-1,1}){
                    Span(set,U+"WW2_T/Sandbags_Straight.prefab","Parapet return",new Vector3(end*14,.23f,side*14),new Vector3(end*14,.23f,side*11.8f));
                    Model(set,U+"WW2_T/Sandbag.prefab","Mitered sandbag corner",new Vector3(end*14,.23f,side*14),1.05f,45);
                }
            }
            var radio=Pivot(interactive,"Signal mast",c[0]);
            Model(radio,U+"Military_T/Military_Tower.prefab","Watchtower",c[0],2.9f);
            var dish=Pivot(radio,"Antenna yaw",c[0]+new Vector3(0,2.8f,0));
            Box(dish,"Antenna horizontal",dish.position,new Vector3(1.9f,.08f,.08f),cap);Box(dish,"Cross dipole",dish.position,new Vector3(.08f,.08f,1),cap);
            Hotspot(radio,"信号天线","点击扫描驻地",c[0]+Vector3.up*1.3f,new Vector3(2.8f,3,2.8f),dish,Vector3.zero,new Vector3(0,155,0),false,"ItemActivate",p.Accent);
            var supply=Pivot(interactive,"Supply chest",c[1]+new Vector3(-1.0f,0,-.8f));
            Chest(supply,supply.position,cap,glow,out var lid);
            Hotspot(supply,"补给箱","点击打开 / 合上",supply.position+Vector3.up*.45f,new Vector3(1.8f,1.2f,1.6f),lid,Vector3.zero,new Vector3(85,0,0),true,"DiceLand",p.Accent);
            var cannon=Pivot(interactive,"Outward artillery",c[2]);
            Model(cannon,U+"Scifi_T/Scifi_Turret_Base_Circle_Big.prefab","Gun mount",c[2],1.5f);
            var barrel=Pivot(cannon,"Recoil carriage",c[2]+new Vector3(0,1,0));
            Model(barrel,U+"Scifi_T/Scifi_Turret_Head_Cannon_Small.prefab","Artillery barrel",barrel.position,2.2f,0);
            var gun=Hotspot(cannon,"岸防炮","点击试射，炮口朝向场外",c[2]+Vector3.up*.8f,new Vector3(3,2,3),barrel,new Vector3(0,0,-.30f),Vector3.zero,false,"WallFall",new Color(.65f,.64f,.55f));gun.Effect.transform.position=c[2]+new Vector3(0,1.1f,1.3f);
            var engine=Pivot(interactive,"Repair vehicle",c[3]+new Vector3(-.6f,0,.6f));
            var vehicle=Model(engine,U+"WW2_T/Jeep_Willys.prefab","Jeep under repair",engine.position,2.7f,90).transform;
            var ignition=Hotspot(engine,"维修吉普","点击启动引擎",engine.position+Vector3.up*.5f,new Vector3(2.8f,1.8f,2.8f),vehicle,new Vector3(0,.055f,0),new Vector3(0,0,3),true,"Slide",new Color(.5f,.56f,.52f));
            ignition.ActiveDecoration=Particles(engine,"Engine exhaust",engine.position+new Vector3(-1,.3f,0),new Color(.4f,.43f,.4f,.35f),4,true,true,.3f).gameObject;ignition.DecorationRequiresEffects=true;ignition.ActiveDecoration.SetActive(false);
            Supplies(set,"fortress",c);
        }
        static void Dungeon(Transform set,Transform interactive,Vector3[] c,Palette p,Material cap,Material glow)
        {
            Model(set,K+"wall_arched.prefab","Main seal arch",new Vector3(-13.0f,.1f,0),4.5f,90);
            Model(set,P+"Dungeon/Prop_Furniture_Church_Altar_01_Atlased.prefab","Altar beneath arch",new Vector3(-11.6f,.3f,0),2.7f,90);
            Box(set,"Rear gallery connecting foundation",new Vector3(-13.1f,.50f,0),new Vector3(.65f,.65f,28),Mat("Dungeon gallery stone",p.Wall));
            // Two outer arcades: exact foundations carry the repeated sculpted pillars and connected walls.
            foreach(var sign in new[]{-1,1}){
                Box(set,"Gallery continuous stone wall",new Vector3(0,.75f,sign*14),new Vector3(28,1.05f,.65f),Mat("Dungeon gallery stone",p.Wall));
                for(int i=0;i<7;i++){
                    Model(set,K+"pillar_decorated.prefab","Arcade pier",new Vector3(-12+i*4,.3f,sign*13.8f),2.6f);
                    if(i<6)Span(set,K+"barrier.prefab","Arcade infill",new Vector3(-12+i*4,.3f,sign*14),new Vector3(-8+i*4,.3f,sign*14));
                }
                foreach(var end in new[]{-1,1}){
                    Box(set,"Connected gallery return",new Vector3(end*14,.75f,sign*12.6f),new Vector3(.65f,1.05f,2.8f),Mat("Dungeon gallery stone",p.Wall));
                    Model(set,K+"barrier_corner.prefab","Gallery return corner",new Vector3(end*13.6f,.3f,sign*13.6f),1.55f,end<0?(sign<0?0:90):(sign>0?180:270));
                }
            }
            Model(set,K+"stairs_wide.prefab","Ceremonial entry stair",new Vector3(11.7f,.02f,0),3.5f,90);
            var rune=Pivot(interactive,"Rune shrine",c[0]);
            Model(rune,K+"pillar_decorated.prefab","Runestone pedestal",c[0],2.1f);
            var seal=Pivot(rune,"Rotating seal",c[0]+Vector3.up*2.05f);Cylinder(seal,"Rune crown",seal.position,new Vector3(.9f,.14f,.9f),glow);
            Hotspot(rune,"封印石柱","点击唤醒 / 熄灭符文",c[0]+Vector3.up*1,new Vector3(2.5f,2.5f,2.5f),seal,Vector3.zero,new Vector3(0,90,0),true,"Portal",p.Accent);
            var alchemy=Pivot(interactive,"Alchemist bench",c[1]);
            Model(alchemy,K+"table_medium.prefab","Alchemy worktable",c[1],2.3f);
            var bottle=Model(alchemy,K+"bottle_A_green.prefab","Bubbling flask",c[1]+new Vector3(0,1.05f,0),.65f).transform;
            Model(alchemy,K+"bottle_A_brown.prefab","Spare bottle",c[1]+new Vector3(.6f,1.05f,.3f),.42f);
            Hotspot(alchemy,"炼金台","点击搅动药剂",c[1]+Vector3.up*.8f,new Vector3(2.8f,2,2.8f),bottle,new Vector3(0,.14f,0),new Vector3(0,0,10),false,"ItemPickup",new Color(.36f,.9f,.44f));
            var chest=Pivot(interactive,"Relic chest",c[2]);Chest(chest,c[2],cap,glow,out var lid);
            Model(set,K+"barrel_small_stack.prefab","Vault supplies",c[2]+new Vector3(1.4f,0,1),1.25f);
            Hotspot(chest,"遗物宝箱","点击打开 / 合上",c[2]+Vector3.up*.5f,new Vector3(2,1.6f,2),lid,Vector3.zero,new Vector3(80,0,0),true,"DiceLand",new Color(.98f,.77f,.29f));
            var lever=Pivot(interactive,"Seal control",c[3]);
            Slab(lever,"Lever base",c[3]+Vector3.up*.2f,new Vector3(1.5f,.4f,1.5f),.2f,cap,.05f);
            var arm=Pivot(lever,"Lever hinge",c[3]+Vector3.up*.6f);Box(arm,"Handle",arm.position+Vector3.up*.5f,new Vector3(.12f,1,.12f),cap);Cylinder(arm,"Grip",arm.position+Vector3.up,new Vector3(.42f,.08f,.42f),glow);
            Hotspot(lever,"廊道拉杆","点击切换廊道灯光",c[3]+Vector3.up*.7f,new Vector3(2.4f,2,2.4f),arm,Vector3.zero,new Vector3(0,0,50),true,"WallRise",p.Accent);
            for(int i=0;i<8;i++){int sign=i%2==0?-1:1;var pos=new Vector3(-10+(i/2)*6.5f,.32f,sign*11);Model(set,K+"torch_lit.prefab","Warm torch",pos,1.0f);Point(set,"Torch wash",pos+Vector3.up, new Color(1,.48f,.18f),1.7f,3);}
            Supplies(set,"dungeon",c);
        }
        static void Hangar(Transform set,Transform interactive,Vector3[] c,Palette p,Material cap,Material glow)
        {
            Model(set,U+"Scifi_T/Scifi_Ship_Hangar.prefab","Docked shuttle shelter",new Vector3(-13,.25f,0),4.6f,90);
            foreach(var sign in new[]{-1,1}){
                Box(set,"Continuous outer bulkhead",new Vector3(0,.9f,sign*14),new Vector3(28,1.3f,.60f),Mat("Hangar bulkhead",p.Wall));
                for(int i=0;i<7;i++){
                    Box(set,"Structural rib",new Vector3(-13+i*4.1f,1.4f,sign*13.8f),new Vector3(.35f,2.2f,.9f),cap);
                    Box(set,"Blue deck guide",new Vector3(-11+i*3.6f,.3f,sign*10.7f),new Vector3(1.1f,.035f,.08f),glow);
                    Span(set,U+"Scifi_T/Scifi_Vent_Long.prefab","Continuous service duct",new Vector3(-13+i*3.8f,.35f,sign*13.1f),new Vector3(-13+(i+1)*3.8f,.35f,sign*13.1f));
                }
                foreach(var end in new[]{-1,1}){
                    Box(set,"Connected bulkhead return",new Vector3(end*14,.9f,sign*12.6f),new Vector3(.6f,1.3f,2.8f),Mat("Hangar bulkhead",p.Wall));
                    Model(set,U+"Scifi_T/Scifi_Wall_Corner.prefab","Bulkhead corner",new Vector3(end*13.5f,.25f,sign*13.5f),1.6f,end<0?(sign<0?0:90):(sign>0?180:270));
                    Slab(set,"Duct right-angle junction housing",new Vector3(end*13.3f,.85f,sign*13.1f),new Vector3(1.25f,1.0f,1.25f),.12f,cap,.06f);
                    Box(set,"Connected duct return",new Vector3(end*13.3f,.85f,sign*12.2f),new Vector3(.85f,.9f,2),cap);
                    Box(set,"Junction status strip",new Vector3(end*13.3f,1.36f,sign*13.1f),new Vector3(.65f,.025f,.08f),glow);
                }
            }
            var radar=Pivot(interactive,"Navigation radar",c[0]);Model(radar,U+"Scifi_T/Scifi_Satelit_Stand_Small.prefab","Radar base",c[0],1.3f);
            var dish=Model(radar,U+"Scifi_T/Scifi_Satelit_Small.prefab","Turning antenna",c[0]+Vector3.up*.8f,1.8f).transform;
            Hotspot(radar,"导航雷达","点击扫描停机坪",c[0]+Vector3.up, new Vector3(2.8f,2.7f,2.8f),dish,Vector3.zero,new Vector3(0,180,0),false,"ItemActivate",p.Accent);
            Model(set,U+"Scifi_T/Scifi_Cargo_Big.prefab","Cargo bay container",c[1]+new Vector3(0,0,1.1f),3.0f);
            var lift=Pivot(interactive,"Cargo lift",c[1]+new Vector3(1.2f,0,-.8f));Box(lift,"Lift well",lift.position,new Vector3(2,.2f,2),cap);
            var platform=Pivot(lift,"Lift platform",lift.position+Vector3.up*.2f);Box(platform,"Deck plate",platform.position,new Vector3(1.8f,.18f,1.8f),cap);Model(platform,U+"Scifi_T/Scifi_Box.prefab","Lift cargo",platform.position+Vector3.up*.10f,.9f);
            Hotspot(lift,"货运升降台","点击升起 / 降下",lift.position+Vector3.up*.5f,new Vector3(2.3f,2,2.3f),platform,new Vector3(0,.9f,0),Vector3.zero,true,"WallRise",p.Accent);
            var arm=Pivot(interactive,"Maintenance arm",c[2]);Cylinder(arm,"Robot base",c[2],new Vector3(1.6f,.3f,1.6f),cap);
            var hinge=Pivot(arm,"Shoulder",c[2]+Vector3.up*.35f);Box(hinge,"Upper arm",hinge.position+Vector3.up*.65f,new Vector3(.35f,1.3f,.35f),cap);
            Box(hinge,"Robot elbow",hinge.position+new Vector3(.5f,1.3f,0),new Vector3(1.35f,.3f,.3f),Mat("Robot orange",new Color(.86f,.45f,.16f)));
            Model(hinge,U+"Scifi_T/Scifi_Box_Divided.prefab","Gripper",hinge.position+new Vector3(1.1f,1,0),.45f);
            Hotspot(arm,"维修机械臂","点击进行焊接检查",c[2]+Vector3.up*.8f,new Vector3(3,2.5f,3),hinge,Vector3.zero,new Vector3(0,45,18),false,"ItemActivate",new Color(.6f,.84f,1));
            var power=Pivot(interactive,"Power station",c[3]);Model(power,U+"Scifi_T/Scifi_Generator_Double_Small.prefab","Generator",c[3],2.3f);
            var valve=Pivot(power,"Cooling valve",c[3]+new Vector3(0,1.3f,0));Cylinder(valve,"Valve wheel",valve.position,new Vector3(.8f,.08f,.8f),glow);
            Hotspot(power,"冷却阀门","点击泄压 / 关闭",c[3]+Vector3.up*.7f,new Vector3(2.9f,2.3f,2.9f),valve,Vector3.zero,new Vector3(0,130,0),true,"Slide",new Color(.67f,.84f,.91f));
            Model(set,P+"SciFi/SciFi_Modular_Computer_Console_Control_Grey_01_Atlased.prefab","Maintenance console",new Vector3(8,.30f,-11.4f),1.6f);
            Supplies(set,"hangar",c);
        }
        static void Forest(Transform set,Transform interactive,Vector3[] c,Palette p,Material cap,Material glow)
        {
            Model(set,P+"Druid Home/Rock_Large_Arch_Cave_Stone_01_Atlased.prefab","Ancient tree arch",new Vector3(-12,.15f,0),4.5f,90);
            Model(set,P+"Druid Home/Rock_Prop_Tribal_Stone_Altar_Rune_01_Atlased.prefab","Rooted altar",new Vector3(-11,.2f,0),2.2f);
            for(int i=0;i<18;i++){
                int sign=i%2==0?-1:1;var x=-13+(i/2)*3.2f+Mathf.Sin(i*5.3f)*.45f;var z=sign*(13.3f+(i%3)*.23f);
                Model(set,U+"Nature_T/Trees_T/"+new[]{"Tree_Beech","Tree_Birch","Tree_Conifer"}[i%3]+".prefab","Forest canopy "+i,new Vector3(x,.12f,z),i<8?3.8f:2.8f,i*51);
                Model(set,U+"Nature_T/Trees_T/Bush_Big.prefab","Understory cluster",new Vector3(x-1,.17f,z-.8f*sign),1.4f,i*31);
                Model(set,U+"Nature_T/Stones_T/Stone_Large.prefab","Bank stone",new Vector3(x+1,.1f,z-.9f*sign),1.3f,i*41);
                if(i<16)Model(set,U+"Nature_T/Grass_T/Grass.prefab","Root path patch",new Vector3(x,.25f,sign*11.2f),.48f,i*37);
            }
            Model(set,K+"stairs_wide.prefab","Reclaimed entrance steps",new Vector3(11.5f,-.1f,0),2.8f,90);
            var stone=Pivot(interactive,"Listening shrine",c[0]);var monolith=Model(stone,P+"Druid Home/Rock_Prop_Tribal_Stone_Altar_Rune_02_Atlased.prefab","Ancient guardian",c[0],2.1f).transform;
            Hotspot(stone,"古树符石","点击唤醒 / 安息",c[0]+Vector3.up*.8f,new Vector3(2.8f,2.4f,2.8f),monolith,new Vector3(0,.16f,0),new Vector3(0,16,0),true,"Portal",p.Accent);
            var pond=Pivot(interactive,"Spring pool",c[1]);Slab(pond,"Pond lip",c[1],new Vector3(2.6f,.25f,2.6f),.7f,cap,.1f);Cylinder(pond,"Spring water",c[1]+Vector3.up*.15f,new Vector3(2.2f,.035f,2.2f),Mat("Spring water",new Color(.15f,.43f,.38f)));
            var ripple=Pivot(pond,"Water ripple",c[1]+Vector3.up*.18f);Ring(ripple,"Spring ring",ripple.position,.75f,.025f,glow);
            var spring=Hotspot(pond,"林间泉眼","点击泛起涟漪",c[1]+Vector3.up*.3f,new Vector3(3,1.2f,3),ripple,new Vector3(0,.03f,0),new Vector3(0,120,0),false,"ItemPickup",new Color(.27f,.69f,.66f));spring.Motions[0].Scale=new Vector3(.4f,0,.4f);
            var fire=Pivot(interactive,"Explorer campfire",c[2]);
            for(int i=0;i<7;i++){float a=i*Mathf.PI*2/7;Model(fire,U+"Nature_T/Stones_T/Stone_Small.prefab","Fire ring stone",c[2]+new Vector3(Mathf.Cos(a)*.65f,0,Mathf.Sin(a)*.65f),.4f,i*35);}
            var log=Model(fire,U+"Nature_T/Trees_T/Log_Birch.prefab","Camp firewood",c[2],1.25f,45).transform;
            var campfire=Hotspot(fire,"探索者营火","点击添柴 / 收火",c[2]+Vector3.up*.4f,new Vector3(2.5f,1.6f,2.5f),log,new Vector3(0,.04f,0),new Vector3(0,0,7),true,"DiceLand",new Color(1,.49f,.14f));
            campfire.ActiveDecoration=Model(fire,U+"Survival_T/Fire.prefab","Low poly campfire flame",c[2]+Vector3.up*.12f,1.05f);campfire.ActiveDecoration.SetActive(false);
            Model(set,K+"barrel_small_stack.prefab","Camp provisions",c[2]+new Vector3(1.2f,0,1),1.1f);
            var flower=Pivot(interactive,"Firefly garden",c[3]);
            var bloom=Model(flower,U+"Nature_T/Flowers_T/Flower_Red.prefab","Rare flower",c[3],1.1f).transform;
            for(int i=0;i<6;i++)Model(set,U+"Nature_T/Flowers_T/Flower_Red.prefab","Wildflower cluster",c[3]+new Vector3(Mathf.Sin(i*2)*1.2f,0,Mathf.Cos(i*2)*1.2f),.5f,i*30);
            Hotspot(flower,"萤火花丛","点击唤来萤火",c[3]+Vector3.up*.4f,new Vector3(2.7f,1.8f,2.7f),bloom,Vector3.zero,new Vector3(0,0,16),false,"ItemPickup",new Color(.83f,.97f,.35f));
        }
        static void Harbor(Transform set,Transform interactive,Vector3[] c,Palette p,Material cap,Material glow)
        {
            Model(set,U+"Pirates_T/Ship_Colonial.prefab","Moored merchant ship",new Vector3(-13.5f,-.4f,0),7.0f,0);
            Model(set,U+"Pirates_T/House_Pirate_Medium.prefab","Harbor warehouse",new Vector3(-4,.28f,12.3f),3.6f,180);
            Model(set,U+"Pirates_T/Shack.prefab","Dockmaster office",new Vector3(4,.28f,12.1f),2.5f,180);
            foreach(var sign in new[]{-1,1}){
                for(int i=0;i<6;i++){
                    // Plank platforms sit on a continuous stone quay instead of floating disconnected in water.
                    var dock=Span(set,U+"Pirates_T/Dock_Wood.prefab","Dock boardwalk",new Vector3(-11+i*3.6f,0,sign*12.7f),new Vector3(-11+(i+1)*3.6f,0,sign*12.7f));dock.transform.position+=Vector3.up*(.30f-DriftlineThemeAuthoring.Bounds(dock).max.y);
                    Cylinder(set,"Mooring post",new Vector3(-8+i*3.5f,.6f,sign*13.6f),new Vector3(.22f,.75f,.22f),cap);
                    if(i<5)Box(set,"Linked dock handrail",new Vector3(-6.25f+i*3.5f,.87f,sign*13.6f),new Vector3(3.5f,.10f,.10f),cap);
                }
                foreach(var end in new[]{-1,1}){Cylinder(set,"Railing corner post",new Vector3(end*13.2f,.6f,sign*13.6f),new Vector3(.25f,.8f,.25f),cap);Box(set,"Railing return",new Vector3(end*13.2f,.87f,sign*12.4f),new Vector3(.1f,.1f,2.4f),cap);}
                foreach(var x in new[]{-7f,0,7})for(int plank=0;plank<4;plank++)Box(set,"Walk-to-dock timber connection",new Vector3(x-.6f+plank*.4f,.28f,sign*12.0f),new Vector3(.37f,.08f,1.8f),Mat("Pier timber",new Color(.38f,.25f,.13f)));
            }
            var bell=Pivot(interactive,"Ship bell",c[0]);Box(bell,"Bell stand",c[0]+new Vector3(0,1,0),new Vector3(.18f,2,.18f),cap);
            Box(bell,"Bell crossbeam",c[0]+new Vector3(0,1.85f,0),new Vector3(1.2f,.16f,.16f),cap);
            var chime=Pivot(bell,"Bell hinge",c[0]+new Vector3(.45f,1.8f,0));Cylinder(chime,"Brass bell",chime.position-new Vector3(0,.25f,0),new Vector3(.6f,.45f,.6f),glow);
            Hotspot(bell,"船钟","点击摇响靠港钟",c[0]+Vector3.up,new Vector3(2.5f,2.7f,2.5f),chime,Vector3.zero,new Vector3(24,0,0),false,"TurnChange",p.Accent);
            var crane=Pivot(interactive,"Cargo crane",c[1]);Model(crane,U+"Pirates_T/Crane_Wood.prefab","Wooden cargo crane",c[1],3.2f,90);
            var load=Model(crane,U+"Fantasy_T/Crate_Box.prefab","Suspended cargo",c[1]+new Vector3(1,1.1f,-.5f),.75f).transform;
            Hotspot(crane,"码头吊机","点击起吊 / 放下货物",c[1]+Vector3.up*1,new Vector3(3.3f,3,3.3f),load,new Vector3(0,.9f,0),Vector3.zero,true,"WallRise",new Color(.66f,.69f,.62f));
            var fishing=Pivot(interactive,"Fishing berth",c[2]);
            Model(fishing,K+"barrel_small_stack.prefab","Fishing tackle",c[2],1.15f);
            var rod=Pivot(fishing,"Fishing rod root",c[2]+new Vector3(.3f,.7f,.5f));var stick=Box(rod,"Fishing rod",rod.position+new Vector3(.6f,.45f,0),new Vector3(1.6f,.045f,.045f),cap);stick.transform.rotation=Quaternion.Euler(0,0,35);
            Hotspot(fishing,"垂钓泊位","点击收竿，看看水花",c[2]+Vector3.up*.65f,new Vector3(2.8f,2,2.8f),rod,Vector3.zero,new Vector3(0,0,35),false,"ItemPickup",new Color(.24f,.65f,.78f));
            var beacon=Pivot(interactive,"Harbor navigation light",c[3]);
            Model(beacon,U+"Pirates_T/Tower_Pirate_Stone.prefab","Navigation tower",c[3],2.3f);
            var lantern=Pivot(beacon,"Beacon shutters",c[3]+Vector3.up*2.3f);Cylinder(lantern,"Lantern lens",lantern.position,new Vector3(.55f,.30f,.55f),glow);
            Hotspot(beacon,"引航灯塔","点击开启 / 关闭航标",c[3]+Vector3.up,new Vector3(2.7f,3.2f,2.7f),lantern,Vector3.zero,new Vector3(0,150,0),true,"ItemActivate",p.Accent);
            Supplies(set,"harbor",c);
            for(int i=0;i<18;i++)Box(set,"Quiet water glint",new Vector3(i%2==0?-14.3f:14.3f,-.43f,-12+i*1.4f),new Vector3(.04f,.012f,.6f+i%3*.2f),Mat("Harbor glints",new Color(.17f,.44f,.49f)));
        }
        static void FineSetDress(Transform parent,string id,Palette p,Material cap,Material glow)
        {
            var set=Group(parent,"Composed workshops and side connections");
            var dark=Mat(id+"_DetailDark",p.Edge);var paving=Mat(id+"_DetailPaving",Color.Lerp(p.Floor,p.Ground,.4f));
            // The common dice stays on a visible low console, physically outside the board.
            Slab(set,"Dice station plinth",new Vector3(12.1f,.42f,-1.8f),new Vector3(1.8f,.38f,2.3f),.22f,cap,.08f);
            FloorText(set,"Dice station title","掷 骰",new Vector3(13.1f,.32f,-1.8f),.075f,p.Accent);
            if(id=="fortress"){
                Model(set,U+"Military_T/Military_Gate.prefab","Front security checkpoint",new Vector3(13.6f,.24f,0),3.2f,90);
                foreach(var sign in new[]{-1,1}){
                    Model(set,U+"WW2_T/Barricade_Tank.prefab","Checkpoint barricade",new Vector3(13.4f,.25f,sign*3),1.4f);
                    Model(set,U+"WW2_T/Trenches_Wire.prefab","Trench wire return",new Vector3(13.5f,.25f,sign*6),2.7f);
                    Model(set,U+"Apocalypse_T/Tires.prefab","Workshop tire stack",new Vector3(11.8f,.31f,sign*8),1.0f);
                    Model(set,U+"People_T/Man_Soldier.prefab","Checkpoint sentry",new Vector3(12.8f,.30f,sign*2.1f),.85f,sign>0?0:180);
                    for(int i=0;i<5;i++)Box(set,"Front approach road paint",new Vector3(12.6f,.29f,sign*(3.5f+i)),new Vector3(.12f,.012f,.55f),Mat("Military road paint",new Color(.72f,.68f,.49f)));
                    Box(set,"Rear workshop apron",new Vector3(-11.8f,.26f,sign*7),new Vector3(2.8f,.04f,2.8f),paving);
                    for(int i=0;i<3;i++)Model(set,U+"WW2_T/Ammo_Crate_Pak.prefab","Protected rear ammo racks",new Vector3(-13+i*.8f,.29f,sign*7.3f),.65f,90);
                }
                Model(set,U+"Tools_T/Anvil.prefab","Repair bench anvil",new Vector3(11.2f,.32f,-8.4f),.65f);
                Model(set,U+"WW2_T/Jeep_Willys.prefab","Command courier",new Vector3(-12,.28f,-6.5f),2.4f,0);
                FloorText(set,"Front defense marking","04 / FIELD COMMAND",new Vector3(11,.30f,4.5f),.048f,p.Accent);
            }else if(id=="dungeon"){
                foreach(var sign in new[]{-1,1}){
                    // Bright inlaid stone is continuous from one corner landing to the next.
                    Box(set,"Gallery inlay inner",new Vector3(0,.30f,sign*11.2f),new Vector3(24,.018f,.04f),glow);
                    Box(set,"Gallery inlay outer",new Vector3(0,.30f,sign*12.3f),new Vector3(24,.018f,.04f),cap);
                    for(int i=0;i<6;i++){
                        var pos=new Vector3(-10+i*4,.28f,sign*12.1f);
                        Slab(set,"Gallery flagstone",pos,new Vector3(3.7f,.04f,1.4f),.12f,paving,.012f);
                        if(i%2==0)Model(set,K+"barrel_large.prefab","Cellar jar",pos+new Vector3(1,.04f,sign*.65f),.65f);
                    }
                    Slab(set,"Front votive platform",new Vector3(12.5f,.32f,sign*5.6f),new Vector3(2.7f,.3f,4.6f),.3f,cap,.06f);
                    Model(set,K+"pillar_decorated.prefab","Votive stone",new Vector3(13.1f,.48f,sign*5.6f),1.8f);
                    Model(set,U+"Medieval_T/Shield_Knight_Cross.prefab","Offering shield",new Vector3(12,.49f,sign*6.5f),.85f,90);
                    Model(set,K+"chest_gold.prefab","Altar reliquary",new Vector3(12,.49f,sign*4.7f),.85f);
                    Ring(set,"Corner inlay junction",new Vector3(-11.8f,.31f,sign*11.8f),.62f,.045f,glow);
                    Model(set,K+"wall_doorway.prefab","Rear gallery doorway",new Vector3(-12.7f,.3f,sign*5.5f),2.6f,90);
                }
                FloorText(set,"Seal inscription","界 钥  /  封 印 大 厅",new Vector3(11,.31f,4.1f),.055f,p.Accent);
            }else if(id=="hangar"){
                foreach(var sign in new[]{-1,1}){
                    Slab(set,"Service bay recessed floor",new Vector3(12,.28f,sign*5.8f),new Vector3(3.2f,.10f,5.6f),.24f,dark,.03f);
                    for(int i=0;i<8;i++){
                        var stripe=Box(set,"Apron hazard stripe",new Vector3(10.55f,.35f,sign*(3.5f+i*.55f)),new Vector3(.3f,.012f,.18f),Mat("Deck amber",new Color(.95f,.59f,.17f)));stripe.transform.rotation=Quaternion.Euler(0,35,0);
                        Box(set,"Service bay floor grate",new Vector3(11.8f,.34f,sign*(3.5f+i*.55f)),new Vector3(2.1f,.025f,.08f),cap);
                    }
                    Model(set,U+"Scifi_T/Scifi_Hatch.prefab","Access hatch",new Vector3(13.0f,.34f,sign*4.6f),1.15f);
                    Model(set,U+"Scifi_T/Belt_Moving_Straight.prefab","Cargo loading conveyor",new Vector3(12.5f,.36f,sign*7.7f),2.6f,90);
                    Model(set,U+"Scifi_T/Scifi_Vial_Crate.prefab","Sealed technical supplies",new Vector3(11.6f,.35f,sign*7.1f),.9f);
                    Model(set,U+"Scifi_T/Scifi_Solar_Panel_Small.prefab","Rear power collector",new Vector3(-12,.30f,sign*6.7f),1.6f);
                    Model(set,U+"Scifi_T/Scifi_Pole_Light.prefab","Rear warning mast",new Vector3(-13.5f,.3f,sign*5),1.6f);
                }
                FloorText(set,"Deck stencil","NAV  /  TRAINING 04",new Vector3(11,.35f,4.8f),.05f,p.Accent);
                Model(set,U+"People_T/Man_Astronaut.prefab","Dock technician",new Vector3(-11.5f,.3f,-4.4f),.9f,90);
            }else if(id=="forest"){
                // A dense rear grove and two uneven foreground banks break the formal symmetry of the stone court.
                for(int i=0;i<13;i++){
                    var pos=new Vector3(-13.2f+Mathf.Sin(i*2.3f)*.6f,.10f,-10.6f+i*1.75f);
                    Model(set,U+"Nature_T/Trees_T/"+(i%3==0?"Tree_Birch":"Tree_Forest")+".prefab","Rear forest cluster",pos,2.3f+(i%4)*.35f,i*39);
                    Model(set,U+"Nature_T/Grass_T/Grass.prefab","Grove understory",pos+new Vector3(.7f,.13f,.5f),.7f,i*67);
                }
                foreach(var sign in new[]{-1,1}){
                    Slab(set,"Low rooted foreground bank",new Vector3(12.2f,.06f,sign*5.6f),new Vector3(3.3f,.68f,6.3f),.85f,Mat("Forest soil edge",new Color(.28f,.27f,.15f)),.15f);
                    for(int i=0;i<6;i++){
                        var pos=new Vector3(12.0f+Mathf.Sin(i*2)*.8f,.25f,sign*(3.2f+i*.9f));
                        Model(set,U+"Nature_T/Stones_T/Stone_Small.prefab","Rock garden cluster",pos,.55f+(i%2)*.35f,i*43);
                        Model(set,U+"Nature_T/Grass_T/Grass.prefab","Bank grasses",pos+new Vector3(.5f,.05f,.3f),.65f,i*17);
                    }
                    Model(set,P+"Druid Home/Vegetation_Tree_Roots_Vines_Snaking_04_Atlased.prefab","Creeping roots on ancient retaining wall",new Vector3(7,.24f,sign*12),2.6f,sign>0?0:180);
                    Model(set,U+"Nature_T/Trees_T/Logs.prefab","Fallen timber",new Vector3(13,.26f,sign*7.4f),1.9f,30);
                }
                for(int i=0;i<5;i++)Model(set,U+"Nature_T/Stones_T/Stone_Small_Flat.prefab","Ancient approach stepping stone",new Vector3(11+i*.62f,.25f,(i%2)*.3f-1.5f),.6f,i*42);
            }else{
                foreach(var sign in new[]{-1,1}){
                    Slab(set,"Grouped cargo footprint",new Vector3(12,.29f,sign*5.8f),new Vector3(2.8f,.08f,5.4f),.25f,Mat("Harbor cargo paving",new Color(.38f,.35f,.26f)),.025f);
                    for(int i=0;i<8;i++){
                        var pos=new Vector3(11.3f+(i%2)*1.2f,.36f,sign*(4+i/2*1.2f));
                        Model(set,i%3==0?K+"barrel_small_stack.prefab":U+"Fantasy_T/Crate_Box.prefab","Cargo stack",pos,.65f+(i%3)*.15f,i%2*90);
                        if(i==3||i==6)Model(set,U+"Fantasy_T/Crate_Box.prefab","Upper stacked cargo",pos+Vector3.up*.70f,.55f,90);
                    }
                    Model(set,U+"Western_T/Wheel_Wood.prefab","Dock cart wheel",new Vector3(13.2f,.35f,sign*7.2f),.8f);
                    Model(set,U+"People_T/Man_Pirate.prefab","Dock worker",new Vector3(11,.35f,sign*3.2f),.9f,90);
                    // Two broad plank aprons attach the rear mooring to the shore and cargo lanes.
                    Box(set,"Rear stone quay wall",new Vector3(-10.8f,-.12f,sign*6.5f),new Vector3(1.6f,.7f,6),cap);
                    for(int i=0;i<8;i++)Box(set,"Quay apron plank",new Vector3(-11.5f,.29f,sign*(3.6f+i*.7f)),new Vector3(3.1f,.08f,.65f),Mat("Pier timber",new Color(.38f,.25f,.13f)));
                    Cylinder(set,"Rear mooring bollard",new Vector3(-12,.55f,sign*4),new Vector3(.25f,.65f,.25f),cap);
                }
                FloorText(set,"Dock district stencil","QUAY  /  04",new Vector3(11.0f,.31f,3.5f),.06f,p.Accent);
            }
        }
        static void Supplies(Transform set,string id,Vector3[] corners)
        {
            for(int corner=0;corner<4;corner++)for(int i=0;i<4;i++){
                var position=corners[corner]+new Vector3((i%2==0?-1:1)*1.6f,.02f,(i<2?-1:1)*1.5f);
                string path=id=="fortress"?U+"WW2_T/Ammo_Crate_Pak.prefab":id=="hangar"?U+"Scifi_T/Scifi_Box_Divided.prefab":id=="harbor"?U+"Fantasy_T/Crate_Box.prefab":K+"barrel_small_stack.prefab";
                Model(set,path,"Grouped workshop supplies",position,.58f+(i%2)*.18f,i%2*90);
            }
        }
        static void Variants(Transform set,Transform standard,Transform alternate,string id,Palette p)
        {
            if(id=="fortress"){
                Model(standard,U+"Military_T/Military_Truck.prefab","Supply convoy",new Vector3(0,.28f,-12),2.8f,90);
                Model(alternate,U+"WW2_T/Barricade_Tank.prefab","Damaged road closure",new Vector3(0,.28f,-12),1.8f);
                for(int i=0;i<8;i++)Model(alternate,U+"Nature_T/Stones_T/Stone_Small.prefab","Detail shell debris",new Vector3(-10+i*3,.30f,i%2==0?-11:11),.35f+i%3*.12f,i*33);
                Particles(alternate,"Distant repair smoke",new Vector3(-12,.7f,5),new Color(.43f,.42f,.38f,.4f),5,true,true);
            }else if(id=="dungeon"){
                for(int i=0;i<6;i++){
                    Model(standard,K+"banner_patternA_blue.prefab","Hall ceremonial banner",new Vector3(-10+i*4,.3f,i%2==0?-12.7f:12.7f),1.6f,90);
                    Model(alternate,K+"wall_broken.prefab","Detail collapsed masonry",new Vector3(-10+i*4,.30f,i%2==0?-12:12),1.4f,i*31);
                }
                Mist(alternate,new Color(.32f,.53f,.66f,.18f));
            }else if(id=="hangar"){
                for(int i=0;i<6;i++){
                    var pos=new Vector3(-9+i*3.6f,.40f,i%2==0?-10.8f:10.8f);
                    Box(standard,"Operational running light",pos,new Vector3(.65f,.04f,.14f),Mat("Operational green",new Color(.18f,.87f,.57f),.7f));
                    Box(alternate,"Maintenance hazard light",pos,new Vector3(.65f,.04f,.14f),Mat("Maintenance amber",new Color(1,.42f,.08f),1));
                    Model(alternate,U+"Scifi_T/Scifi_Box.prefab","Detail repair stores",pos+new Vector3(0,-.1f,1),.65f);
                }
                Particles(alternate,"Coolant vent",new Vector3(11.6f,1,-11.6f),new Color(.63f,.8f,.9f,.3f),4,true,true);
            }else if(id=="forest"){
                for(int i=0;i<12;i++)Model(alternate,U+"Nature_T/Stones_T/Stone_Small_Flat.prefab","Detail wet moss stone",new Vector3(-11+i*2,.28f,i%2==0?-11:11),.50f,i*47);
                Mist(alternate,new Color(.49f,.66f,.56f,.20f));
                Particles(standard,"Sunlit fireflies",new Vector3(11.6f,.4f,-11.6f),new Color(.82f,.94f,.35f,.8f),3,true,false);
            }else{
                Model(standard,U+"Fantasy_T/Crate_Box.prefab","Fresh cargo",new Vector3(4,.3f,-12.0f),1.0f);
                for(int i=0;i<6;i++)Model(alternate,K+"barrel_small_stack.prefab","Detail late cargo",new Vector3(-8+i*3,.3f,-12.3f),.75f);
                Mist(alternate,new Color(.44f,.62f,.69f,.20f));
            }
        }
        static void Mist(Transform parent,Color color){foreach(var sign in new[]{-1,1})Particles(parent,"Low perimeter mist",new Vector3(0,.25f,sign*12.5f),color,3,true,true,10);}
        static ThemeSceneryInteraction Hotspot(Transform root,string label,string hint,Vector3 center,Vector3 size,Transform target,Vector3 shift,Vector3 rotation,bool toggle,string audio,Color color)
        {
            var go=new GameObject(label+" · click",typeof(BoxCollider),typeof(ThemeSceneryInteraction));go.transform.SetParent(root,false);go.transform.position=center;go.layer=2;
            var collider=go.GetComponent<BoxCollider>();collider.isTrigger=true;collider.size=size;
            var interaction=go.GetComponent<ThemeSceneryInteraction>();interaction.Label=label;interaction.Hint=hint;interaction.Toggle=toggle;
            interaction.Motions=new[]{new ThemeSceneryInteraction.Motion{Target=target,Translation=shift,Rotation=rotation}};
            bool smoke=label.Contains("吉普")||label.Contains("阀门")||label.Contains("岸防炮");
            if(label!="船钟")interaction.Effect=Particles(root,"Local response",center+Vector3.up*.3f,new Color(color.r,color.g,color.b,.65f),22,false,smoke,.38f);
            interaction.ResponseLight=Point(root,"Response light",center,color,.15f,3);
            var sound=go.AddComponent<AudioSource>();sound.clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Sfx/"+audio+".wav");sound.playOnAwake=false;sound.spatialBlend=0;sound.volume=.22f;interaction.Sound=sound;
            var marker=Cylinder(root,"Touchable detail glint",center-new Vector3(0,size.y*.4f,0),new Vector3(.20f,.04f,.20f),Mat("Scenery interaction glint",new Color(.73f,.71f,.48f),.2f));interaction.Indicator=marker.GetComponent<Renderer>();
            return interaction;
        }
        static void Chest(Transform parent,Vector3 pos,Material body,Material trim,out Transform lid)
        {
            Slab(parent,"Chest base",pos+Vector3.up*.30f,new Vector3(1.35f,.60f,.95f),.10f,body,.04f);
            lid=Pivot(parent,"Lid hinge",pos+new Vector3(0,.63f,.46f));
            Slab(lid,"Hinged chest lid",pos+new Vector3(0,.70f,0),new Vector3(1.4f,.18f,1),.08f,body,.04f);
            foreach(var sign in new[]{-1,1})Box(lid,"Metal chest strap",pos+new Vector3(sign*.42f,.8f,0),new Vector3(.10f,.025f,1),trim);
            Box(lid,"Latch",pos+new Vector3(0,.66f,-.51f),new Vector3(.20f,.25f,.08f),trim);
        }
        static ParticleSystem Particles(Transform parent,string name,Vector3 position,Color color,float rate,bool loop,bool smoke,float spread=1)
        {
            var go=new GameObject(name,typeof(ParticleSystem));go.transform.SetParent(parent,false);go.transform.position=position;
            var ps=go.GetComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.duration=2;main.loop=loop;main.playOnAwake=loop;main.startLifetime=smoke?3f:1.8f;main.startSpeed=smoke?.25f:.65f;main.startSize=smoke?.45f:.055f;main.startColor=color;main.maxParticles=loop?45:40;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=ps.emission;emission.enabled=true;emission.rateOverTime=loop?rate:0;if(!loop)emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)rate)});
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(spread,.05f,Mathf.Min(spread,1.2f));
            var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.x=.13f;velocity.y=smoke?.14f:.3f;velocity.z=.04f;
            var colorLife=ps.colorOverLifetime;colorLife.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.15f),new GradientAlphaKey(0,1)});colorLife.color=gradient;
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=ParticleMaterial(smoke);renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            return ps;
        }
        static Material ParticleMaterial(bool smoke)
        {
            var path=Root+"/Materials/"+(smoke?"SoftSmoke":"Spark")+".mat";var existing=AssetDatabase.LoadAssetAtPath<Material>(path);if(existing!=null)return existing;
            var texturePath=Root+"/Materials/SoftParticle.asset";var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if(texture==null){texture=new Texture2D(32,32,TextureFormat.RGBA32,false){name="Soft radial particle"};for(int y=0;y<32;y++)for(int x=0;x<32;x++){float d=Vector2.Distance(new Vector2(x,y),new Vector2(15.5f,15.5f))/15.5f;texture.SetPixel(x,y,new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-d),1.6f)));}texture.Apply();AssetDatabase.CreateAsset(texture,texturePath);}
            var m=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));m.SetTexture("_BaseMap",texture);m.SetColor("_BaseColor",Color.white);m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);m.SetFloat("_ZWrite",0);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=3000;AssetDatabase.CreateAsset(m,path);return m;
        }
        static Light Point(Transform parent,string name,Vector3 pos,Color color,float intensity,float range){var go=new GameObject(name,typeof(Light));go.transform.SetParent(parent,false);go.transform.position=pos;var l=go.GetComponent<Light>();l.type=LightType.Point;l.color=color;l.intensity=intensity;l.range=range;l.shadows=LightShadows.None;return l;}
        static GameObject Model(Transform parent,string source,string name,Vector3 position,float size,float yaw=0)=>DriftlineThemeAuthoring.Place(parent,source,name,position,size,yaw);
        static GameObject Span(Transform parent,string source,string name,Vector3 start,Vector3 end)
        {
            var length=Vector3.Distance(start,end);var center=(start+end)*.5f;
            var go=DriftlineThemeAuthoring.Place(parent,source,name,Vector3.zero,length+.025f,0,true);
            var bounds=DriftlineThemeAuthoring.Bounds(go);var direction=(end-start).normalized;
            var yaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg-(bounds.size.x>bounds.size.z?90:0);
            go.transform.rotation=Quaternion.Euler(0,yaw,0);bounds=DriftlineThemeAuthoring.Bounds(go);
            go.transform.position+=center-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);return go;
        }
        static Transform Group(Transform parent,string name){var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
        static Transform Pivot(Transform parent,string name,Vector3 position){var t=Group(parent,name);t.position=position;return t;}
        static GameObject Box(Transform parent,string name,Vector3 pos,Vector3 size,Material material)=>Primitive(PrimitiveType.Cube,parent,name,pos,size,material);
        static GameObject Cylinder(Transform parent,string name,Vector3 pos,Vector3 size,Material material)=>Primitive(PrimitiveType.Cylinder,parent,name,pos,size,material);
        static GameObject Primitive(PrimitiveType type,Transform parent,string name,Vector3 pos,Vector3 size,Material material){var go=GameObject.CreatePrimitive(type);Object.DestroyImmediate(go.GetComponent<Collider>());go.name=name;go.transform.SetParent(parent,false);go.transform.position=pos;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;return go;}
        static GameObject RingVolume(Transform parent,string name,float outer,float inner,float top,float bottom,float cut,Material material)
        {
            Func<float,Vector3[]> contour=size=>new[]{new Vector3(-size+cut,0,-size),new Vector3(size-cut,0,-size),new Vector3(size,0,-size+cut),new Vector3(size,0,size-cut),new Vector3(size-cut,0,size),new Vector3(-size+cut,0,size),new Vector3(-size,0,size-cut),new Vector3(-size,0,-size+cut)};
            var a=contour(outer);var b=contour(inner);var vertices=new List<Vector3>();var indices=new List<int>();
            Action<Vector3,Vector3,Vector3,Vector3> quad=(p,q,r,s)=>{int n=vertices.Count;vertices.AddRange(new[]{p,q,r,s});indices.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});};
            for(int i=0;i<8;i++){int j=(i+1)%8;
                var at=a[i]+Vector3.up*top;var aj=a[j]+Vector3.up*top;var bt=b[i]+Vector3.up*top;var bj=b[j]+Vector3.up*top;
                var ab=a[i]+Vector3.up*bottom;var ax=a[j]+Vector3.up*bottom;var bb=b[i]+Vector3.up*bottom;var bx=b[j]+Vector3.up*bottom;
                quad(at,bt,bj,aj);quad(ab,at,aj,ax);quad(bb,bx,bj,bt);quad(ab,ax,bx,bb);
            }
            var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            string key="RingVolume_"+string.Join("_",new[]{outer,inner,top,bottom,cut}.Select(v=>v.ToString("0.000",System.Globalization.CultureInfo.InvariantCulture)));mesh=SaveMesh(mesh,key);
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }
        static GameObject Slab(Transform parent,string name,Vector3 pos,Vector3 size,float corner,Material material,float bevel)
        {
            var mesh=new Mesh{name=name};var v=new List<Vector3>();var t=new List<int>();
            var outline=new[]{new Vector2(-size.x/2+corner,-size.z/2),new Vector2(size.x/2-corner,-size.z/2),new Vector2(size.x/2,-size.z/2+corner),new Vector2(size.x/2,size.z/2-corner),new Vector2(size.x/2-corner,size.z/2),new Vector2(-size.x/2+corner,size.z/2),new Vector2(-size.x/2,size.z/2-corner),new Vector2(-size.x/2,-size.z/2+corner)};
            Action<Vector3,Vector3,Vector3> triangle=(a,b,c)=>{int n=v.Count;v.AddRange(new[]{a,b,c});t.AddRange(new[]{n,n+1,n+2});};
            for(int i=0;i<8;i++){
                int j=(i+1)%8;var a=outline[i];var b=outline[j];var at=new Vector3(a.x*.985f,size.y/2,a.y*.985f);var bt=new Vector3(b.x*.985f,size.y/2,b.y*.985f);var ab=new Vector3(a.x,-size.y/2,a.y);var bb=new Vector3(b.x,-size.y/2,b.y);var am=new Vector3(a.x,size.y/2-bevel,a.y);var bm=new Vector3(b.x,size.y/2-bevel,b.y);
                triangle(Vector3.up*size.y/2,bt,at);triangle(at,bt,bm);triangle(at,bm,am);triangle(am,bm,bb);triangle(am,bb,ab);triangle(Vector3.down*size.y/2,ab,bb);
            }
            mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            // Shared by exact dimensions within a stage; meshes are saved before the authoring root is destroyed.
            var key="Slab_"+size.x.ToString("0.000",System.Globalization.CultureInfo.InvariantCulture)+"_"+size.y.ToString("0.000",System.Globalization.CultureInfo.InvariantCulture)+"_"+size.z.ToString("0.000",System.Globalization.CultureInfo.InvariantCulture)+"_"+corner.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+"_"+bevel.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture);
            var old=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/"+key+".asset");if(old!=null){Object.DestroyImmediate(mesh);mesh=old;}else SaveMesh(mesh,key);
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.transform.position=pos;go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }
        static void Ring(Transform parent,string name,Vector3 pos,float radius,float width,Material material)
        {
            var path=Root+"/Meshes/Ring_"+radius.ToString("0.000",System.Globalization.CultureInfo.InvariantCulture)+"_"+width.ToString("0.000",System.Globalization.CultureInfo.InvariantCulture)+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){var v=new Vector3[64];var triangles=new int[192];for(int i=0;i<32;i++){float a=i*Mathf.PI/16;v[i*2]=new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);v[i*2+1]=new Vector3(Mathf.Cos(a)*(radius-width),0,Mathf.Sin(a)*(radius-width));int j=(i+1)%32;int n=i*6;triangles[n]=i*2;triangles[n+1]=i*2+1;triangles[n+2]=j*2;triangles[n+3]=j*2;triangles[n+4]=i*2+1;triangles[n+5]=j*2+1;}mesh=new Mesh{name="Engraved ring",vertices=v,triangles=triangles};mesh.RecalculateNormals();AssetDatabase.CreateAsset(mesh,path);}
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.transform.position=pos;go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<Renderer>().sharedMaterial=material;
        }
        static void FloorText(Transform parent,string name,string text,Vector3 pos,float size,Color color){var go=new GameObject(name,typeof(TextMesh));go.transform.SetParent(parent,false);go.transform.position=pos;go.transform.rotation=Quaternion.Euler(90,-90,0);var t=go.GetComponent<TextMesh>();t.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/SourceHanSansCN-Regular.otf");t.fontSize=48;t.characterSize=size;t.anchor=TextAnchor.MiddleCenter;t.text=text;t.color=color;go.GetComponent<Renderer>().sharedMaterial=t.font.material;}
        static Material Mat(string name,Color color,float emission=0){var path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);bool fresh=m==null;if(fresh)m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.18f);if(emission>0){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*emission);}if(fresh)AssetDatabase.CreateAsset(m,path);else EditorUtility.SetDirty(m);return m;}
        static Mesh SaveMesh(Mesh mesh,string key){var path=Root+"/Meshes/"+key+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing!=null){EditorUtility.CopySerialized(mesh,existing);EditorUtility.SetDirty(existing);Object.DestroyImmediate(mesh);return existing;}AssetDatabase.CreateAsset(mesh,path);return mesh;}
        static void Folder(string path){var parts=path.Split('/');var p=parts[0];for(int i=1;i<parts.Length;i++){var n=p+"/"+parts[i];if(!AssetDatabase.IsValidFolder(n))AssetDatabase.CreateFolder(p,parts[i]);p=n;}}
        static void PrepareMeshes(Transform root)
        {
            foreach(var f in root.GetComponentsInChildren<MeshFilter>(true)){
                var source=f.sharedMesh;if(source==null||source.isReadable)continue;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out string guid,out long localId);
                var path=Root+"/Meshes/"+guid+"_"+localId+".asset";var copy=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(copy==null){copy=new Mesh{name=source.name+" static copy",indexFormat=source.indexFormat,vertices=source.vertices,normals=source.normals,tangents=source.tangents,uv=source.uv,uv2=source.uv2,uv3=source.uv3,uv4=source.uv4,colors=source.colors,subMeshCount=source.subMeshCount};for(int i=0;i<source.subMeshCount;i++)copy.SetIndices(source.GetIndices(i),source.GetTopology(i),i,false);copy.bounds=source.bounds;AssetDatabase.CreateAsset(copy,path);}
                f.sharedMesh=copy;
            }
        }
        public static string Install()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Install in Edit Mode");
            var scene=Object.FindObjectOfType<BoardThemeScene>();
            foreach(var e in scene.Entries){if(e.Theme==null||e.Theme.ThemeId=="tabletop")continue;var prefab=e.Theme.PresentationPrefab;if(!AssetDatabase.GetAssetPath(prefab).StartsWith(Root))continue;if(e.Root!=null)Object.DestroyImmediate(e.Root);e.Root=(GameObject)PrefabUtility.InstantiatePrefab(prefab);e.Root.transform.SetParent(scene.transform,false);e.Root.SetActive(false);}
            InstallMenu();DraftlineThemeApplier.Apply("fortress");EditorUtility.SetDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene.gameObject.scene);EditorSceneManager.SaveScene(scene.gameObject.scene);return "Saved V2 stage instances and editable menu";
        }
        static void InstallMenu()
        {
            const string path="Assets/UI/Prefabs/UIRoot.prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try{
                var ui=root.GetComponent<DraftlineCanvasUI>();var card=ui.MainMenu.transform.Find("MenuCard");
                if(ui.EnvironmentSelector==null){
                    var dropdown=Object.Instantiate(ui.ThemeSelector.gameObject,card);dropdown.name="EnvironmentSelector";Position(dropdown.GetComponent<RectTransform>(),new Vector2(220,390),new Vector2(305,48));ui.EnvironmentSelector=dropdown.GetComponent<Dropdown>();ui.EnvironmentSelector.ClearOptions();ui.EnvironmentSelector.AddOptions(new System.Collections.Generic.List<string>{"晴日驻防","黄昏战损","随机环境"});ui.EnvironmentSelector.template.sizeDelta=new Vector2(305,150);
                    var label=Object.Instantiate(card.Find("BoardThemeLabel").gameObject,card);label.name="EnvironmentLabel";label.GetComponent<Text>().text="场景状态";Position(label.GetComponent<RectTransform>(),new Vector2(35,397),new Vector2(170,35));
                    // Authored buttons move down together; theme story remains above the selectors.
                    foreach(var button in card.GetComponentsInChildren<Button>(true)){
                        if(button.GetComponentInParent<Dropdown>()!=null)continue;var r=button.GetComponent<RectTransform>();if(r.anchoredPosition.y < -380)r.anchoredPosition+=Vector2.down*62;
                    }
                }
                if(ui.SceneryHint==null){var hint=new GameObject("SceneryHint",typeof(RectTransform),typeof(Text));hint.transform.SetParent(root.transform,false);var r=hint.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,0);r.anchoredPosition=new Vector2(110,12);r.sizeDelta=new Vector2(1000,32);var text=hint.GetComponent<Text>();text.font=ui.ThemeStory.font;text.fontSize=17;text.color=new Color(.83f,.86f,.78f);text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;text.text="棋盘四周藏着可点击的小机关 · 悬停探索";ui.SceneryHint=text;}
                var instructions=card.Find("Instructions").GetComponent<RectTransform>();Position(instructions,new Vector2(35,810),new Vector2(490,60));instructions.GetComponent<Text>().fontSize=16;
                ui.EnvironmentSelector.template.sizeDelta=new Vector2(0,150);
                ui.ThemeSelector.template.sizeDelta=new Vector2(0,272);
                var goal=ui.Hud.GetComponentsInChildren<Text>(true).FirstOrDefault(t=>t.text.StartsWith("五子连线"));
                if(goal!=null){Position(goal.transform.parent.GetComponent<RectTransform>(),new Vector2(1540,20),new Vector2(360,55));goal.fontSize=18;}
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }finally{PrefabUtility.UnloadPrefabContents(root);}
            var live=Object.FindObjectOfType<DraftlineCanvasUI>();live.EnvironmentSelector=live.MainMenu.transform.Find("MenuCard/EnvironmentSelector").GetComponent<Dropdown>();live.SceneryHint=live.transform.Find("SceneryHint").GetComponent<Text>();EditorUtility.SetDirty(live);
        }
        static void Position(RectTransform r,Vector2 p,Vector2 size){r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(p.x,-p.y);r.sizeDelta=size;}
    }
}

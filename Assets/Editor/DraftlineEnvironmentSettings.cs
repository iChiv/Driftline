using System;
using System.Collections.Generic;
using System.Linq;
using Draftline.Core;
using Draftline.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Draftline.EditorTools
{
    public static class DraftlineEnvironmentSettings
    {
        const string U="Assets/polyperfect/Low Poly Ultimate Pack/_T/Prefabs_T/";
        const string K="Assets/KayKit/Packs/KayKit - Dungeon Remastered Pack (for Unity)/Prefabs/";
        static readonly Color Card=new Color(.08f,.11f,.14f,.98f),Gold=new Color(.96f,.76f,.42f);
        static Sprite rounded;
        static Font font;
        [MenuItem("Driftline/Polish/Apply Environment And Settings")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Apply in Edit Mode.");
            rounded=AssetDatabase.LoadAllAssetsAtPath("Assets/UI/Config/RoundedSurface.asset").OfType<Sprite>().First();
            font=AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/SourceHanSansCN-Regular.otf");
            const string materialPath="Assets/Themes/Atmosphere/ThemeGradient.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null){material=new Material(Shader.Find("Driftline/ThemeGradient"));AssetDatabase.CreateAsset(material,materialPath);}
            foreach(var theme in DraftlineThemeApplier.Available)
            {
                var id=theme.ThemeId;theme.BackgroundMaterial=material;
                theme.BackgroundColor=id=="fortress"?new Color(.055f,.075f,.09f):id=="dungeon"?new Color(.025f,.025f,.05f):id=="hangar"?new Color(.018f,.027f,.045f):id=="forest"?new Color(.025f,.055f,.045f):new Color(.035f,.085f,.10f);
                theme.BackgroundTop=id=="fortress"?new Color(.18f,.22f,.24f):id=="dungeon"?new Color(.09f,.07f,.15f):id=="hangar"?new Color(.065f,.105f,.16f):id=="forest"?new Color(.11f,.18f,.15f):new Color(.18f,.27f,.30f);
                theme.BackgroundHaze=Color.Lerp(theme.BackgroundTop,new Color(.24f,.23f,.20f),id=="fortress"?.3f:.08f);
                EditorUtility.SetDirty(theme);Dress(theme);
            }
            BuildUi();
            var scene=Object.FindObjectOfType<BoardThemeScene>();
            if(scene.GetComponent<ThemeBackdrop>()==null)scene.gameObject.AddComponent<ThemeBackdrop>();
            scene.Preview();
            EditorSceneManager.MarkSceneDirty(scene.gameObject.scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveOpenScenes();
        }
        static void Dress(BoardTheme theme)
        {
            var path=AssetDatabase.GetAssetPath(theme.PresentationPrefab);var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var old=root.transform.Find("InfillDressing");if(old!=null)Object.DestroyImmediate(old.gameObject);
                var group=new GameObject("InfillDressing");group.transform.SetParent(root.transform,false);
                var inventory=group.AddComponent<ThemeInfillDressing>();
                var board=new BoardLayout();var ground=new List<GameObject>();var wall=new List<GameObject>();var rim=new List<GameObject>();
                string large=theme.ThemeId=="fortress"?U+"WW2_T/Ammo_Crate_Pak.prefab":theme.ThemeId=="dungeon"?K+"barrel_small_stack.prefab":theme.ThemeId=="hangar"?U+"Scifi_T/Scifi_Satelit_Stand_Small.prefab":theme.ThemeId=="forest"?U+"Nature_T/Stones_T/Stone_Small_Flat.prefab":U+"Fantasy_T/Crate_Box.prefab";
                string small=theme.ThemeId=="forest"?U+"Nature_T/Grass_T/Grass.prefab":theme.ThemeId=="hangar"?U+"Scifi_T/Scifi_Solar_Panel_Small.prefab":U+"Nature_T/Stones_T/Stone_Small_Flat.prefab";
                foreach(var p in new[]{new GridPosition(6,3),new GridPosition(12,3),new GridPosition(6,15),new GridPosition(12,15),new GridPosition(3,6),new GridPosition(15,6),new GridPosition(3,12),new GridPosition(15,12)})
                {
                    if(board.CellAt(p).Kind!=CellKind.FixedWall)throw new InvalidOperationException("Wall mount must remain on a fixed wall: "+p);
                    var prop=Model(group.transform,large,"Wall mounted supplies "+p,BoardCoordinates.ToWorld(p,1.08f),.64f,0);
                    LimitHeight(prop,.55f,1.08f);wall.Add(prop);
                }
                for(int side=0;side<4;side++)
                {
                    var rotation=Quaternion.Euler(0,side*90,0);
                    foreach(float along in new[]{-1.5f,1.5f})foreach(float distance in new[]{7.5f,8.5f})
                    {
                        var position=rotation*new Vector3(-distance,0,along);position.y=.515f;
                        var detail=Model(group.transform,small,"Walkable margin detail",position,.28f,side*90);
                        var bounds=DriftlineThemeAuthoring.Bounds(detail);
                        if(bounds.size.y>.10f){var s=detail.transform.localScale;s.y*=.10f/bounds.size.y;detail.transform.localScale=s;bounds=DriftlineThemeAuthoring.Bounds(detail);detail.transform.position+=Vector3.up*(.515f-bounds.min.y);}
                        ground.Add(detail);
                    }
                    foreach(float along in new[]{-2.6f,0,2.6f})
                    {
                        var pos=rotation*new Vector3(-10.2f,0,along);pos.y=.25f;
                        var prop=Model(group.transform,large,"Outer service strip",pos,1.0f,side*90);LimitHeight(prop,1.1f,.25f);rim.Add(prop);
                    }
                }
                foreach(var renderer in group.GetComponentsInChildren<Renderer>())renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                inventory.GroundDetails=ground.ToArray();inventory.WallProps=wall.ToArray();inventory.RimProps=rim.ToArray();
                var environment=root.GetComponent<ThemeEnvironment>();
                environment.StandardBackground=theme.BackgroundColor;environment.AlternateBackground=Color.Lerp(theme.BackgroundColor,new Color(.06f,.055f,.075f),.35f);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        static GameObject Model(Transform parent,string source,string name,Vector3 position,float size,float yaw)
        {
            var model=DriftlineThemeAuthoring.Place(parent,source,name,position,size,yaw,true);
            foreach(var c in model.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(c);
            foreach(var t in model.GetComponentsInChildren<Transform>(true))t.gameObject.layer=2;
            return model;
        }
        static void LimitHeight(GameObject go,float height,float bottom)
        {var b=DriftlineThemeAuthoring.Bounds(go);if(b.size.y<=height)return;var s=go.transform.localScale;s.y*=height/b.size.y;go.transform.localScale=s;b=DriftlineThemeAuthoring.Bounds(go);go.transform.position+=Vector3.up*(bottom-b.min.y);}
        static void BuildUi()
        {
            const string path="Assets/UI/Prefabs/UIRoot.prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var ui=root.GetComponent<DraftlineCanvasUI>();
                var settings=ui.Settings.transform.Find("SettingsCard");((RectTransform)settings).sizeDelta=new Vector2(680,800);
                var display=root.GetComponent<DisplaySettingsPanel>()??root.AddComponent<DisplaySettingsPanel>();
                display.SettingsPage=ui.Settings;
                Place(settings.Find("Title"),32,-28,350,54);Place(ui.GuideSettings.transform,492,-32,156,44);
                Place(settings.Find("VolumeLabel"),32,-116,180,36);Place(ui.Volume.transform,240,-110,408,44);
                foreach(var image in ui.Volume.GetComponentsInChildren<Image>(true))
                {image.sprite=rounded;image.type=Image.Type.Sliced;image.color=image.name=="Background"?new Color(.2f,.27f,.33f):Gold;}
                Place(ui.SimpleEffects.transform,32,-182,610,40);
                Text(settings,"ResolutionLabel","分辨率",32,-264,180,36);
                display.Resolution=Dropdown(settings,ui.Difficulty,"DisplayResolution",240,-252,408,new[]{"1280 × 720","1920 × 1080","2560 × 1440","3840 × 2160"});
                Text(settings,"DisplayModeLabel","显示模式",32,-342,180,36);
                display.Mode=Dropdown(settings,ui.Difficulty,"DisplayMode",240,-330,408,new[]{"窗口","无边框全屏"});
                display.Apply=Button(settings,"ApplyDisplay","应用",240,-410,408,50,true);
                var confirmation=Panel(settings,"DisplayConfirmation",32,-484,616,120);display.Confirmation=confirmation.gameObject;
                display.ConfirmationText=Text(confirmation,"Question","保留显示设置？",20,-14,570,32,20);
                display.Keep=Button(confirmation,"Keep","保留",20,-62,280,42,true);display.Revert=Button(confirmation,"Revert","恢复",316,-62,280,42);
                confirmation.gameObject.SetActive(false);
                Place(ui.CloseSettings.transform,32,-650,616,52);Place(ui.ReturnMenu.transform,32,-720,616,48);

                var room=ui.Lobby.transform.Find("Room");var rr=(RectTransform)room;rr.anchorMin=rr.anchorMax=rr.pivot=new Vector2(0,.5f);rr.anchoredPosition=new Vector2(48,0);rr.sizeDelta=new Vector2(700,960);
                Place(room.Find("Title"),32,-28,400,50);
                ui.ConnectionHelp=Button(room,"ConnectionHelpButton","连接说明",508,-34,160,42);
                var old=room.Find("ConnectionHelp");if(old!=null)old.gameObject.SetActive(false);
                Text(room,"AddressLabel","房主 IP",32,-96,450,28,18);Text(room,"PortLabel","端口",508,-96,160,28,18);
                Place(ui.Address.transform,32,-136,460,48);Place(ui.Port.transform,508,-136,160,48);
                ui.Address.text="";((Text)ui.Address.placeholder).text="房主局域网 IP";((Text)ui.Port.placeholder).text="7777";
                foreach(var input in new[]{ui.Address,ui.Port})
                {
                    input.image.color=Color.white;var colors=input.colors;colors.normalColor=new Color(.2f,.27f,.33f);colors.highlightedColor=new Color(.26f,.34f,.41f);colors.selectedColor=colors.highlightedColor;input.colors=colors;
                    input.textComponent.color=new Color(.9f,.92f,.9f);((Text)input.placeholder).color=new Color(.65f,.72f,.75f);
                }
                Place(ui.RoomSize.transform,32,-220,180,48);Place(ui.RoomColor.transform,232,-220,190,48);Place(ui.ComputerFill.transform,32,-294,636,40);
                Place(ui.Host.transform,32,-354,308,50);Place(ui.Join.transform,360,-354,308,50);
                var host=Panel(room,"HostAddressPanel",32,-432,636,118);ui.HostAddressPanel=host.gameObject;
                ui.HostAddressSelector=Dropdown(host,ui.Difficulty,"HostAddresses",12,-8,612,new[]{"局域网地址"});
                ui.HostEndpoint=Text(host,"Endpoint","",16,-68,432,38,22);ui.CopyHostAddress=Button(host,"Copy","复制",464,-66,160,40);
                Place(ui.RoomText.transform,32,-580,636,200);ui.RoomText.fontSize=20;
                var help=Panel(room,"ConnectionInstructions",32,-568,636,222);ui.ConnectionInstructions=help.gameObject;
                Text(help,"Steps","1. 两台电脑连接同一 Wi-Fi 或路由器。\n2. 房主创建房间，复制 IP 和端口。\n3. 另一台输入房主 IP 和相同端口，加入并准备。\n\n127.0.0.1 仅用于同机测试。\n连接失败时检查地址、端口和 Windows 网络权限。",18,-14,600,202,19);
                help.gameObject.SetActive(false);host.gameObject.SetActive(false);
                Place(ui.Ready.transform,32,-812,308,50);Place(ui.StartRoom.transform,360,-812,308,50);Place(ui.LeaveRoom.transform,32,-886,636,44);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        static Transform Panel(Transform parent,string name,float x,float y,float width,float height)
        {
            var t=parent.Find(name);if(t==null){var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);t=go.transform;}
            var image=t.GetComponent<Image>();image.sprite=rounded;image.type=Image.Type.Sliced;image.color=Card;Place(t,x,y,width,height);return t;
        }
        static Text Text(Transform parent,string name,string copy,float x,float y,float width,float height,int size=22)
        {
            var t=parent.Find(name);if(t==null){var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);t=go.transform;}
            var text=t.GetComponent<Text>();text.font=font;text.fontSize=size;text.text=copy;text.color=new Color(.88f,.90f,.88f);text.raycastTarget=false;text.verticalOverflow=VerticalWrapMode.Truncate;Place(t,x,y,width,height);return text;
        }
        static Button Button(Transform parent,string name,string copy,float x,float y,float width,float height,bool primary=false)
        {
            var t=Panel(parent,name,x,y,width,height);var image=t.GetComponent<Image>();image.color=primary?Gold:new Color(.2f,.27f,.33f);
            var button=t.GetComponent<Button>()??t.gameObject.AddComponent<Button>();button.targetGraphic=image;button.navigation=new Navigation{mode=Navigation.Mode.None};
            var label=Text(t,"Label",copy,0,0,width,height,20);label.alignment=TextAnchor.MiddleCenter;label.color=primary?new Color(.15f,.17f,.17f):new Color(.88f,.90f,.88f);
            return button;
        }
        static Dropdown Dropdown(Transform parent,Dropdown template,string name,float x,float y,float width,string[] options)
        {
            var t=parent.Find(name);if(t==null){t=Object.Instantiate(template.gameObject,parent).transform;t.name=name;}
            var dropdown=t.GetComponent<Dropdown>();dropdown.ClearOptions();dropdown.AddOptions(options.ToList());Place(t,x,y,width,48);return dropdown;
        }
        static void Place(Transform t,float x,float y,float width,float height){var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(width,height);}
    }
}

using System;
using System.Linq;
using Draftline.Core;
using Draftline.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Draftline.EditorTools
{
    /// <summary>Explicit authoring migration. Layout and art remain saved, editable assets.</summary>
    public static class DraftlineMinimalUI
    {
        const string PrefabPath="Assets/UI/Prefabs/UIRoot.prefab";
        const string SpritePath="Assets/UI/Config/RoundedSurface.asset";

        [MenuItem("Driftline/Polish/Apply Minimal Rounded UI")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Apply in Edit Mode.");
            var sprite=RoundedSurface();
            var root=PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var ui=root.GetComponent<DraftlineCanvasUI>();
                var menu=ui.MainMenu.transform.Find("MenuCard");
                CenterLeft(menu,64,460,610);
                Place(menu.Find("Brand"),36,-32,388,76,46);
                Hide(menu.Find("Subtitle"),menu.Find("Instructions"),ui.ThemeStory.transform,ui.ThemeDescription!=null?ui.ThemeDescription.transform:null,ui.SceneryHint.transform);
                Place(menu.Find("BoardThemeLabel"),36,-143,388,28,17);
                Place(ui.ThemeSelector.transform,36,-182,388,50);
                Place(ui.LocalGame.transform,36,-268,388,60);Label(ui.LocalGame,"本地游戏");
                Place(ui.Lan.transform,36,-344,388,60);Label(ui.Lan,"局域网");
                Place(ui.GuideMenu.transform,36,-420,388,52);Label(ui.GuideMenu,"游戏说明");
                Place(ui.OpenSettings.transform,36,-510,186,50);
                Place(ui.Quit.transform,238,-510,186,50);

                var setup=ui.Setup.transform.Find("Configuration");
                CenterLeft(setup,48,560,660);
                Place(setup.Find("Title"),32,-28,300,50,32);setup.Find("Title").GetComponent<Text>().text="本地游戏";
                Hide(setup.Find("Copy"));
                Place(ui.GuideSetup.transform,386,-34,140,42);Label(ui.GuideSetup,"游戏说明",18);
                for(int i=0;i<4;i++)
                {
                    var seat=ui.SeatControls[i].transform.parent;
                    Place(seat,32,-118-i*82,496,68);
                    Place(ui.SeatNames[i].transform,18,-18,180,36,23);
                    ui.SeatNames[i].text=new[]{"红方","蓝方","绿方","黄方"}[i];
                    Place(ui.SeatControls[i].transform,258,-10,220,48);
                    Hide(ui.SeatStories[i].transform);
                }
                Place(setup.Find("DifficultyLabel"),35,-478,210,42,21);
                Place(ui.Difficulty.transform,290,-470,238,48);
                Place(ui.StartMatch.transform,32,-564,316,56);
                Place(ui.SetupBack.transform,364,-564,164,56);

                var player=ui.Hud.transform.Find("PlayerPanel");
                Place(player,24,-24,280,1032);
                var turn=player.Find("TurnCard");Place(turn,0,0,280,194);
                Hide(turn.Find("Brand"),ui.HintText.transform,ui.TurnAccent.transform);
                Place(ui.TurnText.transform,20,-16,100,36,26);
                Place(ui.PhaseText.transform,124,-23,136,28,17);ui.PhaseText.alignment=TextAnchor.MiddleRight;
                Place(ui.DiceText.transform,20,-66,240,48,28);
                Place(ui.Roll.transform,20,-66,240,48);
                Place(ui.GuideHud.transform,20,-134,240,40);Label(ui.GuideHud,"游戏说明",17);
                var inventory=ui.InventoryPanel.transform;Place(inventory,0,-214,280,166);
                Hide(inventory.Find("InventoryTitle"));
                Place(ui.ItemButtons[0].transform,20,-18,114,42);Place(ui.ItemButtons[1].transform,146,-18,114,42);
                Place(ui.InventoryHint.transform,20,-80,240,64,16);
                var roster=player.Find("PlayerRoster");((RectTransform)roster).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,280);
                for(int i=0;i<4;i++)
                {
                    var row=ui.PlayerAccents[i].transform.parent;
                    Place(ui.PlayerAccents[i].transform,8,-18,3,28);
                    Place(ui.PlayerTexts[i].transform,20,-6,226,52,17);
                    ui.PlayerTexts[i].text=new[]{"红方","蓝方","绿方","黄方"}[i]+"\n生命 5/5   场上 1/8";
                }
                BottomLeft(ui.CameraToggle.transform,0,8,132,44);BottomLeft(ui.HudSettings.transform,148,8,132,44);
                Hide(ui.Hud.transform.Find("Objective"),ui.LogText.transform.parent);
                var action=(RectTransform)ui.ActionPanel.transform;
                action.anchorMin=action.anchorMax=action.pivot=new Vector2(1,1);
                action.anchoredPosition=new Vector2(-24,-24);action.sizeDelta=new Vector2(280,220);
                Hide(action.Find("HeaderAccent"));
                Place(ui.ActionTitle.transform,20,-18,240,28,18);
                Place(ui.ActionScroll.transform,20,-62,240,120);
                ui.ActionText.fontSize=17;ui.ActionBodyHeightRange=new Vector2(32,180);ui.ActionBottomPadding=20;

                var guide=ui.GuidePanel.transform.Find("GuideCard");
                Hide(guide.Find("Subtitle"));
                var gr=(RectTransform)guide;gr.sizeDelta=new Vector2(960,640);
                Place(ui.GuideTitle.transform,40,-32,850,56,32);
                Place(ui.GuideBody.transform,40,-132,480,360,24);
                ui.GuideTitle.text="游戏说明";
                ui.GuideBody.text="让五枚棋子连成线，或者成为最后存活的一方即可。";
                ui.GuideExample.text="●  ●  ●  ●  ●\n\n横、竖、斜均可";
                ui.GuideProgress.text="1 / 5";
                var example=ui.GuideExample.transform.parent;Place(example,560,-132,360,360);
                Hide(example.Find("Caption"));Place(ui.GuideExample.transform,24,-42,312,290,23);
                Place(ui.GuideProgress.transform,40,-540,150,50,20);
                Place(ui.GuidePrevious.transform,228,-534,198,54);Label(ui.GuidePrevious,"上一页");
                Place(ui.GuideNext.transform,442,-534,198,54);Label(ui.GuideNext,"下一页");
                Place(ui.GuideClose.transform,722,-534,198,54);Label(ui.GuideClose,"关闭");
                Label(ui.GuideSettings,"游戏说明",18);((RectTransform)ui.GuideSettings.transform).sizeDelta=new Vector2(145,45);
                Label(ui.ReturnMenu,"返回菜单");Label(ui.CloseSettings,"继续");
                var effectsLabel=ui.Settings.GetComponentsInChildren<Text>(true).FirstOrDefault(t=>t.text=="简化演出与减弱特效");
                if(effectsLabel!=null)effectsLabel.text="简化特效";
                Hide(ui.Lobby.transform.Find("Room/ConnectionHelp"));Label(ui.LeaveRoom,"返回");Label(ui.StartRoom,"开始对局");
                if(ui.Address.placeholder is Text address)address.text="房主 IP";
                if(ui.Port.placeholder is Text port)port.text="端口";

                foreach(var image in root.GetComponentsInChildren<Image>(true))
                {
                    if(image.color.a<.01f || image.name=="ModalBlocker" || image.name=="Viewport" || image.name.Contains("Accent") || image.name=="PlayerPanel" || image.name=="Arrow")continue;
                    if(image.GetComponentInParent<Slider>()!=null)continue;
                    image.sprite=sprite;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=1;
                }
                foreach(var button in root.GetComponentsInChildren<Button>(true))
                {var colors=button.colors;colors.fadeDuration=.18f;button.colors=colors;}
                foreach(var dropdown in root.GetComponentsInChildren<Dropdown>(true))
                {
                    var toggle=dropdown.template.GetComponentInChildren<Toggle>(true);
                    var background=toggle.targetGraphic as Image;
                    if(background!=null){background.sprite=null;var colors=toggle.colors;colors.normalColor=new Color(.10f,.13f,.17f,1);colors.highlightedColor=new Color(.24f,.32f,.40f);colors.selectedColor=colors.highlightedColor;toggle.colors=colors;}
                    var listBackground=dropdown.template.GetComponent<Image>();
                    if(listBackground!=null)listBackground.color=new Color(.10f,.13f,.17f,1);
                }
                foreach(var text in root.GetComponentsInChildren<Text>(true))text.raycastTarget=false;
                ui.MainMenu.SetActive(true);ui.Setup.SetActive(false);ui.Hud.SetActive(false);
                ui.Lobby.SetActive(false);ui.Settings.SetActive(false);ui.GuidePanel.SetActive(false);ui.Result.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            var words=AssetDatabase.LoadAssetAtPath<MatchTextContent>("Assets/UI/Config/MatchTextContent.asset");
            words.FactionNames=new[]{"红方","蓝方","绿方","黄方"};words.FactionStories=new[]{"","","",""};
            words.FiveVictory="五子连线";words.SurvivalVictory="最后存活";EditorUtility.SetDirty(words);
            var rules=AssetDatabase.LoadAssetAtPath<RuleGuideContent>("Assets/UI/Config/RuleGuideContent.asset");
            var defaults=ScriptableObject.CreateInstance<RuleGuideContent>();rules.Steps=defaults.Steps;UnityEngine.Object.DestroyImmediate(defaults);EditorUtility.SetDirty(rules);
            RemoveSceneryText();
            var director=UnityEngine.Object.FindObjectOfType<DraftlineCameraDirector>();EditorUtility.SetDirty(director);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
            AssetDatabase.SaveAssets();UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        }

        static Sprite RoundedSurface()
        {
            var existing=AssetDatabase.LoadAllAssetsAtPath(SpritePath).OfType<Sprite>().FirstOrDefault();if(existing!=null)return existing;
            // Nine-sliced UI geometry: antialiased corners with a fixed logical radius.
            const int size=64;const float radius=14;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="RoundedSurface",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float dx=Mathf.Max(Mathf.Abs(x+.5f-size*.5f)-(size*.5f-radius),0);
                float dy=Mathf.Max(Mathf.Abs(y+.5f-size*.5f)-(size*.5f-radius),0);
                pixels[y*size+x]=new Color(1,1,1,Mathf.Clamp01(radius-Mathf.Sqrt(dx*dx+dy*dy)+.5f));
            }
            texture.SetPixels(pixels);texture.Apply();AssetDatabase.CreateAsset(texture,SpritePath);
            var sprite=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(18,18,18,18));
            sprite.name="RoundedSurface";AssetDatabase.AddObjectToAsset(sprite,texture);return sprite;
        }

        static void RemoveSceneryText()
        {
            foreach(var theme in DraftlineThemeApplier.Available)
            {
                var path=AssetDatabase.GetAssetPath(theme.PresentationPrefab);var root=PrefabUtility.LoadPrefabContents(path);
                try{foreach(var t in root.GetComponentsInChildren<TextMesh>(true))if(t.text.Length>1 && !t.name.StartsWith("Portal"))t.gameObject.SetActive(false);PrefabUtility.SaveAsPrefabAsset(root,path);}
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
        }
        static void Label(Button button,string text,int size=21){var label=button.GetComponentInChildren<Text>(true);label.text=text;label.fontSize=size;}
        static void Hide(params Transform[] nodes){foreach(var t in nodes)if(t!=null)t.gameObject.SetActive(false);}
        static void Place(Transform node,float x,float y,float width,float height,int font=0)
        {
            var r=(RectTransform)node;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(width,height);
            if(font>0)node.GetComponent<Text>().fontSize=font;
        }
        static void CenterLeft(Transform node,float x,float width,float height)
        {var r=(RectTransform)node;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,.5f);r.anchoredPosition=new Vector2(x,0);r.sizeDelta=new Vector2(width,height);}
        static void BottomLeft(Transform node,float x,float y,float width,float height)
        {Place(node,x,y,width,height);var r=(RectTransform)node;r.anchorMin=r.anchorMax=r.pivot=Vector2.zero;}
    }
}

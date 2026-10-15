using System;
using Draftline.Core;
using Draftline.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Object = UnityEngine.Object;
using Image = UnityEngine.UI.Image;
using Button = UnityEngine.UI.Button;
using Slider = UnityEngine.UI.Slider;
using Toggle = UnityEngine.UI.Toggle;

namespace Draftline.EditorTools
{
    /// <summary>One-time authoring tool. The saved UI is the source of truth, never rebuilt on play.</summary>
    public static class DriftlineExperienceSetup
    {
        private static Font font;
        private static readonly Color Ink = new Color(.055f,.075f,.10f,.98f);
        private static readonly Color Panel = new Color(.10f,.13f,.17f,.97f);
        private static readonly Color Gold = new Color(.96f,.76f,.42f);
        private static readonly Color White = new Color(.91f,.92f,.88f);
        private static readonly DefaultControls.Resources Widgets = new DefaultControls.Resources();

        [MenuItem("Driftline/Install Editable Experience")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Install in Edit Mode.");
            var game = Object.FindObjectOfType<DraftlineGameController>();
            if (game == null) throw new InvalidOperationException("Open SampleScene first.");
            if (Object.FindObjectOfType<DraftlineCanvasUI>() != null)
            { Debug.Log("Editable Canvas already exists; edit it directly. Installation will not overwrite it."); return; }
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/SourceHanSansCN-Regular.otf") ?? Font.CreateDynamicFontFromOSFont("Microsoft YaHei",24);
            EnsureFolder("Assets/UI/Prefabs"); EnsureFolder("Assets/UI/Config"); EnsureFolder("Assets/Prefabs/BoardInteraction");
            var old = Object.FindObjectOfType<UIDocument>();
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var session = Ensure<MatchSession>(game.gameObject);
            Ensure<PhysicalDiceRoller>(game.gameObject);
            Ensure<DraftlineCameraDirector>(game.gameObject);
            Ensure<DraftlineWarnings>(game.gameObject);
            var audio = Ensure<DraftlineAudio>(game.gameObject);
            Ensure<AudioSource>(game.gameObject);
            var content = AssetDatabase.LoadAssetAtPath<MatchTextContent>("Assets/UI/Config/MatchTextContent.asset");
            if (content == null) { content = ScriptableObject.CreateInstance<MatchTextContent>(); AssetDatabase.CreateAsset(content,"Assets/UI/Config/MatchTextContent.asset"); }
            game.Content = content;
            MatchLabels.Configure(content);
            var music = new GameObject("Background Music", typeof(AudioSource)).GetComponent<AudioSource>();
            music.transform.SetParent(game.transform,false);
            music.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Feel/FeelDemos/Wheel/Sounds/FeelWheelMusic.wav");
            music.loop = true; music.playOnAwake = false; music.volume = .1f;
            audio.BackgroundMusic = music;
            var room = Ensure<DraftlineLanRoom>(game.gameObject);
            var networkRoot = new GameObject("NetworkRoot");
            room.Transport = networkRoot.AddComponent<UnityTransport>();
            room.Network = networkRoot.AddComponent<NetworkManager>();
            room.Network.NetworkConfig = new NetworkConfig { NetworkTransport = room.Transport, EnableSceneManagement = false, ConnectionApproval = true, ForceSamePrefabs = false };
            var board = Ensure<BoardInteractionController>(game.gameObject);
            board.Controller = game;
            board.VisualRoot = new GameObject("BoardInteractionRoot").transform;
            board.Style = CreateBoardStyle();
            foreach (var interaction in game.GetComponents<DraftlineTabletopInteraction>()) interaction.enabled = false;
            foreach (var ambience in game.GetComponents<DraftlineBoardAmbience>()) ambience.enabled = false;
            var ui = CreateUI();
            game.ConfigureEditor(ui, session, board);
            var events = new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            PrefabUtility.SaveAsPrefabAssetAndConnect(ui.gameObject,"Assets/UI/Prefabs/UIRoot.prefab",InteractionMode.AutomatedAction);
            EditorUtility.SetDirty(game); EditorUtility.SetDirty(room); EditorUtility.SetDirty(board);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene);
            Selection.activeGameObject = ui.gameObject;
            Debug.Log("Editable UI, session, LAN and board interaction saved. Edit UIRoot prefab or scene overrides directly.");
        }

        private static DraftlineCanvasUI CreateUI()
        {
            var root = new GameObject("UIRoot",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
            var ui = root.AddComponent<DraftlineCanvasUI>();
            ui.MainMenu = Page(root.transform,"MainMenuPanel");
            var main = Card(ui.MainMenu.transform,"MenuCard",new Vector2(40,80),new Vector2(560,900));
            Label(main,"Brand","DRIFTLINE",new Vector2(32,28),new Vector2(490,70),48,Gold);
            Label(main,"Subtitle","界钥棋庭  /  TABLETOP STRATEGY",new Vector2(35,106),new Vector2(480,32),17,White);
            Label(main,"Story","古老的界钥再次苏醒。四支探索队操纵符文傀儡，在不断变化的机关棋庭中争夺它。\n\n让五枚棋子连成共鸣线，或成为最后存活的一方。",new Vector2(35,175),new Vector2(485,170),22,White);
            ui.LocalGame = Action(main,"LocalGame","本地游戏   ·   玩家与电脑自由组合",new Vector2(35,390),new Vector2(490,65),true);
            ui.Lan = Action(main,"LAN","局域网   ·   与朋友对局",new Vector2(35,475),new Vector2(490,65));
            ui.OpenSettings = Action(main,"Settings","设置",new Vector2(35,600),new Vector2(235,50));
            ui.Quit = Action(main,"Quit","退出",new Vector2(290,600),new Vector2(235,50));
            Label(main,"Instructions","点击棋子查看路线，点击终点滑行。\n急停直接点路径上的格子。",new Vector2(35,720),new Vector2(490,75),18,new Color(.6f,.7f,.73f));

            ui.Setup = Page(root.transform,"MatchSetupPanel");
            var setup = Card(ui.Setup.transform,"Configuration",new Vector2(40,80),new Vector2(560,900));
            Label(setup,"Title","选择探索队",new Vector2(32,28),new Vector2(490,55),34,Gold);
            Label(setup,"Copy","2–4 方自由组合，至少保留一位玩家。\n其余席位可选玩家、电脑或关闭。",new Vector2(35,110),new Vector2(490,90),20,White);
            ui.SeatControls = new Dropdown[4];
            ui.SeatNames = new Text[4]; ui.SeatStories = new Text[4];
            for(var i=0;i<4;i++)
            {
                var row = Card(setup,"Seat"+(PlayerColor)i,new Vector2(30,240+i*100),new Vector2(500,82));
                ui.SeatNames[i] = Label(row,"Name",MatchLabels.Player((PlayerColor)i),new Vector2(18,8),new Vector2(245,32),22,MatchLabels.Color((PlayerColor)i));
                ui.SeatStories[i] = Label(row,"Story",MatchLabels.Story((PlayerColor)i),new Vector2(18,42),new Vector2(245,36),14,White);
                ui.SeatControls[i] = Dropdown(row,"Control",new[]{"关闭","玩家","电脑"},new Vector2(280,18),new Vector2(195,48));
            }
            Label(setup,"DifficultyLabel","电脑难度",new Vector2(35,670),new Vector2(240,40),22,White);
            ui.Difficulty = Dropdown(setup,"Difficulty",new[]{"简单","普通"},new Vector2(310,660),new Vector2(210,48));
            ui.Difficulty.value = 1;
            ui.StartMatch = Action(setup,"Start","开始对局",new Vector2(35,755),new Vector2(310,60),true);
            ui.SetupBack = Action(setup,"Back","返回",new Vector2(370,755),new Vector2(150,60));

            ui.Lobby = Page(root.transform,"LanLobbyPanel");
            var lobby = Card(ui.Lobby.transform,"Room",new Vector2(40,60),new Vector2(660,950));
            Label(lobby,"Title","局域网棋局",new Vector2(32,25),new Vector2(590,50),34,Gold);
            Label(lobby,"ConnectionHelp","房主创建房间后，将显示的局域网 IP 发给朋友。",new Vector2(35,90),new Vector2(585,50),19,White);
            ui.Address = Input(lobby,"Address","127.0.0.1",new Vector2(35,155),new Vector2(400,52));
            ui.Port = Input(lobby,"Port","7777",new Vector2(455,155),new Vector2(165,52));
            ui.RoomSize = Dropdown(lobby,"RoomSize",new[]{"2 方","3 方","4 方"},new Vector2(35,235),new Vector2(175,48));
            ui.RoomColor = Dropdown(lobby,"RoomColor",new[]{"红方","蓝方","绿方","黄方"},new Vector2(230,235),new Vector2(175,48));
            ui.ComputerFill = Check(lobby,"ComputerFill","空位由电脑补齐",new Vector2(35,315),new Vector2(540,40)); ui.ComputerFill.isOn = true;
            ui.Host = Action(lobby,"Host","创建房间",new Vector2(35,385),new Vector2(270,55),true);
            ui.Join = Action(lobby,"Join","加入房间",new Vector2(330,385),new Vector2(290,55));
            ui.RoomText = Label(lobby,"RoomStatus","尚未连接",new Vector2(35,470),new Vector2(585,240),21,White);
            ui.Ready = Action(lobby,"Ready","准备",new Vector2(35,730),new Vector2(270,55));
            ui.StartRoom = Action(lobby,"StartRoom","房主开始",new Vector2(330,730),new Vector2(290,55),true);
            ui.LeaveRoom = Action(lobby,"Leave","离开 / 返回",new Vector2(35,830),new Vector2(585,50));

            ui.Hud = Page(root.transform,"MatchHUD");
            var side = Card(ui.Hud.transform,"PlayerPanel",new Vector2(20,20),new Vector2(350,1040));
            Label(side,"Brand","DRIFTLINE",new Vector2(24,18),new Vector2(310,44),29,Gold);
            ui.TurnText = Label(side,"Turn","当前玩家",new Vector2(24,90),new Vector2(310,58),29,White);
            ui.PhaseText = Label(side,"Phase","点选棋子",new Vector2(24,152),new Vector2(310,36),20,White);
            ui.DiceText = Label(side,"Dice","骰子 —  · 剩余滑行 0",new Vector2(24,212),new Vector2(310,44),21,Gold);
            ui.HintText = Label(side,"Hint","点击棋子查看路径",new Vector2(24,275),new Vector2(310,105),20,White);
            ui.Roll = Action(side,"Roll","投掷骰子",new Vector2(24,392),new Vector2(302,48),true);
            Label(side,"InventoryTitle","本回合道具",new Vector2(24,465),new Vector2(302,35),19,Gold);
            ui.ItemButtons = new Button[2]; ui.ItemTexts = new Text[2];
            for(var i=0;i<2;i++) { ui.ItemButtons[i]=Action(side,"Item"+i,"道具",new Vector2(24+i*156,510),new Vector2(145,46)); ui.ItemTexts[i]=ui.ItemButtons[i].GetComponentInChildren<Text>(); var hover=ui.ItemButtons[i].gameObject.AddComponent<InventoryHover>(); hover.View=ui; hover.Slot=i; }
            ui.InventoryHint=Label(side,"InventoryHint","先点选棋子",new Vector2(24,565),new Vector2(302,80),16,White);
            ui.PlayerTexts=new Text[4];
            for(var i=0;i<4;i++) ui.PlayerTexts[i]=Label(side,"Player"+(PlayerColor)i,MatchLabels.Player((PlayerColor)i)+"\n场上 1  储备 9  消耗 0",new Vector2(24,640+i*69),new Vector2(302,61),17,MatchLabels.Color((PlayerColor)i));
            ui.CameraToggle=Action(side,"Camera","俯视棋盘",new Vector2(24,947),new Vector2(145,48));
            ui.HudSettings=Action(side,"Settings","设置",new Vector2(181,947),new Vector2(145,48));
            var top = Card(ui.Hud.transform,"Objective",new Vector2(390,20),new Vector2(650,55));
            Label(top,"Goal","五子连线，或成为最后存活的一方",new Vector2(20,10),new Vector2(610,40),21,Gold);
            var log = Card(ui.Hud.transform,"EventLog",new Vector2(1540,820),new Vector2(360,240));
            ui.LogText=Label(log,"Entries","对局记录",new Vector2(18,12),new Vector2(325,215),16,White);
            ui.Reconnect=Action(root.transform,"Reconnect","重新连接",new Vector2(1110,22),new Vector2(185,50));
            ui.TakeOver=Action(root.transform,"TakeOver","电脑接管掉线玩家",new Vector2(1305,22),new Vector2(300,50));

            ui.Settings=Page(root.transform,"PauseAndSettingsPanel");
            Card(ui.Settings.transform,"ModalBlocker",Vector2.zero,new Vector2(1920,1080),new Color(0,0,0,.7f));
            var settings=Card(ui.Settings.transform,"SettingsCard",new Vector2(650,240),new Vector2(620,600));
            Label(settings,"Title","设置",new Vector2(35,30),new Vector2(550,55),34,Gold);
            Label(settings,"VolumeLabel","声音音量",new Vector2(35,140),new Vector2(550,40),22,White);
            var sliderGo=DefaultControls.CreateSlider(Widgets); sliderGo.name="Volume"; sliderGo.transform.SetParent(settings,false); Position(sliderGo.GetComponent<RectTransform>(),new Vector2(35,205),new Vector2(550,35));
            ui.Volume=sliderGo.GetComponent<Slider>(); ui.Volume.value=.7f;
            ui.SimpleEffects=Check(settings,"SimpleEffects","简化演出与减弱特效",new Vector2(35,290),new Vector2(550,45));
            ui.CloseSettings=Action(settings,"Close","返回棋局",new Vector2(35,395),new Vector2(550,55),true);
            ui.ReturnMenu=Action(settings,"Menu","结束本局并返回菜单",new Vector2(35,480),new Vector2(550,55));

            ui.Result=Page(root.transform,"ResultPanel");
            Card(ui.Result.transform,"ModalBlocker",Vector2.zero,new Vector2(1920,1080),new Color(0,0,0,.6f));
            var result=Card(ui.Result.transform,"Victory",new Vector2(630,280),new Vector2(700,520));
            Label(result,"Title","界钥归属已定",new Vector2(35,30),new Vector2(630,65),36,Gold);
            ui.ResultText=Label(result,"Result","红方获胜\n五枚傀儡形成共鸣。",new Vector2(35,135),new Vector2(630,170),25,White);
            ui.Restart=Action(result,"Restart","再来一局",new Vector2(35,360),new Vector2(300,65),true);
            ui.ResultMenu=Action(result,"Menu","返回菜单",new Vector2(365,360),new Vector2(300,65));
            var message=Card(root.transform,"StatusMessage",new Vector2(720,980),new Vector2(790,65),new Color(.18f,.09f,.08f,.8f));
            ui.ErrorText=Label(message,"Error","",new Vector2(15,8),new Vector2(760,55),21,new Color(1f,.67f,.5f));
            ui.Setup.SetActive(false);ui.Lobby.SetActive(false);ui.Hud.SetActive(false);ui.Settings.SetActive(false);ui.Result.SetActive(false);
            ui.Reconnect.gameObject.SetActive(false);ui.TakeOver.gameObject.SetActive(false);
            AddGuidance(ui);
            return ui;
        }

        [MenuItem("Driftline/Add Action Explanation And Rule Guide")]
        public static void InstallGuidance()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Author in Edit Mode.");
            const string path="Assets/UI/Prefabs/UIRoot.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try { AddGuidance(root.GetComponent<DraftlineCanvasUI>()); PrefabUtility.SaveAsPrefabAsset(root,path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        private static void AddGuidance(DraftlineCanvasUI ui)
        {
            if(ui.GuidePanel!=null)return;
            font=AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/SourceHanSansCN-Regular.otf");
            EnsureFolder("Assets/UI/Config");
            ui.GuideContent=AssetDatabase.LoadAssetAtPath<RuleGuideContent>("Assets/UI/Config/RuleGuideContent.asset");
            if(ui.GuideContent==null){ui.GuideContent=ScriptableObject.CreateInstance<RuleGuideContent>();AssetDatabase.CreateAsset(ui.GuideContent,"Assets/UI/Config/RuleGuideContent.asset");}
            var main=ui.MainMenu.transform.Find("MenuCard");
            ui.GuideMenu=Action(main,"RuleGuide","规则引导   ·   随时查看",new Vector2(35,675),new Vector2(490,50));
            main.Find("Instructions").GetComponent<RectTransform>().anchoredPosition=new Vector2(35,-750);
            ui.GuideSetup=Action(ui.Setup.transform.Find("Configuration"),"RuleGuide","规则",new Vector2(420,32),new Vector2(100,42));
            ui.GuideHud=Action(ui.Hud.transform.Find("PlayerPanel"),"RuleGuide","规则",new Vector2(240,22),new Vector2(85,36));
            ui.GuideHud.GetComponentInChildren<Text>().fontSize=16;
            ui.GuideSettings=Action(ui.Settings.transform.Find("SettingsCard"),"RuleGuide","规则",new Vector2(440,35),new Vector2(135,45));
            var action=Card(ui.Hud.transform,"ActionExplanation",new Vector2(1540,90),new Vector2(360,460));
            ui.ActionTitle=Label(action,"Title","行动预览",new Vector2(18,16),new Vector2(325,38),23,Gold);
            ui.ActionText=Label(action,"Details","悬停棋盘目标，查看行动原因。",new Vector2(18,70),new Vector2(325,350),17,White);
            ui.GuidePanel=Page(ui.transform,"RuleGuidePanel");
            Card(ui.GuidePanel.transform,"ModalBlocker",Vector2.zero,new Vector2(1920,1080),new Color(0,0,0,.78f));
            var book=Card(ui.GuidePanel.transform,"GuideCard",new Vector2(390,110),new Vector2(1140,850));
            ui.GuideTitle=Label(book,"Title",ui.GuideContent.Steps[0].Title,new Vector2(36,30),new Vector2(900,58),34,Gold);
            Label(book,"Subtitle","规则引导 / 可随时关闭，不提交任何棋局动作",new Vector2(36,108),new Vector2(1060,38),19,White);
            ui.GuideBody=Label(book,"Body",ui.GuideContent.Steps[0].Body,new Vector2(36,200),new Vector2(630,450),22,White);
            var example=Card(book,"ExampleCard",new Vector2(700,180),new Vector2(400,490),new Color(.16f,.21f,.25f));
            Label(example,"Caption","棋盘示例",new Vector2(24,20),new Vector2(350,40),22,Gold);
            ui.GuideExample=Label(example,"Diagram",ui.GuideContent.Steps[0].Example,new Vector2(24,98),new Vector2(350,360),22,White);
            ui.GuideProgress=Label(book,"Progress","1 / "+ui.GuideContent.Steps.Length,new Vector2(36,708),new Vector2(180,38),21,Gold);
            ui.GuidePrevious=Action(book,"Previous","上一条",new Vector2(230,735),new Vector2(220,60));
            ui.GuideNext=Action(book,"Next","下一条",new Vector2(480,735),new Vector2(220,60),true);
            ui.GuideClose=Action(book,"Close","关闭引导",new Vector2(855,735),new Vector2(245,60));
            ui.GuidePanel.SetActive(false);
        }

        private static GameObject Page(Transform parent,string name)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
            var r=go.GetComponent<RectTransform>();r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;
            return go;
        }
        private static RectTransform Card(Transform parent,string name,Vector2 position,Vector2 size,Color? color=null)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            var r=go.GetComponent<RectTransform>();Position(r,position,size);go.GetComponent<Image>().color=color??Panel;return r;
        }
        private static void Position(RectTransform r,Vector2 position,Vector2 size)
        {r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(position.x,-position.y);r.sizeDelta=size;}
        private static Text Label(Transform parent,string name,string copy,Vector2 position,Vector2 size,int fontSize,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);Position(go.GetComponent<RectTransform>(),position,size);
            var text=go.GetComponent<Text>();text.font=font;text.fontSize=fontSize;text.text=copy;text.color=color;text.raycastTarget=false;
            text.alignment=TextAnchor.UpperLeft;text.verticalOverflow=VerticalWrapMode.Overflow;return text;
        }
        private static Button Action(Transform parent,string name,string copy,Vector2 position,Vector2 size,bool primary=false)
        {
            var go=DefaultControls.CreateButton(Widgets);go.name=name;go.transform.SetParent(parent,false);Position(go.GetComponent<RectTransform>(),position,size);
            go.GetComponent<Image>().color=primary?Gold:new Color(.2f,.27f,.33f);
            var text=go.GetComponentInChildren<Text>();text.font=font;text.fontSize=21;text.color=primary?Ink:White;text.text=copy;
            var button=go.GetComponent<Button>();button.navigation=new Navigation{mode=Navigation.Mode.None};return button;
        }
        private static Dropdown Dropdown(Transform parent,string name,string[] options,Vector2 position,Vector2 size)
        {
            var go=DefaultControls.CreateDropdown(Widgets);go.name=name;go.transform.SetParent(parent,false);Position(go.GetComponent<RectTransform>(),position,size);
            foreach(var t in go.GetComponentsInChildren<Text>(true)){t.font=font;t.fontSize=19;t.color=Ink;}
            var dropdown=go.GetComponent<Dropdown>();dropdown.ClearOptions();dropdown.AddOptions(new System.Collections.Generic.List<string>(options));
            StyleDropdown(dropdown);return dropdown;
        }

        // Author both the collapsed field and its saved popup template. No runtime layout mutation.
        public static void StyleDropdown(Dropdown dropdown)
        {
            dropdown.GetComponent<Image>().color = Color.white;
            var colors = dropdown.colors;
            colors.normalColor = new Color(.2f,.27f,.33f);
            colors.highlightedColor = new Color(.29f,.38f,.46f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.13f,.20f,.26f);
            colors.disabledColor = new Color(.13f,.17f,.21f,.65f);
            dropdown.colors = colors;
            dropdown.captionText.color = White;
            dropdown.captionText.alignment = TextAnchor.MiddleLeft;
            dropdown.captionText.verticalOverflow = VerticalWrapMode.Overflow;
            var arrow = dropdown.transform.Find("Arrow").GetComponent<Image>();
            arrow.color = Gold;
            // DefaultControls was created without sprites; use a saved text glyph for the arrow.
            arrow.enabled = false;
            var glyph = arrow.transform.Find("Glyph");
            if (glyph == null)
            {
                glyph = new GameObject("Glyph",typeof(RectTransform),typeof(Text)).transform;
                glyph.SetParent(arrow.transform,false);
                var rect = glyph.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            var arrowText = glyph.GetComponent<Text>();
            arrowText.font = dropdown.captionText.font; arrowText.fontSize = 18;
            arrowText.text = "▼"; arrowText.color = Gold; arrowText.alignment = TextAnchor.MiddleCenter;
            arrowText.verticalOverflow = VerticalWrapMode.Overflow; arrowText.raycastTarget = false;
            var template = dropdown.template;
            template.GetComponent<Image>().color = Panel;
            template.Find("Viewport").GetComponent<Image>().color = Panel;
            var toggle = template.GetComponentInChildren<Toggle>(true);
            var row = toggle.GetComponent<RectTransform>();
            row.sizeDelta = new Vector2(row.sizeDelta.x,44);
            var content = row.parent.GetComponent<RectTransform>();
            content.sizeDelta = new Vector2(content.sizeDelta.x,44);
            template.sizeDelta = new Vector2(template.sizeDelta.x,160);
            toggle.targetGraphic.color = Color.white;
            var rowColors = toggle.colors;
            rowColors.normalColor = Panel;
            rowColors.highlightedColor = new Color(.25f,.34f,.42f);
            rowColors.selectedColor = rowColors.highlightedColor;
            rowColors.pressedColor = new Color(.32f,.41f,.48f);
            toggle.colors = rowColors;
            toggle.graphic.color = Gold;
            dropdown.itemText.color = White;
            dropdown.itemText.alignment = TextAnchor.MiddleLeft;
            dropdown.itemText.verticalOverflow = VerticalWrapMode.Overflow;
            var textRect = dropdown.itemText.rectTransform;
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(32,4); textRect.offsetMax = new Vector2(-12,-4);
            foreach (var scrollbar in template.GetComponentsInChildren<Scrollbar>(true))
            { scrollbar.GetComponent<Image>().color = Ink; scrollbar.targetGraphic.color = new Color(.45f,.54f,.60f); }
        }
        private static InputField Input(Transform parent,string name,string value,Vector2 position,Vector2 size)
        {
            var go=DefaultControls.CreateInputField(Widgets);go.name=name;go.transform.SetParent(parent,false);Position(go.GetComponent<RectTransform>(),position,size);
            foreach(var t in go.GetComponentsInChildren<Text>(true)){t.font=font;t.fontSize=21;t.color=Ink;}
            var field=go.GetComponent<InputField>();field.text=value;return field;
        }
        private static Toggle Check(Transform parent,string name,string copy,Vector2 position,Vector2 size)
        {
            var go=DefaultControls.CreateToggle(Widgets);go.name=name;go.transform.SetParent(parent,false);Position(go.GetComponent<RectTransform>(),position,size);
            var text=go.GetComponentInChildren<Text>();text.font=font;text.fontSize=21;text.text=copy;text.color=White;
            var toggle=go.GetComponent<Toggle>();StyleToggle(toggle);return toggle;
        }
        public static void StyleToggle(Toggle toggle)
        {
            var label = toggle.GetComponentInChildren<Text>(true);
            label.color = White; label.alignment = TextAnchor.MiddleLeft;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(32,0); label.rectTransform.offsetMax = new Vector2(-8,0);
            toggle.targetGraphic.color = new Color(.2f,.27f,.33f);
            toggle.graphic.color = Gold;
        }
        private static T Ensure<T>(GameObject go) where T:Component
        { return go.TryGetComponent<T>(out var component) ? component : go.AddComponent<T>(); }
        private static void EnsureFolder(string path)
        {
            var parts=path.Split('/');var current=parts[0];for(var i=1;i<parts.Length;i++){var next=current+"/"+parts[i];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,parts[i]);current=next;}
        }
        private static BoardInteractionStyle CreateBoardStyle()
        {
            const string path="Assets/UI/Config/BoardInteractionStyle.asset";
            var existing=AssetDatabase.LoadAssetAtPath<BoardInteractionStyle>(path);if(existing!=null)return existing;
            var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.color=new Color(.26f,.9f,.65f);
            AssetDatabase.CreateAsset(material,"Assets/Prefabs/BoardInteraction/Target.mat");
            var target=GameObject.CreatePrimitive(PrimitiveType.Cylinder);target.name="BoardTarget";target.GetComponent<Renderer>().sharedMaterial=material;target.AddComponent<BoardTarget>();
            var label=new GameObject("Symbol",typeof(TextMesh));label.transform.SetParent(target.transform,false);label.transform.localPosition=new Vector3(0,.6f,0);label.transform.localRotation=Quaternion.Euler(90,-90,0);
            var text=label.GetComponent<TextMesh>();text.font=font;text.fontSize=36;text.characterSize=.22f;text.anchor=TextAnchor.MiddleCenter;text.text="→";text.color=Ink;
            label.GetComponent<Renderer>().sharedMaterial=font.material;
            var targetPrefab=PrefabUtility.SaveAsPrefabAsset(target,"Assets/Prefabs/BoardInteraction/BoardTarget.prefab");Object.DestroyImmediate(target);
            var ringMesh=new Mesh{name="SelectionRing"};var vertices=new Vector3[64];var triangles=new int[192];
            for(var i=0;i<32;i++){var a=i*Mathf.PI*2/32;vertices[i*2]=new Vector3(Mathf.Cos(a)*.58f,0,Mathf.Sin(a)*.58f);vertices[i*2+1]=new Vector3(Mathf.Cos(a)*.47f,0,Mathf.Sin(a)*.47f);var n=((i+1)%32)*2;var k=i*6;triangles[k]=i*2;triangles[k+1]=n;triangles[k+2]=i*2+1;triangles[k+3]=i*2+1;triangles[k+4]=n;triangles[k+5]=n+1;}
            for(var i=0;i<triangles.Length;i+=3){var old=triangles[i+1];triangles[i+1]=triangles[i+2];triangles[i+2]=old;}
            ringMesh.vertices=vertices;ringMesh.triangles=triangles;ringMesh.RecalculateNormals();ringMesh.RecalculateBounds();AssetDatabase.CreateAsset(ringMesh,"Assets/Prefabs/BoardInteraction/SelectionRing.asset");
            var gold=new Material(material);gold.color=Gold;AssetDatabase.CreateAsset(gold,"Assets/Prefabs/BoardInteraction/Selection.mat");
            var ring=new GameObject("SelectionRing",typeof(MeshFilter),typeof(MeshRenderer));ring.GetComponent<MeshFilter>().sharedMesh=ringMesh;ring.GetComponent<Renderer>().sharedMaterial=gold;
            var ringPrefab=PrefabUtility.SaveAsPrefabAsset(ring,"Assets/Prefabs/BoardInteraction/SelectionRing.prefab");Object.DestroyImmediate(ring);
            var style=ScriptableObject.CreateInstance<BoardInteractionStyle>();style.TargetPrefab=targetPrefab;style.SelectionPrefab=ringPrefab;style.PathMaterial=gold;AssetDatabase.CreateAsset(style,path);return style;
        }

        [MenuItem("Driftline/Add Editable Context Actions")]
        public static void AddContextActions()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Author in Edit Mode.");
            var style=AssetDatabase.LoadAssetAtPath<BoardInteractionStyle>("Assets/UI/Config/BoardInteractionStyle.asset");
            if(style==null||style.ContextMenuPrefab!=null)return;
            font=AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/SourceHanSansCN-Regular.otf");
            var root=new GameObject("BoardContextActions",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
            root.GetComponent<RectTransform>().sizeDelta=new Vector2(280,90);
            root.transform.localScale=Vector3.one*.014f;
            var menu=root.AddComponent<BoardContextMenu>();
            menu.ItemButtons=new Button[2];menu.ItemLabels=new Text[2];
            for(var i=0;i<2;i++)
            {
                menu.ItemButtons[i]=Action(root.transform,"Item"+i,i==0?"急停":"穿墙",new Vector2(i*145,0),new Vector2(135,56));
                menu.ItemLabels[i]=menu.ItemButtons[i].GetComponentInChildren<Text>();
                menu.ItemLabels[i].fontSize=28;
                menu.ItemButtons[i].gameObject.AddComponent<BoardItemAction>();
            }
            menu.Description=Label(root.transform,"Description","道具 · 点目标确认",new Vector2(0,63),new Vector2(280,80),20,White);
            style.ContextMenuPrefab=PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/BoardInteraction/BoardContextActions.prefab");
            Object.DestroyImmediate(root);EditorUtility.SetDirty(style);AssetDatabase.SaveAssets();
        }
    }
}

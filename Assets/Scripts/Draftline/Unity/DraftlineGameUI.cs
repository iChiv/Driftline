using System.Linq;
using Draftline.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;

namespace Draftline.Unity
{
    /// <summary>
    /// UI Toolkit presentation for the local Draftline match.
    /// The visual tree lives in Assets/Resources/UI/DraftlineHUD.uxml (editable in UI
    /// Builder); this class only binds data and callbacks.
    /// </summary>
    public sealed class DraftlineGameUI : MonoBehaviour
    {
        private static readonly string[] Names = { "红", "蓝", "绿", "黄" };
        private static readonly string[] ToggleNames = { "toggle-red", "toggle-blue", "toggle-green", "toggle-yellow" };
        private DraftlineGameController controller;
        private PanelSettings panelSettings;
        private UIDocument document;
        private VisualElement root;
        private VisualElement setup;
        private VisualElement hud;
        private VisualElement result;
        private VisualElement players;
        private VisualElement actions;
        private VisualElement activity;
        private VisualElement dirPad;
        private Button[] dirButtons;
        private Label setupError;
        private Label phase;
        private Label turn;
        private Label dice;
        private Label remaining;
        private Label hint;
        private Label resultTitle;
        private Label rolling;
        private Button cameraButton;
        private TextField addressInput;
        private Label roomStatus;
        private Button hostButton;
        private Button joinButton;
        private Button startRoomButton;
        private int roomSeats = 2;

        public void Initialize(DraftlineGameController owner)
        {
            controller = owner;
            panelSettings = Resources.Load<PanelSettings>("DraftlinePanelSettings");
            if (panelSettings == null)
            {
                Debug.LogError("Draftline PanelSettings asset is missing.");
                return;
            }
            document = gameObject.GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            if (EventSystem.current == null)
            {
                var events = new GameObject("Draftline UI Events");
                events.AddComponent<EventSystem>();
                var input = events.AddComponent<InputSystemUIInputModule>();
                input.AssignDefaultActions();
            }
            StartCoroutine(BuildNextFrame());
        }

        private System.Collections.IEnumerator BuildNextFrame()
        {
            yield return null;
            var tree = Resources.Load<VisualTreeAsset>("UI/DraftlineHUD");
            if (tree == null)
            {
                Debug.LogError("DraftlineHUD.uxml missing from Resources/UI.");
                yield break;
            }
            // 交给 UIDocument 自己重建内容（它会替换 root），下一帧再挂样式和绑定。
            document.visualTreeAsset = tree;
            yield return null;
            root = document.rootVisualElement;
            if (root == null || root.childCount == 0)
            {
                root = document.rootVisualElement;
                tree.CloneTree(root);
            }
            var theme = Resources.Load<StyleSheet>("DraftlineTheme");
            if (theme != null && !root.styleSheets.Contains(theme))
                root.styleSheets.Add(theme);
            Bind();
            Refresh();
        }

        private void Bind()
        {
            setup = root.Q("setup");
            hud = root.Q("hud");
            result = root.Q("result");
            players = root.Q("players");
            actions = root.Q("actions");
            activity = root.Q("activity");
            dirPad = root.Q("dir-pad");
            setupError = root.Q<Label>("setup-error");
            phase = root.Q<Label>("phase");
            turn = root.Q<Label>("turn");
            dice = root.Q<Label>("dice");
            remaining = root.Q<Label>("remaining");
            hint = root.Q<Label>("hint");
            resultTitle = root.Q<Label>("result-title");
            rolling = root.Q<Label>("rolling");
            addressInput = root.Q<TextField>("address-input");
            roomStatus = root.Q<Label>("room-status");

            for (var index = 0; index < ToggleNames.Length; index++)
            {
                var selectedIndex = index;
                var toggle = root.Q<Toggle>(ToggleNames[index]);
                if (toggle == null) continue;
                toggle.SetValueWithoutNotify(controller.SelectedColors[index]);
                toggle.RegisterValueChangedCallback(evt =>
                    controller.SelectedColors[selectedIndex] = evt.newValue);
            }

            cameraButton = root.Q<Button>("camera-button");
            cameraButton.clicked += () => controller.ToggleCameraFromUI();

            BindThemeCard("theme-tabletop", "tabletop");
            BindThemeCard("theme-magic", "magic");
            UpdateThemeSelection();
            var quitButton = root.Q<Button>("quit-button");
            if (quitButton != null)
                quitButton.clicked += OnQuit;

            root.Q<Button>("start-local").clicked += () =>
            {
                if (controller.LanRoom != null && controller.LanRoom.IsNetworked)
                    controller.LanRoom.Leave();
                controller.BeginLocalMatch();
                Refresh();
            };
            for (var seats = 2; seats <= 4; seats++)
            {
                var selectedSeats = seats;
                root.Q<Button>("seat-" + seats).clicked += () =>
                {
                    roomSeats = selectedSeats;
                    Refresh();
                };
            }
            hostButton = root.Q<Button>("host-button");
            hostButton.clicked += () =>
            {
                controller.LanRoom.Host(roomSeats);
                Refresh();
            };
            joinButton = root.Q<Button>("join-button");
            joinButton.clicked += () =>
            {
                controller.LanRoom.Join(addressInput.value);
                Refresh();
            };
            startRoomButton = root.Q<Button>("start-room");
            startRoomButton.clicked += () =>
            {
                controller.LanRoom.StartHostedMatch();
                Refresh();
            };
            root.Q<Button>("restart-button").clicked += () =>
            {
                if (controller.LanRoom != null && controller.LanRoom.IsNetworked)
                    controller.LanRoom.Leave();
                controller.BeginLocalMatch();
                Refresh();
            };

            dirButtons = new[]
            {
                root.Q<Button>("dir-up"),
                root.Q<Button>("dir-down"),
                root.Q<Button>("dir-left"),
                root.Q<Button>("dir-right"),
            };
            var directions = new[]
            {
                SlideDirection.Up, SlideDirection.Down, SlideDirection.Left, SlideDirection.Right
            };
            for (var index = 0; index < dirButtons.Length; index++)
            {
                var direction = directions[index];
                dirButtons[index].clicked += () => controller.MoveFromUI(direction);
            }
        }

        private void BindThemeCard(string elementName, string themeId)
        {
            var card = root.Q(elementName);
            if (card == null) return;
            card.RegisterCallback<ClickEvent>(_ =>
            {
                PlayerPrefs.SetString(DraftlineThemeApplier.PrefKey, themeId);
                PlayerPrefs.Save();
                UpdateThemeSelection();
                if (themeId == "magic")
                    Debug.Log("[Theme] 魔幻主题建设中，开局仍使用桌面玩具主题。");
            });
        }

        private void UpdateThemeSelection()
        {
            var current = DraftlineThemeApplier.CurrentId;
            var tabletop = root.Q("theme-tabletop");
            var magic = root.Q("theme-magic");
            tabletop?.EnableInClassList("theme-card-selected", current == "tabletop");
            magic?.EnableInClassList("theme-card-selected", current == "magic");
        }

        private static void OnQuit()
        {
#if UNITY_EDITOR
            Debug.Log("[UI] 编辑器播放模式下忽略退出按钮。");
#else
            Application.Quit();
#endif
        }

        private void Update()
        {
            if (rolling != null && controller != null && controller.IsDiceRolling)
                rolling.text = controller.DiceStatus;
            if (roomStatus != null && controller != null && controller.LanRoom != null)
            {
                var room = controller.LanRoom;
                roomStatus.text = room.Status;
                if (startRoomButton != null)
                    startRoomButton.SetEnabled(room.IsHost && room.PlayerCount == room.SeatsRequested);
            }
        }

        public void Refresh()
        {
            if (root == null || controller == null || turn == null) return;
            var rules = controller.Rules;
            setup.style.display = rules == null ? DisplayStyle.Flex : DisplayStyle.None;
            hud.style.display = rules == null ? DisplayStyle.None : DisplayStyle.Flex;
            result.style.display = rules != null && rules.State.Phase == MatchPhase.GameOver
                ? DisplayStyle.Flex : DisplayStyle.None;
            if (rules == null)
            {
                setupError.text = controller.SetupError ?? "";
                if (controller.LanRoom != null)
                {
                    hostButton.SetEnabled(!controller.LanRoom.IsNetworked);
                    joinButton.SetEnabled(!controller.LanRoom.IsNetworked);
                    startRoomButton.style.display = controller.LanRoom.IsHost
                        ? DisplayStyle.Flex : DisplayStyle.None;
                }
                return;
            }

            var state = rules.State;
            turn.text = Names[(int)state.CurrentPlayer] + " 方";
            turn.style.color = PlayerTint(state.CurrentPlayer);
            dice.text = state.DiceValue?.ToString() ?? "–";
            remaining.text = "剩余滑行  " + (state.DiceValue.HasValue ? state.RemainingSlides.ToString() : "–");
            phase.text = PhaseName(state.Phase);
            cameraButton.text = controller.IsManualTopDown ? "返回全盘" : "俯视视角";
            rolling.text = controller.IsDiceRolling ? controller.DiceStatus : "";
            hint.text = Hint(state.Phase);
            resultTitle.text = state.Winner.HasValue
                ? Names[(int)state.Winner.Value] + " 方获胜" : "";
            actions.Clear();
            if (!controller.CanControl)
                actions.Add(Text("等待当前玩家行动…", "waiting-text"));
            else
                BuildActions(rules);

            // 十字方向键：Moving 阶段显示，非法方向禁用。
            var showPad = controller.CanControl && !controller.IsAnimating &&
                          state.Phase == MatchPhase.Moving && !controller.PendingStopDirection.HasValue &&
                          state.SelectedPieceId.HasValue;
            dirPad.style.display = showPad ? DisplayStyle.Flex : DisplayStyle.None;
            if (showPad)
            {
                var selected = rules.PieceById(state.SelectedPieceId.Value);
                var legal = selected != null
                    ? rules.LegalDirections()
                    : (System.Collections.Generic.IReadOnlyList<SlideDirection>)new SlideDirection[0];
                var legalSet = new System.Collections.Generic.HashSet<SlideDirection>(legal);
                var padDirections = new[]
                {
                    SlideDirection.Up, SlideDirection.Down, SlideDirection.Left, SlideDirection.Right
                };
                for (var index = 0; index < dirButtons.Length; index++)
                    dirButtons[index].SetEnabled(legalSet.Contains(padDirections[index]));
            }

            players.Clear();
            foreach (var player in state.Players.Where(player => player.Active))
            {
                var row = Box("player-row");
                if (player.Color == state.CurrentPlayer && state.Phase != MatchPhase.GameOver)
                    row.AddToClassList("player-current");
                var dot = Box("player-dot");
                dot.style.backgroundColor = PlayerTint(player.Color);
                row.Add(dot);
                row.Add(Text(Names[(int)player.Color] + (player.Eliminated ? " · 出局" : ""), "player-name"));
                var count = state.Pieces.Count(piece => piece.Owner == player.Color && piece.OnBoard);
                row.Add(Text($"场上 {count}  手中 {rules.HandCount(player.Color)}  消耗 {player.Consumed}",
                    "player-stats"));
                players.Add(row);
            }
            activity.Clear();
            foreach (var item in controller.RecentEvents.Take(8))
                activity.Add(Text(item, "event-entry"));
        }

        private void BuildActions(MatchRules rules)
        {
            var state = rules.State;
            if (controller.IsAnimating)
            {
                actions.Add(Text(controller.IsDiceRolling ? "骰子投掷中…" : "棋子移动中…", "waiting-text"));
                return;
            }
            if (state.Phase == MatchPhase.WaitRoll)
            {
                actions.Add(Action("投掷骰子", () => controller.RollFromUI(), "primary-button"));
                return;
            }
            if (state.Phase == MatchPhase.WaitSelect)
            {
                var selectable = state.Pieces.Where(piece => piece.OnBoard &&
                         piece.Owner == state.CurrentPlayer && rules.HasLegalSlide(piece)).ToArray();
                for (var index = 0; index < selectable.Length; index++)
                {
                    var id = selectable[index].Id;
                    actions.Add(Action($"选择棋子 {index + 1}", () => controller.SelectFromUI(id)));
                }
                var deploy = Action("从出生点放置新棋", () => controller.DeployFromUI());
                deploy.SetEnabled(rules.CanDeploy(state.CurrentPlayer));
                actions.Add(deploy);
                return;
            }
            if (state.Phase != MatchPhase.Moving) return;
            var selected = state.SelectedPieceId.HasValue ? rules.PieceById(state.SelectedPieceId.Value) : null;
            if (selected == null) return;
            actions.Add(Text("已选择棋子，选择滑行方向", "selected-label"));
            foreach (var item in state.Players[(int)state.CurrentPlayer].Items.ToArray())
            {
                var selectedItem = item;
                actions.Add(Action("使用道具  " + ItemName(selectedItem), () => controller.UseItemFromUI(selectedItem)));
            }
            if (controller.PendingStopDirection.HasValue)
            {
                var direction = controller.PendingStopDirection.Value;
                actions.Add(Text("急停：选择路径上的停点", "waiting-text"));
                var plan = rules.PlanSlide(selected, direction);
                for (var index = 0; index < plan.Steps.Count; index++)
                {
                    var stop = index;
                    actions.Add(Action("停在 " + plan.Steps[index].Position,
                        () => controller.StopFromUI(direction, stop)));
                }
            }
        }

        private static Label Text(string value, string style)
        {
            var label = new Label(value);
            label.AddToClassList(style);
            return label;
        }

        private static VisualElement Box(string style)
        {
            var element = new VisualElement();
            element.AddToClassList(style);
            return element;
        }

        private static Button Action(string value, System.Action click, string style = "action-button")
        {
            var button = new Button(click) { text = value };
            button.AddToClassList(style);
            return button;
        }

        private static Color PlayerTint(PlayerColor player)
        {
            switch (player)
            {
                case PlayerColor.Red: return new Color(0.98f, 0.35f, 0.34f);
                case PlayerColor.Blue: return new Color(0.39f, 0.62f, 1f);
                case PlayerColor.Green: return new Color(0.37f, 0.83f, 0.54f);
                default: return new Color(1f, 0.83f, 0.34f);
            }
        }

        private static string ItemName(ItemType item)
        {
            switch (item)
            {
                case ItemType.PassWall: return "穿墙";
                case ItemType.EmergencyStop: return "急停";
                case ItemType.CollisionCapture: return "撞吃";
                default: return "破墙";
            }
        }

        private static string PhaseName(MatchPhase value)
        {
            switch (value)
            {
                case MatchPhase.WaitRoll: return "等待掷骰";
                case MatchPhase.WaitSelect: return "选择棋子";
                case MatchPhase.Moving: return "规划滑行";
                default: return "对局结束";
            }
        }

        private static string Hint(MatchPhase value)
        {
            switch (value)
            {
                case MatchPhase.WaitRoll: return "点击投骰，决定本回合滑行次数。";
                case MatchPhase.WaitSelect: return "选择一枚能移动的己方棋子，或从出生点出棋。";
                case MatchPhase.Moving: return "查看终点标记，选择本次滑行方向。";
                default: return "查看结果，开始下一局。";
            }
        }
    }
}

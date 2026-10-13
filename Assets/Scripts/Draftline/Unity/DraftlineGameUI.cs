using System.Collections.Generic;
using System.Linq;
using Draftline.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;

namespace Draftline.Unity
{
    /// <summary>UI Toolkit presentation for the local Draftline match.</summary>
    public sealed class DraftlineGameUI : MonoBehaviour
    {
        private static readonly string[] Names = { "红", "蓝", "绿", "黄" };
        private static readonly string[] Corners = { "左上角", "右上角", "右下角", "左下角" };
        private DraftlineGameController controller;
        private PanelSettings panelSettings;
        private UIDocument document;
        private VisualElement root;
        private VisualElement setup;
        private VisualElement hud;
        private VisualElement result;
        private VisualElement colorChoices;
        private VisualElement players;
        private VisualElement actions;
        private VisualElement activity;
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
            root = document.rootVisualElement;
            root.Clear();
            root.AddToClassList("draftline-root");
            root.pickingMode = PickingMode.Ignore;
            var theme = Resources.Load<StyleSheet>("DraftlineTheme");
            if (theme != null) root.styleSheets.Add(theme);
            BuildSetup();
            BuildHud();
            BuildResult();
            Refresh();
        }

        private void OnDestroy()
        {
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

        private void BuildSetup()
        {
            setup = Box("setup-screen");
            root.Add(setup);
            var panel = Box("setup-panel");
            setup.Add(panel);
            panel.Add(Text("DRAFTLINE", "brand-large"));
            panel.Add(Text("四方棋局 · 立体策略对战", "subtitle"));
            panel.Add(Text("选择参战的角落", "section-title"));
            panel.Add(Text("2–4 位玩家同机轮流；颜色与角落固定绑定。", "small-copy"));
            colorChoices = Box("choice-grid");
            panel.Add(colorChoices);
            for (var index = 0; index < Names.Length; index++)
            {
                var selectedIndex = index;
                var choice = Box("choice-card");
                choice.AddToClassList("choice-" + ((PlayerColor)index).ToString().ToLowerInvariant());
                var toggle = new Toggle { value = controller.SelectedColors[index] };
                toggle.AddToClassList("choice-toggle");
                toggle.RegisterValueChangedCallback(evt =>
                    controller.SelectedColors[selectedIndex] = evt.newValue);
                choice.Add(toggle);
                var text = Box("choice-copy");
                text.Add(Text(Names[index], "choice-name"));
                text.Add(Text(Corners[index], "choice-corner"));
                choice.Add(text);
                colorChoices.Add(choice);
            }
            setupError = Text("", "error-text");
            panel.Add(setupError);
            panel.Add(Action("开始对局  →", () =>
            {
                if (controller.LanRoom != null && controller.LanRoom.IsNetworked)
                    controller.LanRoom.Leave();
                controller.BeginLocalMatch();
                Refresh();
            }, "primary-button"));
            panel.Add(Text("局域网演示", "section-title"));
            var roomOptions = Box("room-options");
            panel.Add(roomOptions);
            var seatsRow = Box("seat-options");
            for (var seats = 2; seats <= 4; seats++)
            {
                var selectedSeats = seats;
                seatsRow.Add(Action(seats + " 人", () =>
                {
                    roomSeats = selectedSeats;
                    Refresh();
                }, "seat-button"));
            }
            roomOptions.Add(seatsRow);
            addressInput = new TextField("房主 IP") { value = "127.0.0.1" };
            addressInput.AddToClassList("room-address");
            roomOptions.Add(addressInput);
            hostButton = Action("创建房间", () =>
            {
                controller.LanRoom.Host(roomSeats);
                Refresh();
            });
            roomOptions.Add(hostButton);
            joinButton = Action("加入房间", () =>
            {
                controller.LanRoom.Join(addressInput.value);
                Refresh();
            });
            roomOptions.Add(joinButton);
            startRoomButton = Action("房主开始对局", () =>
            {
                controller.LanRoom.StartHostedMatch();
                Refresh();
            }, "primary-button");
            roomOptions.Add(startRoomButton);
            roomStatus = Text("", "room-status");
            roomOptions.Add(roomStatus);
            panel.Add(Text("掷骰决定滑行次数 · 围杀对手 · 连成五子获胜", "setup-footer"));
            panel.Add(Text("方向键移动 · 空格掷骰 · 点击棋子选择", "setup-footer"));
        }

        private void BuildHud()
        {
            hud = Box("hud");
            root.Add(hud);
            var header = Box("header");
            hud.Add(header);
            var brand = Box("header-brand");
            brand.Add(Text("DRAFTLINE", "brand-small"));
            brand.Add(Text("BOARD STRATEGY", "header-caption"));
            header.Add(brand);
            phase = Text("", "phase-pill");
            header.Add(phase);
            cameraButton = Action("俯视视角", () => controller.ToggleCameraFromUI(), "camera-button");
            header.Add(cameraButton);

            var side = Box("side-panel");
            hud.Add(side);
            side.Add(Text("当前回合", "overline"));
            turn = Text("", "turn-title");
            side.Add(turn);
            rolling = Text("", "rolling-label");
            side.Add(rolling);
            var diceRow = Box("dice-row");
            dice = Text("–", "dice-value");
            diceRow.Add(dice);
            var diceInfo = Box("dice-info");
            diceInfo.Add(Text("骰子点数", "stat-label"));
            remaining = Text("", "remaining-value");
            diceInfo.Add(remaining);
            diceRow.Add(diceInfo);
            side.Add(diceRow);
            hint = Text("", "hint-text");
            side.Add(hint);
            side.Add(Text("可执行操作", "section-title"));
            actions = Box("actions");
            side.Add(actions);
            side.Add(Text("玩家状态", "section-title"));
            players = Box("player-list");
            side.Add(players);
            side.Add(Text("对局记录", "section-title"));
            var log = new ScrollView();
            log.AddToClassList("event-scroll");
            activity = Box("event-list");
            log.Add(activity);
            side.Add(log);
        }

        private void BuildResult()
        {
            result = Box("result-screen");
            root.Add(result);
            var panel = Box("result-panel");
            result.Add(panel);
            panel.Add(Text("对局结束", "overline"));
            resultTitle = Text("", "result-title");
            panel.Add(resultTitle);
            panel.Add(Text("五子连线或成为最后的存活者", "small-copy"));
            panel.Add(Action("再来一局", () =>
            {
                if (controller.LanRoom != null && controller.LanRoom.IsNetworked)
                    controller.LanRoom.Leave();
                controller.BeginLocalMatch();
                Refresh();
            }, "primary-button"));
        }

        public void Refresh()
        {
            if (root == null || controller == null) return;
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
                return;
            }
            // 固定十字方向键：非法方向保留按钮但禁用，位置永远可预测。
            var legal = new HashSet<SlideDirection>(rules.LegalDirections());
            var pad = Box("dir-pad");
            var rowUp = Box("dir-row");
            rowUp.Add(DirButton(SlideDirection.Up, "↑", legal));
            var rowMid = Box("dir-row");
            rowMid.Add(DirButton(SlideDirection.Left, "←", legal));
            rowMid.Add(Box("dir-gap"));
            rowMid.Add(DirButton(SlideDirection.Right, "→", legal));
            var rowDown = Box("dir-row");
            rowDown.Add(DirButton(SlideDirection.Down, "↓", legal));
            pad.Add(rowUp);
            pad.Add(rowMid);
            pad.Add(rowDown);
            actions.Add(pad);
        }

        private Button DirButton(SlideDirection direction, string label, HashSet<SlideDirection> legal)
        {
            var button = Action(label, () => controller.MoveFromUI(direction), "direction-button");
            button.SetEnabled(legal.Contains(direction));
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

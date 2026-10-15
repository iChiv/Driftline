using System.Linq;
using Draftline.Core;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

namespace Draftline.Unity
{
    /// <summary>Saved Canvas view. All layout, fonts, art and static copy belong to prefabs.</summary>
    public sealed class DraftlineCanvasUI : MonoBehaviour
    {
        [Header("Pages — editable in Prefab Mode")]
        public GameObject MainMenu, Setup, Lobby, Hud, Settings, Result;
        [Header("Navigation")]
        [FormerlySerializedAs("Solo")] public Button LocalGame;
        public Button Lan, StartMatch, SetupBack, OpenSettings, CloseSettings;
        public Button ReturnMenu, Roll, CameraToggle, Restart, ResultMenu, Quit;
        public Button HudSettings, Reconnect, TakeOver;
        [Header("Match configuration")]
        public Dropdown[] SeatControls;
        public Dropdown Difficulty;
        public Text[] SeatNames, SeatStories;
        [Header("HUD")]
        public Text TurnText, PhaseText, DiceText, HintText, LogText, ErrorText, ResultText;
        public Text[] PlayerTexts;
        public Button[] ItemButtons;
        public Text[] ItemTexts;
        public Text InventoryHint;
        private ItemType? hoveredItem;
        private int inventoryRevision = -1;
        [Header("LAN")]
        public InputField Address, Port;
        public Dropdown RoomSize;
        public Dropdown RoomColor;
        public Toggle ComputerFill;
        public Button Host, Join, Ready, StartRoom, LeaveRoom;
        public Text RoomText;
        [Header("Settings")]
        public Slider Volume;
        public Toggle SimpleEffects;
        [Header("Action explanation and optional rules guide")]
        public Text ActionTitle, ActionText;
        public GameObject GuidePanel;
        public RuleGuideContent GuideContent;
        public Button GuideMenu, GuideSetup, GuideHud, GuideSettings, GuideClose, GuidePrevious, GuideNext;
        public Text GuideTitle, GuideBody, GuideExample, GuideProgress;
        private bool guideOpen, pausedBeforeGuide;
        private int guidePage, guideClosedFrame = -1;
        private string lastAction;
        public bool GuideClosedThisFrame => guideClosedFrame == Time.frameCount;

        private DraftlineGameController controller;
        private bool bound, setupOpen, lobbyOpen, settingsOpen;
        private float nextRefresh;
        private SeatControl[] localControls;

        public void Initialize(DraftlineGameController owner)
        {
            controller = owner;
            if (!bound)
            {
                bound = true;
                LocalGame.onClick.AddListener(OpenSetup);
                Lan.onClick.AddListener(() => { lobbyOpen = true; setupOpen = false; controller.Session.Mode = GameMode.Lan; Refresh(); });
                StartMatch.onClick.AddListener(() => { controller.BeginLocalMatch(); Refresh(); });
                SetupBack.onClick.AddListener(() => { setupOpen = false; Refresh(); });
                OpenSettings.onClick.AddListener(() => { settingsOpen = true; controller.SetPaused(true); Refresh(); });
                CloseSettings.onClick.AddListener(() => { settingsOpen = false; controller.SetPaused(false); Refresh(); });
                HudSettings.onClick.AddListener(() => { settingsOpen = true; controller.SetPaused(true); Refresh(); });
                Reconnect.onClick.AddListener(() => controller.LanRoom.Reconnect());
                TakeOver.onClick.AddListener(() => controller.LanRoom.TakeOverDisconnected());
                ReturnMenu.onClick.AddListener(ToMenu);
                ResultMenu.onClick.AddListener(ToMenu);
                Restart.onClick.AddListener(() => { controller.RestartMatch(); Refresh(); });
                Roll.onClick.AddListener(() => controller.RollFromUI());
                CameraToggle.onClick.AddListener(() => controller.ToggleCameraFromUI());
                Quit.onClick.AddListener(() => Application.Quit());
                foreach (var button in new[] { GuideMenu, GuideSetup, GuideHud, GuideSettings }) button?.onClick.AddListener(OpenGuide);
                GuideClose?.onClick.AddListener(CloseGuide);
                GuidePrevious?.onClick.AddListener(() => { guidePage--; RenderGuide(); });
                GuideNext?.onClick.AddListener(() => { guidePage++; RenderGuide(); });
                for (var i = 0; i < SeatControls.Length; i++)
                {
                    var seat = i;
                    SeatControls[i].onValueChanged.AddListener(value =>
                    {
                        controller.Session.Seats[seat].Control = (SeatControl)value;
                        if (localControls != null) localControls[seat] = (SeatControl)value;
                    });
                }
                Difficulty.onValueChanged.AddListener(value =>
                {
                    foreach (var seat in controller.Session.Seats) seat.Difficulty = (ComputerDifficulty)value;
                });
                for (var i = 0; i < ItemButtons.Length; i++)
                {
                    var slot = i;
                    ItemButtons[i].onClick.AddListener(() =>
                    {
                        var state = controller.Rules?.State;
                        if (state == null) return;
                        var inventory = state.Players[(int)state.CurrentPlayer].Items;
                        if (slot < inventory.Count) controller.UseItemFromUI(inventory[slot]);
                    });
                }
                Host.onClick.AddListener(() =>
                {
                    if (!ushort.TryParse(Port.text, out var port) || port == 0) { controller.ShowError("请输入 1–65535 的端口。"); return; }
                    controller.LanRoom.Host(RoomSize.value + 2, port);
                    Refresh();
                });
                Join.onClick.AddListener(() =>
                {
                    if (!ushort.TryParse(Port.text, out var port) || port == 0) { controller.ShowError("请输入 1–65535 的端口。"); return; }
                    controller.LanRoom.Join(Address.text, port);
                    Refresh();
                });
                Ready.onClick.AddListener(() => { controller.LanRoom.ToggleReady(); Refresh(); });
                StartRoom.onClick.AddListener(() => { controller.LanRoom.StartHostedMatch(); Refresh(); });
                LeaveRoom.onClick.AddListener(() => { controller.ReturnToMenu(); lobbyOpen = false; Refresh(); });
                ComputerFill.onValueChanged.AddListener(value => controller.LanRoom.FillWithComputers = value);
                RoomColor.onValueChanged.AddListener(value => controller.LanRoom.RequestColor(value));
                Volume.value = PlayerPrefs.GetFloat("Driftline.Volume", 0.7f);
                AudioListener.volume = Volume.value;
                Volume.onValueChanged.AddListener(value => { AudioListener.volume = value; PlayerPrefs.SetFloat("Driftline.Volume", value); });
                SimpleEffects.isOn = PlayerPrefs.GetInt("Driftline.SimpleEffects", 0) == 1;
                SimpleEffects.onValueChanged.AddListener(value => { controller.SimpleEffects = value; PlayerPrefs.SetInt("Driftline.SimpleEffects", value ? 1 : 0); });
                controller.SimpleEffects = SimpleEffects.isOn;
            }
            Refresh();
        }

        private void OpenSetup()
        {
            if (localControls == null)
            {
                controller.Session.Configure(GameMode.Local);
                localControls = controller.Session.Seats.Select(seat => seat.Control).ToArray();
            }
            controller.Session.Mode = GameMode.Local;
            for (var i = 0; i < localControls.Length; i++) controller.Session.Seats[i].Control = localControls[i];
            for (var i = 0; i < SeatControls.Length; i++) SeatControls[i].SetValueWithoutNotify((int)controller.Session.Seats[i].Control);
            setupOpen = true;
            lobbyOpen = false;
            Refresh();
        }

        private void ToMenu()
        {
            controller.ReturnToMenu();
            setupOpen = lobbyOpen = settingsOpen = false;
            guideOpen = false;
            controller.SetPaused(false);
            Refresh();
        }

        private void Update()
        {
            if (guideOpen && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame) CloseGuide();
            if (controller == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.1f;
            Refresh();
        }

        public void Refresh()
        {
            if (controller == null) return;
            var rules = controller.Rules;
            var room = controller.LanRoom;
            var playing = rules != null;
            MainMenu.SetActive(!playing && !setupOpen && !lobbyOpen);
            for (var i = 0; i < 4; i++)
            {
                if (SeatNames != null && i < SeatNames.Length && SeatNames[i] != null) SeatNames[i].text = MatchLabels.Player((PlayerColor)i);
                if (SeatStories != null && i < SeatStories.Length && SeatStories[i] != null) SeatStories[i].text = MatchLabels.Story((PlayerColor)i);
            }
            Setup.SetActive(!playing && setupOpen);
            Lobby.SetActive(!playing && lobbyOpen);
            Hud.SetActive(playing);
            Settings.SetActive(settingsOpen);
            if (GuidePanel != null) GuidePanel.SetActive(guideOpen);
            Result.SetActive(playing && rules.State.Phase == MatchPhase.GameOver && !controller.IsAnimating);
            ErrorText.text = controller.SetupError ?? "";
            ErrorText.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(controller.SetupError));
            RoomText.text = room.Status + "\n" + room.RoomDescription;
            Host.interactable = Join.interactable = !room.IsNetworked;
            Ready.interactable = room.IsNetworked && !room.MatchStarted;
            Ready.GetComponentInChildren<Text>().text = room.LocalReady ? "取消准备" : "准备";
            StartRoom.interactable = room.CanStart;
            ComputerFill.interactable = !room.IsNetworked || room.IsHost;
            RoomColor.interactable = room.IsNetworked && !room.MatchStarted;
            RoomSize.interactable = !room.IsNetworked;
            if (room.IsNetworked)
            {
                RoomSize.SetValueWithoutNotify(room.SeatsRequested - 2);
                ComputerFill.SetIsOnWithoutNotify(room.FillWithComputers);
                if (room.LocalColor.HasValue) RoomColor.SetValueWithoutNotify((int)room.LocalColor.Value);
            }
            Reconnect.gameObject.SetActive(room.IsLanSession && !room.IsNetworked);
            TakeOver.gameObject.SetActive(room.IsHost && room.HasDisconnectedPlayers);
            if (!playing) { hoveredItem = null; inventoryRevision = -1; lastAction = null; return; }
            var s = rules.State;
            if (inventoryRevision != s.Revision) { hoveredItem = null; inventoryRevision = s.Revision; }
            TurnText.text = MatchLabels.Player(s.CurrentPlayer) + (controller.Session.Seat(s.CurrentPlayer).Control == SeatControl.Computer ? " · 电脑" : " · 玩家");
            TurnText.color = MatchLabels.Color(s.CurrentPlayer);
            PhaseText.text = s.Phase == MatchPhase.WaitRoll ? "等待掷骰" : s.Phase == MatchPhase.WaitSelect ? (controller.BoardInteraction.HasSelection ? "规划路线" : "点选棋子") : s.Phase == MatchPhase.Moving ? "继续滑行" : "对局结束";
            DiceText.text = "骰子 " + (s.DiceValue?.ToString() ?? "—") + "   ·   剩余滑行 " + s.RemainingSlides;
            HintText.text = controller.SetupError ?? (controller.IsDiceRolling ? controller.DiceStatus : !controller.CanControl ? "等待对手行动 · 你可以查看棋盘" : controller.IsAnimating ? "行动演出中…" : controller.BoardInteraction.Instruction);
            LogText.text = string.Join("\n", controller.RecentEvents.Take(4));
            if (ActionText != null)
            {
                var preview = controller.BoardInteraction.InspectionText;
                ActionTitle.text = !string.IsNullOrEmpty(preview) ? "行动预览" : "最近行动";
                ActionText.text = preview ?? lastAction ?? "悬停终点、急停停点或出生格 +，查看这一步的结果。\n\n红圈：这次动作及结算会吃掉的棋子。\n预览不消耗次数或道具。";
            }
            for (var i = 0; i < 4; i++)
            {
                var p = s.Players[i];
                PlayerTexts[i].gameObject.SetActive(p.Active);
                PlayerTexts[i].text = MatchLabels.Player((PlayerColor)i) + (p.Eliminated ? " · 出局" : "") + "\n场上 " + s.Pieces.Count(piece => piece.OnBoard && piece.Owner == p.Color) + "   储备 " + rules.HandCount(p.Color) + "   消耗 " + p.Consumed;
                PlayerTexts[i].color = MatchLabels.Color((PlayerColor)i);
            }
            var items = s.Players[(int)s.CurrentPlayer].Items;
            for (var i = 0; i < ItemButtons.Length; i++)
            {
                ItemButtons[i].gameObject.SetActive(i < items.Count);
                if (i < items.Count)
                {
                    ItemTexts[i].text = MatchLabels.Item(items[i]);
                    ItemButtons[i].interactable = controller.CanControl && !controller.IsAnimating && controller.BoardInteraction.HasSelection;
                }
            }
            InventoryHint.text = hoveredItem.HasValue ? MatchLabels.ItemDescription(hoveredItem.Value) : controller.BoardInteraction.PendingItem.HasValue ? "待使用：" + MatchLabels.Item(controller.BoardInteraction.PendingItem.Value) + " · 点目标确认" : "先点选棋子，再选择道具";
            Roll.interactable = controller.CanControl && !controller.IsAnimating && s.Phase == MatchPhase.WaitRoll;
            CameraToggle.GetComponentInChildren<Text>().text = controller.IsManualTopDown ? "返回全盘" : "俯视棋盘";
            if (s.Winner.HasValue)
            {
                var line = rules.FindFiveLine(s.Winner.Value);
                ResultText.text = MatchLabels.Player(s.Winner.Value) + "获胜\n" + (line != null ? MatchLabels.FiveVictory : MatchLabels.SurvivalVictory) + "\n共 " + s.TurnCount + " 回合";
                Restart.interactable = !room.IsClientOnly;
            }
        }
        public void SetHoveredItem(int slot,bool entered)
        {
            var state=controller?.Rules?.State;
            if(state==null)return;
            var items=state.Players[(int)state.CurrentPlayer].Items;
            hoveredItem=entered&&slot<items.Count?(ItemType?)items[slot]:null;
            Refresh();
        }
        public void SetHoveredItemType(ItemType? item) { hoveredItem = item; Refresh(); }

        public void RecordEvents(MatchRules rules)
        {
            if (rules.Events.Count == 0) return;
            var actor = rules.Events.FirstOrDefault(e => e.Type == MatchEventType.PieceSlid || e.Type == MatchEventType.PieceDeployed || e.Type == MatchEventType.DiceRolled)?.Player ?? rules.State.CurrentPlayer;
            var inspection = new ActionInspection { Accepted = true, Actor = actor,
                Kind = rules.Events.Any(e=>e.Type==MatchEventType.PieceDeployed) ? MatchCommandKind.Deploy : MatchCommandKind.Slide,
                RemainingSlides = rules.State.RemainingSlides,
                EndsTurn = rules.Events.Any(e=>e.Type==MatchEventType.TurnChanged||e.Type==MatchEventType.Victory),
                ForcedEnd = rules.Events.Any(e=>e.Type==MatchEventType.NoMoves),
                Events = rules.Events.Where(e=>e.Type!=MatchEventType.ItemSpawned).ToArray() };
            if (!inspection.EndsTurn)
            {
                var newDead=rules.FindDeadBlocks().Where(b=>b.Any(p=>!rules.State.DeadAtTurnStart.Contains(p.Id))).ToArray();
                inspection.PendingEnemyPieces=newDead.Where(b=>b[0].Owner!=actor).Sum(b=>b.Count);
                inspection.RetainedOwnPieces=newDead.Where(b=>b[0].Owner==actor).Sum(b=>b.Count);
            }
            lastAction=MatchLabels.Player(actor)+"\n"+MatchExplanation.Describe(inspection);
        }

        private void OpenGuide()
        {
            if (GuidePanel == null || GuideContent == null || GuideContent.Steps.Length == 0) return;
            pausedBeforeGuide=controller.IsPaused; guideOpen=true;
            guidePage=controller.BoardInteraction.PendingItem.HasValue?3:controller.BoardInteraction.HasSelection?1:0;
            controller.SetPaused(true); RenderGuide(); Refresh();
        }
        private void CloseGuide()
        {
            guideOpen=false; guideClosedFrame=Time.frameCount;
            controller.SetPaused(pausedBeforeGuide); Refresh();
        }
        private void RenderGuide()
        {
            guidePage=Mathf.Clamp(guidePage,0,GuideContent.Steps.Length-1);
            var step=GuideContent.Steps[guidePage];
            GuideTitle.text=step.Title; GuideBody.text=step.Body; GuideExample.text=step.Example;
            GuideProgress.text=(guidePage+1)+" / "+GuideContent.Steps.Length;
            GuidePrevious.interactable=guidePage>0; GuideNext.interactable=guidePage<GuideContent.Steps.Length-1;
        }
    }

    public static class MatchLabels
    {
        private static MatchTextContent content;
        public static void Configure(MatchTextContent asset) => content=asset;
        public static string FiveVictory => content!=null?content.FiveVictory:"五枚傀儡共鸣，界钥已被唤醒。";
        public static string SurvivalVictory => content!=null?content.SurvivalVictory:"成为最后存活的一方，夺得界钥。";
        public static string ItemDescription(ItemType item) => content!=null&&content.ItemDescriptions.Length>=4?content.ItemDescriptions[(int)item-1]:item == ItemType.PassWall ? "穿墙：本回合穿过升起的机关墙，并忽略传送门。"
            : item == ItemType.EmergencyStop ? "急停：下一次滑行可停在路径上的指定格，用后失效。"
            : item == ItemType.CollisionCapture ? "撞吃：本回合碰到敌棋时吃掉它，并停在该格。"
            : "破墙：本回合可停在机关墙或中心墙上，离开后墙恢复。";
        public static string Player(PlayerColor color) => content!=null&&content.FactionNames.Length>=4?content.FactionNames[(int)color]:new[] { "红方 · 赤炉", "蓝方 · 苍印", "绿方 · 苔庭", "黄方 · 金环" }[(int)color];
        public static string Story(PlayerColor color) => content!=null&&content.FactionStories.Length>=4?content.FactionStories[(int)color]:"";
        public static Color Color(PlayerColor color) => new[] { new Color(1f,.42f,.36f), new Color(.4f,.68f,1f), new Color(.36f,.85f,.58f), new Color(1f,.84f,.36f) }[(int)color];
        public static string Item(ItemType item) => content!=null&&content.ItemNames.Length>=4?content.ItemNames[(int)item-1]:item == ItemType.PassWall ? "穿墙" : item == ItemType.EmergencyStop ? "急停" : item == ItemType.CollisionCapture ? "撞吃" : "破墙";
    }
}

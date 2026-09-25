using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Draftline.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Draftline.Unity
{
    /// <summary>First playable local slice on the existing SampleScene board.</summary>
    public sealed class DraftlineGameController : MonoBehaviour
    {
        private static readonly Color[] Palette =
        {
            new Color(0.91f, 0.20f, 0.20f),
            new Color(0.20f, 0.43f, 0.91f),
            new Color(0.20f, 0.72f, 0.34f),
            new Color(0.95f, 0.77f, 0.13f)
        };

        private readonly Dictionary<int, DraftlinePieceView> pieceViews =
            new Dictionary<int, DraftlinePieceView>();
        private readonly Dictionary<GridPosition, Renderer> rotatingWallRenderers =
            new Dictionary<GridPosition, Renderer>();
        private readonly Dictionary<GridPosition, Collider> rotatingWallColliders =
            new Dictionary<GridPosition, Collider>();
        private readonly List<string> recentEvents = new List<string>();
        private readonly List<GameObject> hintObjects = new List<GameObject>();
        private readonly bool[] selectedColors = { true, true, true, true };

        private MatchRules rules;
        private GameObject pieceTemplate;
        private Transform runtimePieces;
        private Renderer centerWallRenderer;
        private Collider centerWallCollider;
        private Material[] playerMaterials;
        private Material hintMaterial;
        private bool animating;
        private SlideDirection? pendingStopDirection;
        private string setupError;
        private PhysicalDiceRoller diceRoller;
        private DraftlineCameraDirector cameraDirector;
        private DraftlineGameUI gameUI;
        private DraftlineLanRoom lanRoom;
        private bool applyingRemoteCommand;

        public MatchRules Rules => rules;
        public IReadOnlyList<string> RecentEvents => recentEvents;
        public bool[] SelectedColors => selectedColors;
        public bool IsAnimating => animating;
        public SlideDirection? PendingStopDirection => pendingStopDirection;
        public string SetupError => setupError;
        public string DiceStatus => diceRoller != null ? diceRoller.Status : "";
        public bool IsDiceRolling => diceRoller != null && diceRoller.IsRolling;
        public bool IsManualTopDown => cameraDirector != null && cameraDirector.IsManualTopDown;
        public DraftlineLanRoom LanRoom => lanRoom;
        public bool CanControl => applyingRemoteCommand || lanRoom == null || !lanRoom.IsNetworked ||
                                  lanRoom.LocalColor == rules?.State.CurrentPlayer;

        private void Awake()
        {
            Application.runInBackground = true; // Also needed for a future LAN host.
            var piecesRoot = GameObject.Find("Pieces");
            if (piecesRoot != null)
            {
                var source = piecesRoot.transform.Find("Red/Cylinder");
                if (source != null)
                {
                    pieceTemplate = source.gameObject;
                    pieceTemplate.SetActive(false);
                }
                runtimePieces = new GameObject("Runtime Pieces").transform;
                runtimePieces.SetParent(piecesRoot.transform, false);
            }
            BindWalls();
            diceRoller = gameObject.GetComponent<PhysicalDiceRoller>() ??
                         gameObject.AddComponent<PhysicalDiceRoller>();
            cameraDirector = gameObject.GetComponent<DraftlineCameraDirector>() ??
                             gameObject.AddComponent<DraftlineCameraDirector>();
            gameUI = gameObject.GetComponent<DraftlineGameUI>() ??
                     gameObject.AddComponent<DraftlineGameUI>();
            lanRoom = gameObject.GetComponent<DraftlineLanRoom>() ??
                      gameObject.AddComponent<DraftlineLanRoom>();
            gameUI.Initialize(this);
        }

        private void OnDestroy()
        {
            if (playerMaterials != null)
                foreach (var material in playerMaterials)
                    if (material != null) Destroy(material);
            if (hintMaterial != null) Destroy(hintMaterial);
        }

        private void BindWalls()
        {
            var root = GameObject.Find("Walls");
            if (root == null) return;
            var layout = new BoardLayout();
            foreach (Transform child in root.transform)
            {
                if (!BoardCoordinates.TryFromWorld(child.position, out var cell)) continue;
                var renderer = child.GetComponent<Renderer>();
                var collider = child.GetComponent<Collider>();
                if (layout.CellAt(cell).Kind == CellKind.RotatingWallSlot)
                {
                    rotatingWallRenderers[cell] = renderer;
                    rotatingWallColliders[cell] = collider;
                }
                else if (cell == new GridPosition(9, 9))
                {
                    centerWallRenderer = renderer;
                    centerWallCollider = collider;
                }
            }
            if (rotatingWallRenderers.Count != 8 || centerWallRenderer == null)
                Debug.LogWarning($"Draftline board binding found {rotatingWallRenderers.Count}/8 rotating walls.");
        }

        private void StartLocalMatch()
        {
            var active = Enumerable.Range(0, 4).Where(index => selectedColors[index])
                .Select(index => (PlayerColor)index).ToArray();
            if (active.Length < 2)
            {
                setupError = "Select at least two colors.";
                return;
            }

            rules = new MatchRules(active);
            setupError = null;
            pendingStopDirection = null;
            recentEvents.Clear();
            foreach (var view in pieceViews.Values)
                if (view != null) Destroy(view.gameObject);
            pieceViews.Clear();
            CreatePieceMaterials();
            SyncViews();
        }

        private void CreatePieceMaterials()
        {
            if (playerMaterials != null) return;
            playerMaterials = new Material[4];
            var source = pieceTemplate != null ? pieceTemplate.GetComponent<Renderer>()?.sharedMaterial : null;
            var fallbackShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            for (var index = 0; index < 4; index++)
            {
                playerMaterials[index] = source != null ? new Material(source) : new Material(fallbackShader);
                if (playerMaterials[index].HasProperty("_BaseColor"))
                    playerMaterials[index].SetColor("_BaseColor", Palette[index]);
                if (playerMaterials[index].HasProperty("_Color"))
                    playerMaterials[index].SetColor("_Color", Palette[index]);
            }
        }

        private void SyncViews()
        {
            if (rules == null) return;
            foreach (var piece in rules.State.Pieces)
            {
                if (!pieceViews.TryGetValue(piece.Id, out var view))
                {
                    var visual = pieceTemplate != null
                        ? Instantiate(pieceTemplate, runtimePieces)
                        : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    visual.name = $"{piece.Owner} Piece {piece.Id}";
                    visual.SetActive(true);
                    if (pieceTemplate == null)
                        visual.transform.localScale = new Vector3(0.8f, 0.1f, 0.8f);
                    view = visual.AddComponent<DraftlinePieceView>();
                    view.PieceId = piece.Id;
                    var renderer = visual.GetComponent<Renderer>();
                    if (renderer != null) renderer.sharedMaterial = playerMaterials[(int)piece.Owner];
                    pieceViews.Add(piece.Id, view);
                }
                view.gameObject.SetActive(piece.OnBoard);
                if (piece.OnBoard) view.transform.position = BoardCoordinates.ToWorld(piece.Position);
            }

            foreach (var pair in rotatingWallRenderers)
            {
                var visible = rules.IsWall(pair.Key);
                if (pair.Value != null) pair.Value.enabled = visible;
                if (rotatingWallColliders.TryGetValue(pair.Key, out var collider) && collider != null)
                    collider.enabled = visible;
            }
            var centerVisible = rules.IsWall(new GridPosition(9, 9));
            if (centerWallRenderer != null) centerWallRenderer.enabled = centerVisible;
            if (centerWallCollider != null) centerWallCollider.enabled = centerVisible;

            foreach (var matchEvent in rules.Events)
            {
                recentEvents.Insert(0, EventText(matchEvent));
                if (recentEvents.Count > 12) recentEvents.RemoveAt(recentEvents.Count - 1);
                if (matchEvent.Type == MatchEventType.TurnChanged && cameraDirector != null)
                    StartCoroutine(ShowTurnCameraBriefly(matchEvent.Player));
            }
            if (lanRoom != null && lanRoom.IsHost)
                lanRoom.BroadcastState(rules.Events);
            rules.ClearEvents();
            RenderMoveHints();
            gameUI?.Refresh();
        }

        private void RenderMoveHints()
        {
            foreach (var marker in hintObjects)
                if (marker != null) Destroy(marker);
            hintObjects.Clear();
            if (rules == null || rules.State.Phase != MatchPhase.Moving ||
                !rules.State.SelectedPieceId.HasValue || animating) return;
            var piece = rules.PieceById(rules.State.SelectedPieceId.Value);
            if (piece == null || !piece.OnBoard) return;
            if (hintMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                hintMaterial = new Material(shader);
                var color = new Color(0.26f, 0.95f, 0.52f, 0.8f);
                if (hintMaterial.HasProperty("_BaseColor")) hintMaterial.SetColor("_BaseColor", color);
                if (hintMaterial.HasProperty("_Color")) hintMaterial.SetColor("_Color", color);
            }
            foreach (var direction in rules.LegalDirections())
            {
                var destination = rules.PlanSlide(piece, direction).Destination;
                var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = $"{direction} destination {destination}";
                marker.transform.position = BoardCoordinates.ToWorld(destination, 0.53f);
                marker.transform.localScale = new Vector3(0.54f, 0.025f, 0.54f);
                marker.GetComponent<Renderer>().sharedMaterial = hintMaterial;
                Destroy(marker.GetComponent<Collider>());
                hintObjects.Add(marker);
            }
        }

        private IEnumerator ShowTurnCameraBriefly(PlayerColor player)
        {
            cameraDirector.ShowNextPlayer(player);
            yield return new WaitForSeconds(1.1f);
            cameraDirector.EndTurnView();
        }

        private static string EventText(MatchEvent matchEvent)
        {
            var player = PlayerName(matchEvent.Player);
            switch (matchEvent.Type)
            {
                case MatchEventType.DiceRolled: return $"{player}方掷出 {matchEvent.Value} 点";
                case MatchEventType.PieceDeployed: return $"{player}方放置新棋";
                case MatchEventType.PieceCaptured: return $"{player}方棋子被吃";
                case MatchEventType.ItemCollected: return $"{player}方拾取 {ItemName(matchEvent.Item)}";
                case MatchEventType.ItemSpawned: return $"{ItemName(matchEvent.Item)}出现在 {matchEvent.Position}";
                case MatchEventType.WallRotated: return "旋转墙切换";
                case MatchEventType.NoMoves: return $"{player}方没有合法移动";
                case MatchEventType.Victory: return $"{player}方获胜";
                default: return matchEvent.Type.ToString();
            }
        }

        private static string PlayerName(PlayerColor player)
        {
            switch (player)
            {
                case PlayerColor.Red: return "红";
                case PlayerColor.Blue: return "蓝";
                case PlayerColor.Green: return "绿";
                default: return "黄";
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

        private void Update()
        {
            if (rules == null || animating || !CanControl) return;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (rules.State.Phase == MatchPhase.WaitRoll && keyboard.spaceKey.wasPressedThisFrame)
                    Roll();
                else if (rules.State.Phase == MatchPhase.Moving)
                {
                    if (keyboard.upArrowKey.wasPressedThisFrame) SubmitDirection(SlideDirection.Up);
                    else if (keyboard.downArrowKey.wasPressedThisFrame) SubmitDirection(SlideDirection.Down);
                    else if (keyboard.leftArrowKey.wasPressedThisFrame) SubmitDirection(SlideDirection.Left);
                    else if (keyboard.rightArrowKey.wasPressedThisFrame) SubmitDirection(SlideDirection.Right);
                }
            }

            var mouse = Mouse.current;
            if (rules.State.Phase != MatchPhase.WaitSelect || mouse == null ||
                !mouse.leftButton.wasPressedThisFrame || Camera.main == null) return;
            var screen = mouse.position.ReadValue();
            if (screen.x < 360f) return; // The control panel occupies the left edge.
            if (!Physics.Raycast(Camera.main.ScreenPointToRay(screen), out var hit)) return;
            var pieceView = hit.collider.GetComponent<DraftlinePieceView>();
            if (pieceView == null) return;
            if (rules.TrySelectPiece(pieceView.PieceId, out _)) SyncViews();
        }

        private void Roll()
        {
            if (rules == null || rules.State.Phase != MatchPhase.WaitRoll || animating) return;
            if (lanRoom != null && lanRoom.IsNetworked && !CanControl) return;
            if (lanRoom != null && lanRoom.IsClientOnly)
            {
                lanRoom.SendCommand("roll");
                return;
            }
            StartCoroutine(RollPhysicalDie());
        }

        private IEnumerator RollPhysicalDie()
        {
            animating = true;
            cameraDirector?.ShowDice();
            if (lanRoom != null && lanRoom.IsHost) lanRoom.BroadcastDiceStart();
            int? result = null;
            var rolling = StartCoroutine(diceRoller.Roll(value => result = value));
            gameUI?.Refresh();
            yield return rolling;
            cameraDirector?.ShowBoard();
            if (result.HasValue && rules.TryApplyDice(result.Value, out _)) SyncViews();
            animating = false;
            gameUI?.Refresh();
        }

        private void SubmitDirection(SlideDirection direction)
        {
            if (rules.State.Phase != MatchPhase.Moving) return;
            if (rules.State.EmergencyStopActive)
            {
                pendingStopDirection = direction;
                gameUI?.Refresh();
                return;
            }
            ExecuteSlide(direction, null);
        }

        private void ExecuteSlide(SlideDirection direction, int? stopIndex)
        {
            var selected = rules.State.SelectedPieceId;
            if (!rules.TrySlide(direction, stopIndex, out var path, out _)) return;
            pendingStopDirection = null;
            if (selected.HasValue && pieceViews.TryGetValue(selected.Value, out var view))
                StartCoroutine(AnimateSlide(view, path));
            else
                SyncViews();
        }

        private IEnumerator AnimateSlide(DraftlinePieceView view, SlidePlan path)
        {
            animating = true;
            foreach (var marker in hintObjects)
                if (marker != null) Destroy(marker);
            hintObjects.Clear();
            cameraDirector?.FollowPiece(view.transform);
            foreach (var step in path.Steps)
            {
                var destination = BoardCoordinates.ToWorld(step.Position);
                if (step.PortalExit)
                {
                    view.transform.position = destination;
                    yield return new WaitForSeconds(0.16f);
                    continue;
                }
                var origin = view.transform.position;
                const float duration = 0.09f;
                for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
                {
                    view.transform.position = Vector3.Lerp(origin, destination, elapsed / duration);
                    yield return null;
                }
                view.transform.position = destination;
            }
            cameraDirector?.ShowBoard();
            animating = false;
            SyncViews();
        }

        public void ApplyNetworkSnapshot(MatchState snapshot)
        {
            if (lanRoom == null || !lanRoom.IsClientOnly) return;
            rules = MatchRules.FromSnapshot(snapshot);
            if (playerMaterials == null) CreatePieceMaterials();
            SyncViews();
        }

        public void PlayNetworkDice()
        {
            if (lanRoom == null || !lanRoom.IsClientOnly) return;
            StartCoroutine(PlayNetworkDicePresentation());
        }

        private IEnumerator PlayNetworkDicePresentation()
        {
            animating = true;
            cameraDirector?.ShowDice();
            gameUI?.Refresh();
            yield return new WaitForSeconds(1.6f);
            cameraDirector?.ShowBoard();
            animating = false;
            gameUI?.Refresh();
        }

        public Vector3? PieceViewPosition(int pieceId) =>
            pieceViews.TryGetValue(pieceId, out var view) && view != null
                ? view.transform.position : (Vector3?)null;

        public void ApplyNetworkSlide(SlidePlan path, int pieceId, Vector3? origin)
        {
            if (lanRoom == null || !lanRoom.IsClientOnly || path == null) return;
            if (pieceViews.TryGetValue(pieceId, out var view))
            {
                if (origin.HasValue) view.transform.position = origin.Value;
                StartCoroutine(AnimateSlide(view, path));
            }
        }

        public void BeginLocalMatch() => StartLocalMatch();
        public void BeginNetworkMatch(int activeMask)
        {
            for (var index = 0; index < selectedColors.Length; index++)
                selectedColors[index] = (activeMask & (1 << index)) != 0;
            StartLocalMatch();
        }
        public void ApplyVerifiedRemoteCommand(string kind, int first, int second)
        {
            if (lanRoom == null || !lanRoom.IsHost) return;
            applyingRemoteCommand = true;
            try
            {
                switch (kind)
                {
                    case "roll": RollFromUI(); break;
                    case "select": SelectFromUI(first); break;
                    case "deploy": DeployFromUI(); break;
                    case "move":
                        if (System.Enum.IsDefined(typeof(SlideDirection), first))
                            MoveFromUI((SlideDirection)first);
                        break;
                    case "stop":
                        if (System.Enum.IsDefined(typeof(SlideDirection), first))
                            StopFromUI((SlideDirection)first, second);
                        break;
                    case "item":
                        if (System.Enum.IsDefined(typeof(ItemType), first))
                            UseItemFromUI((ItemType)first);
                        break;
                }
            }
            finally { applyingRemoteCommand = false; }
        }
        public void RollFromUI() => Roll();
        public void SelectFromUI(int pieceId)
        {
            if (lanRoom != null && lanRoom.IsNetworked && !CanControl) return;
            if (lanRoom != null && lanRoom.IsClientOnly)
            {
                lanRoom.SendCommand("select", pieceId);
                return;
            }
            if (rules != null && !animating && rules.TrySelectPiece(pieceId, out _)) SyncViews();
        }
        public void DeployFromUI()
        {
            if (lanRoom != null && lanRoom.IsNetworked && !CanControl) return;
            if (lanRoom != null && lanRoom.IsClientOnly)
            {
                lanRoom.SendCommand("deploy");
                return;
            }
            if (rules != null && !animating && rules.TryDeploy(out _, out _)) SyncViews();
        }
        public void MoveFromUI(SlideDirection direction)
        {
            if (lanRoom != null && lanRoom.IsNetworked && !CanControl) return;
            if (lanRoom != null && lanRoom.IsClientOnly)
            {
                lanRoom.SendCommand("move", (int)direction);
                return;
            }
            if (rules != null && !animating) SubmitDirection(direction);
        }
        public void StopFromUI(SlideDirection direction, int index)
        {
            if (lanRoom != null && lanRoom.IsNetworked && !CanControl) return;
            if (lanRoom != null && lanRoom.IsClientOnly)
            {
                lanRoom.SendCommand("stop", (int)direction, index);
                return;
            }
            if (rules != null && !animating) ExecuteSlide(direction, index);
        }
        public void UseItemFromUI(ItemType item)
        {
            if (lanRoom != null && lanRoom.IsNetworked && !CanControl) return;
            if (lanRoom != null && lanRoom.IsClientOnly)
            {
                lanRoom.SendCommand("item", (int)item);
                return;
            }
            if (rules != null && !animating && rules.TryActivateItem(item, out _)) SyncViews();
        }
        public void ToggleCameraFromUI()
        {
            cameraDirector?.ToggleManualTopDown();
            gameUI?.Refresh();
        }

    }
}

using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
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
        private readonly Dictionary<int, DraftlineItemView> itemViews =
            new Dictionary<int, DraftlineItemView>();
        private Transform runtimeItems;
        private readonly Dictionary<GridPosition, Renderer> rotatingWallRenderers =
            new Dictionary<GridPosition, Renderer>();
        private readonly Dictionary<GridPosition, Collider> rotatingWallColliders =
            new Dictionary<GridPosition, Collider>();
        private readonly List<string> recentEvents = new List<string>();
        private readonly bool[] selectedColors = { true, true, true, true };

        private MatchRules rules;
        private GameObject pieceTemplate;
        private Transform runtimePieces;
        private Renderer centerWallRenderer;
        private Collider centerWallCollider;
        private Material[] playerMaterials;
        private Material[] itemMaterials;
        [SerializeField] private DraftlineWarnings warnings;
        private bool animating;
        private int pendingVisuals;
        private readonly HashSet<int> exitingPieces = new HashSet<int>();
        private readonly Dictionary<Renderer, bool> wallVisualState = new Dictionary<Renderer, bool>();
        private readonly Dictionary<Renderer, Vector3> wallBasePosition = new Dictionary<Renderer, Vector3>();
        private readonly Dictionary<Renderer, Vector3> wallBaseScale = new Dictionary<Renderer, Vector3>();
        private SlideDirection? pendingStopDirection;
        private string setupError;
        [SerializeField] private PhysicalDiceRoller diceRoller;
        [SerializeField] private DraftlineCameraDirector cameraDirector;
        [SerializeField] private DraftlineCanvasUI gameUI;
        public MatchSession Session;
        public BoardInteractionController BoardInteraction;
        public bool SimpleEffects;
        public MatchFeedbackProfile Feedback;
        public MatchTextContent Content;
        private readonly Queue<GameObject> activeEffects = new Queue<GameObject>();
        public bool IsPaused { get; private set; }
        private CancellationTokenSource computerCancellation;
        private Task<MatchCommand> computerTask;
        private int computerRevision = -1;
        private float nextComputerAction;
        private int lifetime;
        private readonly Queue<Tuple<MatchState, MatchEvent[]>> remoteUpdates = new Queue<Tuple<MatchState, MatchEvent[]>>();
        [SerializeField] private DraftlineLanRoom lanRoom;
        [SerializeField] private DraftlineAudio sfx;
        private bool applyingRemoteCommand;

        public MatchRules Rules => rules;
        public IReadOnlyList<string> RecentEvents => recentEvents;
        public bool[] SelectedColors => selectedColors;
        public IReadOnlyDictionary<int, DraftlinePieceView> PieceViews => pieceViews;
        public bool IsAnimating => animating || pendingVisuals > 0;
        private bool InputLocked => IsAnimating;
        public SlideDirection? PendingStopDirection => pendingStopDirection;
        public string SetupError => setupError;
        public string DiceStatus => diceRoller != null ? diceRoller.Status : "";
        public bool IsDiceRolling => diceRoller != null && diceRoller.IsRolling;
        public bool IsManualTopDown => cameraDirector != null && cameraDirector.IsManualTopDown;
        public DraftlineLanRoom LanRoom => lanRoom;
        public DraftlineCanvasUI CanvasUI => gameUI;
        public bool CanControl => (applyingRemoteCommand || !IsPaused) && rules != null &&
            (applyingRemoteCommand || (Session.Seat(rules.State.CurrentPlayer).Control == SeatControl.LocalHuman &&
            (Session.Mode != GameMode.Lan || (lanRoom != null && lanRoom.IsNetworked && lanRoom.LocalColor == rules.State.CurrentPlayer)))) &&
            (lanRoom == null || !lanRoom.HasPendingCommand);

        public void ConfigureEditor(DraftlineCanvasUI ui, MatchSession session, BoardInteractionController board)
        {
            gameUI = ui; Session = session; BoardInteraction = board;
            diceRoller = GetComponent<PhysicalDiceRoller>();
            cameraDirector = GetComponent<DraftlineCameraDirector>();
            lanRoom = GetComponent<DraftlineLanRoom>();
            warnings = GetComponent<DraftlineWarnings>();
            sfx = GetComponent<DraftlineAudio>();
        }

        private void Awake()
        {
            Application.runInBackground = true; // Also needed for a future LAN host.
            MatchLabels.Configure(Content);
            DraftlineThemeApplier.Apply(DraftlineThemeApplier.PreferredId);
            var piecesRoot = GameObject.Find("Pieces");
            if (piecesRoot != null)
            {
                // 优先用兵棋人仔模板（场景可编辑），没有则退回扁平圆片。
                var source = piecesRoot.transform.Find("Red/Pawn");
                if (source == null) source = piecesRoot.transform.Find("Red/Cylinder");
                // 模板组下的所有子件都禁用——旧圆片模板留着会渲染并挡住点击。
                foreach (Transform group in piecesRoot.transform)
                    foreach (Transform child in group)
                        child.gameObject.SetActive(false);
                if (source != null)
                    pieceTemplate = source.gameObject;
                runtimePieces = new GameObject("Runtime Pieces").transform;
                runtimePieces.SetParent(piecesRoot.transform, false);
            }
            BindWalls();
            if (gameUI == null || Session == null || BoardInteraction == null || diceRoller == null || cameraDirector == null || lanRoom == null)
            {
                Debug.LogError("Driftline scene bindings are missing. Use Driftline/Install Editable Experience once in Edit Mode.");
                enabled = false;
                return;
            }
            BoardInteraction.Controller = this;
            gameUI.Initialize(this);
        }

        private void OnDestroy()
        {
            CancelPresentation();
            if (playerMaterials != null)
                foreach (var material in playerMaterials)
                    if (material != null) Destroy(material);
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
                // 地牢皮肤：块上挂了 DungeonWall 子件时，动画绑定到皮肤渲染器，
                // 旧占位方块的渲染器保持禁用。
                var skinned = child.Find("DungeonWall");
                if (skinned != null)
                {
                    var skinnedRenderer = skinned.GetComponentInChildren<Renderer>(true);
                    if (skinnedRenderer != null) renderer = skinnedRenderer;
                }
                if (layout.CellAt(cell).Kind == CellKind.RotatingWallSlot)
                {
                    rotatingWallRenderers[cell] = renderer;
                    rotatingWallColliders[cell] = collider;
                    RegisterWallVisual(renderer);
                }
                else if (cell == new GridPosition(9, 9))
                {
                    centerWallRenderer = renderer;
                    centerWallCollider = collider;
                    RegisterWallVisual(renderer);
                }
            }
            if (rotatingWallRenderers.Count != 8 || centerWallRenderer == null)
                Debug.LogWarning($"Draftline board binding found {rotatingWallRenderers.Count}/8 rotating walls.");
        }

        private void RegisterWallVisual(Renderer renderer)
        {
            if (renderer == null || wallVisualState.ContainsKey(renderer)) return;
            wallVisualState[renderer] = renderer.enabled;
            wallBasePosition[renderer] = renderer.transform.position;
            wallBaseScale[renderer] = renderer.transform.localScale;
        }

        private void StartLocalMatch()
        {
            if (!Session.Validate(out var error)) { ShowError(error); return; }
            StartMatch(Session.ActiveColors);
        }

        private void StartMatch(PlayerColor[] active)
        {
            CancelPresentation();
            var selectedTheme=DraftlineThemeApplier.Apply(DraftlineThemeApplier.CurrentId);
            rules = new MatchRules(active);
            setupError = null;
            pendingStopDirection = null;
            recentEvents.Clear();
            warnings?.ClearAll();
            UpdatePieceTemplate(selectedTheme);
            CreatePieceMaterials();
            cameraDirector?.ResetForMatch();
            IsPaused = false;
            SyncViews();
            BoardInteraction.StateChanged();
        }

        private void CancelPresentation()
        {
            lifetime++;
            computerCancellation?.Cancel();
            computerCancellation?.Dispose();
            computerCancellation = null;
            computerTask = null;
            computerRevision = -1;
            StopAllCoroutines();
            DOTween.Kill(this);
            while (activeEffects.Count > 0) { var effect = activeEffects.Dequeue(); if (effect != null) Destroy(effect); }
            foreach (var renderer in wallVisualState.Keys)
                if (renderer != null) renderer.transform.DOKill();
            foreach (var view in pieceViews.Values)
                if (view != null) { view.transform.DOKill(); Destroy(view.gameObject); }
            pieceViews.Clear();
            foreach (var view in itemViews.Values) if (view != null) Destroy(view.gameObject);
            itemViews.Clear();
            BoardInteraction?.ClearVisuals();
            remoteUpdates.Clear();
            exitingPieces.Clear();
            pendingVisuals = 0;
            animating = false;
            diceRoller?.ResetRoll();
        }

        public void ShowError(string text) { setupError = text; gameUI?.Refresh(); }
        private void UpdatePieceTemplate(BoardTheme theme)
        {
            if(theme!=null && theme.PiecePrefab!=null)pieceTemplate=theme.PiecePrefab;
            else {
                var root=GameObject.Find("Pieces");
                var source=root!=null?root.transform.Find("Red/Pawn"):null;
                if(source!=null)pieceTemplate=source.gameObject;
            }
        }
        public void SetPaused(bool paused) { IsPaused = paused; BoardInteraction?.Rebuild(); }
        public void ReturnToMenu()
        {
            CancelPresentation();
            lanRoom?.Leave();
            rules = null;
            DraftlineThemeApplier.Apply(DraftlineThemeApplier.PreferredId);
            setupError = null;
            IsPaused = false;
            cameraDirector?.ResetForMatch();
            warnings?.ClearAll();
            gameUI?.Refresh();
        }
        public void RestartMatch()
        {
            if (lanRoom.IsNetworked) { if (lanRoom.IsHost) lanRoom.RestartHostedMatch(); }
            else StartLocalMatch();
        }
        public void ClearNetworkMatch()
        {
            CancelPresentation(); rules = null; setupError = null;
            cameraDirector?.ResetForMatch(); warnings?.ClearAll(); gameUI?.Refresh();
        }

        private void CreatePieceMaterials()
        {
            if (playerMaterials != null) return;
            playerMaterials = new Material[4];
            // 人仔模板的 Renderer 在子级（Body/Head）；严格 GetComponent 模式下
            // 直接 GetComponent 会抛 MissingComponentException，必须用 TryGetComponent。
            Renderer templateRenderer = null;
            if (pieceTemplate != null)
            {
                pieceTemplate.TryGetComponent(out templateRenderer);
                if (templateRenderer == null)
                    templateRenderer = pieceTemplate.GetComponentInChildren<Renderer>(true);
            }
            var source = templateRenderer != null ? templateRenderer.sharedMaterial : null;
            var fallbackShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            for (var index = 0; index < 4; index++)
            {
                playerMaterials[index] = source != null ? new Material(source) : new Material(fallbackShader);
                if (playerMaterials[index].HasProperty("_BaseColor"))
                    playerMaterials[index].SetColor("_BaseColor", Palette[index]);
                if (playerMaterials[index].HasProperty("_Color"))
                    playerMaterials[index].SetColor("_Color", Palette[index]);
                // Warnings drive per-renderer emission through MaterialPropertyBlock.
                playerMaterials[index].EnableKeyword("_EMISSION");
                if (playerMaterials[index].HasProperty("_EmissionColor"))
                    playerMaterials[index].SetColor("_EmissionColor", Color.black);
            }
        }

        private static readonly Color[] ItemPalette =
        {
            new Color(0.61f, 0.42f, 1.0f),  // PassWall 穿墙
            new Color(0.21f, 0.69f, 0.88f), // EmergencyStop 急停
            new Color(0.88f, 0.25f, 0.25f), // CollisionCapture 撞吃
            new Color(0.88f, 0.63f, 0.21f)  // BreakWall 破墙
        };

        private void EnsureItemMaterials()
        {
            if (itemMaterials != null) return;
            itemMaterials = new Material[4];
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
            for (var index = 0; index < 4; index++)
            {
                itemMaterials[index] = new Material(shader);
                if (itemMaterials[index].HasProperty("_BaseColor"))
                    itemMaterials[index].SetColor("_BaseColor", ItemPalette[index]);
                if (itemMaterials[index].HasProperty("_Color"))
                    itemMaterials[index].SetColor("_Color", ItemPalette[index]);
            }
        }

        private void SyncItemViews()
        {
            EnsureItemMaterials();
            if (runtimeItems == null)
            {
                runtimeItems = new GameObject("Runtime Items").transform;
                runtimeItems.SetParent(transform, false);
            }
            var alive = new HashSet<int>();
            foreach (var item in rules.State.FieldItems)
            {
                alive.Add(item.Id);
                if (!itemViews.TryGetValue(item.Id, out var view) || view == null)
                {
                    var typeIndex = (int)item.Type - 1; // ItemType values start at 1.
                    if (typeIndex < 0 || typeIndex >= itemMaterials.Length) typeIndex = 0;
                    view = DraftlineItemView.Create(runtimeItems, item, itemMaterials[typeIndex]);
                    itemViews[item.Id] = view;
                }
                view.SetBuried(rules.IsWall(item.Position));
            }
            var stale = new List<int>();
            foreach (var pair in itemViews)
                if (!alive.Contains(pair.Key) || pair.Value == null)
                    stale.Add(pair.Key);
            foreach (var id in stale)
            {
                if (itemViews[id] != null) Destroy(itemViews[id].gameObject);
                itemViews.Remove(id);
            }
        }

        private void SyncViews()
        {
            if (rules == null) return;
            var capturedEvents = rules.Events.Where(e => e.Type == MatchEventType.PieceCaptured).ToDictionary(e => e.PieceId, e => e);
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
                    view.BaseScale = visual.transform.localScale;
                    var appearance=visual.GetComponent<ThemePieceAppearance>();
                    foreach (var renderer in appearance!=null?appearance.FactionRenderers:visual.GetComponentsInChildren<Renderer>())
                        renderer.sharedMaterial = playerMaterials[(int)piece.Owner];
                    pieceViews.Add(piece.Id, view);
                }
                if (piece.OnBoard)
                {
                    view.gameObject.SetActive(true);
                    view.transform.position = BoardCoordinates.ToWorld(piece.Position);
                }
                else if (capturedEvents.TryGetValue(piece.Id, out var exitEvent))
                {
                    view.gameObject.SetActive(true);
                    view.transform.position = BoardCoordinates.ToWorld(exitEvent.Position);
                }
                else if (!exitingPieces.Contains(piece.Id))
                {
                    // Captured pieces stay visible while their exit animation owns them.
                    view.gameObject.SetActive(false);
                }
            }

            foreach (var pair in rotatingWallRenderers)
            {
                var visible = rules.IsWall(pair.Key);
                if (rotatingWallColliders.TryGetValue(pair.Key, out var collider) && collider != null)
                    collider.enabled = visible;
                AnimateWallTo(pair.Value, visible);
            }
            var centerVisible = rules.IsWall(new GridPosition(9, 9));
            if (centerWallCollider != null) centerWallCollider.enabled = centerVisible;
            AnimateWallTo(centerWallRenderer, centerVisible);

            gameUI?.RecordEvents(rules);
            foreach (var matchEvent in rules.Events)
            {
                var text = EventText(matchEvent);
                if (text != null)
                {
                    recentEvents.Insert(0, text);
                    if (recentEvents.Count > 12) recentEvents.RemoveAt(recentEvents.Count - 1);
                }
                switch (matchEvent.Type)
                {
                    case MatchEventType.TurnChanged:
                        sfx?.Play(SfxType.TurnChange, 0.6f);
                        ShowTurnPulse(matchEvent.Player);
                        break;
                    case MatchEventType.PieceCaptured:
                        sfx?.Play(SfxType.Capture);
                        cameraDirector?.Pulse(0.4f);
                        ShowEventFlash(matchEvent.Position);
                        if (Feedback != null) SpawnProfileEffect(Feedback.Capture, BoardCoordinates.ToWorld(matchEvent.Position,.85f),Feedback.CaptureScale,Feedback.CaptureLifetime);
                        if (pieceViews.TryGetValue(matchEvent.PieceId, out var captured) && captured != null)
                            StartCoroutine(AnimateCaptureOut(captured));
                        break;
                    case MatchEventType.PieceDeployed:
                        if (pieceViews.TryGetValue(matchEvent.PieceId, out var deployed) && deployed != null)
                        {
                            deployed.transform.localScale = Vector3.zero;
                            deployed.transform.DOScale(deployed.BaseScale, 0.28f).SetEase(Ease.OutBack);
                        }
                        break;
                    case MatchEventType.NoMoves:
                        if (matchEvent.PieceId >= 0 && pieceViews.TryGetValue(matchEvent.PieceId, out var stuck) && stuck != null)
                            stuck.transform.DOShakePosition(0.35f, 0.18f, 18, 90f);
                        break;
                    case MatchEventType.PortalUsed:
                        cameraDirector?.Pulse(0.3f);
                        ShowEventFlash(matchEvent.Position);
                        if (Feedback != null) SpawnProfileEffect(Feedback.Portal,BoardCoordinates.ToWorld(matchEvent.Position,.8f),Feedback.PortalScale,Feedback.PortalLifetime);
                        break;
                    case MatchEventType.WallRotated:
                        sfx?.Play(SfxType.WallFall, 0.55f);
                        DG.Tweening.DOVirtual.DelayedCall(0.12f,
                            () => sfx?.Play(SfxType.WallRise, 0.7f)).SetTarget(this);
                        cameraDirector?.Pulse(0.25f);
                        break;
                    case MatchEventType.ItemActivated:
                        sfx?.Play(SfxType.ItemActivate);
                        break;
                    case MatchEventType.Victory:
                        sfx?.Play(SfxType.Victory);
                        // 五连胜利：连线上的棋子依次发光弹跳。
                        var line = rules.FindFiveLine(matchEvent.Player);
                        if (line != null)
                        {
                            var order = 0;
                            foreach (var cell in line)
                            {
                                var piece = rules.State.Pieces.FirstOrDefault(candidate =>
                                    candidate.OnBoard && candidate.Position == cell);
                                if (piece == null ||
                                    !pieceViews.TryGetValue(piece.Id, out var lineView) || lineView == null)
                                    continue;
                                lineView.transform.DOPunchScale(Vector3.one * 0.35f, 0.5f, 6, 0.6f)
                                    .SetDelay(order++ * 0.13f);
                            }
                        }
                        var focus = line != null ? line.Select(cell => BoardCoordinates.ToWorld(cell)).Aggregate(Vector3.zero,(sum,p)=>sum+p)/line.Count
                            : BoardCoordinates.ToWorld(rules.Board.SpawnOf(matchEvent.Player));
                        if (!SimpleEffects) cameraDirector?.PlayVictory(focus);
                        if (Feedback != null) SpawnProfileEffect(Feedback.Victory,focus+Vector3.up*.8f,Feedback.VictoryScale,Feedback.VictoryLifetime);
                        break;
                    case MatchEventType.PlayerEliminated:
                        sfx?.Play(SfxType.Capture);
                        cameraDirector?.Pulse(0.6f);
                        break;
                    case MatchEventType.ItemCollected:
                        sfx?.Play(SfxType.ItemPickup);
                        foreach (var pair in itemViews)
                        {
                            if (pair.Value == null || pair.Value.Position != matchEvent.Position) continue;
                            var target = pieceViews.TryGetValue(matchEvent.PieceId, out var collector) && collector != null
                                ? collector.transform.position
                                : BoardCoordinates.ToWorld(matchEvent.Position, 1.1f);
                            var collectedView = pair.Value;
                            itemViews.Remove(pair.Key);
                            collectedView.Collect(target);
                            break;
                        }
                        break;
                }
            }
            if (lanRoom != null && lanRoom.IsHost)
                lanRoom.BroadcastState(rules.Events);
            rules.ClearEvents();
            SyncItemViews();
            RenderMoveHints();
            gameUI?.Refresh();
            warnings?.Refresh(rules, pieceViews);
        }

        private IEnumerator AnimateCaptureOut(DraftlinePieceView view)
        {
            if (!exitingPieces.Add(view.PieceId)) yield break;
            pendingVisuals++;
            view.transform.DOKill();
            // 先翻倒（像被碰倒的棋子），再缩没。
            var tip = view.transform.DORotate(new Vector3(88f, 0f, 0f), 0.32f).SetEase(Ease.InQuad);
            view.transform.DOMoveY(view.transform.position.y - 0.18f, 0.32f).SetEase(Ease.InQuad);
            yield return tip.WaitForCompletion();
            var tween = view.transform.DOScale(Vector3.zero, 0.25f).SetEase(Ease.InBack);
            yield return tween.WaitForCompletion();
            view.gameObject.SetActive(false);
            view.transform.localScale = view.BaseScale;
            view.transform.rotation = Quaternion.identity;
            exitingPieces.Remove(view.PieceId);
            pendingVisuals = Math.Max(0, pendingVisuals - 1);
            gameUI?.Refresh();
        }

        // Rotating/center walls rise and sink instead of popping. Logic (colliders, rules)
        // flips instantly; this is purely the visual transition.
        // 注意：pendingVisuals 的释放必须走 OnKill——OnComplete 在补间被 DOKill 提前杀掉时
        // 不会触发，会泄漏 pendingVisuals 导致输入永久锁定。
        private void AnimateWallTo(Renderer renderer, bool visible)
        {
            if (renderer == null || !wallVisualState.TryGetValue(renderer, out var current) || current == visible)
                return;
            wallVisualState[renderer] = visible;
            var basePosition = wallBasePosition[renderer];
            var baseScale = wallBaseScale[renderer];
            renderer.transform.DOKill();
            if (visible)
            {
                pendingVisuals++;
                renderer.enabled = true;
                renderer.transform.position = new Vector3(basePosition.x, basePosition.y - baseScale.y * 0.5f, basePosition.z);
                renderer.transform.localScale = new Vector3(baseScale.x, 0.05f, baseScale.z);
                var sequence = DOTween.Sequence().SetTarget(this);
                sequence.Append(renderer.transform.DOScaleY(baseScale.y, 0.32f).SetEase(Ease.OutBounce));
                sequence.Join(renderer.transform.DOMoveY(basePosition.y, 0.32f).SetEase(Ease.OutQuad));
                sequence.OnKill(() =>
                {
                    if (renderer == null) return;
                    renderer.transform.position = basePosition;
                    renderer.transform.localScale = baseScale;
                    pendingVisuals = Math.Max(0, pendingVisuals - 1);
                });
            }
            else
            {
                pendingVisuals++;
                var sequence = DOTween.Sequence().SetTarget(this);
                sequence.Append(renderer.transform.DOScaleY(0.05f, 0.18f).SetEase(Ease.InQuad));
                sequence.Join(renderer.transform.DOMoveY(basePosition.y - baseScale.y * 0.5f, 0.18f).SetEase(Ease.InQuad));
                sequence.OnKill(() =>
                {
                    if (renderer == null) return;
                    renderer.enabled = false;
                    if (renderer == null) return;
                    renderer.transform.position = basePosition;
                    renderer.transform.localScale = baseScale;
                    pendingVisuals = Math.Max(0, pendingVisuals - 1);
                });
            }
        }

        private void RenderMoveHints() => BoardInteraction?.Rebuild();

        // 换人：镜头不切走，在下一玩家出生角放一个同色光环脉冲。
        private void ShowTurnPulse(PlayerColor player)
        {
            var spawn = rules.Board.SpawnOf(player);
            SpawnRing(BoardCoordinates.ToWorld(spawn, 0.58f), Palette[(int)player], 1.1f, 2.4f);
        }

        // 传送门/吃子等事件：镜头不动，只在事件格放一个短闪光环。
        // CFXR 特效：prefab 在 Resources/FX 下（从 Cartoon FX Remaster 复制，引用关系保留）。
        private void SpawnProfileEffect(GameObject prefab,Vector3 position,float scale,float duration)
        {
            if (SimpleEffects || prefab == null || Feedback == null) return;
            while (activeEffects.Count > 0 && activeEffects.Peek() == null) activeEffects.Dequeue();
            while (activeEffects.Count >= Math.Max(1,Feedback.MaxConcurrentEffects))
            { var old=activeEffects.Dequeue(); if(old!=null)Destroy(old); }
            var effect=Instantiate(prefab,position,Quaternion.identity);
            effect.transform.localScale*=scale;
            activeEffects.Enqueue(effect);
            Destroy(effect,duration);
        }

        private void ShowEventFlash(GridPosition position)
        {
            SpawnRing(BoardCoordinates.ToWorld(position, 0.58f), new Color(1f, 1f, 1f), 0.5f, 1.5f);
        }

        private void SpawnRing(Vector3 position, Color color, float duration, float endScale)
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Event Ring";
            Destroy(ring.GetComponent<Collider>());
            ring.transform.position = position;
            ring.transform.localScale = new Vector3(0.5f, 0.02f, 0.5f);
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
            var material = new Material(shader);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.renderQueue = 3000;
            color.a = 0.85f;
            material.color = color;
            ring.GetComponent<Renderer>().sharedMaterial = material;
            DOTween.Sequence().SetTarget(this)
                .Append(ring.transform.DOScale(new Vector3(endScale, 0.02f, endScale), duration).SetEase(Ease.OutQuad))
                .Join(material.DOFade(0f, duration).SetEase(Ease.InQuad))
                .OnKill(() =>
                {
                    if (ring != null) Destroy(ring);
                    if (material != null) Destroy(material);
                });
        }

        // 对局记录只保留白话条目；流程类事件（选棋/换人/穿门）不显示。
        private static string EventText(MatchEvent matchEvent)
        {
            var player = PlayerName(matchEvent.Player);
            switch (matchEvent.Type)
            {
                case MatchEventType.DiceRolled: return $"{player}方掷出 {matchEvent.Value} 点";
                case MatchEventType.PieceDeployed: return $"{player}方出新棋（{matchEvent.Position}）";
                case MatchEventType.PieceSlid: return $"{player}棋 {matchEvent.Origin} → {matchEvent.Position}";
                case MatchEventType.PieceCaptured: return $"{player}棋被吃：{MatchExplanation.CaptureReason(matchEvent.Cause)}";
                case MatchEventType.ItemCollected: return $"{player}方拾取【{ItemName(matchEvent.Item)}】";
                case MatchEventType.ItemActivated: return $"{player}方使用【{ItemName(matchEvent.Item)}】";
                case MatchEventType.ItemSpawned: return $"出现道具【{ItemName(matchEvent.Item)}】";
                case MatchEventType.WallRotated: return "旋转墙切换";
                case MatchEventType.NoMoves: return $"{player}方无合法移动，回合跳过";
                case MatchEventType.PlayerEliminated: return $"{player}方出局";
                case MatchEventType.Victory: return $"{player}方获胜！";
                default: return null;
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
            if (rules == null || IsAnimating || (IsPaused && Session.Mode != GameMode.Lan)) return;
            if (remoteUpdates.Count > 0)
            {
                var update = remoteUpdates.Dequeue();
                ApplyRemoteUpdate(update.Item1, update.Item2);
                return;
            }
            if ((Session.Mode == GameMode.Lan && !lanRoom.IsHost) || rules.State.Phase == MatchPhase.GameOver || lanRoom.WaitingForDisconnectedPlayer) return;
            if (Session.Seat(rules.State.CurrentPlayer).Control != SeatControl.Computer) return;
            if (Time.unscaledTime < nextComputerAction) return;
            if (rules.State.Phase == MatchPhase.WaitRoll)
            {
                applyingRemoteCommand = true;
                try { Roll(); } finally { applyingRemoteCommand = false; }
                nextComputerAction = Time.unscaledTime + .4f;
                return;
            }
            if (computerTask == null)
            {
                computerRevision = rules.State.Revision;
                computerCancellation = new CancellationTokenSource();
                var token = computerCancellation.Token;
                var copy = rules.CloneForSimulation();
                var easy = Session.Seat(rules.State.CurrentPlayer).Difficulty == ComputerDifficulty.Easy;
                computerTask = Task.Run(() => ComputerPlanner.Choose(copy, easy, token), token);
                return;
            }
            if (!computerTask.IsCompleted) return;
            var command = !computerTask.IsCanceled && !computerTask.IsFaulted ? computerTask.Result : null;
            if (computerTask.IsFaulted) Debug.LogException(computerTask.Exception);
            computerTask = null;
            computerCancellation?.Dispose(); computerCancellation = null;
            if (command != null && computerRevision == rules.State.Revision)
            {
                applyingRemoteCommand = true;
                try { SubmitCommand(command); } finally { applyingRemoteCommand = false; }
            }
            nextComputerAction = Time.unscaledTime + (SimpleEffects ? .12f : .4f);
        }

        private void Roll()
        {
            if (rules == null || rules.State.Phase != MatchPhase.WaitRoll || InputLocked || !CanControl) return;
            if (lanRoom.IsClientOnly) { lanRoom.SendRoll(); return; }
            StartCoroutine(RollPhysicalDie());
        }

        private IEnumerator RollPhysicalDie()
        {
            var generation = lifetime;
            animating = true;
            cameraDirector?.ShowDice(); sfx?.Play(SfxType.DiceRoll);
            if (lanRoom.IsHost) lanRoom.BroadcastDiceStart();
            int? result = null;
            yield return diceRoller.Roll(value => result = value);
            if (generation != lifetime) yield break;
            cameraDirector?.ShowBoard();
            animating = false;
            if (result.HasValue)
            {
                sfx?.Play(SfxType.DiceLand);
                var command = new MatchCommand { Kind = MatchCommandKind.ApplyDice, Dice = result.Value, ExpectedRevision = rules.State.Revision };
                ExecuteAuthorityCommand(command);
            }
            else { ShowError("骰子未停稳，请再投一次。"); lanRoom.BroadcastState(Array.Empty<MatchEvent>(), true); }
            gameUI?.Refresh(); BoardInteraction.Rebuild();
        }

        public bool SubmitCommand(MatchCommand command)
        {
            if (!CanControl || InputLocked || rules == null) return false;
            if (command.Kind == MatchCommandKind.ApplyDice) { ShowError("骰子结果只能由权威投掷产生。"); return false; }
            if (lanRoom.IsClientOnly) { lanRoom.SendCommand(command); return true; }
            return ExecuteAuthorityCommand(command);
        }

        private bool ExecuteAuthorityCommand(MatchCommand command)
        {
            if (!MatchCommandDispatcher.TryExecute(rules, rules.State.CurrentPlayer, command, out var next, out var error))
            { ShowError(MatchExplanation.FriendlyError(error)); return false; }
            rules = next;
            setupError = null;
            BoardInteraction.StateChanged();
            var slide = rules.Events.FirstOrDefault(e => e.Type == MatchEventType.PieceSlid);
            if (lanRoom.IsHost) lanRoom.BroadcastState(rules.Events);
            if (slide != null && pieceViews.TryGetValue(slide.PieceId, out var view)) StartCoroutine(AnimateSlide(view, slide.Slide));
            else SyncViews();
            return true;
        }

        private IEnumerator AnimateSlide(DraftlinePieceView view, SlidePlan path)
        {
            var generation = lifetime;
            animating = true;
            BoardInteraction.ClearVisuals();
            var sequence = DOTween.Sequence().SetTarget(this);
            sfx?.Play(SfxType.Slide);
            foreach (var step in path.Steps)
            {
                var destination = BoardCoordinates.ToWorld(step.Position);
                if (step.PortalExit)
                {
                    var exitDestination = destination;
                    sequence.Append(view.transform.DOScale(Vector3.zero, .1f));
                    sequence.AppendCallback(() => { if (view != null) view.transform.position = exitDestination; });
                    sequence.AppendCallback(() => sfx?.Play(SfxType.Portal));
                    sequence.Append(view.transform.DOScale(view.BaseScale, .14f).SetEase(Ease.OutBack));
                }
                else sequence.Append(view.transform.DOMove(destination, SimpleEffects ? .025f : .055f).SetEase(Ease.Linear));
                if (step.Hit) sequence.Append(view.transform.DOPunchScale(view.BaseScale * .25f, .16f));
            }
            yield return sequence.WaitForCompletion();
            if (generation != lifetime || view == null) yield break;
            animating = false;
            SyncViews();
            BoardInteraction.Rebuild();
        }

        public bool AcceptRemoteCommand(PlayerColor actor, MatchCommand command, bool roll)
        {
            if (!lanRoom.IsHost || rules == null || rules.State.CurrentPlayer != actor || InputLocked) return false;
            applyingRemoteCommand = true;
            try { if (roll) { if (rules.State.Phase != MatchPhase.WaitRoll) return false; Roll(); return true; } return SubmitCommand(command); }
            finally { applyingRemoteCommand = false; }
        }

        public void ReceiveNetworkUpdate(MatchState state, MatchEvent[] events)
        {
            if (IsAnimating) { remoteUpdates.Enqueue(Tuple.Create(state, events)); return; }
            ApplyRemoteUpdate(state, events);
        }

        private void ApplyRemoteUpdate(MatchState state, MatchEvent[] events)
        {
            if (!lanRoom.IsClientOnly) return;
            if (rules == null) { UpdatePieceTemplate(DraftlineThemeApplier.Load(DraftlineThemeApplier.CurrentId)); CreatePieceMaterials(); cameraDirector.ResetForMatch(); }
            rules = MatchRules.FromSnapshot(state);
            rules.ReplaceEvents(events);
            BoardInteraction.StateChanged();
            var slide = events.FirstOrDefault(e => e.Type == MatchEventType.PieceSlid);
            if (slide != null && pieceViews.TryGetValue(slide.PieceId, out var view))
            {
                view.transform.position = BoardCoordinates.ToWorld(slide.Origin);
                StartCoroutine(AnimateSlide(view, slide.Slide));
            }
            else SyncViews();
            gameUI.Refresh();
        }

        public void PlayNetworkDice() { if (lanRoom.IsClientOnly) StartCoroutine(NetworkDicePresentation()); }
        private IEnumerator NetworkDicePresentation()
        {
            cameraDirector.ShowDice();
            yield return new WaitForSeconds(.6f);
            cameraDirector.ShowBoard();
        }
        public void ApplyNetworkSnapshot(MatchState snapshot) => ReceiveNetworkUpdate(snapshot, Array.Empty<MatchEvent>());
        public Vector3? PieceViewPosition(int id) => pieceViews.TryGetValue(id, out var view) && view != null ? view.transform.position : (Vector3?)null;
        public void BeginLocalMatch() => StartLocalMatch();
        public void BeginNetworkMatch(int activeMask)
        {
            StartMatch(Enumerable.Range(0,4).Where(i => (activeMask & (1 << i)) != 0).Select(i => (PlayerColor)i).ToArray());
        }
        public void RollFromUI() => Roll();
        public void SelectFromUI(int id) => BoardInteraction.Select(id);
        public void DeployFromUI() { if (rules != null) SubmitCommand(new MatchCommand { Kind = MatchCommandKind.Deploy, ExpectedRevision = rules.State.Revision }); }
        public void MoveFromUI(SlideDirection direction) => BoardInteraction.ChooseDirection(direction);
        public void StopFromUI(SlideDirection direction, int index)
        {
            var id = BoardInteraction.SelectedPieceId;
            if (!id.HasValue || rules == null) return;
            SubmitCommand(new MatchCommand { Kind = MatchCommandKind.Slide, PieceId = id.Value, Direction = direction,
                StopIndex = index, Item = BoardInteraction.PendingItem.HasValue ? (int)BoardInteraction.PendingItem.Value : 0, ExpectedRevision = rules.State.Revision });
        }
        public void UseItemFromUI(ItemType item) => BoardInteraction.ToggleItem(item);
        public void ToggleCameraFromUI() { cameraDirector.ToggleManualTopDown(); gameUI.Refresh(); }
    }
}

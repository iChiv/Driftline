using System.Collections;
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
        private readonly List<GameObject> hintObjects = new List<GameObject>();
        private readonly bool[] selectedColors = { true, true, true, true };

        private MatchRules rules;
        private GameObject pieceTemplate;
        private Transform runtimePieces;
        private Renderer centerWallRenderer;
        private Collider centerWallCollider;
        private Material[] playerMaterials;
        private Material[] itemMaterials;
        private Material hintMaterial;
        private DraftlineWarnings warnings;
        private bool animating;
        private float animatingSince;
        private int pendingVisuals;
        private readonly HashSet<int> exitingPieces = new HashSet<int>();
        private readonly Dictionary<Renderer, bool> wallVisualState = new Dictionary<Renderer, bool>();
        private readonly Dictionary<Renderer, Vector3> wallBasePosition = new Dictionary<Renderer, Vector3>();
        private readonly Dictionary<Renderer, Vector3> wallBaseScale = new Dictionary<Renderer, Vector3>();
        private SlideDirection? pendingStopDirection;
        private string setupError;
        private PhysicalDiceRoller diceRoller;
        private DraftlineCameraDirector cameraDirector;
        private DraftlineGameUI gameUI;
        private DraftlineLanRoom lanRoom;
        private DraftlineAudio sfx;
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
        public bool CanControl => applyingRemoteCommand || lanRoom == null || !lanRoom.IsNetworked ||
                                  lanRoom.LocalColor == rules?.State.CurrentPlayer;

        private void Awake()
        {
            Application.runInBackground = true; // Also needed for a future LAN host.
            DraftlineThemeApplier.Apply(DraftlineThemeApplier.CurrentId);
            var piecesRoot = GameObject.Find("Pieces");
            if (piecesRoot != null)
            {
                // 优先用兵棋人仔模板（场景可编辑），没有则退回扁平圆片。
                var source = piecesRoot.transform.Find("Red/Pawn");
                if (source == null) source = piecesRoot.transform.Find("Red/Cylinder");
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
            warnings = gameObject.GetComponent<DraftlineWarnings>() ??
                       gameObject.AddComponent<DraftlineWarnings>();
            sfx = gameObject.GetComponent<DraftlineAudio>() ??
                  gameObject.AddComponent<DraftlineAudio>();
            if (gameObject.GetComponent<DraftlineTabletopInteraction>() == null)
                gameObject.AddComponent<DraftlineTabletopInteraction>();
            if (gameObject.GetComponent<DraftlineBoardAmbience>() == null)
                gameObject.AddComponent<DraftlineBoardAmbience>();
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
            DraftlineThemeApplier.Apply(DraftlineThemeApplier.CurrentId);
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
            exitingPieces.Clear();
            pendingVisuals = 0;
            foreach (var view in pieceViews.Values)
                if (view != null)
                {
                    view.transform.DOKill();
                    Destroy(view.gameObject);
                }
            pieceViews.Clear();
            foreach (var itemView in itemViews.Values)
                if (itemView != null) Destroy(itemView.gameObject);
            itemViews.Clear();
            warnings?.ClearAll();
            CreatePieceMaterials();
            SyncViews();
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
                    foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
                        renderer.sharedMaterial = playerMaterials[(int)piece.Owner];
                    pieceViews.Add(piece.Id, view);
                }
                if (piece.OnBoard)
                {
                    view.gameObject.SetActive(true);
                    view.transform.position = BoardCoordinates.ToWorld(piece.Position);
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
                        SpawnFx("FX/CaptureExplosion",
                            BoardCoordinates.ToWorld(matchEvent.Position, 1.0f), 2.5f);
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
                        break;
                    case MatchEventType.WallRotated:
                        sfx?.Play(SfxType.WallFall, 0.55f);
                        DG.Tweening.DOVirtual.DelayedCall(0.12f,
                            () => sfx?.Play(SfxType.WallRise, 0.7f));
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
                        var focus = Vector3.zero;
                        if (rules.State.SelectedPieceId.HasValue &&
                            pieceViews.TryGetValue(rules.State.SelectedPieceId.Value, out var winnerView) &&
                            winnerView != null)
                            focus = winnerView.transform.position;
                        cameraDirector?.PlayVictory(focus);
                        // 胜利烟花：棋盘四角方向 staggered 三发
                        SpawnFx("FX/VictoryFirework", focus + new Vector3(0f, 0.5f, 0f), 3.5f);
                        DG.Tweening.DOVirtual.DelayedCall(0.4f, () =>
                            SpawnFx("FX/VictoryFirework", focus + new Vector3(2.5f, 0.5f, -2f), 3.5f));
                        DG.Tweening.DOVirtual.DelayedCall(0.8f, () =>
                            SpawnFx("FX/VictoryFirework", focus + new Vector3(-2.5f, 0.5f, 2f), 3.5f));
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
            pendingVisuals--;
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
                var sequence = DOTween.Sequence();
                sequence.Append(renderer.transform.DOScaleY(baseScale.y, 0.32f).SetEase(Ease.OutBounce));
                sequence.Join(renderer.transform.DOMoveY(basePosition.y, 0.32f).SetEase(Ease.OutQuad));
                sequence.OnKill(() =>
                {
                    renderer.transform.position = basePosition;
                    renderer.transform.localScale = baseScale;
                    pendingVisuals--;
                });
            }
            else
            {
                pendingVisuals++;
                var sequence = DOTween.Sequence();
                sequence.Append(renderer.transform.DOScaleY(0.05f, 0.18f).SetEase(Ease.InQuad));
                sequence.Join(renderer.transform.DOMoveY(basePosition.y - baseScale.y * 0.5f, 0.18f).SetEase(Ease.InQuad));
                sequence.OnKill(() =>
                {
                    renderer.enabled = false;
                    renderer.transform.position = basePosition;
                    renderer.transform.localScale = baseScale;
                    pendingVisuals--;
                });
            }
        }

        private void RenderMoveHints()
        {
            foreach (var marker in hintObjects)
                if (marker != null) Destroy(marker);
            hintObjects.Clear();
            if (rules == null || rules.State.Phase != MatchPhase.Moving ||
                !rules.State.SelectedPieceId.HasValue || InputLocked) return;
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
                var targetScale = new Vector3(0.54f, 0.025f, 0.54f);
                marker.transform.localScale = targetScale * 0.4f;
                marker.transform.DOScale(targetScale, 0.14f).SetEase(Ease.OutBack);
                marker.GetComponent<Renderer>().sharedMaterial = hintMaterial;
                Destroy(marker.GetComponent<Collider>());
                hintObjects.Add(marker);
            }
        }

        // 换人：镜头不切走，在下一玩家出生角放一个同色光环脉冲。
        private void ShowTurnPulse(PlayerColor player)
        {
            var spawn = rules.Board.SpawnOf(player);
            SpawnRing(BoardCoordinates.ToWorld(spawn, 0.58f), Palette[(int)player], 1.1f, 2.4f);
        }

        // 传送门/吃子等事件：镜头不动，只在事件格放一个短闪光环。
        // CFXR 特效：prefab 在 Resources/FX 下（从 Cartoon FX Remaster 复制，引用关系保留）。
        private void SpawnFx(string resourcePath, Vector3 position, float destroyAfter)
        {
            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null) return;
            var fx = Instantiate(prefab, position, Quaternion.identity);
            Destroy(fx, destroyAfter);
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
            ring.transform.DOScale(new Vector3(endScale, 0.02f, endScale), duration).SetEase(Ease.OutQuad);
            material.DOFade(0f, duration).SetEase(Ease.InQuad)
                .OnComplete(() =>
                {
                    Destroy(ring);
                    Destroy(material);
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
                case MatchEventType.PieceCaptured: return $"{player}棋被吃（{matchEvent.Position}）";
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
            // 看门狗：表现层异常卡死时强制释放输入锁（正常对局不会触发）。
            if (animating && !IsDiceRolling && Time.unscaledTime - animatingSince > 4f)
            {
                Debug.LogWarning("[Draftline] animation watchdog released a stuck lock.");
                animating = false;
                pendingVisuals = 0;
                SyncViews();
            }
            if (rules == null || InputLocked || !CanControl) return;
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
            if (rules == null || rules.State.Phase != MatchPhase.WaitRoll || InputLocked) return;
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
            animatingSince = Time.unscaledTime;
            cameraDirector?.ShowDice();
            sfx?.Play(SfxType.DiceRoll);
            if (lanRoom != null && lanRoom.IsHost) lanRoom.BroadcastDiceStart();
            int? result = null;
            var rolling = StartCoroutine(diceRoller.Roll(value => result = value));
            gameUI?.Refresh();
            yield return rolling;
            cameraDirector?.ShowBoard();
            if (result.HasValue)
            {
                sfx?.Play(SfxType.DiceLand);
                if (rules.TryApplyDice(result.Value, out _)) SyncViews();
            }
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
            animatingSince = Time.unscaledTime;
            foreach (var marker in hintObjects)
                if (marker != null) Destroy(marker);
            hintObjects.Clear();
            // The camera never follows slides; the 72° overview keeps everything visible.

            var sequence = DOTween.Sequence();
            sfx?.Play(SfxType.Slide);
            foreach (var step in path.Steps)
            {
                var destination = BoardCoordinates.ToWorld(step.Position);
                if (step.PortalExit)
                {
                    var exitDestination = destination;
                    sequence.Append(view.transform.DOScale(Vector3.zero, 0.12f).SetEase(Ease.InQuad));
                    sequence.AppendCallback(() => view.transform.position = exitDestination);
                    sequence.AppendCallback(() => sfx?.Play(SfxType.Portal));
                    sequence.Append(view.transform.DOScale(view.BaseScale, 0.18f).SetEase(Ease.OutBack));
                    continue;
                }
                sequence.Append(view.transform.DOMove(destination, 0.07f).SetEase(Ease.OutQuad));
                if (step.BrokeWall)
                    sequence.Append(view.transform.DOPunchScale(view.BaseScale * 0.25f, 0.18f, 6, 0.6f));
                if (step.Hit)
                    sequence.Append(view.transform.DOPunchScale(view.BaseScale * 0.35f, 0.2f, 8, 0.7f));
            }
            yield return sequence.WaitForCompletion();

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
            animatingSince = Time.unscaledTime;
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
            if (rules != null && !InputLocked && rules.TrySelectPiece(pieceId, out _)) SyncViews();
        }
        public void DeployFromUI()
        {
            if (lanRoom != null && lanRoom.IsNetworked && !CanControl) return;
            if (lanRoom != null && lanRoom.IsClientOnly)
            {
                lanRoom.SendCommand("deploy");
                return;
            }
            if (rules != null && !InputLocked && rules.TryDeploy(out _, out _)) SyncViews();
        }
        public void MoveFromUI(SlideDirection direction)
        {
            if (lanRoom != null && lanRoom.IsNetworked && !CanControl) return;
            if (lanRoom != null && lanRoom.IsClientOnly)
            {
                lanRoom.SendCommand("move", (int)direction);
                return;
            }
            if (rules != null && !InputLocked) SubmitDirection(direction);
        }
        public void StopFromUI(SlideDirection direction, int index)
        {
            if (lanRoom != null && lanRoom.IsNetworked && !CanControl) return;
            if (lanRoom != null && lanRoom.IsClientOnly)
            {
                lanRoom.SendCommand("stop", (int)direction, index);
                return;
            }
            if (rules != null && !InputLocked) ExecuteSlide(direction, index);
        }
        public void UseItemFromUI(ItemType item)
        {
            if (lanRoom != null && lanRoom.IsNetworked && !CanControl) return;
            if (lanRoom != null && lanRoom.IsClientOnly)
            {
                lanRoom.SendCommand("item", (int)item);
                return;
            }
            if (rules != null && !InputLocked && rules.TryActivateItem(item, out _)) SyncViews();
        }
        public void ToggleCameraFromUI()
        {
            cameraDirector?.ToggleManualTopDown();
            gameUI?.Refresh();
        }

    }
}

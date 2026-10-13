using System.Collections.Generic;
using DG.Tweening;
using Draftline.Core;
using UnityEngine;

namespace Draftline.Unity
{
    /// <summary>
    /// Board-side warnings (V0.3 §7): gold pulse for exact four-runs, red pulse for blocks
    /// that the next wall flip would kill (red wins over gold), plus translucent "ghost
    /// wall" holograms on rotating slots that will rise with the next phase.
    /// </summary>
    public sealed class DraftlineWarnings : MonoBehaviour
    {
        private static readonly Color Gold = new Color(1.0f, 0.85f, 0.3f);
        private static readonly Color Danger = new Color(1.0f, 0.25f, 0.25f);

        private readonly Dictionary<GridPosition, GameObject> ghosts =
            new Dictionary<GridPosition, GameObject>();
        private readonly Dictionary<GridPosition, GameObject> bases =
            new Dictionary<GridPosition, GameObject>();
        private readonly HashSet<GridPosition> suppressedSlots = new HashSet<GridPosition>();
        private HashSet<int> threatIds = new HashSet<int>();
        private HashSet<int> wallKillIds = new HashSet<int>();
        private Material ghostMaterial;
        private Material baseMaterial;
        private Material suppressedMaterial;
        private MaterialPropertyBlock propertyBlock;
        private BoardLayout layout;
        private float ghostAlphaSign = 1f;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            layout = new BoardLayout();
        }

        public void Refresh(MatchRules rules, IReadOnlyDictionary<int, DraftlinePieceView> pieceViews)
        {
            if (rules == null)
            {
                threatIds = new HashSet<int>();
                wallKillIds = new HashSet<int>();
                RefreshGhosts(null, pieceViews);
                return;
            }
            threatIds = rules.State.Phase == MatchPhase.GameOver ? new HashSet<int>() : rules.FindFourRuns();
            wallKillIds = rules.State.Phase == MatchPhase.GameOver ? new HashSet<int>() : rules.FindWallKillPreview();
            RefreshGhosts(rules, pieceViews);
        }

        private void RefreshGhosts(MatchRules rules, IReadOnlyDictionary<int, DraftlinePieceView> pieceViews)
        {
            if (ghostMaterial == null) ghostMaterial = CreateGhostMaterial();
            if (baseMaterial == null) baseMaterial = CreateSolidMaterial(new Color(0.13f, 0.17f, 0.23f));
            if (suppressedMaterial == null) suppressedMaterial = CreateSolidMaterial(new Color(0.75f, 0.25f, 0.25f));
            var nextSuppressed = new HashSet<GridPosition>();
            foreach (var slot in layout.RotatingSlots)
            {
                var up = rules != null && rules.IsWall(slot);
                var occupied = rules != null && rules.PieceAt(slot) != null;
                var scheduledNow = layout.IsScheduledWall(slot, rules != null ? rules.State.WallPhase : 0);
                var suppressed = rules != null && rules.State.Phase != MatchPhase.GameOver &&
                                 scheduledNow && occupied; // 棋子压住了本应升起的墙
                var showGhost = rules != null && rules.State.Phase != MatchPhase.GameOver &&
                                !up && !occupied &&
                                layout.IsScheduledWall(slot, rules.State.WallPhase ^ 1);

                // 常显底座：墙升起时隐藏（实体墙盖住），其余时候都在，被压制时换红脉冲。
                var showBase = !up;
                if (showBase && !bases.TryGetValue(slot, out var socket))
                {
                    socket = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    socket.name = $"Slot base {slot}";
                    Destroy(socket.GetComponent<Collider>());
                    socket.transform.SetParent(transform, false);
                    socket.transform.position = BoardCoordinates.ToWorld(slot, 0.56f);
                    socket.transform.localScale = new Vector3(0.96f, 0.06f, 0.96f);
                    socket.GetComponent<Renderer>().sharedMaterial = baseMaterial;
                    bases.Add(slot, socket);
                }
                if (!showBase && bases.TryGetValue(slot, out var staleSocket))
                {
                    staleSocket.transform.DOKill();
                    bases.Remove(slot);
                    if (staleSocket != null) Destroy(staleSocket);
                }
                if (showBase)
                {
                    var socketRenderer = bases[slot].GetComponent<Renderer>();
                    socketRenderer.sharedMaterial = suppressed ? suppressedMaterial : baseMaterial;
                    if (suppressed && !suppressedSlots.Contains(slot))
                        bases[slot].transform.DOScale(new Vector3(0.8f, 0.06f, 0.8f), 0.45f)
                            .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
                    else if (!suppressed && suppressedSlots.Contains(slot))
                    {
                        bases[slot].transform.DOKill();
                        bases[slot].transform.localScale = new Vector3(0.96f, 0.06f, 0.96f);
                    }
                }
                if (suppressed) nextSuppressed.Add(slot);

                if (showGhost && !ghosts.ContainsKey(slot))
                {
                    var ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    ghost.name = $"Ghost wall {slot}";
                    Destroy(ghost.GetComponent<Collider>());
                    ghost.transform.SetParent(transform, false);
                    ghost.transform.position = BoardCoordinates.ToWorld(slot, 1f);
                    ghost.transform.localScale = new Vector3(0.92f, 0.9f, 0.92f);
                    ghost.GetComponent<Renderer>().sharedMaterial = ghostMaterial;
                    ghosts.Add(slot, ghost);
                }
                else if (!showGhost && ghosts.TryGetValue(slot, out var stale))
                {
                    ghosts.Remove(slot);
                    if (stale != null) Destroy(stale);
                }
            }
            suppressedSlots.Clear();
            suppressedSlots.UnionWith(nextSuppressed);
        }

        private void Update()
        {
            // Pulse flagged pieces per-renderer via MaterialPropertyBlock so the shared
            // player materials stay untouched. Red (wall-kill) overrides gold (four-run).
            var pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 5f);
            var controller = GetComponent<DraftlineGameController>();
            if (controller == null || controller.Rules == null) return;
            foreach (var pair in controller.PieceViews)
            {
                var view = pair.Value;
                if (view == null || !view.gameObject.activeSelf) continue;
                var renderer = view.GetComponent<Renderer>();
                if (renderer == null) continue;
                Color? emission = null;
                if (wallKillIds.Contains(pair.Key)) emission = Danger * (0.6f + 1.4f * pulse);
                else if (threatIds.Contains(pair.Key)) emission = Gold * (0.5f + 1.2f * pulse);
                if (emission.HasValue)
                {
                    propertyBlock.SetColor("_EmissionColor", emission.Value);
                    renderer.SetPropertyBlock(propertyBlock);
                }
                else
                {
                    renderer.SetPropertyBlock(null);
                }
            }
            // Ghost walls breathe.
            if (ghostMaterial != null && ghosts.Count > 0)
            {
                var color = ghostMaterial.color;
                color.a = 0.22f + 0.13f * Mathf.Sin(Time.time * 3.2f * ghostAlphaSign);
                ghostMaterial.color = color;
            }
        }

        public void ClearAll()
        {
            threatIds = new HashSet<int>();
            wallKillIds = new HashSet<int>();
            suppressedSlots.Clear();
            foreach (var ghost in ghosts.Values)
                if (ghost != null) Destroy(ghost);
            ghosts.Clear();
            foreach (var socket in bases.Values)
                if (socket != null)
                {
                    socket.transform.DOKill();
                    Destroy(socket);
                }
            bases.Clear();
        }

        private static Material CreateSolidMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            return material;
        }

        private static Material CreateGhostMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
            var material = new Material(shader);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.renderQueue = 3000;
            material.color = new Color(0.6f, 0.72f, 0.85f, 0.28f);
            return material;
        }
    }
}

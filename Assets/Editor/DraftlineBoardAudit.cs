using System.Collections.Generic;
using System.Linq;
using Draftline.Core;
using Draftline.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Draftline.EditorTools
{
    /// <summary>
    /// One-off geometry audit for SampleScene: compares the Walls root against the
    /// authoritative BoardLayout rows (same map as the HTML prototype). Scene walls are
    /// multi-cell blocks, so coverage is computed from renderer bounds, not pivots.
    /// </summary>
    public static class DraftlineBoardAudit
    {
        private static readonly string[] WallRoots = { "Walls", "Border" };

        [MenuItem("Draftline/Board Audit Debug Materials")]
        public static void DebugMaterials()
        {
            var root = GameObject.Find("Walls");
            if (root == null) return;
            var count = 0;
            foreach (Transform child in root.transform)
            {
                if (!child.name.StartsWith("Wall (audit fix)")) continue;
                if (count++ >= 3) break;
                var renderer = child.GetComponent<Renderer>();
                var material = renderer != null ? renderer.sharedMaterial : null;
                var color = material != null && material.HasProperty("_BaseColor")
                    ? material.GetColor("_BaseColor").ToString()
                    : material != null && material.HasProperty("_Color")
                        ? material.GetColor("_Color").ToString()
                        : "n/a";
                Debug.Log($"[BoardAudit] {child.name} active={child.gameObject.activeSelf} " +
                          $"rendererEnabled={renderer != null && renderer.enabled} " +
                          $"material={(material != null ? material.name : "null")} color={color} " +
                          $"pos={child.position} scale={child.localScale} layer={child.gameObject.layer}");
            }
            if (count == 0) Debug.Log("[BoardAudit] no audit-fix walls found in scene.");
            // Also log the donor candidate (largest wall block).
            var largest = root.transform.Cast<Transform>()
                .Select(child => child.GetComponent<Renderer>())
                .Where(renderer => renderer != null)
                .OrderByDescending(renderer => renderer.bounds.size.sqrMagnitude)
                .FirstOrDefault();
            if (largest != null)
            {
                var material = largest.sharedMaterial;
                Debug.Log($"[BoardAudit] largest wall block: {largest.name} material={(material != null ? material.name : "null")} bounds={largest.bounds}");
            }
        }

        [MenuItem("Draftline/Board Audit Debug Roots")]
        public static void DebugRoots()
        {
            foreach (var rootName in WallRoots)
            {
                var root = GameObject.Find(rootName);
                if (root == null) continue;
                foreach (Transform child in root.transform)
                {
                    var renderer = child.GetComponent<Renderer>();
                    if (renderer == null) continue;
                    var cells = CellsCoveredBy(renderer.bounds);
                    Debug.Log($"[BoardAudit] {rootName}/{child.name} center={renderer.bounds.center} size={renderer.bounds.size} cells={cells.Count}");
                }
            }
        }

        [MenuItem("Draftline/Debug UI State")]
        public static void DebugUiState()
        {
            var doc = Object.FindObjectOfType<UIDocument>();
            if (doc == null)
            {
                Debug.LogWarning("[DebugUI] no UIDocument in scene.");
                return;
            }
            var root = doc.rootVisualElement;
            Debug.Log($"[DebugUI] children={root.childCount} draftline-root={(root.Q("draftline-root") != null)} " +
                      $"setup={(root.Q("setup") != null)} hud={(root.Q("hud") != null)}");
        }

        [MenuItem("Draftline/Debug Breakwall Setup")]
        public static void DebugBreakwallSetup()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Debug] Enter play mode first.");
                return;
            }
            var controller = Object.FindObjectOfType<DraftlineGameController>();
            if (controller == null || controller.Rules == null)
            {
                Debug.LogWarning("[Debug] no running match.");
                return;
            }
            var rules = controller.Rules;
            var current = rules.State.CurrentPlayer;
            var layout = rules.Board;
            // 找一个当前升起的旋转墙槽位，把当前玩家一枚场上棋放到它旁边的普通格。
            foreach (var slot in layout.RotatingSlots)
            {
                if (!rules.IsWall(slot)) continue;
                foreach (var dir in new[] { new GridPosition(1, 0), new GridPosition(-1, 0),
                                            new GridPosition(0, 1), new GridPosition(0, -1) })
                {
                    var neighbor = new GridPosition(slot.Column + dir.Column, slot.Row + dir.Row);
                    if (!BoardLayout.Contains(neighbor)) continue;
                    if (layout.CellAt(neighbor).Kind != CellKind.Floor) continue;
                    if (rules.PieceAt(neighbor) != null) continue;
                    var piece = rules.State.Pieces.Find(p => p.OnBoard && p.Owner == current);
                    if (piece == null) continue;
                    piece.Position = neighbor;
                    rules.State.Players[(int)current].Items.Add(ItemType.BreakWall);
                    Debug.Log($"[Debug] breakwall setup: piece {piece.Id} at {neighbor}, wall slot {slot}");
                    return;
                }
            }
            Debug.LogWarning("[Debug] no risen rotating wall slot available.");
        }

        [MenuItem("Draftline/Debug Input State")]
        public static void DebugInputState()
        {
            var controller = Object.FindObjectOfType<DraftlineGameController>();
            if (controller == null) return;
            var pending = typeof(DraftlineGameController)
                .GetField("pendingVisuals", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(controller);
            Debug.Log($"[Debug] IsAnimating={controller.IsAnimating} pendingVisuals={pending} " +
                      $"phase={controller.Rules?.State.Phase} remaining={controller.Rules?.State.RemainingSlides} " +
                      $"diceRolling={controller.IsDiceRolling} diceStatus={controller.DiceStatus}");
        }

        [MenuItem("Draftline/Repair Toy Props")]
        public static void RepairToyProps()
        {
            foreach (var propName in new[] { "Toy Block Tower", "Toy Car", "Toy Spinning Top", "Toy Hourglass" })
            {
                var prop = GameObject.Find(propName);
                if (prop == null)
                {
                    Debug.LogWarning($"[Repair] {propName} not found");
                    continue;
                }
                var removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(prop);
                var had = prop.GetComponent<DraftlineToyProp>() != null;
                if (!had) Undo.AddComponent<DraftlineToyProp>(prop);
                Debug.Log($"[Repair] {propName}: removed={removed} hadValid={had} now={prop.GetComponent<DraftlineToyProp>() != null}");
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[Repair] done, scene saved.");
        }

        [MenuItem("Draftline/Scan Missing Scripts")]
        public static void ScanMissingScripts()
        {
            var count = 0;
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    foreach (var component in transform.GetComponents<Component>())
                    {
                        if (component != null) continue;
                        count++;
                        Debug.LogWarning($"[ScanMissing] {GetPath(transform)} has a missing script.");
                    }
                }
            }
            Debug.Log($"[ScanMissing] total {count} missing components.");
        }

        private static string GetPath(Transform transform)
        {
            var path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
        }

        [MenuItem("Draftline/Board Audit (Log Only)")]
        public static void Audit()
        {
            Collect(out var missing, out var covering);
            Debug.Log($"[BoardAudit] missing fixed/slot walls ({missing.Count}): {string.Join(" ", missing.Select(p => p.ToString()))}");
            Debug.Log($"[BoardAudit] portals covered by walls ({covering.Count}): {string.Join(" ", covering.Select(p => p.ToString()))}");
        }

        [MenuItem("Draftline/Board Audit Fix (Minimal)")]
        public static void Fix()
        {
            var layout = new BoardLayout();
            var rebuilt = 0;
            var removedBlocks = 0;
            var rebuiltCells = new HashSet<GridPosition>();
            var donors = new List<Renderer>();

            // 0) Self-healing: drop unit walls spawned by earlier fix runs so the fix is idempotent.
            foreach (var rootName in WallRoots)
            {
                var root = GameObject.Find(rootName);
                if (root == null) continue;
                var stale = root.transform.Cast<Transform>()
                    .Where(child => child.name.StartsWith("Wall (audit fix)"))
                    .ToList();
                foreach (var child in stale)
                    Undo.DestroyObjectImmediate(child.gameObject);
                donors.AddRange(root.transform.Cast<Transform>()
                    .Select(child => child.GetComponent<Renderer>())
                    .Where(renderer => renderer != null)
                    .Where(renderer => !renderer.name.StartsWith("Wall (audit fix)")));
            }
            // Donor for material/height: the largest wall block (orange frame/blocks,
            // not the small dark accent cubes).
            var donor = donors.OrderByDescending(renderer => renderer.bounds.size.sqrMagnitude)
                .FirstOrDefault();

            // 1) Blocks that bury portal cells: rebuild them as per-cell unit walls that
            //    only cover their legitimate wall cells, then delete the original block.
            foreach (var rootName in WallRoots)
            {
                var root = GameObject.Find(rootName);
                if (root == null) continue;
                var offenders = root.transform.Cast<Transform>()
                    .Select(child => child.GetComponent<Renderer>())
                    .Where(renderer => renderer != null)
                    .Where(renderer => !renderer.name.StartsWith("Wall (audit fix)"))
                    .Where(renderer => CellsCoveredBy(renderer.bounds)
                        .Any(cell => layout.CellAt(cell).Kind == CellKind.Portal))
                    .ToList();
                foreach (var block in offenders)
                {
                    var legitimate = CellsCoveredBy(block.bounds)
                        .Where(cell =>
                        {
                            var kind = layout.CellAt(cell).Kind;
                            return kind == CellKind.FixedWall || kind == CellKind.RotatingWallSlot;
                        })
                        .ToList();
                    foreach (var cell in legitimate)
                        if (rebuiltCells.Add(cell))
                        {
                            SpawnUnitWall(root.transform, block, cell);
                            rebuilt++;
                        }
                    Undo.DestroyObjectImmediate(block.gameObject);
                    removedBlocks++;
                }
            }

            // 2) Fill wall cells that still have no geometry.
            Collect(out var missing, out _);
            var wallsRoot = GameObject.Find("Walls");
            if (donor == null && missing.Count > 0)
            {
                Debug.LogError("[BoardAudit] No wall renderer left to use as material donor.");
                return;
            }
            foreach (var cell in missing)
            {
                SpawnUnitWall(wallsRoot.transform, donor, cell);
                rebuilt++;
            }

            if (rebuilt + removedBlocks > 0)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
            }
            Debug.Log($"[BoardAudit] fix applied: {removedBlocks} offending blocks replaced, {rebuilt} unit walls spawned.");
            Audit();
        }

        private static void SpawnUnitWall(Transform parent, Renderer template, GridPosition cell)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = $"Wall (audit fix) {cell}";
            wall.transform.SetParent(parent, false);
            var height = template != null ? template.bounds.size.y : 1f;
            var centerY = template != null ? template.bounds.center.y : height * 0.5f;
            wall.transform.position = BoardCoordinates.ToWorld(cell, centerY);
            wall.transform.localScale = new Vector3(1f, height, 1f);
            if (template != null)
            {
                var renderer = wall.GetComponent<Renderer>();
                renderer.sharedMaterial = template.sharedMaterial;
            }
            Undo.RegisterCreatedObjectUndo(wall, "Board audit wall fix");
        }

        private static void Collect(out List<GridPosition> missing, out List<GridPosition> covering)
        {
            missing = new List<GridPosition>();
            covering = new List<GridPosition>();
            var covered = new HashSet<GridPosition>();
            foreach (var rootName in WallRoots)
            {
                var root = GameObject.Find(rootName);
                if (root == null) continue;
                foreach (Transform child in root.transform)
                {
                    var renderer = child.GetComponent<Renderer>();
                    if (renderer == null) continue;
                    foreach (var cell in CellsCoveredBy(renderer.bounds))
                        covered.Add(cell);
                }
            }
            var layout = new BoardLayout();
            for (var row = 0; row < BoardLayout.Size; row++)
            for (var col = 0; col < BoardLayout.Size; col++)
            {
                var position = new GridPosition(col, row);
                var kind = layout.CellAt(position).Kind;
                var has = covered.Contains(position);
                if ((kind == CellKind.FixedWall || kind == CellKind.RotatingWallSlot) && !has)
                    missing.Add(position);
                if (kind == CellKind.Portal && has)
                    covering.Add(position);
            }
        }

        [MenuItem("Draftline/Debug Spawn All Items")]
        public static void DebugSpawnAllItems()
        {
            var controller = Object.FindObjectOfType<DraftlineGameController>();
            if (controller == null || controller.Rules == null)
            {
                Debug.LogWarning("[DebugSpawn] no running match.");
                return;
            }
            var rules = controller.Rules;
            var state = rules.State;
            state.FieldItems.Clear();
            var types = (ItemType[])System.Enum.GetValues(typeof(ItemType));
            var candidates = rules.Board.RotatingSlots.Where(position =>
                !rules.Board.IsScheduledWall(position, state.WallPhase) &&
                state.Pieces.All(piece => !piece.OnBoard || piece.Position != position)).ToList();
            var placed = 0;
            foreach (var type in types)
            {
                if (placed >= candidates.Count) break;
                var position = candidates[placed++];
                state.FieldItems.Add(new FieldItem { Id = state.NextItemId++, Type = type, Position = position });
                Debug.Log($"[DebugSpawn] {type} at {position}");
            }
            var sync = typeof(DraftlineGameController).GetMethod("SyncItemViews",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            sync?.Invoke(controller, null);
            Debug.Log($"[DebugSpawn] done, placed={placed}");
        }

        [MenuItem("Draftline/Debug Force Victory")]
        public static void DebugForceVictory()
        {
            var controller = Object.FindObjectOfType<DraftlineGameController>();
            if (controller == null || controller.Rules == null)
            {
                Debug.LogWarning("[DebugVictory] no running match.");
                return;
            }
            var rules = controller.Rules;
            var state = rules.State;
            // 清场后摆五子连线，再反射调用私有 Win 触发胜利事件流。
            foreach (var piece in state.Pieces)
            {
                piece.OnBoard = false;
                piece.Position = new GridPosition(-1, -1);
            }
            var row = 9;
            var column = 4;
            foreach (var piece in state.Pieces.Where(p => p.Owner == state.CurrentPlayer).Take(5))
            {
                piece.OnBoard = true;
                piece.Position = new GridPosition(column++, row);
            }
            typeof(MatchRules).GetMethod("Win", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(rules, new object[] { state.CurrentPlayer });
            typeof(DraftlineGameController).GetMethod("SyncViews", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(controller, null);
            Debug.Log("[DebugVictory] forced win for " + state.CurrentPlayer);
        }

        private static List<GridPosition> CellsCoveredBy(Bounds bounds)
        {
            var cells = new List<GridPosition>();
            for (var row = 0; row < BoardLayout.Size; row++)
            for (var col = 0; col < BoardLayout.Size; col++)
            {
                var position = new GridPosition(col, row);
                var center = BoardCoordinates.ToWorld(position, bounds.center.y);
                // Shrink slightly so neighbouring blocks do not bleed into this cell.
                if (Mathf.Abs(center.x - bounds.center.x) <= bounds.extents.x - 0.02f &&
                    Mathf.Abs(center.z - bounds.center.z) <= bounds.extents.z - 0.02f)
                    cells.Add(position);
            }
            return cells;
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using Draftline.Core;
using Draftline.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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

        [MenuItem("Draftline/Debug Follow Cam")]
        public static void DebugFollowCam()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Debug] Enter play mode first.");
                return;
            }
            var director = Object.FindObjectOfType<Draftline.Unity.DraftlineCameraDirector>();
            var piece = Object.FindObjectOfType<Draftline.Unity.DraftlinePieceView>();
            if (director == null || piece == null)
            {
                Debug.LogWarning("[Debug] director or piece not found.");
                return;
            }
            director.FollowPiece(piece.transform);
            Debug.Log("[Debug] FollowPiece called on " + piece.name);
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

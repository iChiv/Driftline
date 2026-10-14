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
    /// One-shot dungeon reskin (idempotent): swaps the placeholder toy-board visuals for
    /// KayKit Dungeon models. Walls keep their colliders/logic; only renderers change.
    /// Menu: Draftline/Apply Dungeon Skin. Reverting: Draftline/Remove Dungeon Skin.
    /// </summary>
    public static class DraftlineDungeonSkin
    {
        private const string KayKitModels =
            "Assets/KayKit/Packs/KayKit - Dungeon Remastered Pack (for Unity)/Models/";
        private const string MarkerName = "Dungeon Skin";

        [MenuItem("Draftline/Apply Dungeon Skin")]
        public static void Apply()
        {
            if (Object.FindObjectsOfType<Transform>(true).Any(t => t.name == MarkerName))
            {
                Debug.Log("[DungeonSkin] already applied; remove first for a rebuild.");
                return;
            }
            var marker = new GameObject(MarkerName); // 场景标记，保证幂等
            Undo.RegisterCreatedObjectUndo(marker, "Dungeon Skin");

            SkinFloor();
            SkinWalls();
            SkinItemTemplates();
            SkinProps();

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[DungeonSkin] applied.");
        }

        [MenuItem("Draftline/Remove Dungeon Skin")]
        public static void Remove()
        {
            var marker = GameObject.Find(MarkerName);
            if (marker != null) Object.DestroyImmediate(marker);
            foreach (var rootName in new[] { "Walls", "Border" })
            {
                var root = GameObject.Find(rootName);
                if (root == null) continue;
                foreach (Transform child in root.transform)
                {
                    var skinned = child.Find("DungeonWall");
                    if (skinned != null) Object.DestroyImmediate(skinned.gameObject);
                    foreach (var renderer in child.GetComponentsInChildren<MeshRenderer>(true))
                        renderer.enabled = true;
                }
            }
            var visuals = GameObject.Find("Walls Dungeon Visuals");
            if (visuals != null) Object.DestroyImmediate(visuals);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[DungeonSkin] removed (walls restored; floor/props keep last state).");
        }

        // ---------- 地面：石板纹理 ----------
        private static void SkinFloor()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Textures/DungeonFloor.png");
            if (texture == null) { Debug.LogWarning("[DungeonSkin] DungeonFloor.png missing"); return; }
            const string matPath = "Assets/Resources/Materials/DungeonFloor.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                {
                    // 石板略粗糙
                };
                material.SetFloat("_Smoothness", 0.25f);
                AssetDatabase.CreateAsset(material, matPath);
            }
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            var ground = GameObject.Find("Ground");
            if (ground != null)
            {
                var renderer = ground.GetComponent<Renderer>();
                if (renderer != null) renderer.sharedMaterial = material;
            }
            AssetDatabase.SaveAssets();
        }

        // ---------- 墙体：KayKit 石柱替换占位方块 ----------
        private static void SkinWalls()
        {
            var column = LoadModel("column");              // 旋转墙升起态：细柱
            var wallPanel = LoadModel("wall");             // 固定墙/边界：墙板段
            if (column == null || wallPanel == null)
            {
                Debug.LogWarning("[DungeonSkin] wall models missing");
                return;
            }
            var layout = new BoardLayout();
            var worldRoot = new GameObject("Walls Dungeon Visuals").transform;

            // 1. 禁用所有旧占位方块的网格（碰撞体保留，逻辑零改动）。
            foreach (var rootName in new[] { "Walls", "Border" })
            {
                var root = GameObject.Find(rootName);
                if (root == null) continue;
                foreach (Transform block in root.transform)
                    foreach (var r in block.GetComponentsInChildren<MeshRenderer>(true))
                        r.enabled = false;
            }

            // 2. 旋转墙槽位与中心墙：单格块上挂细柱/装饰柱，跟随升沉动画。
            var wallsRoot = GameObject.Find("Walls");
            if (wallsRoot != null)
                foreach (Transform block in wallsRoot.transform)
                {
                    if (!BoardCoordinates.TryFromWorld(block.position, out var cell)) continue;
                    var kind = layout.CellAt(cell).Kind;
                    GameObject model = null;
                    Vector3 scale = Vector3.one;
                    if (kind == CellKind.RotatingWallSlot)
                    {
                        model = column;
                        scale = new Vector3(1.0f, 0.71f, 1.0f); // 细柱：0.7×1×0.7
                    }
                    else if (cell == new GridPosition(9, 9))
                    {
                        model = column;
                        scale = new Vector3(1.3f, 1.1f, 1.3f); // 中心高塔地标
                    }
                    if (model == null) continue;
                    var visual = Object.Instantiate(model);
                    visual.name = "DungeonWall";
                    visual.transform.SetParent(block, false);
                    visual.transform.localPosition = Vector3.zero;
                    visual.transform.localScale = scale;
                }

            // 3. 固定墙（含最外圈）：把连续墙格连成墙段，沿墙段铺 KayKit 墙板。
            var fixedCells = new HashSet<GridPosition>();
            for (var row = 0; row < BoardLayout.Size; row++)
            for (var col = 0; col < BoardLayout.Size; col++)
            {
                var position = new GridPosition(col, row);
                if (layout.CellAt(position).Kind == CellKind.FixedWall)
                    fixedCells.Add(position);
            }
            var used = new HashSet<GridPosition>();
            foreach (var start in fixedCells.OrderBy(c => c.Row).ThenBy(c => c.Column))
            {
                if (used.Contains(start)) continue;
                // 优先沿世界 X（同行号 Row 方向）连段，其次世界 Z。
                var runX = ExtendRun(fixedCells, used, start, alongRow: true);
                var run = runX.Count >= 2 ? runX : ExtendRun(fixedCells, used, start, alongRow: false);
                PlaceWallRun(wallPanel, run, worldRoot);
                foreach (var cell in run) used.Add(cell);
            }
        }

        // 从 start 出发沿一个方向吃连续墙格。alongRow=true 表示沿 Row 递增（世界 X）。
        private static List<GridPosition> ExtendRun(HashSet<GridPosition> cells, HashSet<GridPosition> used,
            GridPosition start, bool alongRow)
        {
            var run = new List<GridPosition> { start };
            var next = alongRow ? start.Offset(1, 0) : start.Offset(0, 1);
            while (cells.Contains(next) && !used.Contains(next))
            {
                run.Add(next);
                next = alongRow ? next.Offset(1, 0) : next.Offset(0, 1);
            }
            return run;
        }

        // KayKit wall 墙板：4 长 × 4 高 × 1 厚，底面 pivot。沿墙段拉伸铺放。
        private static void PlaceWallRun(GameObject wallPanel, List<GridPosition> run,
            Transform parent)
        {
            if (run.Count == 0) return;
            var first = BoardCoordinates.ToWorld(run[0], 0.5f);
            var last = BoardCoordinates.ToWorld(run[run.Count - 1], 0.5f);
            var center = (first + last) * 0.5f;
            var length = run.Count; // 每格 1 单位
            // 朝向按墙段首末格的实际世界方向（BoardCoordinates 里 Row→X、Column→Z，
            // 不能按网格轴想当然）。
            var runAlongX = Mathf.Abs(last.x - first.x) >= Mathf.Abs(last.z - first.z);
            var visual = Object.Instantiate(wallPanel);
            visual.name = "DungeonWall";
            visual.transform.SetParent(parent, false);
            visual.transform.rotation = runAlongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
            visual.transform.localScale = new Vector3(length / 4f, 0.28f, 0.85f);
            visual.transform.position = center;
            // 用渲染包围盒对齐：底面贴地板（y=0.5），中心对墙段中心。
            var renderer = visual.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                var bounds = renderer.bounds;
                var offset = center - bounds.center;
                offset.y = 0.5f - bounds.min.y;
                visual.transform.position += offset;
            }
        }

        // ---------- 道具模板：KayKit 小件 ----------
        private static void SkinItemTemplates()
        {
            var map = new Dictionary<string, string>
            {
                { "Template PassWall", "key_gold" },          // 穿墙=金钥匙
                { "Template EmergencyStop", "sword_shield" }, // 急停=银盾格挡
                { "Template CollisionCapture", "sword_shield_gold" }, // 撞吃=金剑盾
                { "Template BreakWall", "pickaxe_gold" },     // 破墙=金镐
            };
            foreach (var pair in map)
            {
                var template = FindInactive("Template Root", pair.Key);
                if (template == null) continue;
                foreach (Transform child in template.transform.Cast<Transform>().ToArray())
                    Object.DestroyImmediate(child.gameObject);
                var model = LoadModel(pair.Value);
                if (model == null) continue;
                var visual = Object.Instantiate(model);
                visual.name = pair.Value;
                visual.transform.SetParent(template.transform, false);
                visual.transform.localPosition = Vector3.zero;
                FitToMaxDimension(visual.transform, 0.85f);
            }
        }

        // ---------- 桌面摆件：玩具换地牢物件 ----------
        private static void SkinProps()
        {
            var swaps = new Dictionary<string, string>
            {
                { "Toy Car", "chest" },          // 小车 → 宝箱
                { "Toy Block Tower", "barrel_large" }, // 积木塔 → 木桶
                { "Toy Spinning Top", "torch_lit" },   // 陀螺 → 火把
                { "Toy Hourglass", "column" },         // 沙漏 → 残柱
            };
            foreach (var pair in swaps)
            {
                var prop = GameObject.Find(pair.Key);
                if (prop == null) continue;
                foreach (Transform child in prop.transform.Cast<Transform>().ToArray())
                    Object.DestroyImmediate(child.gameObject);
                var model = LoadModel(pair.Value);
                if (model == null) continue;
                var visual = Object.Instantiate(model);
                visual.name = pair.Value;
                visual.transform.SetParent(prop.transform, false);
                visual.transform.localPosition = Vector3.zero;
                FitToMaxDimension(visual.transform, pair.Key == "Toy Car" ? 1.6f : 1.8f);
                // 点击反弹需要碰撞体
                foreach (var old in prop.GetComponents<Collider>()) Object.DestroyImmediate(old);
                var box = prop.AddComponent<BoxCollider>();
                var bounds = visual.GetComponentInChildren<Renderer>().bounds;
                box.center = prop.transform.InverseTransformPoint(bounds.center);
                box.size = bounds.size;
            }
        }

        // ---------- 工具 ----------
        private static GameObject LoadModel(string name)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(KayKitModels + name + ".fbx");
            if (model == null) Debug.LogWarning("[DungeonSkin] model missing: " + name);
            return model;
        }

        private static GameObject FindInactive(string unused, string name)
        {
            foreach (var transform in Object.FindObjectsOfType<Transform>(true))
                if (transform.name == name) return transform.gameObject;
            return null;
        }

        private static void FitToMaxDimension(Transform visual, float target)
        {
            var renderer = visual.GetComponentInChildren<Renderer>();
            if (renderer == null) return;
            var max = Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.y, renderer.bounds.size.z);
            if (max <= 0.001f) return;
            visual.localScale *= target / max;
            // 缩放后重读包围盒，把底面贴到父级原点
            var bounds = renderer.bounds;
            visual.position += Vector3.up * (0f - bounds.min.y);
        }

    }
}

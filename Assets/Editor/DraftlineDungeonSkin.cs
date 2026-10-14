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
            var pillar = LoadModel("pillar");
            var decorated = LoadModel("pillar_decorated");
            if (pillar == null) { Debug.LogWarning("[DungeonSkin] pillar.fbx missing"); return; }
            var layout = new BoardLayout();
            var worldRoot = new GameObject("Walls Dungeon Visuals").transform;
            foreach (var rootName in new[] { "Walls", "Border" })
            {
                var root = GameObject.Find(rootName);
                if (root == null) continue;
                foreach (Transform block in root.transform)
                {
                    var renderers = block.GetComponentsInChildren<MeshRenderer>(true);
                    if (renderers.Length == 0) continue;
                    // 有些砖块的网格在子级（Cube），合并所有后代渲染器的包围盒。
                    var bounds = renderers[0].bounds;
                    foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                    var cells = CellsCoveredBy(bounds);
                    var singleSlot = BoardCoordinates.TryFromWorld(block.position, out var slotCell) &&
                                     layout.CellAt(slotCell).Kind == CellKind.RotatingWallSlot;
                    foreach (var r in renderers) r.enabled = false; // 碰撞体保留，占位方块隐藏
                    foreach (var cell in cells)
                    {
                        var isCenter = cell == new GridPosition(9, 9);
                        var model = isCenter && decorated != null ? decorated : pillar;
                        var visual = Object.Instantiate(model);
                        visual.name = "DungeonWall";
                        var position = BoardCoordinates.ToWorld(cell, 0.5f);
                        visual.transform.position = position;
                        // pillar 1.5×4×1.5 → 压进 1×1×1 格
                        visual.transform.localScale = new Vector3(0.67f, 0.25f, 0.67f);
                        // 旋转墙槽位的单格块：视觉挂块上，跟随升沉动画；其余挂世界根
                        if (singleSlot || isCenter)
                        {
                            visual.transform.SetParent(block, true);
                        }
                        else
                        {
                            visual.transform.SetParent(worldRoot, true);
                        }
                    }
                }
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

        private static List<GridPosition> CellsCoveredBy(Bounds bounds)
        {
            var cells = new List<GridPosition>();
            for (var row = 0; row < BoardLayout.Size; row++)
            for (var col = 0; col < BoardLayout.Size; col++)
            {
                var position = new GridPosition(col, row);
                var center = BoardCoordinates.ToWorld(position, bounds.center.y);
                if (Mathf.Abs(center.x - bounds.center.x) <= bounds.extents.x - 0.02f &&
                    Mathf.Abs(center.z - bounds.center.z) <= bounds.extents.z - 0.02f)
                    cells.Add(position);
            }
            return cells;
        }
    }
}

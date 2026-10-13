using System.Collections.Generic;
using System.IO;
using Draftline.Core;
using Draftline.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Draftline.EditorTools
{
    /// <summary>
    /// One-shot bake: turns everything the runtime theme/dice code used to generate at
    /// runtime into real scene objects with persistent material assets, so the board is
    /// editable WYSIWYG in the editor. Run once; safe to re-run (idempotent).
    /// </summary>
    public static class DraftlineSceneBake
    {
        private const string MaterialDir = "Assets/Resources/Materials";

        private static readonly Dictionary<string, Color> LitColors = new Dictionary<string, Color>
        {
            { "TableWood", new Color(0.42f, 0.30f, 0.19f) },
            { "TableRim", new Color(0.55f, 0.38f, 0.22f) },
            { "BrickMaple", new Color(0.87f, 0.63f, 0.38f) },
            { "BrickWalnut", new Color(0.42f, 0.28f, 0.16f) },
            { "HintGreen", new Color(0.26f, 0.95f, 0.52f) },
        };

        private static readonly Dictionary<string, Color> EmissiveColors = new Dictionary<string, Color>
        {
            { "PortalA", new Color(1.0f, 0.55f, 0.1f) },
            { "PortalB", new Color(1.0f, 0.45f, 0.4f) },
            { "PortalC", new Color(0.95f, 0.3f, 0.85f) },
            { "PortalD", new Color(1.0f, 0.2f, 0.2f) },
            { "FactionRed", new Color(0.91f, 0.20f, 0.20f) },
            { "FactionBlue", new Color(0.20f, 0.43f, 0.91f) },
            { "FactionGreen", new Color(0.20f, 0.72f, 0.34f) },
            { "FactionYellow", new Color(0.95f, 0.77f, 0.13f) },
        };

        [MenuItem("Draftline/Bake Tabletop Into Scene")]
        public static void Bake()
        {
            var layout = new BoardLayout();
            Directory.CreateDirectory(MaterialDir);

            var materials = new Dictionary<string, Material>();
            foreach (var pair in LitColors)
                materials[pair.Key] = EnsureMaterial(pair.Key, pair.Value, false, 0f);
            foreach (var pair in EmissiveColors)
                materials[pair.Key] = EnsureMaterial(pair.Key, pair.Value, true, 1.1f);
            materials["GhostWall"] = EnsureTransparent("GhostWall", new Color(0.6f, 0.72f, 0.85f, 0.28f));
            materials["SlotBase"] = EnsureMaterial("SlotBase", new Color(0.13f, 0.17f, 0.23f), false, 0f);
            materials["SlotSuppressed"] = EnsureMaterial("SlotSuppressed", new Color(0.75f, 0.25f, 0.25f), false, 0f);
            materials["TableRipple"] = EnsureEmissive("TableRipple", new Color(1f, 0.95f, 0.8f), 0.8f);

            // 墙体：跨格大块按包围盒中心映射，旋转墙槽位/中心用胡桃木，其余枫木。
            foreach (var rootName in new[] { "Walls", "Border" })
            {
                var root = GameObject.Find(rootName);
                if (root == null) continue;
                foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                {
                    var dark = false;
                    if (BoardCoordinates.TryFromWorld(renderer.bounds.center, out var cell))
                        dark = layout.CellAt(cell).Kind == CellKind.RotatingWallSlot ||
                               cell == new GridPosition(9, 9);
                    Undo.RecordObject(renderer, "Bake wall material");
                    renderer.sharedMaterial = dark ? materials["BrickWalnut"] : materials["BrickMaple"];
                }
            }

            // 传送门：方块压扁成发光圆盘并挂缓旋。
            var portalRoot = GameObject.Find("Portal");
            if (portalRoot != null)
            {
                foreach (Transform child in portalRoot.transform)
                {
                    var material = materials["PortalC"];
                    if (BoardCoordinates.TryFromWorld(child.position, out var cell))
                    {
                        var definition = layout.CellAt(cell);
                        if (definition.Kind == CellKind.Portal)
                        {
                            var idx = "OCRM".IndexOf(definition.PortalKey);
                            if (idx >= 0) material = materials["Portal" + "ABCD"[idx]];
                        }
                    }
                    Undo.RecordObject(child.transform, "Bake portal pad");
                    Undo.RecordObject(child.GetComponent<Renderer>(), "Bake portal pad");
                    child.localScale = new Vector3(0.92f, 0.12f, 0.92f);
                    child.position = new Vector3(child.position.x, 0.55f, child.position.z);
                    child.GetComponent<Renderer>().sharedMaterial = material;
                    if (child.GetComponent<DraftlinePortalSpin>() == null)
                        Undo.AddComponent<DraftlinePortalSpin>(child.gameObject);
                }
            }

            // 出生角：阵营色自发光。
            var start = GameObject.Find("Start");
            if (start != null)
            {
                var factionByCorner = new Dictionary<PlayerColor, Material>
                {
                    { PlayerColor.Red, materials["FactionRed"] },
                    { PlayerColor.Blue, materials["FactionBlue"] },
                    { PlayerColor.Green, materials["FactionGreen"] },
                    { PlayerColor.Yellow, materials["FactionYellow"] },
                };
                foreach (Transform child in start.transform)
                {
                    var renderer = child.GetComponent<Renderer>();
                    if (renderer == null) continue;
                    if (BoardCoordinates.TryFromWorld(child.position, out var cell))
                    {
                        Undo.RecordObject(renderer, "Bake faction pad");
                        renderer.sharedMaterial = factionByCorner[layout.CellAt(cell).Owner];
                    }
                }
            }

            // 主题层与桌面道具进场景。
            var themes = FindOrCreate("Themes", null);
            var tabletop = FindOrCreate("Tabletop", themes.transform);
            FindOrCreate("Magic", themes.transform); // 占位空层（阶段三内容）

            // 主题资产（ScriptableObject）：引用烘焙材质与摆件，魔幻主题占位。
            BakeThemeAssets(tabletop.transform);
            if (GameObject.Find("Wooden Table") == null)
                CreateBox("Wooden Table", new Vector3(0, -0.15f, 0), new Vector3(30f, 0.3f, 30f),
                    materials["TableWood"], tabletop.transform);
            if (GameObject.Find("Table Rim") == null)
                CreateBox("Table Rim", new Vector3(0, 0.1f, 0), new Vector3(21.4f, 0.5f, 21.4f),
                    materials["TableRim"], tabletop.transform);
            if (GameObject.Find("Toy Block Tower") == null)
                BuildBlockTower(new Vector3(13.2f, 0, 3f), materials, tabletop.transform);
            if (GameObject.Find("Toy Car") == null)
                BuildToyCar(new Vector3(-13.2f, 0, -2f), materials, tabletop.transform);
            if (GameObject.Find("Toy Spinning Top") == null)
                BuildTop(new Vector3(2.5f, 0, 13.2f), materials, tabletop.transform);
            if (GameObject.Find("Toy Hourglass") == null)
                BuildHourglass(new Vector3(-3f, 0, -13.2f), materials, tabletop.transform);

            // 骰子与隐形投骰台入场景（默认隐藏，编辑器里可直接选中调整）。
            BakeDiceRig();

            // 道具图标模板：四种形状进场景，默认隐藏，供运行时克隆。
            BakeItemTemplates(tabletop.transform);

            // 修复摆件上失效的 ToyProp 组件（fileID-only 的断链）再重挂。
            foreach (var propName in new[] { "Toy Block Tower", "Toy Car", "Toy Spinning Top", "Toy Hourglass" })
            {
                var prop = GameObject.Find(propName);
                if (prop == null) continue;
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(prop);
                if (prop.GetComponent<DraftlineToyProp>() == null)
                    Undo.AddComponent<DraftlineToyProp>(prop);
            }

            // 相机背景转暖（一次性）。
            if (Camera.main != null)
            {
                Undo.RecordObject(Camera.main, "Bake camera background");
                Camera.main.backgroundColor = new Color(0.32f, 0.27f, 0.22f);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneBake] Tabletop baked into scene and saved.");
        }

        private static void BakeDiceRig()
        {
            if (GameObject.Find("Dice Rig") != null) return;
            var rig = new GameObject("Dice Rig");

            var tray = new GameObject("Dice Tray");
            tray.transform.SetParent(rig.transform, false);
            CreateColliderOnly("Tray Floor", new Vector3(0, 1.75f, 0), new Vector3(19f, 0.25f, 19f), tray.transform);
            CreateColliderOnly("Wall North", new Vector3(0, 3.1f, 9.5f), new Vector3(19.5f, 2.6f, 0.5f), tray.transform);
            CreateColliderOnly("Wall South", new Vector3(0, 3.1f, -9.5f), new Vector3(19.5f, 2.6f, 0.5f), tray.transform);
            CreateColliderOnly("Wall East", new Vector3(9.5f, 3.1f, 0), new Vector3(0.5f, 2.6f, 19f), tray.transform);
            CreateColliderOnly("Wall West", new Vector3(-9.5f, 3.1f, 0), new Vector3(0.5f, 2.6f, 19f), tray.transform);
            tray.SetActive(false);

            var die = new GameObject("Physical D6");
            die.transform.SetParent(rig.transform, false);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Dice_6/Prefabs/Dice_d6_Plastic Glossy Pure write.prefab");
            if (prefab != null)
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, die.transform);
                visual.name = "Die Visual";
                visual.transform.localPosition = Vector3.zero;
                foreach (var collider in visual.GetComponentsInChildren<Collider>())
                    Object.DestroyImmediate(collider);
                var bounds = visual.GetComponentInChildren<Renderer>().bounds;
                var fit = 0.8f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                visual.transform.localScale = Vector3.one * fit;
            }
            die.AddComponent<BoxCollider>().size = Vector3.one * 0.8f;
            var body = die.AddComponent<Rigidbody>();
            body.mass = 0.4f;
            body.drag = 0.2f;
            body.angularDrag = 0.25f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.isKinematic = true;
            die.SetActive(false);
        }

        /* ---------- 工具 ---------- */

        private static GameObject FindOrCreate(string name, Transform parent)
        {
            var existing = GameObject.Find(name);
            if (existing != null) return existing;
            var created = new GameObject(name);
            if (parent != null) created.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(created, "Bake " + name);
            return created;
        }

        private static GameObject CreateBox(string name, Vector3 position, Vector3 scale,
            Material material, Transform parent)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            Undo.RegisterCreatedObjectUndo(box, "Bake " + name);
            return box;
        }

        private static void CreateColliderOnly(string name, Vector3 position, Vector3 scale, Transform parent)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.transform.localScale = scale;
            var renderer = box.GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = false; // 隐形碰撞体：不渲染不投影。
            Undo.RegisterCreatedObjectUndo(box, "Bake " + name);
        }

        private static Material EnsureMaterial(string name, Color color, bool emissive, float intensity)
        {
            var path = $"{MaterialDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.5f);
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                if (material.HasProperty("_EmissionColor"))
                    material.SetColor("_EmissionColor", color * Mathf.Max(intensity, 0.5f));
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material EnsureEmissive(string name, Color color, float intensity) =>
            EnsureMaterial(name, color, true, intensity);

        private static Material EnsureTransparent(string name, Color color)
        {
            var path = $"{MaterialDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
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
            material.color = color;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /* ---------- 摆件（与运行时版本同造型） ---------- */

        private static void BuildBlockTower(Vector3 position, Dictionary<string, Material> materials, Transform parent)
        {
            var root = FindOrCreate("Toy Block Tower", parent);
            var colors = new[] { new Color(0.9f, 0.4f, 0.35f), new Color(0.35f, 0.7f, 0.9f),
                                 new Color(0.95f, 0.8f, 0.3f) };
            for (var index = 0; index < 3; index++)
            {
                var matName = $"PropTower{index}";
                var material = EnsureMaterial(matName, colors[index], false, 0f);
                materials[matName] = material;
                CreateBox($"Block {index}", position + new Vector3(0, 0.45f + index * 0.9f, 0),
                    Vector3.one * (0.9f - index * 0.12f), material, root.transform);
            }
            AddProp(root);
        }

        private static void BuildToyCar(Vector3 position, Dictionary<string, Material> materials, Transform parent)
        {
            var root = FindOrCreate("Toy Car", parent);
            var bodyMat = EnsureMaterial("PropCarBody", new Color(0.85f, 0.3f, 0.3f), false, 0f);
            var wheelMat = EnsureMaterial("PropWheel", new Color(0.2f, 0.2f, 0.22f), false, 0f);
            CreateBox("Car Body", position + new Vector3(0, 0.5f, 0), new Vector3(1.6f, 0.45f, 0.85f), bodyMat, root.transform);
            CreateBox("Car Cabin", position + new Vector3(-0.15f, 0.85f, 0), new Vector3(0.8f, 0.35f, 0.75f), bodyMat, root.transform);
            foreach (var offset in new[] { new Vector3(0.55f, 0, 0.45f), new Vector3(0.55f, 0, -0.45f),
                                           new Vector3(-0.55f, 0, 0.45f), new Vector3(-0.55f, 0, -0.45f) })
            {
                var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheel.name = "Wheel";
                wheel.transform.SetParent(root.transform, false);
                wheel.transform.position = position + offset + new Vector3(0, 0.22f, 0);
                wheel.transform.localScale = new Vector3(0.44f, 0.12f, 0.44f);
                wheel.transform.rotation = Quaternion.Euler(90f, 0, 0);
                wheel.GetComponent<Renderer>().sharedMaterial = wheelMat;
                Undo.RegisterCreatedObjectUndo(wheel, "Bake wheel");
            }
            AddProp(root);
        }

        private static void BuildTop(Vector3 position, Dictionary<string, Material> materials, Transform parent)
        {
            var root = FindOrCreate("Toy Spinning Top", parent);
            var mat = EnsureMaterial("PropTop", new Color(0.5f, 0.75f, 0.9f), false, 0f);
            CreateBox("Top Body", position + new Vector3(0, 0.5f, 0), Vector3.one * 0.9f, mat, root.transform)
                .transform.localScale = new Vector3(0.9f, 0.5f, 0.9f);
            AddProp(root);
        }

        private static void BuildHourglass(Vector3 position, Dictionary<string, Material> materials, Transform parent)
        {
            var root = FindOrCreate("Toy Hourglass", parent);
            var mat = EnsureMaterial("PropHourglass", new Color(0.8f, 0.55f, 0.85f), false, 0f);
            foreach (var offset in new[] { 0f, 0.5f })
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "Glass Bulb";
                sphere.transform.SetParent(root.transform, false);
                sphere.transform.position = position + new Vector3(0, 0.5f + offset, 0);
                sphere.transform.localScale = Vector3.one * 0.7f;
                sphere.GetComponent<Renderer>().sharedMaterial = mat;
                Undo.RegisterCreatedObjectUndo(sphere, "Bake bulb");
            }
            AddProp(root);
        }

        private static void AddProp(GameObject root)
        {
            if (root.GetComponent<DraftlineToyProp>() == null)
                Undo.AddComponent<DraftlineToyProp>(root);
        }

        private static void BakeThemeAssets(Transform tabletop)
        {
            Directory.CreateDirectory("Assets/Themes");
            var tabletopTheme = AssetDatabase.LoadAssetAtPath<BoardTheme>("Assets/Themes/TabletopTheme.asset");
            if (tabletopTheme == null)
            {
                tabletopTheme = ScriptableObject.CreateInstance<BoardTheme>();
                tabletopTheme.ThemeName = "桌面玩具";
                AssetDatabase.CreateAsset(tabletopTheme, "Assets/Themes/TabletopTheme.asset");
            }
            tabletopTheme.WallMaterial = LoadMat("BrickMaple");
            tabletopTheme.RotatingWallMaterial = LoadMat("BrickWalnut");
            tabletopTheme.TableMaterial = LoadMat("TableWood");
            tabletopTheme.RimMaterial = LoadMat("TableRim");
            tabletopTheme.PortalA = LoadMat("PortalA");
            tabletopTheme.PortalB = LoadMat("PortalB");
            tabletopTheme.PortalC = LoadMat("PortalC");
            tabletopTheme.PortalD = LoadMat("PortalD");
            tabletopTheme.FactionMaterials = new[]
            {
                LoadMat("FactionRed"), LoadMat("FactionBlue"), LoadMat("FactionGreen"), LoadMat("FactionYellow")
            };
            var props = new System.Collections.Generic.List<GameObject>();
            foreach (Transform child in tabletop)
                props.Add(child.gameObject);
            tabletopTheme.Props = props.ToArray();
            EditorUtility.SetDirty(tabletopTheme);

            if (AssetDatabase.LoadAssetAtPath<BoardTheme>("Assets/Themes/MagicTheme.asset") == null)
            {
                var magic = ScriptableObject.CreateInstance<BoardTheme>();
                magic.ThemeName = "魔幻（占位）";
                AssetDatabase.CreateAsset(magic, "Assets/Themes/MagicTheme.asset");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneBake] theme assets baked.");
        }

        private static Material LoadMat(string name) =>
            AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/{name}.mat");

        /* ---------- 道具图标模板（形状+颜色双编码，运行时克隆） ---------- */

        private static void BakeItemTemplates(Transform tabletop)
        {
            var root = FindOrCreate("Item Templates", tabletop);
            root.SetActive(false); // 模板隐藏，只供克隆。
            var mats = new Dictionary<ItemType, Material>
            {
                { ItemType.PassWall, EnsureMaterial("ItemPassWall", new Color(0.61f, 0.42f, 1.0f), false, 0f) },
                { ItemType.EmergencyStop, EnsureMaterial("ItemEmergencyStop", new Color(0.21f, 0.69f, 0.88f), false, 0f) },
                { ItemType.CollisionCapture, EnsureMaterial("ItemCollisionCapture", new Color(0.88f, 0.25f, 0.25f), false, 0f) },
                { ItemType.BreakWall, EnsureMaterial("ItemBreakWall", new Color(0.88f, 0.63f, 0.21f), false, 0f) },
            };
            if (root.transform.Find("Template PassWall") == null)
                BuildPassWallTemplate(root.transform, mats[ItemType.PassWall]);
            if (root.transform.Find("Template EmergencyStop") == null)
                BuildEmergencyStopTemplate(root.transform, mats[ItemType.EmergencyStop]);
            if (root.transform.Find("Template CollisionCapture") == null)
                BuildCollisionCaptureTemplate(root.transform, mats[ItemType.CollisionCapture]);
            if (root.transform.Find("Template BreakWall") == null)
                BuildBreakWallTemplate(root.transform, mats[ItemType.BreakWall]);
        }

        // 穿墙：两个菱形板相对，中间留缝——“穿过去”。
        private static void BuildPassWallTemplate(Transform parent, Material material)
        {
            var root = CreateEmpty("Template PassWall", parent);
            foreach (var side in new[] { -1f, 1f })
            {
                var diamond = GameObject.CreatePrimitive(PrimitiveType.Cube);
                diamond.name = "Diamond";
                diamond.transform.SetParent(root.transform, false);
                diamond.transform.localPosition = new Vector3(side * 0.16f, 0.25f, 0);
                diamond.transform.localRotation = Quaternion.Euler(0, 45f, 0);
                diamond.transform.localScale = new Vector3(0.2f, 0.5f, 0.2f);
                diamond.GetComponent<Renderer>().sharedMaterial = material;
                Undo.RegisterCreatedObjectUndo(diamond, "Bake item template");
            }
        }

        // 急停：竖起的圆牌+立杆——“停”。
        private static void BuildEmergencyStopTemplate(Transform parent, Material material)
        {
            var root = CreateEmpty("Template EmergencyStop", parent);
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Stop Disc";
            disc.transform.SetParent(root.transform, false);
            disc.transform.localPosition = new Vector3(0, 0.45f, 0);
            disc.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            disc.transform.localScale = new Vector3(0.55f, 0.08f, 0.55f);
            disc.GetComponent<Renderer>().sharedMaterial = material;
            Undo.RegisterCreatedObjectUndo(disc, "Bake item template");
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pole.name = "Pole";
            pole.transform.SetParent(root.transform, false);
            pole.transform.localPosition = new Vector3(0, 0.15f, 0);
            pole.transform.localScale = new Vector3(0.08f, 0.3f, 0.08f);
            pole.GetComponent<Renderer>().sharedMaterial = material;
            Undo.RegisterCreatedObjectUndo(pole, "Bake item template");
        }

        // 撞吃：拳套（球）+底座。
        private static void BuildCollisionCaptureTemplate(Transform parent, Material material)
        {
            var root = CreateEmpty("Template CollisionCapture", parent);
            var fist = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fist.name = "Fist";
            fist.transform.SetParent(root.transform, false);
            fist.transform.localPosition = new Vector3(0, 0.45f, 0);
            fist.transform.localScale = Vector3.one * 0.5f;
            fist.GetComponent<Renderer>().sharedMaterial = material;
            Undo.RegisterCreatedObjectUndo(fist, "Bake item template");
            var baseObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseObj.name = "Base";
            baseObj.transform.SetParent(root.transform, false);
            baseObj.transform.localPosition = new Vector3(0, 0.08f, 0);
            baseObj.transform.localScale = new Vector3(0.5f, 0.16f, 0.5f);
            baseObj.GetComponent<Renderer>().sharedMaterial = material;
            Undo.RegisterCreatedObjectUndo(baseObj, "Bake item template");
        }

        // 破墙：锤子（方头+柄）。
        private static void BuildBreakWallTemplate(Transform parent, Material material)
        {
            var root = CreateEmpty("Template BreakWall", parent);
            var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Hammer Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0, 0.5f, 0);
            head.transform.localScale = new Vector3(0.55f, 0.25f, 0.25f);
            head.GetComponent<Renderer>().sharedMaterial = material;
            Undo.RegisterCreatedObjectUndo(head, "Bake item template");
            var handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.name = "Handle";
            handle.transform.SetParent(root.transform, false);
            handle.transform.localPosition = new Vector3(0, 0.2f, 0);
            handle.transform.localScale = new Vector3(0.09f, 0.45f, 0.09f);
            handle.GetComponent<Renderer>().sharedMaterial = material;
            Undo.RegisterCreatedObjectUndo(handle, "Bake item template");
        }

        private static GameObject CreateEmpty(string name, Transform parent)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(obj, "Bake " + name);
            return obj;
        }
    }
}

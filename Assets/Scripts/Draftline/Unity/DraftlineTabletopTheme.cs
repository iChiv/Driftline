using DG.Tweening;
using Draftline.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Draftline.Unity
{
    /// <summary>
    /// Desktop-toy look: applied at runtime so it works in builds without scene edits.
    /// Re-materials the existing board (wooden table, toy-brick walls, glowing portal pads,
    /// faction spawn carpets) and places clickable toy props on the four side areas.
    /// Presentation only — rule coordinates are never touched.
    /// </summary>
    public sealed class DraftlineTabletopTheme : MonoBehaviour
    {
        private static readonly Color WoodTable = new Color(0.42f, 0.30f, 0.19f);
        private static readonly Color WoodRim = new Color(0.55f, 0.38f, 0.22f);
        private static readonly Color BrickMaple = new Color(0.87f, 0.63f, 0.38f);
        private static readonly Color BrickWalnut = new Color(0.42f, 0.28f, 0.16f);
        private static readonly Color BoardPlate = new Color(0.93f, 0.91f, 0.86f);
        private static readonly Color[] PortalPalette =
        {
            Color.clear,
            new Color(1.0f, 0.55f, 0.1f),  // A 橙
            new Color(1.0f, 0.45f, 0.4f),  // B 珊瑚
            new Color(0.95f, 0.3f, 0.85f), // C 品红
            new Color(1.0f, 0.2f, 0.2f)    // D 红
        };
        private static readonly Color[] FactionPalette =
        {
            new Color(0.91f, 0.20f, 0.20f),
            new Color(0.20f, 0.43f, 0.91f),
            new Color(0.20f, 0.72f, 0.34f),
            new Color(0.95f, 0.77f, 0.13f)
        };

        private readonly System.Collections.Generic.List<DraftlineToyProp> props =
            new System.Collections.Generic.List<DraftlineToyProp>();
        private Material rippleMaterial;

        private void Awake()
        {
            Apply();
        }

        public void Apply()
        {
            var layout = new BoardLayout();

            // 桌面：棋盘下方垫一张大木桌，背景色转暖。
            var ground = GameObject.Find("Ground");
            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Wooden Table";
            table.transform.position = new Vector3(0, -0.15f, 0);
            table.transform.localScale = new Vector3(30f, 0.3f, 30f);
            table.GetComponent<Renderer>().sharedMaterial = MakeLit(WoodTable, 0.25f);
            var rim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rim.name = "Table Rim";
            // 边框沿放在棋盘面板下缘，露出一圈木边。
            rim.transform.position = new Vector3(0, 0.1f, 0);
            rim.transform.localScale = new Vector3(21.4f, 0.5f, 21.4f);
            rim.GetComponent<Renderer>().sharedMaterial = MakeLit(WoodRim, 0.35f);
            if (Camera.main != null) Camera.main.backgroundColor = new Color(0.32f, 0.27f, 0.22f);

            // 棋盘面板保持浅色；墙体统一为枫木积木色，旋转墙与中心墙用胡桃木色区分。
            ApplyWallMaterials("Walls", layout);
            ApplyWallMaterials("Border", layout);
            // 传送门：品红方块压扁成发光圆盘并缓慢自旋。
            var portalRoot = GameObject.Find("Portal");
            if (portalRoot != null)
            {
                foreach (Transform child in portalRoot.transform)
                {
                    var color = Color.magenta;
                    if (BoardCoordinates.TryFromWorld(child.position, out var cell))
                    {
                        var definition = layout.CellAt(cell);
                        if (definition.Kind == CellKind.Portal)
                        {
                            var idx = "OCRM".IndexOf(definition.PortalKey);
                            if (idx >= 0) color = PortalPalette[idx + 1];
                        }
                    }
                    child.localScale = new Vector3(0.92f, 0.12f, 0.92f);
                    child.position = new Vector3(child.position.x, 0.55f, child.position.z);
                    var renderer = child.GetComponent<Renderer>();
                    if (renderer != null) renderer.sharedMaterial = MakeEmissive(color, 1.1f);
                    if (child.GetComponent<DraftlinePortalSpin>() == null)
                        child.gameObject.AddComponent<DraftlinePortalSpin>();
                }
            }
            // 出生角：四角色块保留颜色，加轻微自发光。
            var start = GameObject.Find("Start");
            if (start != null)
            {
                foreach (Transform child in start.transform)
                {
                    var renderer = child.GetComponent<Renderer>();
                    if (renderer == null) continue;
                    if (BoardCoordinates.TryFromWorld(child.position, out var cell))
                    {
                        var definition = layout.CellAt(cell);
                        var corner = definition.Owner;
                        var colorIndex = corner == PlayerColor.Red ? 0 : corner == PlayerColor.Blue ? 1
                            : corner == PlayerColor.Green ? 2 : 3;
                        renderer.sharedMaterial = MakeEmissive(FactionPalette[colorIndex], 0.5f);
                    }
                }
            }

            // 四侧摆件（桌面玩具彩蛋）。
            SpawnProp(BuildBlockTower(new Vector3(13.2f, 0, 3f)));
            SpawnProp(BuildToyCar(new Vector3(-13.2f, 0, -2f)));
            SpawnProp(BuildTop(new Vector3(2.5f, 0, 13.2f)));
            SpawnProp(BuildHourglass(new Vector3(-3f, 0, -13.2f)));
        }

        // 场景里的墙是跨格大块且可能嵌套：遍历根下所有 Renderer，
        // 按各自包围盒中心映射格子——旋转墙槽位或正中心 (9,9) 用胡桃木色，其余枫木色。
        private void ApplyWallMaterials(string rootName, BoardLayout layout)
        {
            var root = GameObject.Find(rootName);
            if (root == null) return;
            var maple = MakeLit(BrickMaple, 0.55f);
            var walnut = MakeLit(BrickWalnut, 0.5f);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                var dark = false;
                if (BoardCoordinates.TryFromWorld(renderer.bounds.center, out var cell))
                {
                    dark = layout.CellAt(cell).Kind == CellKind.RotatingWallSlot ||
                           cell == new GridPosition(9, 9);
                }
                renderer.sharedMaterial = dark ? walnut : maple;
            }
        }

        private static Material MakeLit(Color color, float smoothness)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        private static Material MakeEmissive(Color color, float intensity)
        {
            var material = MakeLit(color, 0.6f);
            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", color * intensity);
            return material;
        }

        /* ---------------- 摆件 ---------------- */

        private void SpawnProp(GameObject prop)
        {
            prop.AddComponent<DraftlineToyProp>();
            props.Add(prop.GetComponent<DraftlineToyProp>());
        }

        private static GameObject BuildBlockTower(Vector3 position)
        {
            var root = new GameObject("Toy Block Tower");
            var colors = new[] { new Color(0.9f, 0.4f, 0.35f), new Color(0.35f, 0.7f, 0.9f),
                                 new Color(0.95f, 0.8f, 0.3f) };
            for (var index = 0; index < 3; index++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.SetParent(root.transform, false);
                cube.transform.position = position + new Vector3(0, 0.45f + index * 0.9f, 0);
                cube.transform.localScale = Vector3.one * (0.9f - index * 0.12f);
                cube.GetComponent<Renderer>().sharedMaterial = MakeLit(colors[index], 0.5f);
            }
            return root;
        }

        private static GameObject BuildToyCar(Vector3 position)
        {
            var root = new GameObject("Toy Car");
            var bodyMat = MakeLit(new Color(0.85f, 0.3f, 0.3f), 0.6f);
            var wheelMat = MakeLit(new Color(0.2f, 0.2f, 0.22f), 0.3f);
            var bodyObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bodyObj.transform.SetParent(root.transform, false);
            bodyObj.transform.position = position + new Vector3(0, 0.5f, 0);
            bodyObj.transform.localScale = new Vector3(1.6f, 0.45f, 0.85f);
            bodyObj.GetComponent<Renderer>().sharedMaterial = bodyMat;
            var cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cabin.transform.SetParent(root.transform, false);
            cabin.transform.position = position + new Vector3(-0.15f, 0.85f, 0);
            cabin.transform.localScale = new Vector3(0.8f, 0.35f, 0.75f);
            cabin.GetComponent<Renderer>().sharedMaterial = bodyMat;
            foreach (var offset in new[] { new Vector3(0.55f, 0, 0.45f), new Vector3(0.55f, 0, -0.45f),
                                           new Vector3(-0.55f, 0, 0.45f), new Vector3(-0.55f, 0, -0.45f) })
            {
                var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheel.transform.SetParent(root.transform, false);
                wheel.transform.position = position + offset + new Vector3(0, 0.22f, 0);
                wheel.transform.localScale = new Vector3(0.44f, 0.12f, 0.44f);
                wheel.transform.rotation = Quaternion.Euler(90f, 0, 0);
                wheel.GetComponent<Renderer>().sharedMaterial = wheelMat;
            }
            return root;
        }

        private static GameObject BuildTop(Vector3 position)
        {
            var root = new GameObject("Toy Spinning Top");
            var cone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cone.transform.SetParent(root.transform, false);
            cone.transform.position = position + new Vector3(0, 0.5f, 0);
            cone.transform.localScale = new Vector3(0.9f, 0.5f, 0.9f);
            cone.GetComponent<Renderer>().sharedMaterial = MakeLit(new Color(0.5f, 0.75f, 0.9f), 0.65f);
            return root;
        }

        private static GameObject BuildHourglass(Vector3 position)
        {
            var root = new GameObject("Toy Hourglass");
            var mat = MakeLit(new Color(0.8f, 0.55f, 0.85f), 0.55f);
            foreach (var offset in new[] { -0.35f, 0.35f })
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.SetParent(root.transform, false);
                sphere.transform.position = position + new Vector3(0, 0.5f + (offset > 0 ? 0.5f : 0f), 0);
                sphere.transform.localScale = Vector3.one * 0.7f;
                sphere.GetComponent<Renderer>().sharedMaterial = mat;
            }
            return root;
        }

        /* ---------------- 点击互动（纯表现） ---------------- */

        private void Update()
        {
            // 只在正式对局中响应（开局屏/结果屏的点击不触发桌面涟漪）。
            var controller = GetComponent<DraftlineGameController>();
            if (controller == null || controller.Rules == null ||
                controller.Rules.State.Phase == MatchPhase.GameOver) return;
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || Camera.main == null) return;
            if (!Physics.Raycast(Camera.main.ScreenPointToRay(mouse.position.ReadValue()), out var hit))
                return;
            // 摆件优先：点到摆件就播小动作。
            var prop = hit.collider.GetComponentInParent<DraftlineToyProp>();
            if (prop != null)
            {
                prop.Play();
                return;
            }
            // 桌面涟漪：只响应棋盘以外的桌面区域，不干扰棋盘选棋。
            var point = hit.point;
            if (Mathf.Abs(point.x) > 10.2f || Mathf.Abs(point.z) > 10.2f)
                SpawnRipple(point);
        }

        private void SpawnRipple(Vector3 point)
        {
            if (rippleMaterial == null)
            {
                rippleMaterial = MakeLit(new Color(1f, 0.95f, 0.8f), 0.3f);
                rippleMaterial.EnableKeyword("_EMISSION");
                if (rippleMaterial.HasProperty("_EmissionColor"))
                    rippleMaterial.SetColor("_EmissionColor", new Color(1f, 0.9f, 0.6f) * 0.8f);
            }
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Table Ripple";
            Destroy(ring.GetComponent<Collider>());
            ring.transform.position = new Vector3(point.x, 0.05f, point.z);
            ring.transform.localScale = new Vector3(0.3f, 0.01f, 0.3f);
            ring.GetComponent<Renderer>().sharedMaterial = rippleMaterial;
            ring.transform.DOScale(new Vector3(2.2f, 0.01f, 2.2f), 0.55f).SetEase(Ease.OutQuad);
            ring.transform.DOScale(Vector3.zero, 0.2f).SetDelay(0.5f).SetEase(Ease.InQuad)
                .OnComplete(() => Destroy(ring));
        }
    }

    /// <summary>Clickable toy prop on the table; plays a small action when clicked.</summary>
    public sealed class DraftlineToyProp : MonoBehaviour
    {
        public void Play()
        {
            transform.DOKill();
            transform.DOPunchScale(Vector3.one * 0.2f, 0.4f, 6, 0.5f);
            transform.DORotate(new Vector3(0, 360f, 0), 0.7f, RotateMode.LocalAxisAdd)
                .SetEase(Ease.OutQuad);
        }
    }
}

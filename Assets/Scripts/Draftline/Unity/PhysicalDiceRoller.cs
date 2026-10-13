using System;
using System.Collections;
using UnityEngine;

namespace Draftline.Unity
{
    /// <summary>Host-side physical 1D6 roll above the existing chessboard.</summary>
    public sealed class PhysicalDiceRoller : MonoBehaviour
    {
        // Dice_6 模型实测映射：+Y=2、+Z=1、+X=3、-X=4、-Z=6、-Y=5（相对面和为 7）。
        private static readonly Vector3[] Normals =
            { Vector3.up, Vector3.forward, Vector3.right, Vector3.left, Vector3.back, Vector3.down };
        private static readonly int[] Values = { 2, 1, 3, 4, 6, 5 };

        private GameObject tray;
        private Transform die;
        private Rigidbody body;
        private Material trayMaterial;
        private Material wallMaterial;

        public bool IsRolling { get; private set; }
        public string Status { get; private set; } = "Ready";
        public Transform DieTransform => die;

        private void Awake()
        {
            BuildTray();
            BuildDie();
            tray.SetActive(false);
            die.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
        }

        public IEnumerator Roll(Action<int?> finished)
        {
            if (IsRolling) yield break;
            IsRolling = true;
            Status = "Rolling above the board...";
            tray.SetActive(true);
            die.gameObject.SetActive(true);
            var result = 0;

            for (var attempt = 0; attempt < 4 && result == 0; attempt++)
            {
                ResetAndThrow();
                var elapsed = 0f;
                var stillFor = 0f;
                while (elapsed < 8f)
                {
                    yield return new WaitForFixedUpdate();
                    elapsed += Time.fixedDeltaTime;
                    if (body.velocity.sqrMagnitude < 0.015f &&
                        body.angularVelocity.sqrMagnitude < 0.025f &&
                        die.position.y > 1.9f && die.position.y < 3.2f)
                        stillFor += Time.fixedDeltaTime;
                    else
                        stillFor = 0f;
                    if (stillFor < 0.55f) continue;

                    result = ReadTopFace();
                    if (result != 0) break;
                    Status = "Die is tilted; rerolling...";
                    break;
                }
            }

            if (result != 0)
            {
                Status = $"Rolled {result}";
                yield return new WaitForSeconds(0.65f);
            }
            else
                Status = "Die did not settle. Roll again.";

            body.isKinematic = true;
            die.gameObject.SetActive(false);
            tray.SetActive(false);
            IsRolling = false;
            finished?.Invoke(result == 0 ? (int?)null : result);
        }

        private void ResetAndThrow()
        {
            body.isKinematic = true;
            die.position = new Vector3(UnityEngine.Random.Range(-1.5f, 1.5f), 3.4f,
                UnityEngine.Random.Range(-1.5f, 1.5f));
            die.rotation = UnityEngine.Random.rotation;
            body.isKinematic = false;
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.AddForce(new Vector3(UnityEngine.Random.Range(-3f, 3f), -1f,
                UnityEngine.Random.Range(-3f, 3f)), ForceMode.Impulse);
            body.AddTorque(UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(6f, 12f),
                ForceMode.Impulse);
        }

        private int ReadTopFace()
        {
            var bestDot = -1f;
            var bestValue = 0;
            for (var index = 0; index < Normals.Length; index++)
            {
                var dot = Vector3.Dot(die.TransformDirection(Normals[index]), Vector3.up);
                if (dot <= bestDot) continue;
                bestDot = dot;
                bestValue = Values[index];
            }
            return bestDot >= 0.93f ? bestValue : 0;
        }

        private void BuildTray()
        {
            tray = new GameObject("Temporary Dice Tray");
            tray.transform.SetParent(transform, false);
            trayMaterial = NewMaterial(new Color(0.6f, 0.8f, 0.95f, 0.16f));
            if (trayMaterial.HasProperty("_Surface")) trayMaterial.SetFloat("_Surface", 1f);
            if (trayMaterial.HasProperty("_ZWrite")) trayMaterial.SetFloat("_ZWrite", 0f);
            if (trayMaterial.HasProperty("_SrcBlend"))
                trayMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (trayMaterial.HasProperty("_DstBlend"))
                trayMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            trayMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            trayMaterial.SetOverrideTag("RenderType", "Transparent");
            trayMaterial.renderQueue = 3000;
            wallMaterial = NewMaterial(new Color(0.20f, 0.25f, 0.35f, 0.7f));
            // The tray sits just above the tallest board walls and is collision-only:
            // renderers stay off so nothing shows or casts shadows on the board.
            // Walls are tall enough that an energetic throw cannot escape.
            CreateBox("Tray Floor", new Vector3(0, 1.75f, 0),
                new Vector3(19f, 0.25f, 19f), trayMaterial);
            CreateBox("Wall North", new Vector3(0, 3.1f, 9.5f),
                new Vector3(19.5f, 2.6f, 0.5f), wallMaterial);
            CreateBox("Wall South", new Vector3(0, 3.1f, -9.5f),
                new Vector3(19.5f, 2.6f, 0.5f), wallMaterial);
            CreateBox("Wall East", new Vector3(9.5f, 3.1f, 0),
                new Vector3(0.5f, 2.6f, 19f), wallMaterial);
            CreateBox("Wall West", new Vector3(-9.5f, 3.1f, 0),
                new Vector3(0.5f, 2.6f, 19f), wallMaterial);
        }

        private void CreateBox(string name, Vector3 position, Vector3 scale, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(tray.transform, false);
            box.transform.position = position;
            box.transform.localScale = scale;
            var renderer = box.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.enabled = false; // Collision-only tray: invisible and casts no shadows.
        }

        private void BuildDie()
        {
            var root = new GameObject("Physical D6");
            die = root.transform;
            die.SetParent(transform, false);

            // Dice_6 pack (from main branch): proper PBR die with texture pips.
            var prefab = Resources.Load<GameObject>("DiceD6");
            if (prefab != null)
            {
                var visual = Instantiate(prefab, die, false);
                visual.name = "Die Visual";
                visual.transform.localPosition = Vector3.zero;
                foreach (var collider in visual.GetComponentsInChildren<Collider>())
                    Destroy(collider);
                // Normalize to the tray scale regardless of the mesh's authored size.
                var bounds = visual.GetComponentInChildren<Renderer>().bounds;
                var fit = 0.8f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                visual.transform.localScale = Vector3.one * fit;
            }
            else
            {
                Debug.LogWarning("DiceD6 prefab missing from Resources; falling back to a plain cube.");
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Die Body";
                cube.transform.SetParent(die, false);
                cube.transform.localScale = Vector3.one * 0.8f;
                cube.GetComponent<Renderer>().sharedMaterial = NewMaterial(Color.white);
                Destroy(cube.GetComponent<Collider>());
            }

            root.AddComponent<BoxCollider>().size = Vector3.one * 0.8f;
            body = root.AddComponent<Rigidbody>();
            body.mass = 0.4f;
            body.drag = 0.2f;
            body.angularDrag = 0.25f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.isKinematic = true;
        }

        private static Material NewMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            return material;
        }
    }
}

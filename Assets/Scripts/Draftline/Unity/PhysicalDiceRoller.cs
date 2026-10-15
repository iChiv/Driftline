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

        public bool IsRolling { get; private set; }
        public string Status { get; private set; } = "Ready";
        public Transform DieTransform => die;

        private void Awake()
        {
            // Dice rig lives in the scene (baked); find it instead of building at runtime.
            var rig = GameObject.Find("Dice Rig");
            if (rig == null)
            {
                Debug.LogError("Dice Rig missing from scene — run Draftline/Bake Tabletop Into Scene.");
                enabled = false;
                return;
            }
            var trayTransform = rig.transform.Find("Dice Tray");
            var dieTransform = rig.transform.Find("Physical D6");
            if (trayTransform == null || dieTransform == null)
            {
                Debug.LogError("Dice Rig is incomplete (needs Dice Tray + Physical D6 children).");
                enabled = false;
                return;
            }
            tray = trayTransform.gameObject;
            die = dieTransform;
            body = die.GetComponent<Rigidbody>();
            tray.SetActive(false);
            die.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            ResetRoll();
        }

        public void ResetRoll()
        {
            IsRolling = false;
            if (body != null) body.isKinematic = true;
            if (die != null) die.gameObject.SetActive(false);
            if (tray != null) tray.SetActive(false);
            Status = "准备投掷";
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
                if (attempt == 0) yield return FlyInFromTable();
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

        // 首次投掷时，骰子从桌边骰子（Roll Dice）的位置弧线飞入托盘上空。
        private IEnumerator FlyInFromTable()
        {
            body.isKinematic = true;
            var tableDice = GameObject.Find("Roll Dice");
            var target = new Vector3(UnityEngine.Random.Range(-1.5f, 1.5f), 3.4f,
                UnityEngine.Random.Range(-1.5f, 1.5f));
            if (tableDice == null)
            {
                die.position = target;
                die.rotation = UnityEngine.Random.rotation;
                yield break;
            }
            var start = tableDice.transform.position + Vector3.up * 0.6f;
            die.position = start;
            die.rotation = UnityEngine.Random.rotation;
            const float duration = 0.45f;
            var spin = UnityEngine.Random.onUnitSphere * 540f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var k = Mathf.Clamp01(elapsed / duration);
                var position = Vector3.Lerp(start, target, k);
                position.y += Mathf.Sin(k * Mathf.PI) * 1.8f;
                die.position = position;
                die.Rotate(spin * Time.deltaTime, Space.World);
                yield return null;
            }
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

    }
}

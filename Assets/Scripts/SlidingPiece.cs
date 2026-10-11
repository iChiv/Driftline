using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

public class SlidingPiece : MonoBehaviour
{
    [Header("网格与速度配置")]
    public float gridSize = 1.0f;
    public float slideSpeed = 12f;

    private int remainingSteps = 0;
    private bool isMoving = false;
    private bool isWaitingForDirectionInput = false;

    // 存储预测出来的最终目的地，以及中途是否需要触发传送
    private Vector3 finalDestination;
    private bool willPortal = false;
    private Portal crossedPortal = null;
    private float originalY; 
    void Start()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
        originalY = transform.position.y;
        transform.position = SnapPosition(transform.position);
    }

    public void StartSlidingRoutine(int totalSteps)
    {
        remainingSteps = totalSteps;
        isWaitingForRollOrInput();
    }

    private void isWaitingForRollOrInput()
    {
        if (remainingSteps <= 0)
        {
            isWaitingForDirectionInput = false;
            GameManager.Instance.SwitchToNextTurn();
            return;
        }
        isWaitingForDirectionInput = true;
    }

    void Update()
    {
        if (isWaitingForDirectionInput && !isMoving && Keyboard.current != null)
        {
            Vector3 chosenDirection = Vector3.zero;
            if (Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame) chosenDirection = Vector3.forward;
            else if (Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame) chosenDirection = Vector3.back;
            else if (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame) chosenDirection = Vector3.left;
            else if (Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame) chosenDirection = Vector3.right;

            if (chosenDirection != Vector3.zero)
            {
                isWaitingForDirectionInput = false;
                ExecuteSlideStep(chosenDirection);
            }
        }
    }

    private void ExecuteSlideStep(Vector3 direction)
    {
        // 【核心升级】：预测时传入当前方向，计算是否会进入传送门
        PredictRoute(transform.position, direction);

        if (finalDestination == transform.position)
        {
            remainingSteps--;
            isWaitingForRollOrInput();
            return;
        }

        // 开启支持传送的平滑滑行协程
        StartCoroutine(SlideWithPortalRoutine(direction));
    }

    // 【多段智能射线预测 - 增加堵塞检测版】
    private void PredictRoute(Vector3 startPos, Vector3 dir)
    {
        willPortal = false;
        crossedPortal = null;
        finalDestination = startPos;

        RaycastHit hit;
        Vector3 rayOrigin = startPos + Vector3.up * 0.04f;
        Debug.DrawRay(rayOrigin, dir * gridSize * 5f, Color.green, 2f);
        // 第一段射线：看从当前位置出发会撞到什么
        if (Physics.Raycast(rayOrigin, dir, out hit, 100f))
        {
            // 情况 A：撞到了传送门！
            if (hit.collider.CompareTag("Portal"))
            {
                Portal srcPortal = hit.collider.GetComponent<Portal>();
                if (srcPortal != null && srcPortal.targetPortal != null)
                {
                    // 1. 算出如果没有被堵住，跨出传送门后的【第一个落脚格点】在哪里
                    Vector3 exitPos = srcPortal.targetPortal.transform.position;
                    Vector3 exitDir = srcPortal.targetPortal.exitDirection; // 使用你之前修正好的方向
                    Vector3 firstExitGrid = SnapPosition(exitPos + exitDir * gridSize);

                    // 2. 【核心重叠检测】：用一个半径 0.4 米的隐形球，去扫描这个出口格点上有没有其他棋子
                    // 传入 PieceLayer 的层级掩码
                    int pieceLayerMask = LayerMask.GetMask("PieceLayer");
                    bool isExitBlocked = Physics.CheckSphere(firstExitGrid + Vector3.up * 0.04f, 0.4f, pieceLayerMask);

                    if (isExitBlocked)
                    {
                        // 🔴 【核心逻辑触发】：出口被堵住了！
                        willPortal = false; // 取消传送！
                        crossedPortal = null;

                        // 算出进门前的最后一个完美格点（即紧贴着入口门前的那一格）
                        int entranceSteps = Mathf.FloorToInt((hit.distance - 0.1f) / gridSize);
                        finalDestination = startPos + dir * entranceSteps * gridSize;
                        finalDestination = SnapPosition(finalDestination);

                        Debug.LogWarning($"⚠️ 传送门对面的出口被其他棋子堵住了！拒绝传送，改停在原本的入口旁！");
                        return;
                    }
                    else
                    {
                        // 🟢 出口安全，允许传送！
                        willPortal = true;
                        crossedPortal = srcPortal;

                        RaycastHit secondHit;
                        float offsetDistance = 0.5f;
                        Vector3 secondRayOrigin = exitPos + Vector3.down * 0.46f + exitDir * offsetDistance;

                        int layerMaskWithoutPortals = LayerMask.GetMask("PortalLayer", "InvisibleWall");
                        int filter = ~layerMaskWithoutPortals;
                        if (Physics.Raycast(secondRayOrigin, exitDir, out secondHit, 100f, filter))
                        {
                            if (secondHit.collider.CompareTag("Wall") || secondHit.collider.CompareTag("Piece"))
                            {
                                // 实际的总距离 = 射线测出的距离 + 我们之前往前挪的 0.5 米
                                float realDistance = secondHit.distance + offsetDistance;

                                // 用真实的无误差距离来计算完美的格子步数
                                int secondSteps = Mathf.FloorToInt((realDistance - 0.1f) / gridSize);

                                // 算出最终精准的目的地
                                finalDestination = exitPos + exitDir * secondSteps * gridSize;
                                finalDestination = SnapPosition(finalDestination);
                                return;

                            }
                        }
                    }
                }
            }
            // 情况 B：普通的普通撞墙
            else if (hit.collider.CompareTag("Wall") || hit.collider.CompareTag("Piece"))
            {
                int steps = Mathf.FloorToInt((hit.distance - 0.1f) / gridSize);
                finalDestination = startPos + dir * steps * gridSize;
                finalDestination = SnapPosition(finalDestination);
                return;
            }
        }
    }
    // 【配合堵塞机制的滑行表现】
    private IEnumerator SlideWithPortalRoutine(Vector3 initialDir)
    {
        isMoving = true;

        // 只有当预测可以通过、且不被堵塞时，才触发中途传送
        if (willPortal && crossedPortal != null)
        {
            Vector3 portalGridPos = SnapPosition(crossedPortal.transform.position);

            // 阶段 1：平滑滑向 A 门
            while (Vector3.Distance(transform.position, portalGridPos) > 0.05f)
            {
                transform.position = Vector3.MoveTowards(transform.position, portalGridPos, slideSpeed * Time.deltaTime);
                yield return null;
            }

            // 让入口和出口门闪烁
            if (crossedPortal != null) crossedPortal.TriggerFlash();
            if (crossedPortal.targetPortal != null) crossedPortal.targetPortal.TriggerFlash();

            // 阶段 2：瞬间移动到 B 门
            transform.position = SnapPosition(crossedPortal.targetPortal.transform.position);
            yield return new WaitForSeconds(0.02f);
        }

        // 阶段 3：继续滑向最终阻挡点（如果willPortal为false，这里会直接平滑滑到入口前的格点）
        while (Vector3.Distance(transform.position, finalDestination) > 0.001f)
        {
            transform.position = Vector3.MoveTowards(transform.position, finalDestination, slideSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = finalDestination;
        isMoving = false;

        remainingSteps--;
        Debug.Log($"🛑 停稳！消耗 1 步。剩余步数：{remainingSteps}");

        yield return new WaitForSeconds(0.15f);
        isWaitingForRollOrInput();
    }
    private Vector3 SnapPosition(Vector3 pos)
    {
        float x = Mathf.Round(pos.x / gridSize) * gridSize;
        float z = Mathf.Round(pos.z / gridSize) * gridSize;
        return new Vector3(x, originalY, z);
    }
}
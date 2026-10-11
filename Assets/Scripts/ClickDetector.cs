using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; 
public class PieceClickDetector : MonoBehaviour
{
    private Camera mainCamera;

    void Start()
    {
        // 缓存主摄像机，提升性能
        mainCamera = Camera.main; 
    }

    void Update()
    {
        // 1. 检测鼠标左键被按下
        bool isClick = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        if (isClick)
        {
            // 2. 从摄像机发射一条穿过屏幕鼠标位置的射线
            Ray ray = mainCamera.ScreenPointToRay(mousePos);
            RaycastHit hit;

            // 3. 【核心物理过滤】获取 InvisibleWall 层的 LayerMask，然后取反 (~) 
            // 这意味着射线会穿透空气墙，只触碰剩下的所有物体
            int layerMask = ~LayerMask.GetMask("Barrier");

            // 4. 进行射线检测，传入 layerMask 过滤器
            if (Physics.Raycast(ray, out hit, 100f, layerMask))
            {
                // 如果射线穿过空气墙后，刚好撞到了当前这个棋子
                if (hit.transform == this.transform)
                {
                    Debug.Log("棋子被点击！");
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.SelectPieceToMove(this.transform);
                    }
                }
            }
        }
    }
}

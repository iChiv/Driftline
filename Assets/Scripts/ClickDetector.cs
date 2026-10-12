using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; 

public class PieceClickDetector : MonoBehaviour
{
    private Camera mainCamera;
    private SlidingPiece slidingPiece;
    private bool isHovering = false;

    [Header("悬停高光配置")]
    public float hoverYOffset = 0.15f; // 鼠标悬停时，棋子往上抬高的高度（建议 0.15f 即可，1f 太高了）

    void Start()
    {
        mainCamera = Camera.main; 
        slidingPiece = GetComponent<SlidingPiece>();
    }

    void Update()
    {
        // 核心安全拦截：只有轮到当前玩家，且处于【等待选择棋子】阶段时，这枚棋子才允许被悬停或点击
        if (GameManager.Instance == null || GameManager.Instance.currentState != GameManager.GameState.WaitingForSelectPiece)
        {
            if (isHovering) ResetHoverHeight();
            return;
        }

        // =================== 【1. 每帧主动发射穿透射线（解决空气墙阻挡）】 ===================
        Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        Ray ray = mainCamera.ScreenPointToRay(mousePos);
        RaycastHit hit;

        // 穿透 Barrier（空气墙）层，只看底下的棋子
        int layerMask = ~LayerMask.GetMask("Barrier");
        bool isMouseOverMe = false;

        if (Physics.Raycast(ray, out hit, 100f, layerMask))
        {
            // 射线穿过空气墙后，正好照在自己身上
            if (hit.transform == this.transform)
            {
                isMouseOverMe = true;
            }
        }

        // =================== 【2. 处理手动的悬停高光（仅限己方棋子）】 ===================
        if (isMouseOverMe && IsMyPiece())
        {
            if (!isHovering)
            {
                isHovering = true;
                // 悬停时抬高 Y 轴
                transform.position = new Vector3(transform.position.x, transform.position.y + hoverYOffset, transform.position.z);
            }
        }
        else
        {
            if (isHovering)
            {
                ResetHoverHeight();
            }
        }

        // =================== 【3. 处理手动的点击出子/移动】 ===================
        bool isClick = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        
        if (isClick && isMouseOverMe && IsMyPiece())
        {
            Debug.Log($"{gameObject.name} 被成功点击！");
            
            // 点击前先强行恢复悬停高度，防止带着错位的高度数据传给起飞或滑行逻辑
            ResetHoverHeight();
            
            GameManager.Instance.SelectPieceToMove(this.transform);
        }
    }

    // 严谨的恢复高度函数（防止陷进地板）
    private void ResetHoverHeight()
    {
        if (!isHovering) return;
        isHovering = false;
        transform.position = new Vector3(transform.position.x, transform.position.y - hoverYOffset, transform.position.z);
    }

    // 当棋子不小心被隐藏或禁用时，强制归位
    void OnDisable()
    {
        if (isHovering) ResetHoverHeight();
    }

    // 辅助工具：判断这枚棋子是不是属于当前回合的玩家
    private bool IsMyPiece()
    {
        int currentTurn = GameManager.Instance.currentTurn;
        foreach (Transform p in GameManager.Instance.players[currentTurn].myPieces)
        {
            if (p == this.transform) return true;
        }
        return false;
    }
}
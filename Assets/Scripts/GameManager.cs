using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [System.Serializable]
    public struct PlayerConfig
    {
        public string playerName;        // 玩家名字（如：红方、蓝方）
        public Transform[] myPieces;   // 旗下的 10 个棋子
        public Vector3 startPos;         // 该玩家对应的棋盘起始点坐标
    }

    [Header("4个玩家的多棋子配置")]
    public PlayerConfig[] players = new PlayerConfig[4];

    [Header("当前回合状态")]
    public int currentTurn = 0;
    public int currentDicePoints = 0;
    public bool isDouble = false;         // 当前摇出的是否为双数

    public enum GameState { WaitingForRoll, WaitingForSelectPiece, PieceMoving }
    [Header("当前游戏所处阶段")]
    public GameState currentState = GameState.WaitingForRoll;
    private bool wallState = false; //false->inner ring | true->outer ring
    private List<DynamicWall> allDynamicWalls = new List<DynamicWall>();
    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        Debug.Log($"游戏开始！当前是【{players[currentTurn].playerName}】的回合，请摇骰子。");
        DynamicWall[] foundWalls = FindObjectsByType<DynamicWall>(FindObjectsSortMode.None);
        allDynamicWalls.AddRange(foundWalls);

        // 开局第一回合：初始化一次地图（让外圈墙1先隐退并暗中移动）
        InitializeMazeOnStart();
    }
    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
        {
            Debug.LogWarning("~【开发者指令】: 强制跳过当前玩家的回合！~");

            SwitchToNextTurn();
        }
    }
    private void InitializeMazeOnStart()
    {
        foreach (DynamicWall wall in allDynamicWalls)
        {
            if (wall.wallType == 1) // 假设第一回合是内圈2的舞台，外圈1先退场准备
            {
                wall.HideAndAdvance();
            }
            else if (wall.wallType == 2)
            {
                wall.TryToSpawn();
            }
        }
    }
    private void RefreshDynamicMaze()
    {
        // 交替标志位
        wallState = !wallState;
        // 只需要一个循环，统一对总仓里的所有墙壁进行标签过滤操作
        foreach (DynamicWall wall in allDynamicWalls)
        {
            if (wall.wallType == 0) continue; // 常驻墙0绝对不干预，直接跳过

            if (!wallState)
            {
                // 当前是【内圈回合】：内圈2尝试出现，外圈1隐藏并暗中移动
                if (wall.wallType == 2) wall.TryToSpawn();
                if (wall.wallType == 1) wall.HideAndAdvance();
            }
            else
            {
                // 当前是【外圈回合】：外圈1尝试出现，内圈2隐藏并暗中移动
                if (wall.wallType == 1) wall.TryToSpawn();
                if (wall.wallType == 2) wall.HideAndAdvance();
            }
        }
    }
    // 1. 骰子停稳后调用
    public void OnDiceFinished(int points)
    {
        currentDicePoints = points;
        // 判断是否为双数 (2点、4点、6点)
        isDouble = (points % 2 == 0);

        Debug.Log($"【{players[currentTurn].playerName}】摇出了 {points} 点！(双数: {isDouble})");

        // 检查起始点是否被堵住
        bool isStartBlocked = IsStartPointBlocked();

        // 核心规则验证：看看当前玩家有没有资格“做出选择”
        if (HasValidMove(isStartBlocked))
        {
            currentState = GameState.WaitingForSelectPiece;
            Debug.Log("请在场景中用鼠标点击你的一枚棋子进行移动或出子起飞。");
        }
        else
        {
            // 如果摇了单数且场上没子，或者起始点被堵死且场上没别的子可走，直接洗白换人
            Debug.LogWarning($"❌ 【{players[currentTurn].playerName}】当前无法进行任何合法移动！直接跳过本回合。");
            SwitchToNextTurn();
        }
    }

    // 检查当前玩家的起始点位上是否有任何棋子（不分敌我）
    public bool IsStartPointBlocked()
    {
        Vector3 checkPos = players[currentTurn].startPos;
        // 这里的半径用 0.35f 刚好可以覆盖一格
        int pieceLayerMask = LayerMask.GetMask("PieceLayer");
        bool isBlocked = Physics.CheckSphere(checkPos + Vector3.up * 0.2f, 0.35f, pieceLayerMask);

        if (isBlocked) Debug.Log($"【提示】{players[currentTurn].playerName} 的起始点位当前有棋子堵着！");
        return isBlocked;
    }

    // 判断当前玩家是否有棋子可走（用于防死锁）
    private bool HasValidMove(bool isStartBlocked)
    {
        bool hasPiecesActiveInField = false;

        // 遍历当前玩家的 10 个子
        foreach (Transform piece in players[currentTurn].myPieces)
        {
            SlidingPiece script = piece.GetComponent<SlidingPiece>();
            if (script != null && script.isSpawned)
            {
                hasPiecesActiveInField = true; // 场上有已经起飞的子
                break;
            }
        }

        // 情况 1：摇到了双数，且起始点没有被堵住 -> 绝对可以出子（或者走场上的子）
        if (isDouble && !isStartBlocked) return true;

        // 情况 2：摇到了单数（或起始点被堵），但场上有已经起飞的棋子 -> 可以选择移动场上的子
        if (hasPiecesActiveInField) return true;

        // 否则，既不能出子，场上又没子可走，死路一条
        return false;
    }

    // 2. 玩家点击属于自己的棋子
    public void SelectPieceToMove(Transform selectedPiece)
    {
        if (currentState != GameState.WaitingForSelectPiece) return;

        if (IsPieceOwnedByCurrentPlayer(selectedPiece))
        {
            SlidingPiece pieceScript = selectedPiece.GetComponent<SlidingPiece>();

            // 【核心分支 A】：点击的是在基地、未出生的棋子
            if (!pieceScript.isSpawned)
            {
                if (!isDouble)
                {
                    Debug.LogWarning("❌ 只有摇到双数（2/4/6）才能让基地里的棋子出子起飞！");
                    return;
                }
                if (IsStartPointBlocked())
                {
                    Debug.LogWarning("❌ 起始点位有子阻挡，无法强行出子！请选择场上的其他棋子移动。");
                    return;
                }

                // 成功出子起飞！
                currentState = GameState.PieceMoving;
                pieceScript.SpawnToStartPoint(players[currentTurn].startPos);
            }
            // 【核心分支 B】：点击的是已经在场上滑行的棋子
            else
            {
                currentState = GameState.PieceMoving;
                pieceScript.StartSlidingRoutine(currentDicePoints);
            }
        }
        else
        {
            Debug.LogWarning($"选错啦！这不是【{players[currentTurn].playerName}】的棋子！");
        }
    }

    private bool IsPieceOwnedByCurrentPlayer(Transform piece)
    {
        foreach (Transform p in players[currentTurn].myPieces)
        {
            if (p == piece) return true;
        }
        return false;
    }
    public void SwitchToNextTurn()
    {
        currentTurn = (currentTurn + 1) % 4;
        currentState = GameState.WaitingForRoll;
        Debug.Log($"【回合切换】现在轮到【{players[currentTurn].playerName}】的回合了！");
        if (currentTurn == 0)
        {
            RefreshDynamicMaze();
        }
    }
}
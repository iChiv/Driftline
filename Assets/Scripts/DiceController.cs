using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DiceController : MonoBehaviour
{
    private Rigidbody rb;
    private bool isRolling = false;

    void Start()
    {
        // 获取骰子身上的刚体组件
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // 当玩家按下“空格键”，且骰子当前没有在滚动时
        if (Input.GetKeyDown(KeyCode.Space) && !isRolling)
        {
            RollDice();
        }

        // 检测骰子是否已经停止滚动
        if (isRolling && rb.velocity.sqrMagnitude < 0.001f && rb.angularVelocity.sqrMagnitude < 0.001f)
        {
            isRolling = false;
            CheckDiceSide(); // 骰子停稳后，计算点数
        }
    }

    // 摇骰子：给它一个向上的力和随机的旋转力
    void RollDice()
    {
        isRolling = true;

        // 1. 给骰子一个随机的初始旋转角度，防止每次落下的轨迹一样
        transform.rotation = Random.rotation;

        // 2. 给它一个向上的弹跳力（数值 5 可以根据需要调整）
        rb.AddForce(Vector3.up * 5f, ForceMode.Impulse);

        // 3. 给它一个随机的扭转力（让它在空中翻滚）
        Vector3 randomTorque = new Vector3(Random.Range(5, 15), Random.Range(5, 15), Random.Range(5, 15));
        rb.AddTorque(randomTorque, ForceMode.Impulse);
    }

    // 判断哪一面朝上
    void CheckDiceSide()
    {
        // 通过对比骰子的各个局部方向与世界的“正上方(Vector3.up)”来判断点数
        // 这里的点数对应关系取决于你的骰子贴图/模型方向
        if (Vector3.Dot(transform.up, Vector3.up) > 0.9f) Debug.Log("摇出了：6 点");
        else if (Vector3.Dot(-transform.up, Vector3.up) > 0.9f) Debug.Log("摇出了：1 点");
        else if (Vector3.Dot(transform.right, Vector3.up) > 0.9f) Debug.Log("摇出了：5 点");
        else if (Vector3.Dot(-transform.right, Vector3.up) > 0.9f) Debug.Log("摇出了：2 点");
        else if (Vector3.Dot(transform.forward, Vector3.up) > 0.9f) Debug.Log("摇出了：4 点");
        else if (Vector3.Dot(-transform.forward, Vector3.up) > 0.9f) Debug.Log("摇出了：3 点");
        else Debug.Log("骰子好像立起来了或斜着，没停稳！");
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Key : MonoBehaviour
{
    private bool isCollected = false; // 标记是否已拾取

    void OnTriggerEnter2D(Collider2D other)
    {
        // 1. 判断：玩家触发 + 未拾取过
        if (other.CompareTag("Player") && !isCollected)
        {
            // 2. 通知玩家收集钥匙（需确保Player有KeyManager脚本）
            other.GetComponent<KeyManager>().CollectKey();

            // 3. 禁用碰撞器（防止重复触发）+ 标记已拾取
            GetComponent<Collider2D>().enabled = false;
            isCollected = true;

            // （可选）隐藏钥匙视觉（若需保留外观，可跳过；若需半透明，取消下方注释）
            // GetComponent<SpriteRenderer>().color = new Color(1, 1, 1, 0.5f); 
        }
    }
        // Start is called before the first frame update
        void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

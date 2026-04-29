using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Key : MonoBehaviour
{
    private bool isCollected = false;

    void OnTriggerEnter2D(Collider2D other)
    {
        // 确保碰撞体是玩家且还没被收集
        if (other.CompareTag("Player") && !isCollected)
        {
            // 获取玩家身上的 KeyManager 组件
            KeyManager km = other.GetComponent<KeyManager>();

            if (km != null)
            {
                km.CollectKey();
                isCollected = true;

                // 禁用碰撞体防止重复触发
                GetComponent<Collider2D>().enabled = false;

                // 变成半透明或销毁
                GetComponent<SpriteRenderer>().color = new Color(1, 1, 1, 0.5f);
                // 如果想让钥匙消失，直接 Destroy(gameObject);
            }
        }
    }
}

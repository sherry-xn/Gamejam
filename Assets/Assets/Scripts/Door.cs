using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Door : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            KeyManager km = other.GetComponent<KeyManager>();
            if (km != null && km.HasAllKeys())
            {
                Debug.Log("逃脱成功！");
                // 这里可以加切换场景或显示胜利UI
            }
            else
            {
                Debug.Log("钥匙不足，门打不开！");
            }
        }
    }
}

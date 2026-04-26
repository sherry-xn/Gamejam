using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Key : MonoBehaviour
{
    private bool isCollected = false; // ����Ƿ���ʰȡ

    // void OnTriggerEnter2D(Collider2D other)
    // {
    //     // 1. �жϣ���Ҵ��� + δʰȡ��
    //     if (other.CompareTag("Player") && !isCollected)
    //     {
    //         // 2. ֪ͨ����ռ�Կ�ף���ȷ��Player��KeyManager�ű���
    //         other.GetComponent<KeyManager>().CollectKey();

    //         // 3. ������ײ������ֹ�ظ�������+ �����ʰȡ
    //         GetComponent<Collider2D>().enabled = false;
    //         isCollected = true;

    //         // ����ѡ������Կ���Ӿ������豣����ۣ��������������͸����ȡ���·�ע�ͣ�
    //         // GetComponent<SpriteRenderer>().color = new Color(1, 1, 1, 0.5f); 
    //     }
    // }
        // Start is called before the first frame update
        void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float speed = 5f;    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        float moveX = Input.GetAxis("Horizontal");
        float moveY = Input.GetAxis("Vertical");

        transform.Translate(new Vector2(moveX, moveY) * speed * Time.deltaTime);
        //限制视角，不出屏幕
        float camHeight = Camera.main.orthographicSize;
        float camWidth = camHeight * Camera.main.aspect;

        float clampedX = Mathf.Clamp(transform.position.x, -camWidth, camWidth);
        float clampedY = Mathf.Clamp(transform.position.y, -camHeight, camHeight);
        transform.position = new Vector2(clampedX, clampedY);
    }
}

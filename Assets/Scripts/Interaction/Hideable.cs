using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Hideable : MonoBehaviour
{
    [SerializeField] private Transform hidePoint;
    [SerializeField, Range(0f, 1f)] private float hiddenAlpha = 0.7f;

    private Collider2D col;
    private SpriteRenderer sr;
    private float originalAlpha;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) originalAlpha = sr.color.a;
    }

    public void OnEnter(PlayerController player)
    {
        col.enabled = false;
        if (sr != null)
        {
            Color c = sr.color;
            c.a = hiddenAlpha;
            sr.color = c;
        }
        if (hidePoint != null)
            player.SetPlayerPosition(hidePoint.position);
    }

    public void OnExit(PlayerController player)
    {
        col.enabled = true;
        if (sr != null)
        {
            Color c = sr.color;
            c.a = originalAlpha;
            sr.color = c;
        }
    }
}

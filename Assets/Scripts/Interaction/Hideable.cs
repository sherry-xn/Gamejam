using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Hideable : MonoBehaviour
{
    [SerializeField] private Transform hidePoint;
    [SerializeField, Tooltip("离开躲藏时放置玩家；不填则用进入躲藏前的世界坐标，避免仍在家具碰撞体内就恢复碰撞导致被挤出穿墙。")]
    private Transform exitPoint;
    [SerializeField, Range(0f, 1f)] private float hiddenAlpha = 0.7f;

    private Collider2D col;
    private SpriteRenderer sr;
    private float originalAlpha;
    private readonly List<Collider2D> ignoredPlayerColliders = new List<Collider2D>();
    private Vector3 worldPositionBeforeHide;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) originalAlpha = sr.color.a;
    }

    private void OnDisable()
    {
        RestoreIgnoredPlayerCollisions();
    }

    public void OnEnter(PlayerController player)
    {
        worldPositionBeforeHide = player.transform.position;
        SetPlayerCollisionIgnored(player, true);
        if (sr != null)
        {
            Color c = sr.color;
            c.a = hiddenAlpha;
            sr.color = c;
        }
        if (hidePoint != null)
            player.SetPlayerPosition(hidePoint.position);
        AudioManager.Instance.Play(SFX.HideIn);
    }

    public void OnExit(PlayerController player)
    {
        // 先移到家具外再恢复碰撞：否则在 hidePoint 内与 Collider 重叠，物理解算可能把玩家挤出墙体。
        Vector3 exitWorld = exitPoint != null ? exitPoint.position : worldPositionBeforeHide;
        player.SetPlayerPosition(exitWorld);
        if (player.Rigidbody != null)
            player.Rigidbody.velocity = Vector2.zero;
        Physics2D.SyncTransforms();

        SetPlayerCollisionIgnored(player, false);
        if (sr != null)
        {
            Color c = sr.color;
            c.a = originalAlpha;
            sr.color = c;
        }
        AudioManager.Instance.Play(SFX.HideOut);
    }

    private void SetPlayerCollisionIgnored(PlayerController player, bool ignored)
    {
        if (col == null || player == null) return;

        if (ignored)
        {
            RestoreIgnoredPlayerCollisions();
            Collider2D[] playerColliders = player.GetComponentsInChildren<Collider2D>();
            foreach (Collider2D playerCollider in playerColliders)
            {
                if (playerCollider == null || !playerCollider.enabled) continue;

                Physics2D.IgnoreCollision(col, playerCollider, true);
                ignoredPlayerColliders.Add(playerCollider);
            }

            return;
        }

        RestoreIgnoredPlayerCollisions();
    }

    private void RestoreIgnoredPlayerCollisions()
    {
        if (col == null)
        {
            ignoredPlayerColliders.Clear();
            return;
        }

        foreach (Collider2D playerCollider in ignoredPlayerColliders)
        {
            if (playerCollider != null)
            {
                Physics2D.IgnoreCollision(col, playerCollider, false);
            }
        }

        ignoredPlayerColliders.Clear();
    }
}

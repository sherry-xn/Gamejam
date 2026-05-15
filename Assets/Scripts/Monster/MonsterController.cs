using UnityEngine;
using System.Collections.Generic;
using Pathfinding;

public enum MonsterState
{
    Tracking,
    Wandering
}

[RequireComponent(typeof(Seeker))]
[RequireComponent(typeof(AIPath))]
public class MonsterController : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private MonsterInfo data;

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private PlayerController playerController;

    [Header("Animation")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite idleSprite;
    [SerializeField] private Sprite[] runFrames;
    [SerializeField, Min(1f)] private float runFrameRate = 12f;
    [SerializeField, Min(0f)] private float movementAnimationThreshold = 0.01f;

    [Header("Debug")]
    [SerializeField] private MonsterState currentState = MonsterState.Wandering;

    [Header("Door")]
    [SerializeField, Tooltip("Distance used to detect doors in front of the monster.")]
    private float doorDetectionRange = 1.5f;

    [Header("Path")]
    [SerializeField, Tooltip("How close the monster must get to a waypoint before moving to the next one.")]
    private float nextWaypointDistance = 0.2f;
    [SerializeField, Tooltip("How often the monster refreshes its path while chasing.")]
    private float trackingRepathInterval = 0.25f;
    [SerializeField, Tooltip("Layers that block monster movement.")]
    private LayerMask movementBlockMask;
    [SerializeField, Tooltip("How often the monster requests a new path after its collider hits a wall.")]
    private float blockedRepathInterval = 0.35f;
    [SerializeField, Tooltip("Extra clearance around the monster when validating wander destinations.")]
    private float destinationClearance = 0.05f;

    [Header("SFX")]
    [SerializeField, Tooltip("Monster footstep interval in seconds.")]
    private float monsterFootstepInterval = 0.5f;

    private float lastMonsterFootstepTime;

    [Header("BGM")]
    [SerializeField, Tooltip("Distance at which the monster-near BGM starts.")]
    private float bgmNearDistance = 15f;

    private const int MaxWanderPointAttempts = 12;
    private const int MaxGlobalWanderPointAttempts = 32;
    private const float MinWanderPointDistance = 1f;
    private const float NavGraphClearancePadding = 0.1f;

    private AIPath aiPath;
    private Seeker seeker;
    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private float lastAttackTime;
    private float lastWanderTime;
    private Vector2 wanderTarget;
    private bool hasWanderTarget;
    private readonly HashSet<int> openedDoorIds = new HashSet<int>();
    private BGM lastBGMState = BGM.None;

    private List<Vector3> currentPath;
    private int currentWaypoint;
    private bool pathPending;
    private float lastPathRequestTime = float.NegativeInfinity;
    private Vector3 currentDestination;
    private bool hasDestination;
    private readonly RaycastHit2D[] movementHits = new RaycastHit2D[8];
    private readonly Collider2D[] destinationHits = new Collider2D[8];
    private float lastBlockedRepathTime = float.NegativeInfinity;
    private bool wasMoving;
    private int runFrameIndex;
    private float runFrameTimer;

    private void Awake()
    {
        aiPath = GetComponent<AIPath>();
        seeker = GetComponent<Seeker>();
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        CacheAnimationReferences();
    }

    private void Start()
    {
        if (data == null)
        {
            enabled = false;
            return;
        }

        // We use Seeker for path calculation, and move in XY ourselves to avoid AIPath 2D/3D orientation issues.
        aiPath.isStopped = true;
        aiPath.canSearch = false;
        aiPath.simulateMovement = false;
        aiPath.enabled = false;

        if (movementBlockMask.value == 0)
        {
            movementBlockMask = LayerMask.GetMask("Default", "Obstacle");
        }

        EnsureNavigationGraphClearance();
        SwitchToWandering();
    }

    private void Update()
    {
        UpdateBGMState();

        switch (currentState)
        {
            case MonsterState.Tracking:
                TrackingUpdate();
                break;
            case MonsterState.Wandering:
                WanderingUpdate();
                break;
        }

        MoveAlongPath(Time.deltaTime);
        UpdateMonsterAnimation();
    }

    private void CacheAnimationReferences()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (idleSprite == null && spriteRenderer != null)
        {
            idleSprite = spriteRenderer.sprite;
        }
    }

    private void UpdateMonsterAnimation()
    {
        if (spriteRenderer == null) return;

        bool isMoving = IsMoving() || rb != null && rb.velocity.sqrMagnitude > movementAnimationThreshold;
        if (!isMoving)
        {
            wasMoving = false;
            runFrameIndex = 0;
            runFrameTimer = 0f;
            if (idleSprite != null)
            {
                spriteRenderer.sprite = idleSprite;
            }
            return;
        }

        if (runFrames == null || runFrames.Length == 0) return;
        if (!wasMoving)
        {
            wasMoving = true;
            runFrameIndex = 0;
            runFrameTimer = 0f;
            spriteRenderer.sprite = runFrames[runFrameIndex];
            return;
        }

        runFrameTimer += Time.deltaTime;
        float frameDuration = 1f / runFrameRate;
        while (runFrameTimer >= frameDuration)
        {
            runFrameTimer -= frameDuration;
            runFrameIndex = (runFrameIndex + 1) % runFrames.Length;
            spriteRenderer.sprite = runFrames[runFrameIndex];
        }
    }

    private void CheckPlayerState()
    {
        if (playerController == null || player == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        if (playerController.IsInteracting || distanceToPlayer > data.trackingRange)
        {
            if (currentState == MonsterState.Tracking)
            {
                SwitchToWandering();
            }
        }
        else if (currentState == MonsterState.Wandering && distanceToPlayer <= data.trackingRange)
        {
            SwitchToTracking();
        }
    }

    private void SwitchToTracking()
    {
        currentState = MonsterState.Tracking;
        hasWanderTarget = false;
        ClearCurrentPath();

        AudioManager.Instance.PlayAtPosition(SFX.MonsterGrowl, transform.position);
        AudioManager.Instance.PlayAtPosition(SFX.MonsterChase, transform.position);
    }

    private void SwitchToWandering()
    {
        currentState = MonsterState.Wandering;
        hasWanderTarget = false;
        lastWanderTime = Time.time;
        ClearCurrentPath();
        SetNewWanderPoint();
    }

    private void TrackingUpdate()
    {
        if (Time.time - lastMonsterFootstepTime >= monsterFootstepInterval)
        {
            lastMonsterFootstepTime = Time.time;
            AudioManager.Instance.PlayAtPosition(SFX.MonsterFootstep, transform.position);
        }

        CheckPlayerState();

        if (currentState != MonsterState.Tracking) return;
        if (player == null) return;

        CheckForDoor();
        if (Time.time - lastPathRequestTime >= trackingRepathInterval || !hasDestination)
        {
            RequestPathToNearestWalkable(player.position);
        }

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        if (distanceToPlayer <= data.attackRange)
        {
            TryAttackPlayer();
        }
    }

    private void WanderingUpdate()
    {
        if (IsMoving() && Time.time - lastMonsterFootstepTime >= monsterFootstepInterval * 1.5f)
        {
            lastMonsterFootstepTime = Time.time;
            AudioManager.Instance.PlayAtPosition(SFX.MonsterFootstep, transform.position);
        }

        CheckPlayerState();

        if (currentState != MonsterState.Wandering) return;

        CheckForDoor(GetCurrentMoveDirection());

        bool reachedWanderTarget = hasWanderTarget && !IsFollowingPath();
        if ((!hasWanderTarget || reachedWanderTarget) && Time.time - lastWanderTime >= data.wanderInterval)
        {
            SetNewWanderPoint();
            lastWanderTime = Time.time;
        }
    }

    private void SetNewWanderPoint()
    {
        if (TryGetGlobalWanderPoint(out Vector3 globalTarget))
        {
            SetWanderTarget(globalTarget);
            return;
        }

        SetLocalWanderPoint();
    }

    private void SetLocalWanderPoint()
    {
        for (int i = 0; i < MaxWanderPointAttempts; i++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * data.wanderRadius;
            if (randomOffset.sqrMagnitude < MinWanderPointDistance * MinWanderPointDistance)
            {
                continue;
            }

            Vector3 candidate = transform.position + (Vector3)randomOffset;
            if (!TryGetNearestWalkablePoint(candidate, out Vector3 walkableTarget))
            {
                continue;
            }

            if (Vector2.Distance(transform.position, walkableTarget) < MinWanderPointDistance)
            {
                continue;
            }

            if (!IsPositionClear(walkableTarget))
            {
                continue;
            }

            SetWanderTarget(walkableTarget);
            return;
        }

        hasWanderTarget = false;
        ClearCurrentPath();
    }

    private bool TryGetGlobalWanderPoint(out Vector3 target)
    {
        target = transform.position;
        if (AstarPath.active == null || AstarPath.active.data == null)
        {
            return false;
        }

        NNInfo currentNearest = AstarPath.active.GetNearest(transform.position, NearestNodeConstraint.Walkable);
        GraphNode currentNode = currentNearest.node;
        if (currentNode == null || !currentNode.Walkable)
        {
            return false;
        }

        uint currentArea = currentNode.Area;
        for (int i = 0; i < MaxGlobalWanderPointAttempts; i++)
        {
            GraphNode selectedNode = null;
            int eligibleCount = 0;
            AstarPath.active.data.GetNodes(node =>
            {
                if (node == null || !node.Walkable || node.Area != currentArea) return;

                Vector3 nodePosition = (Vector3)node.position;
                if (Vector2.Distance(transform.position, nodePosition) < MinWanderPointDistance) return;

                eligibleCount++;
                if (Random.Range(0, eligibleCount) == 0)
                {
                    selectedNode = node;
                }
            });

            if (selectedNode == null)
            {
                return false;
            }

            Vector3 candidate = selectedNode.RandomPointOnSurface();
            candidate.z = transform.position.z;
            if (IsPositionClear(candidate))
            {
                target = candidate;
                return true;
            }
        }

        return false;
    }

    private void SetWanderTarget(Vector3 target)
    {
        wanderTarget = target;
        hasWanderTarget = true;
        RequestPath(wanderTarget);
    }

    private void RequestPathToNearestWalkable(Vector3 target)
    {
        if (TryGetNearestWalkablePoint(target, out Vector3 walkableTarget))
        {
            RequestPath(walkableTarget);
        }
    }

    private void RequestPath(Vector3 target)
    {
        if (seeker == null || pathPending) return;
        if (hasDestination && Vector2.Distance(currentDestination, target) < 0.05f && IsFollowingPath()) return;

        currentDestination = target;
        hasDestination = true;
        pathPending = true;
        lastPathRequestTime = Time.time;
        seeker.StartPath(transform.position, target, OnPathComplete);
    }

    private void OnPathComplete(Path path)
    {
        pathPending = false;
        if (path == null || path.error || path.vectorPath == null || path.vectorPath.Count == 0)
        {
            ClearCurrentPath();
            if (currentState == MonsterState.Wandering)
            {
                hasWanderTarget = false;
            }
            return;
        }

        currentPath = BuildPathPoints(path);
        currentWaypoint = 0;
        AdvancePastReachedWaypoints();
    }

    private void MoveAlongPath(float deltaTime)
    {
        if (!IsFollowingPath()) return;

        AdvancePastReachedWaypoints();
        if (!IsFollowingPath()) return;

        Vector3 current = transform.position;
        Vector3 target = currentPath[currentWaypoint];
        target.z = current.z;

        Vector3 desiredNext = Vector3.MoveTowards(current, target, data.moveSpeed * deltaTime);
        if (!TryGetUnblockedPosition(current, desiredNext, out Vector3 next))
        {
            RequestBlockedRepath();
            return;
        }

        if (rb != null)
        {
            rb.MovePosition(next);
        }
        else
        {
            transform.position = next;
        }
    }

    private List<Vector3> BuildPathPoints(Path path)
    {
        List<Vector3> points = new List<Vector3>();
        if (path.path != null && path.path.Count > 0)
        {
            for (int i = 0; i < path.path.Count; i++)
            {
                AddPathPoint(points, (Vector3)path.path[i].position);
            }
        }
        else
        {
            for (int i = 0; i < path.vectorPath.Count; i++)
            {
                AddPathPoint(points, path.vectorPath[i]);
            }
        }

        return points;
    }

    private void AddPathPoint(List<Vector3> points, Vector3 point)
    {
        point.z = transform.position.z;
        if (points.Count == 0 || Vector2.Distance(points[points.Count - 1], point) > nextWaypointDistance * 0.5f)
        {
            points.Add(point);
        }
    }

    private bool TryGetUnblockedPosition(Vector3 current, Vector3 desiredNext, out Vector3 next)
    {
        next = desiredNext;
        Vector2 delta = desiredNext - current;
        float distance = delta.magnitude;
        if (distance <= Mathf.Epsilon || movementBlockMask.value == 0)
        {
            return true;
        }

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(movementBlockMask);
        filter.useTriggers = false;

        int hitCount = 0;
        Vector2 direction = delta / distance;
        if (bodyCollider != null)
        {
            hitCount = bodyCollider.Cast(direction, filter, movementHits, distance + 0.02f);
        }
        else
        {
            hitCount = Physics2D.Raycast(current, direction, filter, movementHits, distance + 0.02f);
        }

        float allowedDistance = distance;
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = movementHits[i].collider;
            if (hitCollider == null || hitCollider.isTrigger) continue;
            if (bodyCollider != null && hitCollider == bodyCollider) continue;
            if (rb != null && hitCollider.attachedRigidbody == rb) continue;

            allowedDistance = Mathf.Min(allowedDistance, Mathf.Max(0f, movementHits[i].distance - 0.02f));
        }

        if (allowedDistance <= 0.001f)
        {
            return false;
        }

        next = current + (Vector3)(direction * allowedDistance);
        return true;
    }

    private void RequestBlockedRepath()
    {
        if (!hasDestination || pathPending) return;
        if (Time.time - lastBlockedRepathTime < blockedRepathInterval) return;

        lastBlockedRepathTime = Time.time;
        if (currentState == MonsterState.Wandering)
        {
            hasWanderTarget = false;
            ClearCurrentPath();
            lastWanderTime = Time.time - data.wanderInterval;
            SetNewWanderPoint();
            return;
        }

        ClearCurrentPath(false);
        RequestPathToNearestWalkable(currentDestination);
    }

    private void AdvancePastReachedWaypoints()
    {
        while (currentPath != null && currentWaypoint < currentPath.Count)
        {
            Vector3 waypoint = currentPath[currentWaypoint];
            waypoint.z = transform.position.z;
            if (Vector2.Distance(transform.position, waypoint) > nextWaypointDistance)
            {
                break;
            }

            currentWaypoint++;
        }

        if (currentPath != null && currentWaypoint >= currentPath.Count)
        {
            ClearCurrentPath();
            if (currentState == MonsterState.Wandering)
            {
                hasWanderTarget = false;
            }
        }
    }

    private bool IsFollowingPath()
    {
        return currentPath != null && currentWaypoint < currentPath.Count;
    }

    private bool IsMoving()
    {
        return IsFollowingPath() || pathPending;
    }

    private Vector2 GetCurrentMoveDirection()
    {
        if (IsFollowingPath())
        {
            Vector2 direction = (Vector2)(currentPath[currentWaypoint] - transform.position);
            if (direction.sqrMagnitude > 0.001f)
            {
                return direction.normalized;
            }
        }

        if (hasDestination)
        {
            Vector2 direction = (Vector2)(currentDestination - transform.position);
            if (direction.sqrMagnitude > 0.001f)
            {
                return direction.normalized;
            }
        }

        if (player != null)
        {
            Vector2 direction = (Vector2)(player.position - transform.position);
            if (direction.sqrMagnitude > 0.001f)
            {
                return direction.normalized;
            }
        }

        return transform.right;
    }

    private void ClearCurrentPath()
    {
        ClearCurrentPath(true);
    }

    private void ClearCurrentPath(bool clearDestination)
    {
        if (pathPending && seeker != null)
        {
            seeker.CancelCurrentPathRequest();
        }

        currentPath = null;
        currentWaypoint = 0;
        pathPending = false;
        if (clearDestination)
        {
            hasDestination = false;
        }
    }

    private bool TryGetNearestWalkablePoint(Vector3 position, out Vector3 walkablePoint)
    {
        walkablePoint = position;
        if (AstarPath.active == null)
        {
            return true;
        }

        NNInfo nearest = AstarPath.active.GetNearest(position, NearestNodeConstraint.Walkable);
        if (nearest.node == null)
        {
            return false;
        }

        walkablePoint = nearest.position;
        walkablePoint.z = transform.position.z;
        return true;
    }

    private void EnsureNavigationGraphClearance()
    {
        if (AstarPath.active == null || AstarPath.active.data == null || bodyCollider == null)
        {
            return;
        }

        Bounds bounds = bodyCollider.bounds;
        float requiredWorldDiameter = Mathf.Max(bounds.size.x, bounds.size.y) + NavGraphClearancePadding;
        bool graphChanged = false;

        foreach (NavGraph graph in AstarPath.active.data.graphs)
        {
            if (graph is GridGraph gridGraph)
            {
                float nodeSize = Mathf.Max(0.01f, gridGraph.nodeSize);
                float requiredGraphDiameter = requiredWorldDiameter / nodeSize;
                if (gridGraph.collision.diameter < requiredGraphDiameter)
                {
                    gridGraph.collision.diameter = requiredGraphDiameter;
                    graphChanged = true;
                }

                if (gridGraph.cutCorners)
                {
                    gridGraph.cutCorners = false;
                    graphChanged = true;
                }
            }
        }

        if (graphChanged)
        {
            AstarPath.active.Scan();
        }
    }

    private bool IsPositionClear(Vector3 position)
    {
        if (movementBlockMask.value == 0)
        {
            return true;
        }

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(movementBlockMask);
        filter.useTriggers = false;

        int hitCount;
        if (bodyCollider is BoxCollider2D boxCollider)
        {
            Vector2 center = position + (Vector3)boxCollider.offset;
            Vector2 size = Vector2.Scale(boxCollider.size, transform.lossyScale);
            size.x = Mathf.Abs(size.x) + destinationClearance * 2f;
            size.y = Mathf.Abs(size.y) + destinationClearance * 2f;
            hitCount = Physics2D.OverlapBox(center, size, transform.eulerAngles.z, filter, destinationHits);
            return !HasBlockingDestinationHit(hitCount);
        }

        if (bodyCollider is CircleCollider2D circleCollider)
        {
            Vector2 center = position + (Vector3)circleCollider.offset;
            float radius = circleCollider.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
            hitCount = Physics2D.OverlapCircle(center, radius + destinationClearance, filter, destinationHits);
            return !HasBlockingDestinationHit(hitCount);
        }

        hitCount = Physics2D.OverlapCircle(position, nextWaypointDistance + destinationClearance, filter, destinationHits);
        return !HasBlockingDestinationHit(hitCount);
    }

    private bool HasBlockingDestinationHit(int hitCount)
    {
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = destinationHits[i];
            if (hitCollider == null || hitCollider.isTrigger) continue;
            if (bodyCollider != null && hitCollider == bodyCollider) continue;
            if (rb != null && hitCollider.attachedRigidbody == rb) continue;
            return true;
        }

        return false;
    }

    private void CheckForDoor()
    {
        if (player == null) return;

        Vector2 direction = ((Vector2)player.position - (Vector2)transform.position).normalized;
        CheckForDoor(direction);
    }

    private void CheckForDoor(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.001f) return;
        direction.Normalize();

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            doorDetectionRange,
            LayerMask.GetMask("Default")
        );

        foreach (Collider2D hit in hits)
        {
            if (hit == null || !hit.CompareTag("Door")) continue;

            Door doorComponent = hit.GetComponent<Door>();
            if (doorComponent != null && (!doorComponent.CanMonsterOpen || doorComponent.IsOpen))
            {
                continue;
            }

            Vector2 doorDir = ((Vector2)hit.transform.position - (Vector2)transform.position).normalized;
            float dot = Vector2.Dot(direction, doorDir);
            if (dot <= 0.5f) continue;

            int doorId = hit.GetInstanceID();
            if (!openedDoorIds.Contains(doorId))
            {
                OpenDoor(hit.gameObject);
            }
        }
    }

    private void OpenDoor(GameObject door)
    {
        int doorId = door.GetInstanceID();
        Door doorComponent = door.GetComponent<Door>();
        if (doorComponent != null)
        {
            if (!doorComponent.CanMonsterOpen || doorComponent.IsOpen) return;
            doorComponent.ApplyOpenState(true, 0.7f);
        }
        else
        {
            SpriteRenderer sr = door.GetComponentInChildren<SpriteRenderer>(true);
            if (sr != null)
            {
                Color c = sr.color;
                c.a = 0.7f;
                sr.color = c;
            }

            foreach (Collider2D col in door.GetComponentsInChildren<Collider2D>(true))
            {
                if (!col.isTrigger)
                {
                    col.enabled = false;
                }
            }
        }

        openedDoorIds.Add(doorId);
        AudioManager.Instance.PlayAtPosition(SFX.DoorOpen, transform.position);
        AudioManager.Instance.PlayAtPosition(SFX.MonsterWallHit, transform.position);
        if (hasDestination)
        {
            RequestPathToNearestWalkable(currentDestination);
        }
    }

    private void TryAttackPlayer()
    {
        if (playerController == null) return;
        if (Time.time - lastAttackTime < data.attackCooldown) return;

        lastAttackTime = Time.time;
        playerController.TakeDamage((int)data.attackDamage);
        AudioManager.Instance.Play(SFX.PlayerHit);
    }

    private void UpdateBGMState()
    {
        if (player == null) return;

        float distance = Vector2.Distance(transform.position, player.position);
        BGM desiredBGM = distance <= bgmNearDistance ? BGM.MonsterNear : BGM.Exploration;
        if (desiredBGM != lastBGMState)
        {
            lastBGMState = desiredBGM;
            AudioManager.Instance.PlayBGM(desiredBGM);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (data == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, data.trackingRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, data.attackRange);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, data.wanderRadius);

        Gizmos.color = Color.green;
        Vector2 dir = player != null ?
            ((Vector2)player.position - (Vector2)transform.position).normalized :
            (Vector2)transform.right;
        Gizmos.DrawRay(transform.position, dir * doorDetectionRange);
    }
}

using UnityEngine;
using Pathfinding;

public class Door : MonoBehaviour
{
    [Header("寻路更新")]
    [SerializeField] private bool updateNavGraph = true;
    
    /// <summary>
    /// 当门状态改变时调用此方法更新寻路网格
    /// </summary>
    public void OnDoorStateChanged()
    {
        if (updateNavGraph && AstarPath.active != null)
        {
            // 更新门周围的寻路网格
            AstarPath.active.UpdateGraphs(GetComponent<Collider2D>().bounds);
            Debug.Log("门状态改变，更新寻路网格");
        }
    }
}

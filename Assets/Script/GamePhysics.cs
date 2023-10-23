using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class GamePhysics : MonoBehaviour
{
    public static float FrameRate { get { return 60; } }
    [SerializeField]private float gravity;
    public static float Gravity { get { return Instance.gravity; } }

    private static Vector2 FloatFixed = new(0.5f, 0.5f);
    [SerializeField] private LayerMask solidLayer, actorLayer;
    public static bool CheckHorizontalSolid(Vector2 startPoint, Vector2 endPoint)
    {
        if (startPoint.y != endPoint.y)
        {
            Debug.Log("Invalid parameters are gived for GetHorizontalSolids()");
            Debug.Break();
        }
        if (startPoint.x > endPoint.x)
        {
            Vector2 temp = startPoint;
            startPoint = endPoint;
            endPoint = temp;
        }

        startPoint += FloatFixed;
        float distance = Vector2.Distance(endPoint + FloatFixed, startPoint);
        Collider2D hitCollider = Physics2D.Raycast(startPoint, Vector2.right, distance, Instance.solidLayer).collider;
        return hitCollider != null;
    }
    
    public static bool CheckVerticleSolid(Vector2 startPoint, Vector2 endPoint)
    {
        if (startPoint.y > endPoint.y)
        {
            Vector2 temp = startPoint;
            startPoint = endPoint;
            endPoint = temp;
        }

        startPoint += FloatFixed;
        float distance = Vector2.Distance(endPoint + FloatFixed, startPoint);
        Collider2D hitCollider = Physics2D.Raycast(startPoint, Vector2.up, distance, Instance.solidLayer).collider;
        return hitCollider != null;
    }
    // check solid and one-way platform
    private static bool CheckHorizontalOWPlatform(OneWayPlatform platform, float yCollidingPosition, float yCallerPosition, float callerHeight)
    {
        if (platform.collidingDirection == Direction8.Up)
            return yCallerPosition > yCollidingPosition;
        else if (platform.collidingDirection == Direction8.Down)
            return yCallerPosition + callerHeight < yCollidingPosition;
        else
            return false;
    }
    private static bool CheckVerticleOWPlatform(OneWayPlatform platform, float xCollidingPosition, float xCallerPosition, float callerWidth)
    {
        if (platform.collidingDirection == Direction8.Left)
            return xCallerPosition + callerWidth < xCollidingPosition;
        else if (platform.collidingDirection == Direction8.Right)
            return xCallerPosition > xCollidingPosition;
        else
            return false;
    }
    public static bool CheckHorizontalPlatform(Vector2 startPoint, Vector2 endPoint, float yCallerPosition, float callerHeight)
    {
        if (startPoint.y != endPoint.y)
        {
            Debug.Log("Invalid parameters are gived for CheckHorizontalPlatform()");
            Debug.Break();
        }
        if (startPoint.x > endPoint.x)
        {
            Vector2 temp = startPoint;
            startPoint = endPoint;
            endPoint = temp;
        }
        startPoint += FloatFixed;
        float distance = Vector2.Distance(endPoint + FloatFixed, startPoint);
        RaycastHit2D[] hitColliders = Physics2D.RaycastAll(startPoint, Vector2.right, distance, Instance.solidLayer);
        foreach (RaycastHit2D hitSolid in hitColliders)
        {
            Solid solid = hitSolid.collider.GetComponent<Solid>();
            return !solid.IsOneWayPlatform() || CheckHorizontalOWPlatform(solid.GetComponent<OneWayPlatform>(), startPoint.y, yCallerPosition, callerHeight);
        }
        return false;
    }
    public static bool CheckVerticlePlatform(Vector2 startPoint, Vector2 endPoint, float xCallerPosition, float callerWidth)
    {
        if (startPoint.x != endPoint.x)
        {
            Debug.Log("Invalid parameters are gived for CheckVerticlePlatform()");
            Debug.Break();
        }
        if (startPoint.y > endPoint.y)
        {
            Vector2 temp = startPoint;
            startPoint = endPoint;
            endPoint = temp;
        }

        startPoint += FloatFixed;
        float distance = Vector2.Distance(endPoint + FloatFixed, startPoint);
        RaycastHit2D[] hitColliders = Physics2D.RaycastAll(startPoint, Vector2.up, distance, Instance.solidLayer);
        foreach (RaycastHit2D hitSolid in hitColliders)
        {
            Solid solid = hitSolid.collider.GetComponent<Solid>();
            return !solid.IsOneWayPlatform() || CheckVerticleOWPlatform(solid.GetComponent<OneWayPlatform>(), startPoint.x, xCallerPosition, callerWidth);
        }
        return false;
    }
    public static bool CheckSpecificSolidInArea(Vector2 leftBottom, Vector2 rightTop, Solid targetSolid)
    {
        leftBottom += FloatFixed;
        rightTop += FloatFixed;
        Vector2 center = (leftBottom + rightTop) / 2;
        Vector2 size = rightTop - leftBottom;
        Collider2D[] colliders = Physics2D.OverlapBoxAll(center, size, 0f, Instance.solidLayer);

        foreach (var collider in colliders)
        {
            Solid solid = collider.GetComponent<Solid>();
            if (targetSolid == solid)
                return true;
        }
        return false;
    }
    //public static bool CheckSpecificSolidInArea(Vector2 leftBottom, Vector2 rightTop, Solid targetSolid)
    //{
    //    Solid[] overlappedSolids = GetOverlappedSolids(leftBottom, rightTop);
    //    return overlappedSolids.Length > 0 && Array.IndexOf(overlappedSolids, targetSolid) > -1;
    //}
    public static bool CheckSpecificSolidInPosition(Vector2 position, Solid targetSolid)
    {
        Solid[] overlappedSolids = GetHorizontalSolids(position, position);
        return overlappedSolids.Length > 0 && Array.IndexOf(overlappedSolids, targetSolid) > -1;
    }
    public static bool CheckSpecificActorInArea(Vector2 leftBottom, Vector2 rightTop, Actor targetActor)
    {
        Actor[] overlappedSolids = GetOverlappedActors(leftBottom, rightTop);
        return overlappedSolids.Length > 0 && Array.IndexOf(overlappedSolids, targetActor) > -1;
    }



    #region Get Solid
    public static Solid[] GetHorizontalSolids(Vector2 startPoint, Vector2 endPoint)
    {
        if(startPoint.y != endPoint.y)
        {
            Debug.Log("Invalid parameters are gived for GetHorizontalSolids()");
            Debug.Break();
        }
        if (startPoint.x > endPoint.x)
        {
            Vector2 temp = startPoint;
            startPoint = endPoint;
            endPoint = temp;
        }

        startPoint += FloatFixed;
        float distance = Vector2.Distance(endPoint + FloatFixed, startPoint);
        RaycastHit2D[] hitSolids = Physics2D.RaycastAll(startPoint, Vector2.right, distance, Instance.solidLayer);
        List<Solid> solids = new();
        foreach (var hitSolid in hitSolids)
        {
            Solid solid = hitSolid.transform.GetComponent<Solid>();
            if (solid != null)
            {
                solids.Add(solid);
            }
        }
        return solids.ToArray();
    }
    public static Solid[] GetVerticleSolids(Vector2 startPoint, Vector2 endPoint)
    {
        if (startPoint.x != endPoint.x)
        {
            Debug.Log("Invalid parameters are gived for GetVerticleSolids()");
            Debug.Break();
        }
        if (startPoint.y > endPoint.y)
        {
            Vector2 temp = startPoint;
            startPoint = endPoint;
            endPoint = temp;
        }

        startPoint += FloatFixed;
        float distance = Vector2.Distance(endPoint + FloatFixed, startPoint);
        RaycastHit2D[] hitSolids = Physics2D.RaycastAll(startPoint, Vector2.up, distance, Instance.solidLayer);
        List<Solid> solids = new();
        foreach (var hitSolid in hitSolids)
        {
            Solid solid = hitSolid.transform.GetComponent<Solid>();
            if (solid != null)
            {
                solids.Add(solid);
            }
        }
        return solids.ToArray();
    }
    public static Solid[] GetOverlappedSolids(Vector2 leftBottom, Vector2 rightTop)
    {
        leftBottom += FloatFixed;
        rightTop += FloatFixed;
        Vector2 center = (leftBottom + rightTop) / 2;
        Vector2 size = rightTop - leftBottom;
        Collider2D[] colliders = Physics2D.OverlapBoxAll(center, size, 0f, Instance.solidLayer);
        // 檢查碰撞體是否有Actor類別並加入列表
        List<Solid> solids = new();
        foreach (var collider in colliders)
        {
            Solid solid = collider.GetComponent<Solid>();
            if (solid != null)
            {
                solids.Add(solid);
            }
        }

        return solids.ToArray();
    }

    public static Actor[] GetOverlappedActors(Vector2 leftBottom, Vector2 rightTop)
    {
        leftBottom += FloatFixed;
        rightTop += FloatFixed;
        Vector2 center = (leftBottom + rightTop) / 2;
        Vector2 size = rightTop - leftBottom;
        Collider2D[] colliders = Physics2D.OverlapBoxAll(center, size, 0f, Instance.actorLayer);
        // 檢查碰撞體是否有Actor類別並加入列表
        List<Actor> actors = new();
        foreach (var collider in colliders)
        {
            Actor actor = collider.GetComponent<Actor>();
            if (actor != null)
            {
                actors.Add(actor);
            }
        }

        return actors.ToArray();
    }
    #endregion

    
    #region Updates
    private int stopTimeStartFrame, stopTimeEndFrame;
    public static void EngineStop(int frame)
    {
        Instance.stopTimeStartFrame = 0;
        Instance.stopTimeEndFrame = frame;
    }
    private bool CheckStopTime()
    {
        Instance.stopTimeStartFrame++;
        return Instance.stopTimeStartFrame <= Instance.stopTimeEndFrame;
    }
    #endregion


    private static GamePhysics _instance;

    // 使用List保存所有註冊的方法
    private List<PhysicsUpdateMessage> registeredUpdateMessages = new List<PhysicsUpdateMessage>();

    public static GamePhysics Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<GamePhysics>();
                if (_instance == null)
                {
                    GameObject obj = new GameObject();
                    _instance = obj.AddComponent<GamePhysics>();
                    obj.name = "GamePhysics";
                }
            }
            return _instance;
        }
    }
    
    void FixedUpdate()
    {
        if (CheckStopTime())
            return;

        // 遍歷所有已註冊的方法，然後依序呼叫
        foreach (var updateMessage in registeredUpdateMessages)
        {
            updateMessage.updateAction.Invoke();
        }
    }

    public void RegisterUpdate(PhysicsUpdateMessage updateMessage)
    {
        if (updateMessage != null && !registeredUpdateMessages.Contains(updateMessage))
        {
            registeredUpdateMessages.Add(updateMessage);
            registeredUpdateMessages.Sort((x, y) =>
            {
                int priorityComparison = x.updatePriority.CompareTo(y.updatePriority);
                if (priorityComparison != 0)
                {
                    return priorityComparison;
                }
                return x.order.CompareTo(y.order);
            });
        }
    }

    public void UnregisterUpdate(PhysicsUpdateMessage updateMessage)
    {
        if (updateMessage != null)
        {
            registeredUpdateMessages.Remove(updateMessage);
        }
    }

    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            Application.Quit();
        if (Input.GetKeyDown(KeyCode.Space))
            Debug.Break();
    }
}

public class PhysicsUpdateMessage
{
    public Action updateAction;
    public int updatePriority;
    public int order;
    public PhysicsUpdateMessage(Action action, int priority, int orderNumber)
    {
        updateAction = action;
        updatePriority = priority;
        order = orderNumber;
    }
}
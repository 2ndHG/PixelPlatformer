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
        if(startPoint.x > endPoint.x)
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
    public static Solid[] GetOverlappedSolids(Vector2 leftBottom, Vector2 rightTop)
    {
        leftBottom += FloatFixed;
        rightTop += FloatFixed;
        Vector2 center = (leftBottom + rightTop) / 2;
        Vector2 size = rightTop - leftBottom;
        Collider2D[] colliders = Physics2D.OverlapBoxAll(center, size, 0f, Instance.solidLayer);
        // �ˬd�I����O�_��Actor���O�å[�J�C��
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
        // �ˬd�I����O�_��Actor���O�å[�J�C��
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

    
    private static GamePhysics _instance;

    // �ϥ�List�O�s�Ҧ����U����k
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
        // �M���Ҧ��w���U����k�A�M��̧ǩI�s
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
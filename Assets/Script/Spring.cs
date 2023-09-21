using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spring : Actor
{
    [SerializeField] private float yJumpVelocity;
    private Vector2 DetectLeftBottom, DetectRightTop;

    [SerializeField] private bool rideBelowSolid, mountOnBelowSolid;

    public Spring()
    {
        UpdatePriority = (int)UpdatePriorityEnum.beforePlayer;
    }

    public override void PhysicUpdate()
    {
        //Debug.Log(GamePhysic.GetOverlappedActor(new Vector2(position.x, position.y), new Vector2(position.x + size.width, position.y + size.width)).Length);
        Actor[] OverlappedActors = GamePhysics.GetOverlappedActors(DetectLeftBottom, DetectRightTop);
        foreach (Actor a in OverlappedActors)
        {
            PlayerController PC = a.GetComponent<PlayerController>();
            //Move to Exact Y
            PC.MoveY(position.y+4 - PC.GetLeftBottomPoint().y);

            PC.ForceJump(yJumpVelocity);

        }
            
    }
    private void UpdateDetectingPoint()
    {
        DetectLeftBottom = new Vector2(position.x, position.y);
        DetectRightTop = new Vector2(position.x + size.width - 1, position.y + size.height - 1);
    }
    public override void MoveX(float amount, Action onCollide = null)
    {
        if (mountOnBelowSolid)
            base.MoveXIgnoreSolid((int)amount);
        else
        {
            base.MoveX(amount, onCollide);
        }
        UpdateDetectingPoint();
    }
    public override void MoveY(float amount, Action onCollide = null)
    {
        if (mountOnBelowSolid)
            base.MoveYIgnoreSolid((int)amount);
        else
        {
            base.MoveY(amount, onCollide);
        }
        UpdateDetectingPoint();
    }
    private void Start()
    {
        InitializePosition();
        GamePhysics.Instance.RegisterUpdate(new PhysicsUpdateMessage(PhysicUpdate, (int)UpdatePriority, Order));
        UpdateDetectingPoint();
        if(rideBelowSolid)
        {
            Solid[] solids = GetSolidsBelow();
            if (solids.Length == 0)
            {
                if (ridingSolid != null)
                    ridingSolid.Leave(this);
                ridingSolid = null;
            }
            else if (solids.Length == 1)
            {
                UpdateRidingSolidAndTell(solids[0]);
            }
            else
            {
                Solid toRide = solids[0].RidingPriority < solids[1].RidingPriority ? solids[0] : solids[1];
                UpdateRidingSolidAndTell(toRide);
            }
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingPlatform : Solid
{
    private float xRemainder, yRemainder;
    [SerializeField] private float speed;
    [SerializeField] private Position endPoint1, endPoint2;
    private Vector2 endPoint1v, endPoint2v;
    private Vector2 toEndPoint2Speed;
    private float minimumStep;

    private bool goingTo1;
    #region Riding
    private HashSet<Actor> movedActors = new HashSet<Actor>();
    #endregion


    public MovingPlatform()
    {
        UpdatePriority = (int)UpdatePriorityEnum.beforePlayer;
    }


    public void CalculateStepX(float amount)
    {
        xRemainder += amount;
    }

    public void CalculateStepY(float amount)
    {
        yRemainder += amount;
    }
    public override void MoveXY(int xAmount, int yAmount)
    {
        int xPixel = (int)MathF.Round(xRemainder);
        int yPixel = (int)MathF.Round(yRemainder);
        base.MoveXY(xPixel, yPixel);
        xRemainder -= xPixel;
        yRemainder -= yPixel;
    }
    public override void MoveXYIgnoreSolid(int xAmount, int yAmount)
    {
        int xPixel = (int)MathF.Round(xRemainder);
        int yPixel = (int)MathF.Round(yRemainder);
        base.MoveXYIgnoreSolid(xPixel, yPixel);
        xRemainder -= xPixel;
        yRemainder -= yPixel;
    }
    private bool LastStepToPoint()
    {
        if (goingTo1)
        {
            //Debug.Log(Vector2.Distance(endPoint1v, new Vector2(position.x + xRemainder, position.y + yRemainder)));
            if(Vector2.Distance(endPoint1v, new Vector2(position.x + xRemainder, position.y + yRemainder)) < minimumStep)
            {

                if (position.x != endPoint1.x)
                    xRemainder = endPoint1.x - position.x;
                if (position.y != endPoint1.y)
                    xRemainder = endPoint1.y - position.y;
                MoveXYIgnoreSolid(0, 0);
                xRemainder = yRemainder = 0;
                goingTo1 = false;
                return true;
            }
        }
        else if (Vector2.Distance(endPoint2v, new Vector2(position.x + xRemainder, position.y + yRemainder)) < minimumStep)
        {
            if (position.x != endPoint2.x)
                xRemainder = endPoint2.x - position.x;
            if (position.y != endPoint2.y)
                xRemainder = endPoint2.y - position.y;
            MoveXYIgnoreSolid(0, 0);
            xRemainder = yRemainder = 0;
            goingTo1 = true;

            return true;
        }
        //Debug.Log(Vector2.Distance(endPoint2v, new Vector2(position.x + xRemainder, position.y + yRemainder)));
        return false;
    }
    public override void PhysicUpdate()
    {

        if (LastStepToPoint())
            return;
        CalculateStepX((goingTo1 ? -toEndPoint2Speed.x : toEndPoint2Speed.x) / GamePhysics.FrameRate);
        CalculateStepY((goingTo1 ? -toEndPoint2Speed.y : toEndPoint2Speed.y) / GamePhysics.FrameRate);
        MoveXYIgnoreSolid(0, 0); // parameters doesn't matter for this object.
    }
    private void Start()
    {
        InitializePosition();
        GamePhysics.Instance.RegisterUpdate(new PhysicsUpdateMessage(PhysicUpdate, (int)UpdatePriority, Order));
        endPoint1v = new Vector2(endPoint1.x, endPoint1.y);
        endPoint2v = new Vector2(endPoint2.x, endPoint2.y);
        toEndPoint2Speed = (endPoint2v - endPoint1v).normalized * speed;
        //minimumStep = MathF.Abs( MathF.Sqrt(toEndPoint2Speed.sqrMagnitude) /GamePhysics.FrameRate);

        minimumStep = speed / GamePhysics.FrameRate;
    }
    private void OnDestroy()
    {
        //GamePhysics.Instance.UnregisterUpdate(new PhysicsUpdateMessage(PhysicUpdate, (int)UpdatePriority, Order));
    }
}

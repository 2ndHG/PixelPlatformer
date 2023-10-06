using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
public class Solid : MonoBehaviour, IPrioritizable
{
    #region Update Priority
    public int UpdatePriority { get; set; }
    [SerializeField]
    private int order;
    public int Order
    {
        get { return order; }
        set { order = value; }
    }
    #endregion

    [SerializeField]
    private int ridingPriority;
    public int RidingPriority { get { return ridingPriority; } }

    #region Size and Position
    [System.Serializable]
    protected struct Position
    {
        public int x, y;
    }
    [System.Serializable]
    protected struct Size
    {
        public int width, height;
    }
    [SerializeField] protected Position position;
    [SerializeField] protected Size size;
    #endregion

    #region Self Information
    public virtual bool IsOneWayPlatform()
    {
        return false;
    }
    #endregion

    #region Riding
    private List<Actor> ridingOnMeActors = new();
    private HashSet<Actor> movedActors = new HashSet<Actor>();
    #endregion

    public Vector2 GetLeftBottomPoint()
    {
        return new Vector2(position.x, position.y);
    }
    public Vector2 GetRightBottomPoint()
    {
        return new Vector2(position.x + size.width - 1, position.y);
    }
    public Vector2 GetLeftTopPoint()
    {
        return new Vector2(position.x, position.y + size.height - 1);
    }
    public Vector2 GetRightTopPoint()
    {
        return new Vector2(position.x + size.width - 1, position.y + size.height - 1);
    }
    public bool CheckSolidBelow()
    {
        return GamePhysics.CheckHorizontalSolid(GetLeftBottomPoint() - Vector2.up, GetRightBottomPoint() - Vector2.up);
    }
    public bool CheckSolidBelow(Vector2 assumingPositon)
    {
        assumingPositon.y -= 1;
        Vector2 destinationPosition = assumingPositon;
        destinationPosition.x += size.width - 1;
        return GamePhysics.CheckHorizontalSolid(assumingPositon, destinationPosition);
    }
    public bool CheckSolidAbove()
    {
        return GamePhysics.CheckHorizontalSolid(GetLeftTopPoint() + Vector2.up, GetRightTopPoint() + Vector2.up);
    }
    public bool CheckSolidAbove(Vector2 assumingPositon)
    {
        assumingPositon.y += size.height;
        Vector2 destinationPosition = assumingPositon;
        destinationPosition.x += size.width - 1;
        return GamePhysics.CheckHorizontalSolid(assumingPositon, destinationPosition);
    }
    public bool CheckSolidLeft()
    {
        return GamePhysics.CheckVerticleSolid(GetLeftBottomPoint() + Vector2.left, GetLeftTopPoint() + Vector2.left);
    }
    public bool CheckSolidLeft(Vector2 assumingPositon)
    {
        assumingPositon.x -= 1;
        Vector2 destinationPosition = assumingPositon;
        destinationPosition.y += size.height - 1;
        return GamePhysics.CheckVerticleSolid(assumingPositon, destinationPosition);
    }
    public bool CheckSolidRight()
    {
        return GamePhysics.CheckVerticleSolid(GetRightBottomPoint() + Vector2.right, GetRightTopPoint() + Vector2.right);
    }
    public bool CheckSolidRight(Vector2 assumingPositon)
    {
        assumingPositon.x += size.width;
        Vector2 destinationPosition = assumingPositon;
        destinationPosition.y += size.height - 1;
        return GamePhysics.CheckVerticleSolid(assumingPositon, destinationPosition);
    }

    #region Riding
    public virtual void Ride(Actor ridingActor)
    {
        if(!ridingOnMeActors.Contains(ridingActor))
        {
            ridingOnMeActors.Add(ridingActor);
        }
    }
    public virtual void Leave(Actor ridingActor)
    {
        if(ridingOnMeActors.Contains(ridingActor))
        {
            ridingOnMeActors.Remove(ridingActor);
        }
    }
    public Actor[] GetAllRidingActors()
    {
        return ridingOnMeActors.ToArray();
    }
    #endregion
    protected virtual void InitializePosition()
    {
        position.x = (int)transform.position.x;
        position.y = (int)transform.position.y;
    }
    public virtual void MoveXY(int xAmount, int yAmount)
    {
        // Move Y
        int yMove = yAmount;
        if (yMove != 0)
        {
            int sign = Math.Sign(yMove), totalMoves = 0;
            int preventDeadLoop = 0;
            while (yMove != 0)
            {
                preventDeadLoop++;
                if (preventDeadLoop == 50)
                {
                    Debug.Log("Infinite Loop");
                    Debug.Break();
                    break;
                }

                if (sign < 0 ? !CheckSolidLeft() : !CheckSolidRight())
                {
                    // update position to get right CheckXXX() function
                    totalMoves += sign;
                    position.y += sign;
                    yMove -= sign;
                }
                else
                {
                    break;
                }

            }

            movedActors.Clear();
            // push first
            Actor[] overlappedActors = GamePhysics.GetOverlappedActors(GetLeftBottomPoint(), GetRightTopPoint());
            foreach (Actor actor in overlappedActors)
            {
                actor.MoveY(totalMoves, actor.Squish);
                movedActors.Add(actor);
            }

            // really put transform to the position for physics detecting
            transform.position = new Vector3(position.x, position.y, 0);

            // carry riders
            Actor[] isRidingMe = GetAllRidingActors();
            foreach (Actor actor in isRidingMe)
            {
                if (actor is ISolidUpdateReceiver receiver)
                {
                    receiver.SolidUpdatesPosition(this, 0, totalMoves);
                }
                if (!movedActors.Contains(actor))  // if this actor was pushed, no need to move them again.
                {
                    actor.MoveY(totalMoves, actor.Squish);
                }
            }
        }

        // Move X
        int xMove = xAmount;
        if (xMove != 0)
        {
            int sign = Math.Sign(xMove), totalMoves = 0;
            int preventDeadLoop = 0;
            while (xMove != 0)
            {
                preventDeadLoop++;
                if (preventDeadLoop == 50)
                {
                    Debug.Log("Infinite Loop");
                    Debug.Break();
                    break;
                }

                if (sign < 0 ? !CheckSolidLeft() : !CheckSolidRight())
                {
                    // update position to get right CheckXXX() function
                    totalMoves += sign;
                    position.x += sign;
                    xMove -= sign;
                }
                else
                {
                    break;
                }

            }

            movedActors.Clear();
            // push first
            Actor[] overlappedActors = GamePhysics.GetOverlappedActors(GetLeftBottomPoint(), GetRightTopPoint());
            foreach (Actor actor in overlappedActors)
            {
                actor.MoveX(totalMoves, actor.Squish);
                movedActors.Add(actor);
            }

            // really put transform to the position for physics detecting
            transform.position = new Vector3(position.x, position.y, 0);

            // carry riders
            Actor[] isRidingMe = GetAllRidingActors();
            foreach (Actor actor in isRidingMe)
            {
                if (actor is ISolidUpdateReceiver receiver)
                {
                    receiver.SolidUpdatesPosition(this, totalMoves, 0);
                }
                if (!movedActors.Contains(actor))  // if this actor was pushed, no need to move them again.
                {
                    actor.MoveX(totalMoves, actor.Squish);
                }
            }
        }
        
        
    }
    public virtual void MoveXYIgnoreSolid(int xAmount, int yAmount)
    {
        // Move Y
        int yMove = yAmount;
        if (yMove != 0)
        {
            position.y += yMove;

            movedActors.Clear();
            // push first
            Actor[] overlappedActors = GamePhysics.GetOverlappedActors(GetLeftBottomPoint(), GetRightTopPoint());
            foreach (Actor actor in overlappedActors)
            {
                actor.MoveY(yMove, actor.Squish);
                movedActors.Add(actor);
            }

            // really put transform to the position for physics detecting
            transform.position = new Vector3(position.x, position.y, 0);

            // check again if riders overlapped with solids
            foreach (Actor actor in overlappedActors)
            {
                actor.CheckOverlappingDeath();
            }

            // carry riders
            Actor[] isRidingMe = GetAllRidingActors();
            foreach (Actor actor in isRidingMe)
            {
                if (actor is ISolidUpdateReceiver receiver)
                {
                    receiver.SolidUpdatesPosition(this, 0, yMove);
                }
                if (!movedActors.Contains(actor))  // if this actor was pushed, no need to move them again.
                {
                    //actor.MoveY(yMove, actor.Squish);
                    // Note: Previous code is above, now trying not squish actor when carrying
                    actor.MoveY(yMove);
                }
            }
        }

        // Move X
        int xMove = xAmount;
        if (xMove != 0)
        {
            position.x += xMove;

            movedActors.Clear();
            // push first
            Actor[] overlappedActors = GamePhysics.GetOverlappedActors(GetLeftBottomPoint(), GetRightTopPoint());
            foreach (Actor actor in overlappedActors)
            {
                actor.MoveX(xMove, actor.Squish);
                movedActors.Add(actor);
            }

            // really put transform to the position for physics detecting
            transform.position = new Vector3(position.x, position.y, 0);

            // check again if riders overlapped with solids
            foreach (Actor actor in overlappedActors)
            {
                actor.CheckOverlappingDeath();
            }

            // carry riders
            Actor[] isRidingMe = GetAllRidingActors();
            foreach (Actor actor in isRidingMe)
            {
                if (actor is ISolidUpdateReceiver receiver)
                {
                    receiver.SolidUpdatesPosition(this, xMove, 0);
                }
                if (!movedActors.Contains(actor))  // if this actor was pushed, no need to move them again.
                {
                    //actor.MoveX(xMove, actor.Squish);
                    // Note: Previous code is above, now trying not squish actor when carrying
                    actor.MoveX(xMove);
                }
            }
            
        }


    }
    
    public virtual void PhysicUpdate()
    {
    }

}

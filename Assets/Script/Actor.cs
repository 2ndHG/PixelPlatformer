using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Actor : MonoBehaviour, IPrioritizable
{
    public int UpdatePriority { get; set; }
    [SerializeField]
    private int order;

    #region Size and Position
    [System.Serializable]
    protected struct Position
    {
        public Position(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
        public Position(Vector2 value)
        {
            x = (int)value.x;
            y = (int)value.y;
        }
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

    #region Riding
    protected Solid ridingSolid;
    #endregion
    protected void InitializePosition()
    {
        position.x = (int)transform.position.x;
        position.y = (int)transform.position.y;
    }
    public Vector2 GetLeftBottomPoint(){
        return new Vector2(position.x, position.y); 
    }
    public Vector2 GetRightBottomPoint()
    {
        return new Vector2(position.x + size.width-1, position.y);
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

    public Actor[] GetActorsOverlapped()
    {
        return GamePhysics.GetOverlappedActors(GetLeftBottomPoint(), GetRightTopPoint());
    }

    public virtual Solid GetRidingSolid()
    {
        return ridingSolid;
    }
    public Solid[] GetSolidsBelow()
    {
        return GamePhysics.GetOverlappedSolids(GetLeftBottomPoint()+Vector2.down, GetRightBottomPoint()+Vector2.down);
    }

    #region Riding
    protected void UpdateRidingSolidAndTell(Solid toRide)
    {
        // if current riding Solid are same to toRide, no need to change anything.
        if (ridingSolid == toRide)
            return;

        // leave current riding Solid
        if (ridingSolid != null)
            ridingSolid.Leave(this);


        ridingSolid = toRide;
        ridingSolid.Ride(this);
    }
    protected virtual void UpdateRidingSolid()
    {
        Solid[] solids = GetSolidsBelow();
        if(solids.Length == 0)
        {
            if (ridingSolid != null)
                ridingSolid.Leave(this);
            ridingSolid = null;
        }
        else if(solids.Length == 1)
        {
            UpdateRidingSolidAndTell(solids[0]);
        }
        else
        {
            Solid toRide = solids[0].RidingPriority < solids[1].RidingPriority ? solids[0] : solids[1];
            UpdateRidingSolidAndTell(toRide);
        }

    }


    #endregion
    public int Order
    {
        get { return order; }
        set { order = value; }
    }

    public virtual void MoveX(float amount, Action onCollide = null)
    {
        int preventDeadLoop = 0;
        int move = (int)Math.Round(amount);
        if (move != 0)
        {
            int sign = Math.Sign(move);
            while (move != 0)
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
                    //There is no Solid immediately beside us 
                    position.x += sign;
                    move -= sign;
                }
                else
                {
                    //Hit a solid!
                    //Debug.Log((GetLeftBottomPoint() + Vector2.left)+ " " + GetRightTopPoint());
                    //Solid hitSolid = GamePhysics.GetOverlappedSolids(GetLeftBottomPoint()+Vector2.left, GetRightTopPoint())[0];
                    //Debug.Log("Hit Solid: " + hitSolid.name + " " + hitSolid.GetRightBottomPoint());
                    //Debug.Log("My Left Botton Point: " + position.x);
                    onCollide?.Invoke();

                    break;
                }

            }
            transform.position = new Vector2(position.x, position.y);
        }
    }
    public virtual void MoveY(float amount, Action onCollide = null)
    {
        int move = (int)Math.Round(amount);
        int preventDeadLoop = 0;
        if (move != 0)
        {
            int sign = Math.Sign(move);
            while (move != 0)
            {
                preventDeadLoop++;
                if (preventDeadLoop == 50)
                {
                    Debug.Log("Infinite Loop");
                    Debug.Break();
                    break;
                }
                if (sign < 0 ? !CheckSolidBelow() : !CheckSolidAbove())
                {
                    //There is no Solid immediately beside us 
                    position.y += sign;
                    move -= sign;
                }
                else
                {
                    //Hit a solid!
                    //Debug.Log("Hit Solid");
                    onCollide?.Invoke();

                    break;
                }

            }
            transform.position = new Vector2(position.x, position.y);
        }
    }
    
    public virtual void MoveXIgnoreSolid(int amount)
    {
        int move = amount;
        if (move != 0)
        {
            position.x += move;
            // really put transform to the position for physics detecting
            transform.position = new Vector3(position.x, position.y, 0);
        }
    }
    public virtual void MoveYIgnoreSolid(int amount)
    {
        int move = amount;
        if (move != 0)
        {
            position.y += move;
            // really put transform to the position for physics detecting
            transform.position = new Vector3(position.x, position.y, 0);
        }
    }
    public virtual void Squish()
    {
        // If overlaps, Kill Actor
    }
    public virtual void CheckOverlappingDeath()
    {
        if (GamePhysics.GetOverlappedSolids(GetLeftBottomPoint(), GetRightTopPoint()).Length == 0)
            return;

        //Kill Object
    }
    public virtual void PhysicUpdate()
    {
        //MoveX();
    }
}

public enum UpdatePriorityEnum: int
{
    beforePlayer,
    playerMovement,
    afterPlayer
}
public interface IPrioritizable
{
    int UpdatePriority { get; set; }
    int Order { get; set; }
}
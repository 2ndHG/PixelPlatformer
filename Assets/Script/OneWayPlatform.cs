using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OneWayPlatform : Solid
{
    [SerializeField] private Position SurfaceLeftBottomPoint;
    public override bool IsOneWayPlatform()
    {
        return true;
    }
    public Direction8 collidingDirection;
    protected override void InitializePosition()
    {
        base.InitializePosition();
        position.x += SurfaceLeftBottomPoint.x;
        position.y += SurfaceLeftBottomPoint.y;
    }
}

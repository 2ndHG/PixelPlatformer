using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OneWayPlatform : Solid
{
    public override bool IsOneWayPlatform()
    {
        return true;
    }
    public Direction8 collidingDirection;
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IInertiaReceiver
{
    public void ReceiveVelocity(Vector2 velocity);
}

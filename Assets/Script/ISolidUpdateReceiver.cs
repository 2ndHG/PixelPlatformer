using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ISolidUpdateReceiver
{
    public void SolidUpdatesPosition(Solid updater, int xAmount, int yAmount);
    public void SolidDestroyed(Solid solid);

}

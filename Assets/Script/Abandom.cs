using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Abandom
{
    //private Vector2 DetectGloveExactPixel(Vector2 startPoint, Solid solidToContact)
    //{
    //    if (gloveDecidedDirection > Direction8.Down)
    //        return startPoint;

    //    Vector2 detectingPixel = startPoint;
    //    // detect glove exact point
    //    if (gloveDecidedDirection <= Direction8.Right)
    //    {
    //        detectingPixel.y += sizeGloveAxis;
    //        for (int i = 1; i <= gloveLengthAxis; i++)
    //        {
    //            if (GamePhysics.CheckSolidInPosition(new Vector2(detectingPixel.x, detectingPixel.y - i), solidToContact))
    //            {
    //                detectingPixel.y -= i;
    //                break;
    //            }
    //        }
    //    }
    //    else if (gloveDecidedDirection <= Direction8.Down)
    //    {
    //        bool found = false;
    //        detectingPixel = new(startPoint.x + 1, startPoint.y);
    //        found = GamePhysics.CheckSolidInPosition(detectingPixel, solidToContact);
    //        if (!found)
    //        {
    //            detectingPixel.x++;
    //            found = GamePhysics.CheckSolidInPosition(detectingPixel, solidToContact);
    //        }
    //        if (!found)
    //        {
    //            detectingPixel.x++;
    //            found = GamePhysics.CheckSolidInPosition(detectingPixel, solidToContact);
    //        }
    //        if (!found)
    //        {
    //            detectingPixel.x -= 3;
    //        }
    //    }

    //    return detectingPixel;
    //}
    //private void DetectGloveSurface(Position goalPoint, Solid solidToContact)
    //{
    //    Vector2 leftPoint = new(goalPoint.x - 1, goalPoint.y);
    //    Vector2 rightPoint = new(goalPoint.x + 1, goalPoint.y);
    //    Vector2 upPoint = new(goalPoint.x, goalPoint.y + 1);
    //    Vector2 downtPoint = new(goalPoint.x, goalPoint.y - 1);
    //    //Debug.Log(GamePhysics.GetHorizontalSolids(leftPoint, leftPoint).Length);
    //    gloveOnHSurface = Array.IndexOf(GamePhysics.GetHorizontalSolids(leftPoint, leftPoint), solidToContact) != -1 && Array.IndexOf(GamePhysics.GetHorizontalSolids(rightPoint, rightPoint), solidToContact) != -1;
    //    gloveOnVSurface = Array.IndexOf(GamePhysics.GetHorizontalSolids(upPoint, upPoint), solidToContact) != -1 && Array.IndexOf(GamePhysics.GetHorizontalSolids(downtPoint, downtPoint), solidToContact) != -1;

    //    if (gloveOnHSurface == gloveOnVSurface)
    //        gloveOnHSurface = gloveOnVSurface = true;
    //}

}

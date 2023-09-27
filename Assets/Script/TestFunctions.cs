using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class TestFunctions : MonoBehaviour
{
    private Stopwatch stopwatch;

    void Start()
    {
        stopwatch = new Stopwatch();
    }

    void RayCastVSBoxCast()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            stopwatch.Reset();
            stopwatch.Start();
            for (int i = 0; i < 1000; i++)
            {
                Vector2 origin = new Vector2(0, 0);
                Vector2 direction = new Vector2(1, 0);
                float distance = 10.0f;
                Physics2D.BoxCast(origin, new Vector2(100, 100), 0, direction, distance);
            }

            stopwatch.Stop();
            UnityEngine.Debug.Log("Elapsed Time for 70 BoxCasts: " + stopwatch.ElapsedMilliseconds + " ms");

            stopwatch.Reset();
            stopwatch.Start();
            for (int i = 0; i < 3000; i++)
            {
                Vector2 origin = new Vector2(0, 0);
                Vector2 direction = new Vector2(1, 0);
                //float distance = 10.0f;
                Physics2D.Raycast(origin, new Vector2(1, 1), 100);
            }

            stopwatch.Stop();
            UnityEngine.Debug.Log("Elapsed Time for 140 RayCasts: " + stopwatch.ElapsedMilliseconds + " ms");
            //RaycastHit2D hit = Physics2D.Raycast(new Vector2(8.0000001f, 8.0000001f), Vector2.right, 1);
            //if (hit.collider != null)
            //    UnityEngine.Debug.Log(hit.collider.name);
        }
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}

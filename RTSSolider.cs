using System.Collections;
using System.Collections.Generic;
using System.Numerics;

public class RTSSolider
{
    public bool walking;
    public Vector2 target;
    public Vector2 current;
    public float speed;

    private Client parent;
    
    public RTSSolider(Vector2 startPosition, float speed, Client parent)
    {
        walking = false;
        target = startPosition;
        current = startPosition;
        this.speed = speed;
        this.parent = parent;
        parent.onUpdate += Update;
    }

    ~RTSSolider()
    {
        parent.onUpdate -= Update;
    }

    void Update()
    {
        if (walking)
        {
            float distance = Vector2.Distance(current, target);
            Vector2 move = Vector2.Normalize(target - current);
            if (distance < speed)
            {
                move *= speed - distance;
                walking = false;
            }
            else
            {
                move *= speed;
            }
            current += move;
        }
    }

    public void SetTarget(Vector2 target)
    {
        if (this.target == target) return;
        walking = true;
        this.target = target;
    }
}

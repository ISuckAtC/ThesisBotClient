using System.Collections;
using System.Collections.Generic;

public class InputSolider
{
    public int id;
    public System.Numerics.Vector3 position;
    public static float gravitySpeed = 9.81f;
    public static float speed = 0.5f;

    public InputSolider(int id, System.Numerics.Vector3 position)
    {
        this.id = id;
        this.position = position;
    }

    public void Update()
    {
        if ((position.X > 10f || position.X < -10f || position.Z > 10f || position.Z < -10f) || position.Y < 0.5f)
        {
            position.Y -= (((float)NewNetwork.localTickrate) / 1000f) * gravitySpeed;
        }
        if (position.Y > 1f)
        {
            position.Y -= (((float)NewNetwork.localTickrate) / 1000f) * gravitySpeed;
            if (position.Y < 1f) position.Y = 1f;
        }
    }

    public void Input(bool[] buttons, System.Numerics.Vector2 analog)
    {
        position += new System.Numerics.Vector3(analog.X, 0f, 0f) * speed;
        if (buttons[0]) position += new System.Numerics.Vector3(0f, 5f, 0f);
    }
}
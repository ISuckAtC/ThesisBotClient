public class UpdateSolider
{
    public static float gravitySpeed = 9.81f;
    public static float speed = 5f;
    public int id;
    public System.Numerics.Vector3 position;

    public UpdateSolider(int id, System.Numerics.Vector3 position)
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
}

public class Api
{
    public int Id;
    public bool canDirectAlive;
    public Api()
    {
        canDirectAlive = true;
    }

    public bool IsDirectAlive()
    {
        return canDirectAlive;
    }
    public void Release()
    {

    }
    public void ConnectDirect()
    {
        canDirectAlive = true;
    }

    public void Dispose()
    {

    }
}
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

public class Test
{
    public static ApiPool2 pool = new ApiPool2();

    public void OnQuest()
    {
        Stopwatch sw = new Stopwatch();
        sw.Start();
        var api = pool.GetApi();
        System.Console.WriteLine("using:"+ api.Id);
        if(api == null)
        {
            System.Console.WriteLine("api is null");
            return;
        }
        Thread.Sleep(500);
        System.Console.WriteLine("release:"+api.Id);
        pool.Release(api);
        sw.Stop();
        // System.Console.WriteLine("apiId:"+api.Id+" ms:"+sw.ElapsedMilliseconds);
    }
}
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;


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

public class ApiPool2
{
    /* 队列存放api使用情况，一上来存放了所有可用api的id，每次使用都会出队O(1),使用完入队O(1),
    获取效率比随机高 O(1)，并且保证了每一个api的使用率都相同，不会因随机导致，某一个api很久才调用。
    ConcurrentQueue也保证了出队入队的原子操作，线程安全，不需要手动lock（已验证）。*/
    public static ConcurrentQueue<Api> usingQueue;
    private ConcurrentQueue<Api> reconnApi;
    private int lockedId;//原子自增api的id
    private volatile bool expanding = false;//扩容线程是否开启中
    private volatile bool lessening = false;//缩小线程是否开启中
    private const int initApiNum = 3;//初始化的api数量
    private int currentApiNum;//记录当前总共开了多少个api数量
    public ApiPool2()
    {
        usingQueue = new ConcurrentQueue<Api>();
        reconnApi = new ConcurrentQueue<Api>();
        currentApiNum = 0;
        Start();
    }

    public void Start()
    {
        ReconnTask();
        SetApiPool(initApiNum);
    }

    public void Release(Api api)
    {
        usingQueue.Enqueue(api);
        SetLessenTask();
    }

    public void Stop()
    {
        while (usingQueue.TryDequeue(out Api result))
        {
            result.Release();
        }

        Interlocked.Exchange(ref currentApiNum, 0);
        Interlocked.Exchange(ref lockedId, 0);
    }

    public Api GetApi()
    {
        Api api = null;
        while (true)
        {
            if (!usingQueue.TryDequeue(out api))
            {
                StartExpand();
                continue;
            }

            if (!api.IsDirectAlive())//此api是c++中校验的
            {
                reconnApi.Enqueue(api);
                continue;
            }
            // api.canDirectAlive = false;//断线测试
            break;
        }
        return api;
    }

    private void ReconnTask()
    {
        Task.Factory.StartNew(() =>{
 while (true)
        {
            Thread.Sleep(5000);
            if (reconnApi.Count < 0) continue;
            if (reconnApi.TryDequeue(out Api api))
            {
                if (ReConnect(api))
                {
                    usingQueue.Enqueue(api);
                }
                // else
                // {
                //     System.Console.WriteLine("重连失败 移除此api：" + api.Id);
                // }
            }

        }

        });
       
    }

    private bool ReConnect(Api api)
    {
        Thread.Sleep(500);
        api.ConnectDirect();
        return true;
    }

    private void CreateApi()
    {
        var api = new Api();
        if (!api.IsDirectAlive())
        {
            if (!ReConnect(api)) return;
        }

        api.Id = Interlocked.Increment(ref lockedId);//不能用lockedId要用赋值的结果才是线程安全的
        Interlocked.Increment(ref currentApiNum);
        usingQueue.Enqueue(api);
    }

    private void SetApiPool(int len)
    {
        Parallel.For(0, len, (i) =>
           {
               try
               {
                   CreateApi();
               }
               catch (Exception ex)
               {
                   System.Console.WriteLine("create api fail: " + ex.Message);
               }
           });
        System.Console.WriteLine("一共开了:" + usingQueue.Count);
    }

    //扩容
    private void StartExpand()
    {
        if (expanding) return;
        expanding = true;
        int pre = currentApiNum;
        int finNum = currentApiNum + pre / 3;
        Task.Run(() =>
        {
            Parallel.For(0, pre / 3, (i) =>
              {
                    try
                    {
                        Thread.Sleep(500);
                        CreateApi();
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine("create api fail: " + ex.Message);
                    }
              });
            System.Console.WriteLine("扩容前大小: {0}, 扩容后大小{1}", pre, finNum);
            expanding = false;
        });
    }

    private void SetLessenTask()
    {
        return;
        if (usingQueue.Count < initApiNum) return;
        if (lessening) return;
        lessening = true;
        Task.Run(() =>
        {
            Thread.Sleep(5000);
            int pre = currentApiNum;
            while (usingQueue.Count > initApiNum)
            {
                if (usingQueue.TryDequeue(out Api api))
                {
                    api.Release();
                    Interlocked.Decrement(ref currentApiNum);
                }
            }

            System.Console.WriteLine("缩小前：{0},缩小后：{1}", pre, currentApiNum);
            lessening = false;
        });
    }
}
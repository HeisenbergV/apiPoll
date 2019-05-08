using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

/*

 */
public class ApiPool4
{
    /* 队列存放api使用情况，一上来存放了所有可用api的id，每次使用都会出队O(1),使用完入队O(1),
    获取效率比随机高 O(1)，并且保证了每一个api的使用率都相同，不会因随机导致，某一个api很久才调用。
    BlockingCollection也保证了出队入队的原子操作，线程安全，不需要手动lock（已验证）。*/
    public static BlockingCollection<Api> usingQueue;
    private BlockingCollection<Api> reconnQueue;
    private int lockedId;//原子自增api的id
    private int expandLock;
    private int lessenLock;
    private const int initApiNum = 5;//初始化的api数量
    public static int currentApiNum;//记录当前总共开了多少个api数量
    private SpinWait spinWait;
    private Stopwatch sw = new Stopwatch();//计时器
    private const int UseInterval = 5;//api周期内的使用间隔单位秒
    private DateTime lastTime = DateTime.Now;//上次周期的使用时间
    public ApiPool4()
    {
        usingQueue = new BlockingCollection<Api>();
        spinWait = new SpinWait();
        reconnQueue = new BlockingCollection<Api>();
        currentApiNum = 0;
        lessenLock = 0;
        expandLock = 0;
        Start();
    }

    public void Start()
    {
        ReconnTask();
        SetApiPool(initApiNum);
    }

    public void Release(Api api)
    {
        // if (api.Id%20 == 0)
        // {
        //     api.canDirectAlive = false;
        // }
        usingQueue.Add(api);
        // SetLessenTask();
    }

    public void Stop()
    {
        while (usingQueue.Count > 0)
        {
            usingQueue.Take().Release();
        }

        Interlocked.Exchange(ref currentApiNum, 0);
        Interlocked.Exchange(ref lockedId, 0);
    }


    public Api GetApi()
    {
        Api api = null;

        sw.Start();
        while (true)
        {
            if (usingQueue.Count <= 0)
            {
                ExpandTask();
                spinWait.SpinOnce();
            }

            api = usingQueue.Take();

            if (!api.IsDirectAlive())//此api是c++中校验的
            {
                reconnQueue.Add(api);
                continue;
            }
            break;
        }
        sw.Stop();
        //以初始化的api大小为一个周期，检测这个周期的使用时间
        //当使用一个周期超过指定时间，说明api池数量过多，可以缩小了
        if (Interlocked.Increment(ref lessenLock) >= initApiNum)
        {
            var now = DateTime.Now;
            if ((now - lastTime).Seconds >= UseInterval && usingQueue.Count >= initApiNum)
            {
                SetLessenTask();
            }
            lastTime = now;
        }
        if (sw.ElapsedMilliseconds > 100)
        {
            // System.Console.WriteLine("api get time = " + sw.ElapsedMilliseconds);
        }

        return api;
    }

    private void ReconnTask()
    {
        Task.Run(() =>
        {
            while (true)
            {
                var api = reconnQueue.Take();
                if (ReConnect(api))
                {
                    System.Console.WriteLine("重连成功 id：" + api.Id);
                    usingQueue.Add(api);
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
        usingQueue.Add(api);
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

    private void ExpandTask()
    {
        if (Interlocked.Increment(ref expandLock) != 1) return;
        new Task(() =>
        {
            int pre = currentApiNum;
            int finNum = currentApiNum + pre / 3;
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
            Interlocked.Exchange(ref expandLock, 0);
        }).Start();
    }

    private void SetLessenTask()
    {
        Task.Run(() =>
        {
            int pre = currentApiNum;
            while (usingQueue.Count >= initApiNum)
            {
                usingQueue.Take().Release();
                Interlocked.Decrement(ref currentApiNum);
            }

            System.Console.WriteLine("缩小前：{0},缩小后：{1}", pre, currentApiNum);
        });
    }
}
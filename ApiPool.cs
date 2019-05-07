using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

public class ApiPool
{
    private ConcurrentDictionary<int, Api> apiDic;

    //队列存放api使用情况，一上来存放了所有可用api的id，每次使用都会出队O(1),使用完入队O(1),
    //获取效率比随机高 O(1)，并且保证了每一个api的使用率都相同，不会因随机导致，某一个api很久才调用。
    //ConcurrentQueue也保证了出队入队的原子操作，线程安全，不需要手动lock（已验证）。
    private ConcurrentQueue<int> usingQueue;
    //原子自增id
    private int lockedId;
    private int capacityLock;
    private volatile bool Expanding = false;
    public ApiPool()
    {
        apiDic = new ConcurrentDictionary<int, Api>();
        usingQueue = new ConcurrentQueue<int>();
        capacityLock = 0;
        Start();
    }

    private void Start()
    {
        SetApiPool(1);
    }

    public void Release(Api api)
    {
        if (apiDic.ContainsKey(api.Id))
        {
            usingQueue.Enqueue(api.Id);
        }
        else
        {
            System.Console.WriteLine("api池不存在此api：" + api.Id + " 给他自动释放掉");
            api.Release();
        }
    }

    public void Stop()
    {
        foreach (var api in apiDic)
        {
            api.Value.Release();
        }
        usingQueue.Clear();
        Interlocked.Exchange(ref lockedId, 0);
    }

    public Api GetApi()
    {
        Api api = null;
        while (true)
        {
            if (!usingQueue.TryDequeue(out int id))
            {
                StartExpand();
                continue;
            }

            if (!apiDic.TryGetValue(id, out api)) continue;
            if (!api.IsDirectAlive())//此api是c++中校验的
            {
                ReconnApi(api);
                continue;
            }
            // api.canDirectAlive = false;//断线测试
            break;
        }
        return api;
    }

    private async void ReconnApi(Api recApi)
    {
        await Task.Run(() =>
        {
            System.Console.WriteLine("api重连：" + recApi.Id);
            if (ReConnect(recApi))
            {
                System.Console.WriteLine("重连成功：" + recApi.Id);
                usingQueue.Enqueue(recApi.Id);
            }
            else
            {
                System.Console.WriteLine("重连失败 移除此api：" + recApi.Id);
                if (apiDic.TryRemove(recApi.Id, out Api value))
                {
                    value.Dispose();
                }
            }
        });
    }

    private bool ReConnect(Api api)
    {
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

        int apiId = Interlocked.Increment(ref lockedId);//不能用lockedId要用赋值的结果才是线程安全的
        api.Id = apiId;
        apiDic.TryAdd(apiId, api);
        usingQueue.Enqueue(api.Id);
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
    }

    //扩容
    private void StartExpand()
    {
        if(Expanding) return;
        Expanding = true;
        Task.Run(()=>{
            System.Console.WriteLine("当前api池大小: {0}, 开始扩容操作 扩大:{1}", apiDic.Count, apiDic.Count / 3+1);
            Parallel.For(0, apiDic.Count / 3+1, (i) =>
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
            Expanding = false;
        });
    }

    //缩小
    private void SetLessen()
    {
    }
}
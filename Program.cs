using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

class Program
{

    public static BlockingCollection<int> coolect;
    static void Main(string[] args)
    {
        // coolect = new BlockingCollection<int>();
        // new Task(()=>{
        //     for(int i = 0;i < 10;i++)
        //     {
        //         Thread.Sleep(1000);
        //         coolect.Add(i);
        //     }
        // }).Start();
        // new Task(()=>{
        //     Parallel.For(0,11,(i)=>{
        //         System.Console.WriteLine("11111");
        //         System.Console.WriteLine(coolect.Take());
        //     });
        // }).Start();
        // Console.Read();
        for (int i = 0; i < 1; i++)
        {
            apiTest();
            System.Console.WriteLine("准备下一阶段请求00000");
        }
        Console.Read();
    }

    private static void apiTest()
    {
        Stopwatch sw = new Stopwatch();
        sw.Start();
            Parallel.For(0, 1000, (a) =>
            {
                new Test().OnQuest();
            });
      
        sw.Stop();
        System.Console.WriteLine("=========================并行总耗时：" + sw.ElapsedMilliseconds);
        System.Console.WriteLine("当前api数量："+ ApiPool4.currentApiNum);
        for(;;)
        {
            System.Console.WriteLine("qqqqqq");
            new Test().OnQuest();
            Thread.Sleep(5000);
        }
    }
}
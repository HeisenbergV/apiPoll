using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static void Main(string[] args)
    {
        for (int i = 0; i < 1; i++)
        {
            apiTest();
            System.Console.WriteLine("准备下一阶段请求00000");
        }
        foreach(var nb in ApiPool2.usingQueue.ToArray())
        {
            System.Console.WriteLine("id:{0}, isok:{1}",nb.Id,nb.canDirectAlive);
        }
        Console.Read();
    }

    private static void apiTest()
    {
        Stopwatch sw = new Stopwatch();
        sw.Start();
        Parallel.For(0, 100, (i) =>
        {
            new Test().OnQuest();
        });
        sw.Stop();
        System.Console.WriteLine("=========================并行总耗时：" + sw.ElapsedMilliseconds);
    }

    static void testleg()
    {
        int a = show(1, 2, 3, 1);
        System.Console.WriteLine(a);

        a = show(2, 1, 3, 1);
        System.Console.WriteLine(a);

        a = show(1, 2, 3, 0);
        System.Console.WriteLine(a);

        a = show(0, 1, 1, 0);
        System.Console.WriteLine(a);

        a = show(1, 0, 1, 1);
        System.Console.WriteLine(a);

        a = show(2, 1, 3, 0);
        System.Console.WriteLine(a);
    }
    static int show(int sellMargin, int buyMargin, int legMargin, int cmd)
    {
        //1. sell:1 buy:2 newsell:3    result: 2
        //2. sell:2 buy:1 newsell:3    result: 3
        //3. sell:1 buy:2 newbuy:3     result: 3   
        //4. sell:0 buy:1 newbuy:1     result: 1
        //5. sell:1 buy:0 newsell:1    result: 1
        //6. sell:2 buy:1 newbuy:3      result: 2
        if (cmd == 1)
        {
            if (sellMargin + legMargin < buyMargin) return 0;
            if (sellMargin > buyMargin) return legMargin;
            return sellMargin + legMargin - buyMargin;
        }
        else
        {
            if (buyMargin + legMargin < sellMargin) return 0;
            if (buyMargin > sellMargin) return legMargin;
            return buyMargin + legMargin - sellMargin;
        }
    }
}
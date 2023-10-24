using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;
using Xunit;

namespace AsyncEx.Context.UnitTests
{
    public class ThreadPoolStarvation
    {
        [Fact]
        public void TryStarvation()
        {

            // create a bunch of tasks that will cause a massive amount of threadpool starvation
            var tasks = new List<Task>();

            for (int i = 0; i < 100; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var ok = FakeJobDeeplyAsync(42).Result;
                }));
            }

            AsyncContext.Run(async () =>
            {
                await Task.WhenAll(tasks);
            }, TimeSpan.FromSeconds(20));
        }

        public async Task<bool> FakeJobDeeplyAsync(int recursion)
        {
            if (recursion > 0)
            {
                return await FakeJobDeeplyAsync(recursion - 1);
            }
            else
            {
                await Task.Delay(30);
                //await Task.Yield();
                Thread.Sleep(1);
                await Task.Delay(20);
                return true;
            }
        }
    }
}

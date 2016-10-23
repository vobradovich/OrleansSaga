using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OrleansSaga.Grains;
using System.Diagnostics;
using OrleansSaga.Grains.Services;

namespace OrleansSaga.Tests
{
    [TestFixture]
    public class TaskExtensionsTest
    {
        [Test]
        public void FibonacciBackoff_Test()
        {
            var backoff = new FibonacciBackoff(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1000));
            Assert.AreEqual(1, backoff.Next(0).TotalMilliseconds);
            Assert.AreEqual(1, backoff.Next(1).TotalMilliseconds);
            Assert.AreEqual(2, backoff.Next(2).TotalMilliseconds);
            Assert.AreEqual(3, backoff.Next(3).TotalMilliseconds);
            Assert.AreEqual(5, backoff.Next(4).TotalMilliseconds);
            Assert.AreEqual(8, backoff.Next(5).TotalMilliseconds);
            Assert.AreEqual(13, backoff.Next(6).TotalMilliseconds);
            Assert.AreEqual(21, backoff.Next(7).TotalMilliseconds);
            Assert.AreEqual(34, backoff.Next(8).TotalMilliseconds);
            Assert.AreEqual(55, backoff.Next(9).TotalMilliseconds);

            var sw = new Stopwatch();
            sw.Start();
            for (int i = 0; i < 50; i++)
            {
                Console.WriteLine($"{sw.ElapsedMilliseconds} - {i} - {backoff.Next(i).TotalMilliseconds}");
            }
            sw.Stop();
            Assert.AreEqual(1000, backoff.Next(100).TotalMilliseconds);
        }

        [Test]
        public async Task Retry_Test()
        {
            var service = new SimpleService();
            var result = await Grains.TaskExtensions.Retry(i => service.ThrowNotTen(i), 11, new FixedBackoff(TimeSpan.FromMilliseconds(1)));
            Assert.AreEqual(10, result);
        }
    }
}

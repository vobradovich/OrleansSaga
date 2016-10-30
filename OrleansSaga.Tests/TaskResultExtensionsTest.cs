using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OrleansSaga.Grains.Model;
using OrleansSaga.Grains;
using System.Threading;

namespace OrleansSaga.Tests
{
    [TestFixture]
    public class AsyncUsageExamples
    {
        [Test]
        public async Task Promote_with_async_methods_in_the_beginning_of_the_chain()
        {
            var gateway = new EmailGateway();

            var t = await GetByIdAsync(0)
                .Ensure(customer => customer.CanBePromoted(), new Exception("The customer has the highest status possible"))
                .OnSuccess(customer => customer.Promote())
                .OnSuccess(customer => gateway.SendPromotionNotification(customer.Email))
                .OnBoth(result => result.IsCompleted ? "Ok" : result.Exception.Message);

            Assert.AreEqual("Ok", t);
        }

        [Test]
        public async Task Promote_with_async_methods_in_the_beginning_and_in_the_middle_of_the_chain()
        {
            var gateway = new EmailGateway();

            var t = await GetByIdAsync(0)
                .Ensure(customer => customer.CanBePromoted(), new Exception("The customer has the highest status possible"))
                .OnSuccess(customer => { customer.PromoteAsync(); return customer; })
                .OnSuccess(customer => gateway.SendPromotionNotificationAsync(customer.Email))
                .OnBoth(result => result.IsCompleted ? "Ok" : result.Exception.Message);

            Assert.AreEqual("Ok", t);
        }

        public Task<Customer> GetByIdAsync(long id)
        {
            Console.WriteLine($"{DateTime.Now} GetByIdAsync");
            return Task.FromResult(new Customer());
        }

        public class Customer
        {
            public string Email { get; }

            public Customer()
            {
                Console.WriteLine($"{DateTime.Now} new Customer");
            }

            public bool CanBePromoted()
            {
                Console.WriteLine($"{DateTime.Now} CanBePromoted");
                return true;
            }

            public void Promote()
            {
            }

            public Task PromoteAsync()
            {
                Console.WriteLine($"{DateTime.Now} PromoteAsync");
                return Task.FromResult(1);
            }
        }

        public class EmailGateway
        {
            public Task SendPromotionNotification(string email)
            {
                Console.WriteLine($"{DateTime.Now} SendPromotionNotification");
                return Task.FromResult(0);
            }

            public Task SendPromotionNotificationAsync(string email)
            {
                Console.WriteLine($"{DateTime.Now} SendPromotionNotificationAsync");
                return Task.FromResult(0);
            }
        }
    }
}

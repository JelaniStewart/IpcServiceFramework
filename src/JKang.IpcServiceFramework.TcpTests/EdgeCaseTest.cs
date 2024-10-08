﻿using JKang.IpcServiceFramework.Client;
using JKang.IpcServiceFramework.TcpTests.Fixtures;
using JKang.IpcServiceFramework.Testing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace JKang.IpcServiceFramework.TcpTests
{
    public class EdgeCaseTest : IClassFixture<IpcApplicationFactory<ITestService>>
    {
        private readonly IpcApplicationFactory<ITestService> _factory;

        public EdgeCaseTest(IpcApplicationFactory<ITestService> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task ConnectionTimeout_Throw()
        {
            int timeout = 3000; // 3s
            IIpcClient<ITestService> client = _factory
                .CreateClient((name, services) =>
                {
                    services.AddTcpIpcClient<ITestService>(name, (_, options) =>
                    {
                        // Connect to a non-routable IP address can trigger timeout
                        options.ServerIp = IPAddress.Parse("10.0.0.0");
                        options.ConnectionTimeout = timeout;
                    });
                });

            var sw = Stopwatch.StartNew();
            await Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                var request = TestHelpers.CreateIpcRequest(typeof(ITestService), "StringType", new object[] { "abc" });
                string output = await client.InvokeAsync<string>(request);
            });

            Assert.True(sw.ElapsedMilliseconds < timeout * 2); // make sure timeout works with marge
        }

        [Fact]
        public void ConnectionCancelled_Throw()
        {
            IIpcClient<ITestService> client = _factory
                .CreateClient((name, services) =>
                {
                    services.AddTcpIpcClient<ITestService>(name, (_, options) =>
                    {
                        // Connect to a non-routable IP address can trigger timeout
                        options.ServerIp = IPAddress.Parse("10.0.0.0");
                    });
                });

            using (var cts = new CancellationTokenSource())
            {
                Task.WaitAll(
                    Task.Run(async () =>
                    {
                        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                        {
                            var request = TestHelpers.CreateIpcRequest(typeof(ITestService), "StringType", new object[] { string.Empty });
                            await client.InvokeAsync(request, cts.Token);
                        });
                    }),
                    Task.Run(() => cts.CancelAfter(1000)));
            }
        }
    }
}

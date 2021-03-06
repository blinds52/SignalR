// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Internal.Protocol;
using Microsoft.AspNetCore.SignalR.Tests.Common;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.AspNetCore.SignalR.Client.Tests
{
    // This includes tests that verify HubConnection conforms to the Hub Protocol, without setting up a full server (even TestServer).
    // We can also have more control over the messages we send to HubConnection in order to ensure that protocol errors and other quirks
    // don't cause problems.
    public class HubConnectionProtocolTests
    {
        [Fact]
        public async Task InvokeSendsAnInvocationMessage()
        {
            var connection = new TestConnection();
            var hubConnection = new HubConnection(connection, new JsonHubProtocol(new JsonSerializer()), new LoggerFactory());
            try
            {
                await hubConnection.StartAsync();

                var invokeTask = hubConnection.Invoke("Foo");

                var invokeMessage = await connection.ReadSentTextMessageAsync().OrTimeout();

                Assert.Equal("{\"invocationId\":\"1\",\"type\":1,\"target\":\"Foo\",\"arguments\":[]}", invokeMessage);
            }
            finally
            {
                await hubConnection.DisposeAsync().OrTimeout();
                await connection.DisposeAsync().OrTimeout();
            }
        }

        [Fact]
        public async Task InvokeCompletedWhenCompletionMessageReceived()
        {
            var connection = new TestConnection();
            var hubConnection = new HubConnection(connection, new JsonHubProtocol(new JsonSerializer()), new LoggerFactory());
            try
            {
                await hubConnection.StartAsync();

                var invokeTask = hubConnection.Invoke("Foo");

                await connection.ReceiveJsonMessage(new { invocationId = "1", type = 3 }).OrTimeout();

                await invokeTask.OrTimeout();
            }
            finally
            {
                await hubConnection.DisposeAsync().OrTimeout();
                await connection.DisposeAsync().OrTimeout();
            }
        }

        [Fact]
        public async Task InvokeYieldsResultWhenCompletionMessageReceived()
        {
            var connection = new TestConnection();
            var hubConnection = new HubConnection(connection, new JsonHubProtocol(new JsonSerializer()), new LoggerFactory());
            try
            {
                await hubConnection.StartAsync();

                var invokeTask = hubConnection.Invoke<int>("Foo");

                await connection.ReceiveJsonMessage(new { invocationId = "1", type = 3, result = 42 }).OrTimeout();

                Assert.Equal(42, await invokeTask.OrTimeout());
            }
            finally
            {
                await hubConnection.DisposeAsync().OrTimeout();
                await connection.DisposeAsync().OrTimeout();
            }
        }

        [Fact]
        public async Task InvokeFailsWithExceptionWhenCompletionWithErrorReceived()
        {
            var connection = new TestConnection();
            var hubConnection = new HubConnection(connection, new JsonHubProtocol(new JsonSerializer()), new LoggerFactory());
            try
            {
                await hubConnection.StartAsync();

                var invokeTask = hubConnection.Invoke<int>("Foo");

                await connection.ReceiveJsonMessage(new { invocationId = "1", type = 3, error = "An error occurred" }).OrTimeout();

                var ex = await Assert.ThrowsAsync<HubException>(() => invokeTask).OrTimeout();
                Assert.Equal("An error occurred", ex.Message);
            }
            finally
            {
                await hubConnection.DisposeAsync().OrTimeout();
                await connection.DisposeAsync().OrTimeout();
            }
        }

        [Fact]
        // This will fail (intentionally) when we support streaming!
        public async Task InvokeFailsWithErrorWhenStreamingItemReceived()
        {
            var connection = new TestConnection();
            var hubConnection = new HubConnection(connection, new JsonHubProtocol(new JsonSerializer()), new LoggerFactory());
            try
            {
                await hubConnection.StartAsync();

                var invokeTask = hubConnection.Invoke<int>("Foo");

                await connection.ReceiveJsonMessage(new { invocationId = "1", type = 2, result = 42 }).OrTimeout();

                var ex = await Assert.ThrowsAsync<NotSupportedException>(() => invokeTask).OrTimeout();
                Assert.Equal("Streaming method results are not supported", ex.Message);
            }
            finally
            {
                await hubConnection.DisposeAsync().OrTimeout();
                await connection.DisposeAsync().OrTimeout();
            }
        }

        [Fact]
        public async Task HandlerRegisteredWithOnIsFiredWhenInvocationReceived()
        {
            var connection = new TestConnection();
            var hubConnection = new HubConnection(connection, new JsonHubProtocol(new JsonSerializer()), new LoggerFactory());
            var handlerCalled = new TaskCompletionSource<object[]>();
            try
            {
                await hubConnection.StartAsync();

                hubConnection.On<int, string, float>("Foo", (r1, r2, r3) => handlerCalled.TrySetResult(new object[] { r1, r2, r3 }));

                var args = new object[] { 1, "Foo", 2.0f };
                await connection.ReceiveJsonMessage(new { invocationId = "1", type = 1, target = "Foo", arguments = args }).OrTimeout();

                Assert.Equal(args, await handlerCalled.Task.OrTimeout());
            }
            finally
            {
                await hubConnection.DisposeAsync().OrTimeout();
                await connection.DisposeAsync().OrTimeout();
            }
        }
    }
}

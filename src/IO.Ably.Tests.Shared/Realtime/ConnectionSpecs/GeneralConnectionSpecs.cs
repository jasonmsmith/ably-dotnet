using System.Threading.Tasks;
using FluentAssertions;
using IO.Ably.Realtime;
using IO.Ably.Transport;
using IO.Ably.Types;
using Xunit;
using Xunit.Abstractions;

namespace IO.Ably.Tests.Realtime
{
    public class GeneralConnectionSpecs : ConnectionSpecsBase
    {
        [Fact]
        [Trait("spec", "RTN1")]
        public async Task ShouldUseWebSocketTransport()
        {
            var client = await GetConnectedClient(); // The transport is created by the connecting state

            client.ConnectionManager.Transport.GetType().Should().BeAssignableTo<ITransport>();
        }

        [Fact]
        [Trait("spec", "RTN3")]
        [Trait("spec", "RTN6")]
        public void WithAutoConnect_CallsConnectOnTransport()
        {
            var client = GetClientWithFakeTransport(opts => opts.AutoConnect = true);
            client.FakeProtocolMessageReceived(new ProtocolMessage(ProtocolMessage.MessageAction.Connected));

            client.ConnectionManager.ConnectionState.Should().Be(ConnectionState.Connected);
            LastCreatedTransport.ConnectCalled.Should().BeTrue();
        }

        [Fact]
        [Trait("spec", "RTN3")]
        public void WithAutoConnectFalse_LeavesStateAsInitialized()
        {
            var client = GetClientWithFakeTransport(opts => opts.AutoConnect = false);

            client.ConnectionManager.ConnectionState.Should().Be(ConnectionState.Initialized);
            LastCreatedTransport.Should().BeNull("Transport shouldn't be created without calling connect when AutoConnect is false");
        }

        [Fact]
        [Trait("spec", "RTN19")]
        public async Task WhenConnectedMessageReceived_ConnectionShouldBeInConnectedStateAndConnectionDetailsAreUpdated()
        {
            var client = GetClientWithFakeTransport();

            var connectionDetailsMessage = new ConnectionDetails()
            {
                ClientId = "123",
                ConnectionKey = "boo"
            };
            client.FakeProtocolMessageReceived(new ProtocolMessage(ProtocolMessage.MessageAction.Connected)
            {
                ConnectionDetails = connectionDetailsMessage
            });

            await client.WaitForState(ConnectionState.Connected);

            client.Connection.Key.Should().Be("boo");
        }

        [Fact]
        [Trait("spec", "RSA15a")]
        [Trait("sandboxTest", "needed")]
        public void WhenConnectedMessageReceivedWithClientId_AblyAuthShouldUseConnectionClientId()
        {
            var client = GetClientWithFakeTransport();

            client.FakeProtocolMessageReceived(new ProtocolMessage(ProtocolMessage.MessageAction.Connected)
            {
                ConnectionDetails = new ConnectionDetails { ClientId = "realtimeClient" }
            });

            client.RestClient.AblyAuth.ClientId.Should().Be("realtimeClient");
        }

        public GeneralConnectionSpecs(ITestOutputHelper output)
            : base(output)
        {
        }
    }
}

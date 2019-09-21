using System.Threading.Tasks;
using FluentAssertions;
using IO.Ably.Realtime;
using IO.Ably.Realtime.Workflow;
using IO.Ably.Transport;
using IO.Ably.Transport.States.Connection;
using IO.Ably.Types;
using Xunit;
using Xunit.Abstractions;

namespace IO.Ably.Tests
{
    public class ConnectedStateSpecs : AblySpecs
    {
        private FakeConnectionContext _context;
        private ConnectionConnectedState _state;
        private RealtimeState EmptyState = new RealtimeState();

        public ConnectedStateSpecs(ITestOutputHelper output)
            : base(output)
        {
            _context = new FakeConnectionContext();
            _state = GetState();
        }

        private ConnectionConnectedState GetState(ConnectionInfo info = null)
        {
            return new ConnectionConnectedState(_context, info ?? new ConnectionInfo(string.Empty, 0, string.Empty, string.Empty));
        }

        [Fact]
        public void ConnectedState_CorrectState()
        {
            // Assert
            _state.State.Should().Be(ConnectionState.Connected);
        }

        [Theory]
        [InlineData(ProtocolMessage.MessageAction.Ack)]
        [InlineData(ProtocolMessage.MessageAction.Attach)]
        [InlineData(ProtocolMessage.MessageAction.Attached)]
        [InlineData(ProtocolMessage.MessageAction.Closed)]
        [InlineData(ProtocolMessage.MessageAction.Connect)]
        [InlineData(ProtocolMessage.MessageAction.Detach)]
        [InlineData(ProtocolMessage.MessageAction.Detached)]
        [InlineData(ProtocolMessage.MessageAction.Disconnect)]
        [InlineData(ProtocolMessage.MessageAction.Heartbeat)]
        [InlineData(ProtocolMessage.MessageAction.Message)]
        [InlineData(ProtocolMessage.MessageAction.Nack)]
        [InlineData(ProtocolMessage.MessageAction.Presence)]
        [InlineData(ProtocolMessage.MessageAction.Sync)]
        public async Task ShouldNotHandleInboundMessageAction(ProtocolMessage.MessageAction action)
        {
            // Act
            bool result = await _state.OnMessageReceived(new ProtocolMessage(action), EmptyState);

            // Assert
            Assert.False(result);
            _context.ShouldHaveNotChangedState();
        }

        [Fact]
        public async Task ShouldHandleInboundDisconnectedMessageAndShouldQueueSetDisconnectedStateCommand()
        {
            // Act
            bool result = await _state.OnMessageReceived(new ProtocolMessage(ProtocolMessage.MessageAction.Disconnected), EmptyState);

            // Assert
            result.Should().BeTrue();
            //_context.ExecutedCommands.
            _context.ShouldQueueCommand<SetDisconnectedStateCommand>();
        }

        [Fact]
        public async Task ShouldHandlesInboundErrorMessageAndGoToFailedState()
        {
            ErrorInfo targetError = new ErrorInfo("test", 123);

            // Act
            bool result = await _state.OnMessageReceived(new ProtocolMessage(ProtocolMessage.MessageAction.Error) { Error = targetError }, EmptyState);

            // Assert
            result.Should().BeTrue();
            _context.ShouldQueueCommand<SetFailedStateCommand>(cmd => cmd.Error.ShouldBeEquivalentTo(targetError));
        }

        [Fact]
        public void WhenConnectCalled_ShouldDoNothing()
        {
            // Act
            _state.Connect();

            // Asser
            _context.ShouldHaveNotChangedState();
        }

        [Fact]
        [Trait("spec", "RTN12a")]
        public void WhenCloseCalled_ShouldCHangeStateToClosing()
        {
            // Act
            _state.Close();

            // Assert
            _context.ShouldQueueCommand<SetClosingStateCommand>();
        }

        [Fact]
        [Trait("spec", "RTN12c")]
        public async Task WhenCloseMessageReceived_ShouldChangeStateToClosed()
        {
            bool result = await _state.OnMessageReceived(new ProtocolMessage(ProtocolMessage.MessageAction.Close), EmptyState);

            result.Should().BeTrue();
            _context.ShouldQueueCommand<SetClosedStateCommand>();
        }

        [Fact]
        [Trait("spec", "RTN8b")]
        public void ConnectedState_UpdatesConnectionInformation()
        {
            // Act
            var state = GetState(new ConnectionInfo("test", 12564, "test test", string.Empty));

            state.BeforeTransition();

            // Assert
            var connection = _context.Connection;
            connection.Id.Should().Be("test");
            connection.Serial.Should().Be(12564);
            connection.Key.Should().Be("test test");
        }
    }
}

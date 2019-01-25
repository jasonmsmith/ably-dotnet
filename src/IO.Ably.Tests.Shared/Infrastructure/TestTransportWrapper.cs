﻿using System;
using System.Collections.Generic;
using IO.Ably.MessageEncoders;
using IO.Ably.Realtime;
using IO.Ably.Transport;
using IO.Ably.Types;

namespace IO.Ably.Tests.Infrastructure
{
    internal class TestTransportWrapper : ITransport
    {
        private class TransportListenerWrapper : ITransportListener
        {
            private readonly ITransportListener _wrappedListener;
            private readonly TestTransportWrapper _wrappedTransport;
            private readonly MessageHandler _handler;

            public List<ProtocolMessage> ProtocolMessagesReceived { get; set; } = new List<ProtocolMessage>();

            public TransportListenerWrapper(TestTransportWrapper wrappedTransport, ITransportListener wrappedListener, MessageHandler handler)
            {
                _wrappedTransport = wrappedTransport;
                _wrappedListener = wrappedListener;
                _handler = handler;
            }

            public void OnTransportDataReceived(RealtimeTransportData data)
            {
                ProtocolMessage msg = null;
                try
                {
                    msg = _handler.ParseRealtimeData(data);
                    ProtocolMessagesReceived.Add(msg);
                    if (_wrappedTransport.BeforeDataProcessed != null)
                    {
                        _wrappedTransport.BeforeDataProcessed?.Invoke(msg);
                        data = _handler.GetTransportData(msg);
                    }
                }
                catch (Exception ex)
                {
                    DefaultLogger.Error("Error handling beforeMessage helper.", ex);
                }

                try
                {
                    _wrappedListener.OnTransportDataReceived(data);
                }
                catch (Exception e)
                {
                    DefaultLogger.Error("Test transport factory on receive error ", e);
                }

                try
                {
                    _wrappedTransport.AfterDataReceived?.Invoke(msg);
                }
                catch (Exception ex)
                {
                    DefaultLogger.Error("Error handling afterMessage helper.", ex);
                }
            }

            public void OnTransportEvent(TransportState state, Exception exception = null)
            {
                _wrappedListener?.OnTransportEvent(state, exception);
            }
        }

        internal ITransport WrappedTransport { get; }

        private readonly MessageHandler _handler;

        /// <summary>
        /// A list of all protocol messages that have been received from the ably service since the transport was created
        /// </summary>
        public List<ProtocolMessage> ProtocolMessagesReceived => (Listener as TransportListenerWrapper)?.ProtocolMessagesReceived;

        public Action<ProtocolMessage> BeforeDataProcessed;
        public Action<ProtocolMessage> AfterDataReceived;
        public Action<ProtocolMessage> MessageSent = delegate { };

        public TestTransportWrapper(ITransport wrappedTransport, Protocol protocol)
        {
            WrappedTransport = wrappedTransport;
            _handler = new MessageHandler(protocol);
        }

        public TransportState State => WrappedTransport.State;

        public ITransportListener Listener
        {
            get => WrappedTransport.Listener;
            set => WrappedTransport.Listener = new TransportListenerWrapper(this, value, _handler);
        }

        public void FakeTransportState(TransportState state, Exception ex = null)
        {
            Listener?.OnTransportEvent(state, ex);
        }

        public void FakeReceivedMessage(ProtocolMessage message)
        {
            var data = _handler.GetTransportData(message);
            Listener?.OnTransportDataReceived(data);
        }

        public void Connect()
        {
            WrappedTransport.Connect();
        }

        public void Close(bool suppressClosedEvent = true)
        {
            DefaultLogger.Debug("Closing test transport!");
            WrappedTransport.Close(suppressClosedEvent);
        }

        public void Send(RealtimeTransportData data)
        {
            MessageSent(data.Original);
            WrappedTransport.Send(data);
        }

        public void Dispose()
        {
        }
    }
}

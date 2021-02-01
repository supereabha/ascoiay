using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Collections.Concurrent;

namespace AOSharp.Bootstrap.IPC
{
    public class IPCClient
    {
        public Action<IPCClient> OnConnected = null;
        public Action<IPCClient> OnDisconnected = null;
        private Dictionary<byte, IPCCallback> _callbacks = new Dictionary<byte, IPCCallback>();
        private IDictionary<int, IPCAsyncResult> _pendingTransactions = new ConcurrentDictionary<int, IPCAsyncResult>();
        private NamedPipeClientStream _client;
        private byte[] _buffer = new byte[ushort.MaxValue];
        private int _nextTransactionId = 0;

        public IPCClient(string name)
        {
            _client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        }

        public void RegisterCallback(byte opCode, Type type, Action<object, IPCMessage> callback = null)
        {
            if (!_callbacks.ContainsKey(opCode))
                _callbacks.Add(opCode, new IPCCallback() { OpCode = opCode, Type = type, Callback = callback });
        }

        public void DeregisterCallback(byte opCode)
        {
            if (_callbacks.ContainsKey(opCode))
                _callbacks.Remove(opCode);
        }

        public void Connect(int timeout = 10000)
        {
            _client.Connect(timeout);

            if (OnConnected != null)
                OnConnected(this);

            BeginRead();
        }

        public void Disconnect()
        {
            _client.Close();
            _client.Dispose();
        }

        private void BeginRead()
        {
            try
            {
                _client.BeginRead(_buffer, 0, _buffer.Length, ReadCallback, null);
            }
            catch (Exception)
            {
                if (OnDisconnected != null)
                    OnDisconnected(this);
            }
        }

        public void ReadCallback(IAsyncResult result)
        {
            int bytesRead;

            try
            {
                bytesRead = _client.EndRead(result);

                if(bytesRead == 0)
                    throw new IOException("bytesRead == 0");
            }
            catch (IOException)
            {
                if (OnDisconnected != null)
                    OnDisconnected(this);

                return;
            }

            byte opCode = _buffer[0];

            if (_callbacks.ContainsKey(opCode))
            {
                IPCCallback callback = _callbacks[opCode];
                IPCMessage message = (IPCMessage)Activator.CreateInstance(callback.Type);
                message.Deserialize(_buffer);

                if(message is IPCResponseMessage response)
                {
                    if(_pendingTransactions.ContainsKey(response.TransactionId))
                    {
                        _pendingTransactions[response.TransactionId].Response = response;
                        _pendingTransactions[response.TransactionId].Set();
                        _pendingTransactions.Remove(response.TransactionId);
                    }
                }

                if (callback.Callback != null)
                    callback.Callback(this, message);
            }

            BeginRead();
        }

        public void Send(IPCMessage message)
        {
            using (BinaryWriter writer = new BinaryWriter(_client, Encoding.Default, true))
            {
                writer.Write(message.Serialize());
                writer.Flush();
            }
        }

        public IPCAsyncResult Send(IPCRequestMessage message)
        {
            //Set the transaction id for this request.
            message.TransactionId = _nextTransactionId;

            //Setup a wait event for the response.
            IPCAsyncResult result = new IPCAsyncResult();
            _pendingTransactions.Add(_nextTransactionId, result);

            //Finally send the message to the dll.
            Send((IPCMessage)message);

            _nextTransactionId++;

            return result;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.IO.Pipes;

namespace AOSharp.Bootstrap.IPC
{
    public enum MsgType
    {
        Transaction,
        Event
    }

    [Serializable]
    public struct IPCCallback
    {
        public byte OpCode;
        public Type Type;
        public Action<object, IPCMessage> Callback;
    }

    [Serializable]
    public class IPCServer
    {
        public bool IsConnected { get { return _server.IsConnected; } }

        public Action<IPCServer> OnConnected = null;
        public Action<IPCServer> OnDisconnected = null;
        private Dictionary<byte, IPCCallback> _callbacks = new Dictionary<byte, IPCCallback>();
        private NamedPipeServerStream _server;
        private byte[] _buffer = new byte[ushort.MaxValue];

        public IPCServer(string pipeName)
        {
            _server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, -1, PipeTransmissionMode.Message, PipeOptions.Asynchronous);
        }

        public void Close()
        {
            _server.Close();
        }

        public void RegisterCallback(byte opCode, Type type, Action<object, IPCMessage> callback)
        {
            if(!_callbacks.ContainsKey(opCode))
                _callbacks.Add(opCode, new IPCCallback() { OpCode = opCode, Type = type, Callback = callback});
        }

        public void DeregisterCallback(byte opCode)
        {
            if (_callbacks.ContainsKey(opCode))
                _callbacks.Remove(opCode);
        }

        public void Start()
        {
            //Begin waiting for a connection from a client. 
            //Note: We will only allow one connection at a time making it a 1 on 1 conversation.
            _server.BeginWaitForConnection(WaitForConnectionCallback, null);
        }

        private void BeginRead()
        {
            try
            {
                _server.BeginRead(_buffer, 0, _buffer.Length, ReadCallback, null);
            }
            catch (Exception)
            {
                if (OnDisconnected != null)
                    OnDisconnected(this);
            }
        }

        private void WaitForConnectionCallback(IAsyncResult result)
        {
            //Accept the connection
            _server.EndWaitForConnection(result);

            //Notify subscribers that we have a new client connected
            if (OnConnected != null)
                OnConnected(this);

            //Begin reading messages from the client.
            BeginRead();
        }

        private void ReadCallback(IAsyncResult result)
        {
            int bytesRead;

            try
            {
                bytesRead = _server.EndRead(result);

                if (bytesRead == 0)
                    throw new IOException("bytesRead == 0");
            }
            catch(IOException)
            {
                if (OnDisconnected != null)
                    OnDisconnected(this);

                return;
            }

            byte opCode = _buffer[0];

            if(_callbacks.ContainsKey(opCode))
            {
                IPCCallback callback = _callbacks[opCode];
                IPCMessage message = (IPCMessage)Activator.CreateInstance(callback.Type);
                message.Deserialize(_buffer);

                if (callback.Callback != null)
                    callback.Callback(this, message);
            }

            BeginRead();
        }

        public void Send(IPCMessage message)
        {
            using (BinaryWriter writer = new BinaryWriter(_server, Encoding.Default, true))
            {
                writer.Write(message.Serialize());
                writer.Flush();
            }
        }
    }
}

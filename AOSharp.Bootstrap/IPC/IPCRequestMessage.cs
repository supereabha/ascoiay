using System.IO;

namespace AOSharp.Bootstrap.IPC
{
    public abstract class IPCRequestMessage : IPCMessage
    {
        public int TransactionId;

        protected IPCRequestMessage(byte opCode) : base(opCode)
        {
        }

        protected override void SerializeInternal(BinaryWriter writer)
        {
            base.SerializeInternal(writer);
            writer.Write(TransactionId);        
        }
    }
}

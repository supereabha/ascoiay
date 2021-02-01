using System.IO;

namespace AOSharp.Bootstrap.IPC
{
    public abstract class IPCResponseMessage : IPCMessage
    {
        public int TransactionId;

        public IPCResponseMessage(byte opCode) : base(opCode) { }

        protected IPCResponseMessage(byte opCode, int transactionId) : base (opCode)
        {
            TransactionId = transactionId;
        }

        protected override void SerializeInternal(BinaryWriter writer)
        {
            base.SerializeInternal(writer);
            writer.Write(TransactionId);        
        }
    }
}

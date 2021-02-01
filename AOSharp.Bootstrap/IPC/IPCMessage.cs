using System.IO;

namespace AOSharp.Bootstrap.IPC
{
    public abstract class IPCMessage
    {
        public byte OpCode { get; private set; }

        protected IPCMessage(byte opCode)
        {
            OpCode = opCode;
        }

        protected abstract void OnSerialize(BinaryWriter writer);
        protected abstract void OnDeserialize(BinaryReader reader);

        public byte[] Serialize()
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    SerializeInternal(writer);
                    OnSerialize(writer);
                    return stream.ToArray();
                }
            }
        }

        public void Deserialize(byte[] data)
        {
            using (MemoryStream stream = new MemoryStream(data))
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    DeserializeInternal(reader);
                    OnDeserialize(reader);
                }
            }
        }

        protected virtual void SerializeInternal(BinaryWriter writer)
        {
            writer.Write(OpCode);
        }

        protected virtual void DeserializeInternal(BinaryReader reader)
        {
            OpCode = reader.ReadByte();
        }
    }
}

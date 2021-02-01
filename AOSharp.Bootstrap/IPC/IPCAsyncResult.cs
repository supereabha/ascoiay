using System.Threading;

namespace AOSharp.Bootstrap.IPC
{
    public class IPCAsyncResult
    {
        public IPCMessage Response;
        private ManualResetEvent _waitEvent;

        public IPCAsyncResult()
        {
            _waitEvent = new ManualResetEvent(false);
        }

        public bool WaitOne(int timeout)
        {
            return _waitEvent.WaitOne(timeout);
        }

        public bool Set()
        {
            return _waitEvent.Set();
        }
    }
}

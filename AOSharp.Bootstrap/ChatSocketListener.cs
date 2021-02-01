using AOSharp.Common.Unmanaged.Imports;
using Serilog;
using SmokeLounge.AOtomation.Messaging.Messages;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOSharp.Bootstrap
{
    public class ChatSocketListener
    {
        public static int Socket => GetSocket();

        private byte[] _buffer = new byte[0];

        internal List<byte[]> ProcessBuffer(byte[] frameBuffer)
        {
            List<byte[]> packets = new List<byte[]>();

            try
            {
                _buffer = _buffer.Concat(frameBuffer).ToArray();

                while (_buffer.Length > 0)
                {

                    if (_buffer.Length < 4)
                    {
                        Log.Error($"ChatSocketListener buffer.Length ({_buffer.Length}) < 4!");
                        throw new Exception(); //Cause AO to crash
                    }

                    ushort length = (ushort)(((ushort)(_buffer[2] << 8) + _buffer[3]) + 4);

                    if (_buffer.Length < length)
                        return packets;

                    packets.Add(_buffer.Take(length).ToArray());
                    _buffer = _buffer.Skip(length).ToArray();
                }
            }
            catch (Exception e)
            {
                Log.Error(e.Message);
            }

            return packets;
        }

        private unsafe static int GetSocket()
        {
            IntPtr pChatServerInterface = *(IntPtr*)(Kernel32.GetProcAddress(Kernel32.GetModuleHandle("GUI.dll"), "?s_pcInstance@ChatGUIModule_c@@0PAV1@A") + 0x18);

            if (pChatServerInterface == IntPtr.Zero)
                return 0;

            IntPtr pChatServerUnk = *(IntPtr*)(pChatServerInterface + 0x30);

            if (pChatServerUnk == IntPtr.Zero)
                return 0;

            return *(int*)(pChatServerUnk + 0x78);
        }
    }
}

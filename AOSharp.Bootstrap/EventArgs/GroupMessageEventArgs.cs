using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AOSharp.Common.GameData;

namespace AOSharp.Bootstrap
{
    public class GroupMessageEventArgs : EventArgs
    {
        public readonly GroupMessage Message;
        public bool Cancel { get; set; } = false;

        public GroupMessageEventArgs(GroupMessage message)
        {
            Message = message;
        }
    }
}

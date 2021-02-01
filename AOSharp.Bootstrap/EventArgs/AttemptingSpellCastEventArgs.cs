using System;
using AOSharp.Common.GameData;

namespace AOSharp.Bootstrap
{
    public class AttemptingSpellCastEventArgs : EventArgs
    {
        public readonly Identity Nano;
        public readonly Identity Target;
        public bool Blocked { get; private set; }

        public AttemptingSpellCastEventArgs(Identity nano, Identity target)
        {
            Nano = nano;
            Target = target;
            Blocked = false;
        }

        public void Block() => Blocked = true;
    }
}

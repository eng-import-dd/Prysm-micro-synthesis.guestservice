using System;

namespace Synthesis.GuestService.Modules.Test.Utilities
{
    public static class LoopUtilities
    {
        public static void Repeat(int count, Action action)
        {
            for (int i = 0; i < count; i++)
            {
                action();
            }
        }
    }
}

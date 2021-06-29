using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiddleClickScroller
{
    public class ExtensionSettings
    {
        internal const double
          MIN_MOVE_POINTER_TRIGGER = 10.0,
          MIN_MOVE_POINTER_DEADBAND = 1.8,
          MIN_TIME_MS = 25.0,
          MOVE_DIVISOR = 200.0;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.VisualStudio.Text.Editor;

namespace MiddleClickScroller.Interfaces
{
    interface IMiddleClickScroll
    {
        IWpfTextView View { get; }

        IAdornmentLayer Layer { get; }

        Image ZeroPointImage { get; }

    }
}

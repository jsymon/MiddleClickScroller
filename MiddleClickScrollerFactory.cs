
namespace MiddleClickScroller
{
    using System.ComponentModel.Composition;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Utilities;

    [Export(typeof(IMouseProcessorProvider))]
    [Name("MiddleClickScroller")]
    [Order(Before = "UrlClickMouseProcessor")]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class MiddleClickScrollFactory : IMouseProcessorProvider
    {
        internal const string ADORNER_LAYER_NAME = "MiddleClickScrollLayer";
        //#pragma warning disable 649
        [Export]
        [Name(ADORNER_LAYER_NAME)]
        [Order(Before = PredefinedAdornmentLayers.Selection)]
        internal AdornmentLayerDefinition viewLayerDefinition;
        //#pragma warning restore 649

        public IMouseProcessor GetAssociatedProcessor(IWpfTextView textView)
        {
            return MiddleClickScroll.Create(textView);
        }
    }
}

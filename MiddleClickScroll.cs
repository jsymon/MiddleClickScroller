
namespace MiddleClickScroller
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using Microsoft.VisualStudio.Text.Editor;
    using MiddleClickScroller.Interfaces;

    internal sealed class MiddleClickScroll : MouseProcessorBase, IMiddleClickScroll
    {
        public static IMouseProcessor Create(IWpfTextView view)
        {
            return view.Properties.GetOrCreateSingletonProperty(() => new MiddleClickScroll(view));
        }

        private MiddleClickScroll(IWpfTextView view)
        {
            View = view;
            Layer = view.GetAdornmentLayer(MiddleClickScrollFactory.ADORNER_LAYER_NAME);
            this._middleClickScrollSessionFactory = new MiddleClickScrollSessionFactory();
            View.Closed += OnClosed;
            View.VisualElement.IsVisibleChanged += OnIsVisibleChanged;
        }

        public IWpfTextView View { get; }

        public IAdornmentLayer Layer { get; }

        private readonly MiddleClickScrollSessionFactory _middleClickScrollSessionFactory;

        private Image _zeroPointImage;
        public Image ZeroPointImage => _zeroPointImage = _zeroPointImage ?? CursorImages.BuildScrollAllImage();

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!MiddleClickScrollSessionFactory.CanScroll(this))
            {
                _middleClickScrollSessionFactory.StopSession();
            }
        }

        private void OnClosed(object sender, EventArgs e)
        {
            _middleClickScrollSessionFactory.StopSession();
            View.VisualElement.IsVisibleChanged -= OnIsVisibleChanged;
            View.Closed -= OnClosed;
        }

        // These methods get called for the entire mouse processing chain before calling PreprocessMouseDown
        // (& there is not an equivalent for PreprocessMouseMiddleButtonDown)
        public override void PreprocessMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            this.PreprocessMouseDown(e);
        }

        public override void PreprocessMouseRightButtonDown(MouseButtonEventArgs e)
        {
            this.PreprocessMouseDown(e);
        }

        public override void PreprocessMouseDown(MouseButtonEventArgs e)
        {
            if (_middleClickScrollSessionFactory.HasActiveSession)
            {
                _middleClickScrollSessionFactory.StopSession();
                e.Handled = true;
            }
            //middle scroll is not active. Activate!
            else if (e.ChangedButton == MouseButton.Middle)
            {
                Point elementPosition = e.GetPosition(View.VisualElement);
                if (_middleClickScrollSessionFactory.TryStartSession(this, elementPosition))
                {
                    e.Handled = true;
                }
            }
        }

        public override void PreprocessMouseUp(MouseButtonEventArgs e)
        {
            if (_middleClickScrollSessionFactory.HasSessionScrolled && (e.ChangedButton == MouseButton.Middle))
            {
                _middleClickScrollSessionFactory.StopSession();
                e.Handled = true;
            }
        }
    }
}

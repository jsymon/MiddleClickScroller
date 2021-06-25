
namespace MiddleClickScroller
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Interop;
    using System.Windows.Media.Imaging;
    using System.Windows.Threading;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Formatting;

    internal sealed class MiddleClickScroll : MouseProcessorBase
    {
        public static IMouseProcessor Create(IWpfTextView view)
        {
            return view.Properties.GetOrCreateSingletonProperty(() => new MiddleClickScroll(view));
        }

        private MiddleClickScroll(IWpfTextView view)
        {
            _view = view;
            _layer = view.GetAdornmentLayer(MiddleClickScrollFactory.ADORNER_LAYER_NAME);
            _view.Closed += OnClosed;
            _view.VisualElement.IsVisibleChanged += OnIsVisibleChanged;
        }

        private const double
            MIN_MOVE_POINTER_TRIGGER = 10.0,
            MIN_TIME_MS = 25.0,
            MOVE_DIVISOR = 200.0;

        private readonly IWpfTextView _view;

        private readonly IAdornmentLayer _layer;

        private Point? _location;
        private Cursor _oldCursor;
        private DispatcherTimer _moveTimer;
        private DateTime _lastMoveTime;

        private bool _dismissOnMouseUp;

        private Image _zeroPointImage;
        private Image ZeroPointImage => _zeroPointImage = _zeroPointImage ?? BuildZeroPointImage();


        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!_view.VisualElement.IsVisible)
            {
                this.StopScrolling();
            }
        }

        private void OnClosed(object sender, EventArgs e)
        {
            this.StopScrolling();

            _view.VisualElement.IsVisibleChanged -= OnIsVisibleChanged;
            _view.Closed -= OnClosed;
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
            if (_location.HasValue)
            {
                //The user didn't move enough so we didn't stop scrolling when they released the mouse.
                //Release it now (on any mouse down).
                this.StopScrolling();

                e.Handled = true;
            }
            else if (e.ChangedButton == MouseButton.Middle)
            {
                if ((!_view.IsClosed) && _view.VisualElement.IsVisible)
                {
                    if (_view.VisualElement.CaptureMouse())
                    {
                        _oldCursor = _view.VisualElement.Cursor;
                        _view.VisualElement.Cursor = Cursors.ScrollAll;

                        Point position = e.GetPosition(_view.VisualElement);
                        _location = _view.VisualElement.PointToScreen(position);

                        Canvas.SetLeft(ZeroPointImage, _view.ViewportLeft + position.X - ZeroPointImage.DesiredSize.Width * 0.5);
                        Canvas.SetTop(ZeroPointImage, _view.ViewportTop + position.Y - ZeroPointImage.DesiredSize.Height * 0.5);

                        _layer.AddAdornment(AdornmentPositioningBehavior.ViewportRelative, null, null, ZeroPointImage, null);


                        _lastMoveTime = DateTime.Now;

                        Debug.Assert(_moveTimer == null);
                        _moveTimer = new DispatcherTimer(new TimeSpan(0, 0, 0, 0, (int)MIN_TIME_MS), DispatcherPriority.Normal, OnTimerElapsed, _view.VisualElement.Dispatcher);

                        _dismissOnMouseUp = false;

                        e.Handled = true;
                    }
                }
            }
        }

        public override void PreprocessMouseUp(MouseButtonEventArgs e)
        {
            if (_dismissOnMouseUp && (e.ChangedButton == MouseButton.Middle))
            {
                this.StopScrolling();

                e.Handled = true;
            }
        }

        private void StopScrolling()
        {
            if (_location.HasValue)
            {
                _location = null;
                _view.VisualElement.Cursor = _oldCursor;
                _oldCursor = null;
                _view.VisualElement.ReleaseMouseCapture();
                _moveTimer.Stop();
                _moveTimer.Tick -= OnTimerElapsed;
                _moveTimer = null;

                _layer.RemoveAllAdornments();
            }

            Debug.Assert(_moveTimer == null);
        }

        private void OnTimerElapsed(object sender, EventArgs e)
        {
            TryMoveDisplay();
        }

        private void TryMoveDisplay()
        {
            if (_view.IsClosed 
                || !_view.VisualElement.IsVisible 
                || !_location.HasValue)
            {
                this.StopScrolling();
                return;
            }

            DateTime dateNow = DateTime.Now;
            
            Point currentPosition = _view.VisualElement.PointToScreen(Mouse.GetPosition(_view.VisualElement));            
            Vector delta = currentPosition - _location.Value;
                        
            double 
                absDeltaX = Math.Abs(delta.X),
                absDeltaY = Math.Abs(delta.Y);

            double maxDelta = Math.Max(absDeltaX, absDeltaY);
            
            if (maxDelta > MIN_MOVE_POINTER_TRIGGER)
            {
                _dismissOnMouseUp = true;
                
                //cast to int fixes jitter
                double pixelsToMove = GetPixelsToShift(dateNow, _lastMoveTime, maxDelta);
                pixelsToMove = Math.Truncate(pixelsToMove);

                //------------------------------------------------
                //if the pointer move is greatest on the X axis...
                //------------------------------------------------
                if (absDeltaX > absDeltaY)
                {
                    if (delta.X > 0.0)
                    {
                        //_view.ViewportLeft += pixelsToMove;
                        _view.ViewScroller.ScrollViewportHorizontallyByPixels(pixelsToMove);
                        _view.VisualElement.Cursor = Cursors.ScrollE;
                    }
                    else
                    {
                        _view.ViewScroller.ScrollViewportHorizontallyByPixels(-pixelsToMove);
                        //_view.ViewportLeft -= pixelsToMove;
                        _view.VisualElement.Cursor = Cursors.ScrollW;
                    }
                }
                else
                {
                    //ITextViewLine top = _view.TextViewLines[0];
                    //double newOffset = top.Top - _view.ViewportTop;
                    if (delta.Y > 0.0)
                    {
                        //newOffset = (newOffset - pixelsToMove);
                        _view.ViewScroller.ScrollViewportVerticallyByPixels(-pixelsToMove);
                        _view.VisualElement.Cursor = Cursors.ScrollS;
                    }
                    else
                    {
                        _view.ViewScroller.ScrollViewportVerticallyByPixels(pixelsToMove);
                        //newOffset = (newOffset + pixelsToMove);
                        _view.VisualElement.Cursor = Cursors.ScrollN;
                    }
                    //_view.DisplayTextLineContainingBufferPosition(top.Start, newOffset, ViewRelativePosition.Top);
                }
            }
            else
            {
                _view.VisualElement.Cursor = Cursors.ScrollAll;
            }
            _lastMoveTime = dateNow;

        }

        private static double GetPixelsToShift(DateTime dateNow, DateTime prevMoveDate, double movementDelta)
        {
            double deltaT = (dateNow - prevMoveDate).TotalMilliseconds;
            double pixelsToMove = (movementDelta - MIN_MOVE_POINTER_TRIGGER) * deltaT / MOVE_DIVISOR;
            return pixelsToMove;
        }

        private static Image BuildZeroPointImage()
        {
            //IMAGE_CURSOR      LR_CREATEDDIBSECTION   LR_SHARED
            IntPtr hScrollAllCursor = User32.LoadImage(IntPtr.Zero, new IntPtr(32512 + 142), (uint)2, 0, 0, (uint)(0x00002000 | 0x00008000));
            BitmapSource source = Imaging.CreateBitmapSourceFromHIcon(hScrollAllCursor, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();

            var zeroPointImage = new Image() { Source = source, Opacity = 0.5 };
            zeroPointImage.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return zeroPointImage;
        }
    }

    internal static class User32
    {
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr LoadImage(IntPtr hinst, IntPtr lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);
    }
}

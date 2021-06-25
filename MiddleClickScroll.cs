﻿
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


        private readonly IWpfTextView _view;
        private readonly IAdornmentLayer _layer;

        private Point? _location;
        private Cursor _oldCursor;
        private DispatcherTimer _moveTimer;
        private DateTime _lastMoveTime;
        private Image _zeroPointImage;
        private bool _dismissOnMouseUp;

        private const double MIN_MOVE_POINTER = 10.0;
        private const double MIN_TIME_MS = 25.0;
        private const double MOVE_DIVISOR = 200.0;

       
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
            else if (e.ChangedButton == System.Windows.Input.MouseButton.Middle)
            {
                if ((!_view.IsClosed) && _view.VisualElement.IsVisible)
                {
                    if (_view.VisualElement.CaptureMouse())
                    {
                        _oldCursor = _view.VisualElement.Cursor;
                        _view.VisualElement.Cursor = Cursors.ScrollAll;

                        Point position = e.GetPosition(_view.VisualElement);
                        _location = _view.VisualElement.PointToScreen(position);

                        if (_zeroPointImage == null)
                        {                                                                             //IMAGE_CURSOR      LR_CREATEDDIBSECTION   LR_SHARED
                            IntPtr hScrollAllCursor = User32.LoadImage(IntPtr.Zero, new IntPtr(32512 + 142), (uint)2, 0, 0, (uint)(0x00002000 | 0x00008000));
                            BitmapSource source = Imaging.CreateBitmapSourceFromHIcon(hScrollAllCursor, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                            source.Freeze();

                            _zeroPointImage = new Image();
                            _zeroPointImage.Source = source;

                            _zeroPointImage.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                            _zeroPointImage.Opacity = 0.5;
                        }

                        Canvas.SetLeft(_zeroPointImage, _view.ViewportLeft + position.X - _zeroPointImage.DesiredSize.Width * 0.5);
                        Canvas.SetTop(_zeroPointImage, _view.ViewportTop + position.Y - _zeroPointImage.DesiredSize.Height * 0.5);

                        _layer.AddAdornment(AdornmentPositioningBehavior.ViewportRelative, null, null, _zeroPointImage, null);

                        _lastMoveTime = DateTime.Now;

                        Debug.Assert(_moveTimer == null);
                        _moveTimer = new DispatcherTimer(new TimeSpan(0, 0, 0, 0, (int)MIN_TIME_MS), DispatcherPriority.Normal, OnTimerElapsed, _view.VisualElement.Dispatcher);

                        _dismissOnMouseUp = false;

                        e.Handled = true;
                    }
                }
            }
        }

        public override void PreprocessMouseUp(System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_dismissOnMouseUp && (e.ChangedButton == System.Windows.Input.MouseButton.Middle))
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
            if ((!_view.IsClosed) && _view.VisualElement.IsVisible && _location.HasValue)
            {
                DateTime now = DateTime.Now;

                Point currentPosition = _view.VisualElement.PointToScreen(Mouse.GetPosition(_view.VisualElement));

                var delta = currentPosition - _location.Value;

                double absDeltaX = Math.Abs(delta.X);
                double absDeltaY = Math.Abs(delta.Y);

                double maxDelta = Math.Max(absDeltaX, absDeltaY);
                if (maxDelta > MIN_MOVE_POINTER)
                {
                    _dismissOnMouseUp = true;

                    double deltaT = (now - _lastMoveTime).TotalMilliseconds;
                    double pixels = (maxDelta - MIN_MOVE_POINTER) * deltaT / MOVE_DIVISOR;

                    if (absDeltaX > absDeltaY)
                    {
                        if (delta.X > 0.0)
                        {
                            _view.ViewportLeft += pixels;
                            _view.VisualElement.Cursor = Cursors.ScrollE;
                        }
                        else
                        {
                            _view.ViewportLeft -= pixels;
                            _view.VisualElement.Cursor = Cursors.ScrollW;
                        }
                    }
                    else
                    {
                        ITextViewLine top = _view.TextViewLines[0];
                        double newOffset = top.Top - _view.ViewportTop;
                        if (delta.Y > 0.0)
                        {
                            newOffset = (newOffset - pixels);
                            _view.VisualElement.Cursor = Cursors.ScrollS;
                        }
                        else
                        {
                            newOffset = (newOffset + pixels);
                            _view.VisualElement.Cursor = Cursors.ScrollN;
                        }

                        _view.DisplayTextLineContainingBufferPosition(top.Start, newOffset, ViewRelativePosition.Top);
                    }
                }
                else
                {
                    _view.VisualElement.Cursor = Cursors.ScrollAll;
                }
                _lastMoveTime = now;
            }
            else
            {
                this.StopScrolling();
            }
        }
    }

    internal static class User32
    {
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr LoadImage(IntPtr hinst, IntPtr lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);
    }
}

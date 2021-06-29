using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.VisualStudio.Text.Editor;
using MiddleClickScroller.Interfaces;

namespace MiddleClickScroller
{
    class MiddleClickScrollSessionFactory
    {
        private MiddleClickScrollSession _middleClickScrollSession;

        internal bool HasActiveSession => _middleClickScrollSession != null;

        internal bool HasSessionScrolled => _middleClickScrollSession?.HasScrolled == true;


        public bool TryStartSession(IMiddleClickScroll middleClickScroll, Point elementPosition)
        {
            StopSession();
            if (CanScroll(middleClickScroll)
                && middleClickScroll.View.VisualElement.CaptureMouse())
            {
                _middleClickScrollSession = new MiddleClickScrollSession(middleClickScroll, elementPosition);
                return true;

            }
            return false;
        }

        internal static bool CanScroll(IMiddleClickScroll middleClickScroll)
        {
            return !middleClickScroll.View.IsClosed
                     && middleClickScroll.View.VisualElement.IsVisible;
        }

        public void StopSession()
        {
            _middleClickScrollSession?.AbortSession();
            _middleClickScrollSession = null;
        }


        class MiddleClickScrollSession
        {
            internal MiddleClickScrollSession(IMiddleClickScroll middleClickScroll, Point elementPosition)
            {
                this._middleClickScroll = middleClickScroll;
                this._preSessionCursor = this._middleClickScroll.View.VisualElement.Cursor;
                this._dateLastActivity = DateTime.Now;
                this._startPosition = _middleClickScroll.View.VisualElement.PointToScreen(elementPosition);
                AddZeroPointImage(elementPosition);

                this._moveTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(ExtensionSettings.MIN_TIME_MS), DispatcherPriority.Normal, OnTimerElapsed, middleClickScroll.View.VisualElement.Dispatcher);
            }

            private bool _isAborted = false;

            private readonly IMiddleClickScroll _middleClickScroll;

            private readonly Cursor _preSessionCursor;

            private readonly DispatcherTimer _moveTimer;

            private readonly Point _startPosition;

            private double _accumulatedHorizontalPixels = 0, _accumulatedVerticalPixels = 0;

            internal bool HasScrolled { get; private set; }

            private DateTime _dateLastActivity;

            private void OnTimerElapsed(object sender, EventArgs e)
            {
                TryMoveDisplay();
            }

            internal void AbortSession()
            {
                _isAborted = true;
                _middleClickScroll.View.VisualElement.Cursor = _preSessionCursor;
                _middleClickScroll.View.VisualElement.ReleaseMouseCapture();
                _moveTimer.Stop();
                _moveTimer.Tick -= OnTimerElapsed;
                _middleClickScroll.Layer.RemoveAllAdornments();
            }

            private void TryMoveDisplay()
            {
                if (!CanScroll(_middleClickScroll)
                    || _isAborted)
                {
                    this.AbortSession();
                    return;
                }

                DateTime dateNow = DateTime.Now;
                TimeSpan activityDelta = DateTime.Now.Subtract(_dateLastActivity);
                {
                    ScrollViewport(activityDelta);
                }
                _dateLastActivity = dateNow;
            }

            private void ScrollViewport(TimeSpan activityDelta)
            {
                Vector movementDelta = GetMovementDelta();

                GetPixelsToScroll(movementDelta.X, activityDelta, out double horizontalPixels);
                GetPixelsToScroll(movementDelta.Y, activityDelta, out double verticalPixels);
                _middleClickScroll.View.VisualElement.Cursor = CursorImages.GetScrollCursorByMovement(horizontalPixels, verticalPixels);

                ScrollAxis(horizontalPixels, ref _accumulatedHorizontalPixels, _middleClickScroll.View.ViewScroller.ScrollViewportHorizontallyByPixels);
                ScrollAxis(-verticalPixels, ref _accumulatedVerticalPixels, _middleClickScroll.View.ViewScroller.ScrollViewportVerticallyByPixels);
            }

            private static void ScrollAxis(double instancePixels, ref double accumulatedPixels, Action<double> scrollViewport)
            {
                if (instancePixels == 0)
                {
                    accumulatedPixels = 0;
                    return;
                }

                accumulatedPixels += instancePixels;
                double truncatedPixels = Math.Truncate(accumulatedPixels);
                if (truncatedPixels != 0)
                {
                    scrollViewport.Invoke(truncatedPixels);
                    accumulatedPixels -= truncatedPixels;
                }
            }

            private void GetPixelsToScroll(double movementDelta, TimeSpan activityDelta, out double pixels)
            {
                double absMovementDelta = Math.Abs(movementDelta);
                if (absMovementDelta > ExtensionSettings.MIN_MOVE_POINTER_TRIGGER)
                {
                    pixels = (absMovementDelta - ExtensionSettings.MIN_MOVE_POINTER_TRIGGER) * activityDelta.TotalMilliseconds / ExtensionSettings.MOVE_DIVISOR;
                    //reverse
                    if (movementDelta < 0)
                    {
                        pixels = -pixels;
                    }
                }
                else
                {
                    pixels = 0;
                }
            }

            //private static double GetPixelsToShift(TimeSpan activityDelta, double movementDelta, bool truncate = true)
            //{
            //    double deltaT = activityDelta.TotalMilliseconds;
            //    double pixelsToMove = (movementDelta - MiddleClickScroll.MIN_MOVE_POINTER_TRIGGER) * deltaT / MiddleClickScroll.MOVE_DIVISOR;
            //    if (truncate)
            //    {
            //        pixelsToMove = Math.Truncate(pixelsToMove);
            //    }
            //    return pixelsToMove;
            //}

            private Vector GetMovementDelta()
            {
                Point currentPosition = _middleClickScroll.View.VisualElement.PointToScreen(Mouse.GetPosition(_middleClickScroll.View.VisualElement));
                Vector delta = currentPosition - _startPosition;
                return delta;
            }

            private void AddZeroPointImage(Point relativePosition)
            {
                Canvas.SetLeft(_middleClickScroll.ZeroPointImage, _middleClickScroll.View.ViewportLeft + relativePosition.X - _middleClickScroll.ZeroPointImage.DesiredSize.Width * 0.5);
                Canvas.SetTop(_middleClickScroll.ZeroPointImage, _middleClickScroll.View.ViewportTop + relativePosition.Y - _middleClickScroll.ZeroPointImage.DesiredSize.Height * 0.5);
                _middleClickScroll.Layer.AddAdornment(AdornmentPositioningBehavior.ViewportRelative, null, null, _middleClickScroll.ZeroPointImage, null);
            }
        }

    }


}

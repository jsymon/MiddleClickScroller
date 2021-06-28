using System;
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
                this._startPosition =   _middleClickScroll.View.VisualElement.PointToScreen(elementPosition);
                AddZeroPointImage(elementPosition);

                this._moveTimer = new DispatcherTimer(new TimeSpan(0, 0, 0, 0, (int)MiddleClickScroll.MIN_TIME_MS), DispatcherPriority.Normal, OnTimerElapsed, middleClickScroll.View.VisualElement.Dispatcher);
            }

            private bool _isAborted = false;

            private readonly IMiddleClickScroll _middleClickScroll;

            private readonly Cursor _preSessionCursor;

            private readonly DispatcherTimer _moveTimer;

            private readonly Point _startPosition;

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
                MoveDisplay(activityDelta);
                _dateLastActivity = dateNow;
            }

            private void MoveDisplay(TimeSpan activityDelta)
            {
                Vector movementDelta = GetMovementDelta();

                double absDeltaX = Math.Abs(movementDelta.X),
                       absDeltaY = Math.Abs(movementDelta.Y);
                double absDeltaMax = Math.Max(absDeltaX, absDeltaY);

                if (absDeltaMax > MiddleClickScroll.MIN_MOVE_POINTER_TRIGGER)
                {
                    HasScrolled = true;

                    //cast to int fixes jitter
                    double pixelsToMove = GetPixelsToShift(activityDelta, absDeltaMax);

                    //------------------------------------------------
                    //if the pointer move is greatest on the X axis...
                    //------------------------------------------------
                    if (absDeltaX > absDeltaY)
                    {
                        if (movementDelta.X > 0.0)
                        {
                            //_view.ViewportLeft += pixelsToMove;
                            _middleClickScroll.View.ViewScroller.ScrollViewportHorizontallyByPixels(pixelsToMove);
                            _middleClickScroll.View.VisualElement.Cursor = Cursors.ScrollE;
                        }
                        else
                        {
                            _middleClickScroll.View.ViewScroller.ScrollViewportHorizontallyByPixels(-pixelsToMove);
                            //_view.ViewportLeft -= pixelsToMove;
                            _middleClickScroll.View.VisualElement.Cursor = Cursors.ScrollW;
                        }
                    }
                    else
                    {
                        //ITextViewLine top = _view.TextViewLines[0];
                        //double newOffset = top.Top - _view.ViewportTop;
                        if (movementDelta.Y > 0.0)
                        {
                            //newOffset = (newOffset - pixelsToMove);
                            _middleClickScroll.View.ViewScroller.ScrollViewportVerticallyByPixels(-pixelsToMove);
                            _middleClickScroll.View.VisualElement.Cursor = Cursors.ScrollS;
                        }
                        else
                        {
                            _middleClickScroll.View.ViewScroller.ScrollViewportVerticallyByPixels(pixelsToMove);
                            //newOffset = (newOffset + pixelsToMove);
                            _middleClickScroll.View.VisualElement.Cursor = Cursors.ScrollN;
                        }
                        //_view.DisplayTextLineContainingBufferPosition(top.Start, newOffset, ViewRelativePosition.Top);
                    }
                }
                else
                {
                    _middleClickScroll.View.VisualElement.Cursor = Cursors.ScrollAll;
                }
            }

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

            private static double GetPixelsToShift( TimeSpan activityDelta, double movementDelta, bool truncate = true)
            {
                double deltaT = activityDelta.TotalMilliseconds;
                double pixelsToMove = (movementDelta - MiddleClickScroll.MIN_MOVE_POINTER_TRIGGER) * deltaT / MiddleClickScroll.MOVE_DIVISOR;
                if (truncate)
                {
                    pixelsToMove = Math.Truncate(pixelsToMove);
                }
                return pixelsToMove;
            }
        }

    }


}

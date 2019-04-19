using System;
using System.Drawing;
using JetBrains.Annotations;
using Microsoft.Ink;
using Microsoft.StylusInput;
using Microsoft.StylusInput.PluginData;
using SlickWindows.Canvas;

namespace SlickWindows
{
    /// <summary>
    /// A real time stylus plugin that demonstrates
    /// custom dynamic rendering.  
    /// </summary>
    public class RealtimeRendererPlugin:IStylusSyncPlugin
    {
        // Declare the graphics object used for dynamic rendering
        [NotNull]private readonly EndlessCanvas _canvas;
        private TabletDeviceKind tabletKind;

        DPoint? _lastTouch;
        private static readonly object _tlock = new object();

        /// <summary>
        /// The highest pressure value we've seen (initial guess, updated as we collect data)
        /// </summary>
        private static float maxPressure = 2540.0F;

        private int _touchId;

        /// <summary>
        /// Constructor for this plugin
        /// </summary>
        /// <param name="g">The graphics object used for dynamic rendering.</param>
        public RealtimeRendererPlugin(EndlessCanvas g)
        {
            _canvas = g ?? throw new ArgumentNullException(nameof(g));
            _lastTouch = null;
            tabletKind = TabletDeviceKind.Pen;
        }


        /// <summary>
        /// Occurs when the stylus moves on the digitizer surface. 
        /// Use this notification to draw a small circle around each
        /// new packet received.
        /// </summary>
        /// <param name="sender">The real time stylus associated with the notification</param>
        /// <param name="data">The notification data</param>
        public void Packets(RealTimeStylus sender, PacketsData data)
        {
            if (sender == null || data == null) return;


            // For each new packet received, extract the x,y data
            // and draw a small circle around the result.
            for (int i = 0; i < data.Count; i += data.PacketPropertyCount)
            {
                // Packet data always has x followed by y followed by the rest
                var point = new Point(data[i], data[i+1]);

                // Since the packet data is in Ink Space coordinates, we need to convert to Pixels...
                point.X = (int)Math.Round(point.X * _canvas.DpiX / 2540.0F);
                point.Y = (int)Math.Round(point.Y * _canvas.DpiY / 2540.0F);
                var pressure = 1.0F;

                if (data.PacketPropertyCount > 2) // Contains pressure info
                {
                    if (data[i+2] > maxPressure) maxPressure = data[i+2];
                    pressure = data[i+2] / maxPressure;
                }

                // Draw a circle corresponding to the packet
                switch (tabletKind)
                {
                    case TabletDeviceKind.Pen:
                        // Make the packets from the stylus smaller and green
                        // TODO: join prev pos from same input (dictionary and up/down)
                        var x = new DPoint{ X = point.X, Y = point.Y, Pressure = pressure};
                        _canvas.Ink(x,x);

                        //myGraphics.DrawEllipse(Pens.Green, point.X - 2, point.Y - 2, 10 * pressure, 10 * pressure);
                        break;
                    case TabletDeviceKind.Mouse:
                        // Make the packets from the mouse/pointing device mid-sized and red
                        //myGraphics.DrawEllipse(Pens.Red, point.X - 2, point.Y - 2, 4, 4);
                        var m = new DPoint{ X = point.X, Y = point.Y, Pressure = pressure};
                        _canvas.Ink(m,m);
                        
                        break;
                    case TabletDeviceKind.Touch:
                        // Make the packets from a finger/touch digitizer larger and blue
                        //var indicPen = new Pen(Color.FromArgb(255, 0, 0, ((data.Stylus?.Id ?? 1) * 50) % 255), 1); // each touch point gets a new ID
                        //myGraphics.DrawEllipse(indicPen, point.X - 2, point.Y - 2, 20, 20);
                        // TODO: use delta of prev pos.

                        lock (_tlock)
                        {
                            // this needs improvement
                            if (_lastTouch == null)
                            {
                                _touchId = data.Stylus.Id;
                                _lastTouch = new DPoint { X = point.X, Y = point.Y, Pressure = pressure };
                                break;
                            }

                            if (data.Stylus.Id == _touchId)
                            {
                                _canvas.Scroll(
                                    _lastTouch.Value.X - point.X,
                                    _lastTouch.Value.Y - point.Y
                                    );
                                _lastTouch = null;
                            }
                        }
                        break;

                }
            }
        }

        /// <summary>
        /// Called when the current plugin or the ones previous in the list
        /// threw an exception.
        /// </summary>
        /// <param name="sender">The real time stylus associated with the notification</param>
        /// <param name="data">The notification data</param>
        public void Error(RealTimeStylus sender, ErrorData data)
        {
            //Debug.Assert(false, null, "An error occurred.  DataId=" + data.DataId + ", " + "Exception=" + data.InnerException);
        }

        /// <summary>
        /// Defines the types of notifications the plugin is interested in.
        /// </summary>
        public DataInterestMask DataInterest
        {
            get
            {
                // ReSharper disable BitwiseOperatorOnEnumWithoutFlags
                return DataInterestMask.StylusDown | DataInterestMask.StylusUp 
                     | DataInterestMask.Packets | DataInterestMask.Error;
                // ReSharper restore BitwiseOperatorOnEnumWithoutFlags
            }
        }

        // The remaining interface methods are not used in this sample application.
        public void RealTimeStylusDisabled(RealTimeStylus sender, RealTimeStylusDisabledData data) {}
        public void RealTimeStylusEnabled(RealTimeStylus sender, RealTimeStylusEnabledData data){}
        public void StylusOutOfRange(RealTimeStylus sender, StylusOutOfRangeData data) {}
        public void StylusInRange(RealTimeStylus sender, StylusInRangeData data) {}
        public void StylusDown(RealTimeStylus sender, StylusDownData data) 
        {
            // make a queue type, and set the stylus type
            if (data?.Stylus == null) return;
            var currentTablet = sender?.GetTabletFromTabletContextId(data.Stylus.TabletContextId);

            if (currentTablet != null)
            {
                tabletKind = currentTablet.DeviceKind;
            }
        }
        public void StylusUp(RealTimeStylus sender, StylusUpData data)
        {
            // TODO: clear down matching queue
            lock (_tlock)
            {
                _lastTouch = null;
            }
        }

        public void StylusButtonDown(RealTimeStylus sender, StylusButtonDownData data) {}
        public void StylusButtonUp(RealTimeStylus sender, StylusButtonUpData data) {}
        public void CustomStylusDataAdded(RealTimeStylus sender, CustomStylusData data){}
        public void SystemGesture(RealTimeStylus sender, SystemGestureData data){}
        public void InAirPackets(RealTimeStylus sender, InAirPacketsData data){}
        public void TabletAdded(RealTimeStylus sender, TabletAddedData data){}
        public void TabletRemoved(RealTimeStylus sender, TabletRemovedData data) {}
    }
}
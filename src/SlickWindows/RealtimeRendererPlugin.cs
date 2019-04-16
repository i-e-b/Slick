using System;
using System.Diagnostics;
using System.Drawing;
using JetBrains.Annotations;
using Microsoft.Ink;
using Microsoft.StylusInput;
using Microsoft.StylusInput.PluginData;

namespace SlickWindows
{
    /// <summary>
    /// A real time stylus plugin that demonstrates
    /// custom dynamic rendering.  
    /// </summary>
    public class RealtimeRendererPlugin:IStylusSyncPlugin
    {
        // Declare the graphics object used for dynamic rendering
        [NotNull]private readonly Graphics myGraphics;
        private TabletDeviceKind tabletKind;

        /// <summary>
        /// Constructor for this plugin
        /// </summary>
        /// <param name="g">The graphics object used for dynamic rendering.</param>
        public RealtimeRendererPlugin(Graphics g)
        {
            myGraphics = g ?? throw new ArgumentNullException(nameof(g));
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
                Point point = new Point(data[i], data[i+1]);

                // Since the packet data is in Ink Space coordinates, we need to convert to Pixels...
                point.X = (int)Math.Round(point.X * myGraphics.DpiX/2540.0F);
                point.Y = (int)Math.Round(point.Y * myGraphics.DpiY/2540.0F);

                // Draw a circle corresponding to the packet
                switch (this.tabletKind)
                {
                    case TabletDeviceKind.Pen:
                        // Make the packets from the stylus smaller and green
                        myGraphics.DrawEllipse(Pens.Green, point.X - 2, point.Y - 2, 4, 4);
                        break;
                    case TabletDeviceKind.Mouse:
                        // Make the packets from the mouse/pointing device mid-sized and red
                        myGraphics.DrawEllipse(Pens.Red, point.X - 2, point.Y - 2, 10, 10);
                        break;
                    case TabletDeviceKind.Touch:
                        // Make the packets from a finger/touch digitizer larger and blue
                        myGraphics.DrawEllipse(Pens.Blue, point.X - 2, point.Y - 2, 20, 20);
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
            Debug.Assert(false, null, "An error occurred.  DataId=" + data.DataId + ", " + "Exception=" + data.InnerException);
        }

        /// <summary>
        /// Defines the types of notifications the plugin is interested in.
        /// </summary>
        public DataInterestMask DataInterest
        {
            get
            {
                // ReSharper disable BitwiseOperatorOnEnumWithoutFlags
                return DataInterestMask.StylusDown | DataInterestMask.Packets | DataInterestMask.Error;
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
            var currentTablet = sender.GetTabletFromTabletContextId(data.Stylus.TabletContextId);

            if(currentTablet != null)
            {
                tabletKind = currentTablet.DeviceKind;
            }        
        }
        public void StylusUp(RealTimeStylus sender, StylusUpData data) {}
        public void StylusButtonDown(RealTimeStylus sender, StylusButtonDownData data) {}
        public void StylusButtonUp(RealTimeStylus sender, StylusButtonUpData data) {}
        public void CustomStylusDataAdded(RealTimeStylus sender, CustomStylusData data){}
        public void SystemGesture(RealTimeStylus sender, SystemGestureData data){}
        public void InAirPackets(RealTimeStylus sender, InAirPacketsData data){}
        public void TabletAdded(RealTimeStylus sender, TabletAddedData data){}
        public void TabletRemoved(RealTimeStylus sender, TabletRemovedData data) {}
    }
}
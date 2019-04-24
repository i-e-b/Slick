﻿using System;
using System.Collections.Generic;
using System.Drawing;
using JetBrains.Annotations;
using Microsoft.Ink;
using Microsoft.StylusInput;
using Microsoft.StylusInput.PluginData;
using SlickWindows.Canvas;

namespace SlickWindows.Input
{
    /// <summary>
    /// A real time stylus plugin that demonstrates
    /// custom dynamic rendering.  
    /// </summary>
    public class RealtimeRendererPlugin:IStylusSyncPlugin
    {
        [NotNull]private static readonly object _tlock = new object();
        [NotNull]private readonly EndlessCanvas _canvas;
        [NotNull]private readonly IKeyboard _keyboard;

        [NotNull] private readonly Dictionary<int,TabletDeviceKind> StylusId_to_DeviceKind;
        [NotNull] private readonly Dictionary<int,Queue<DPoint>> StylusId_to_Points;


        /// <summary>
        /// The highest pressure value we've seen (initial guess, updated as we collect data)
        /// </summary>
        private static float maxPressure = 2540.0F;

        /// <summary>
        /// Constructor for this plugin
        /// </summary>
        /// <param name="g">The graphics object used for dynamic rendering.</param>
        /// <param name="keyboard">Key state helper</param>
        public RealtimeRendererPlugin(EndlessCanvas g, IKeyboard keyboard)
        {
            StylusId_to_Points = new Dictionary<int, Queue<DPoint>>();
            StylusId_to_DeviceKind = new Dictionary<int, TabletDeviceKind>();
            _canvas = g ?? throw new ArgumentNullException(nameof(g));
            _keyboard = keyboard ?? throw new ArgumentNullException(nameof(keyboard));
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
            if (sender == null || data?.Stylus == null) return;
            lock (_tlock)
            {
                if (!StylusId_to_DeviceKind.ContainsKey(data.Stylus.Id)) return; // unmapped!
            }

            // For each new packet received, extract the x,y data
            // and draw a small circle around the result.
            for (int i = 0; i < data.Count; i += data.PacketPropertyCount)
            {
                // Packet data always has x followed by y followed by the rest
                var point = new Point(data[i], data[i+1]);

                // Since the packet data is in Ink Space coordinates, we need to convert to Pixels...
                point.X = (int)Math.Round(point.X * _canvas.DpiX / 2540.0F);
                point.Y = (int)Math.Round(point.Y * _canvas.DpiY / 2540.0F);
                var pressure = 0.5F;

                if (data.PacketPropertyCount > 2) // Contains pressure info
                {
                    if (data[i+2] > maxPressure) maxPressure = data[i+2];
                    pressure = data[i+2] / maxPressure;
                }

                var thisPt = new DPoint{ X = point.X, Y = point.Y, Pressure = pressure};

                TabletDeviceKind tabletKind;
                Queue<DPoint> ptQ;
                lock (_tlock)
                {
                    tabletKind = StylusId_to_DeviceKind[data.Stylus.Id];
                    ptQ = StylusId_to_Points[data.Stylus.Id];
                    if (ptQ == null) break;
                    ptQ.Enqueue(thisPt);
                }

                // Draw a circle corresponding to the packet
                switch (tabletKind)
                {
                    case TabletDeviceKind.Pen:
                    case TabletDeviceKind.Mouse:
                        if (_keyboard.IsPanKeyHeld()) {
                            Scroll(ptQ);
                        } else {
                            Draw(data.Stylus.Id, ptQ);
                        }
                        break;

                    case TabletDeviceKind.Touch:
                        Scroll(ptQ);
                        break;

                }
            }
        }

        private void Draw(int stylusId, [NotNull]Queue<DPoint> ptQ)
        {
            while (ptQ.Count > 1)
            {
                var a = ptQ.Dequeue();
                var b = ptQ.Peek();
                _canvas.Ink(stylusId, a, b);
            }
        }

        private void Scroll([NotNull]Queue<DPoint> ptQ)
        {
            while (ptQ.Count > 1)
            {
                var a = ptQ.Dequeue();
                var b = ptQ.Peek();
                _canvas.Scroll(a.X - b.X, a.Y - b.Y);
            }
        }

        /// <summary>
        /// Defines the types of notifications the plugin is interested in.
        /// </summary>
        public DataInterestMask DataInterest
        {
            get
            {
                // ReSharper disable BitwiseOperatorOnEnumWithoutFlags
                return DataInterestMask.StylusDown | DataInterestMask.StylusUp | DataInterestMask.Packets;
                // ReSharper restore BitwiseOperatorOnEnumWithoutFlags
            }
        }

        public void StylusDown(RealTimeStylus sender, StylusDownData data) 
        {
            // make a queue type, and set the stylus type
            if (data?.Stylus == null) return;

            lock (_tlock)
            {
                if (StylusId_to_DeviceKind.ContainsKey(data.Stylus.Id)) return;

                var currentTablet = sender?.GetTabletFromTabletContextId(data.Stylus.TabletContextId);
                if (currentTablet != null) {
                    StylusId_to_DeviceKind.Add(data.Stylus.Id, currentTablet.DeviceKind);
                    StylusId_to_Points.Add(data.Stylus.Id, new Queue<DPoint>());
                }
            }
        }

        public void StylusUp(RealTimeStylus sender, StylusUpData data)
        {
            if (data?.Stylus == null) return;
            lock (_tlock)
            {
                if (StylusId_to_DeviceKind[data.Stylus.Id] != TabletDeviceKind.Touch) {
                    _canvas.SaveChanges();
                }

                StylusId_to_DeviceKind.Remove(data.Stylus.Id);
                StylusId_to_Points.Remove(data.Stylus.Id);
            }
        }

        // The remaining interface methods are not used in this
        public void Error(RealTimeStylus sender, ErrorData data) { }
        public void RealTimeStylusDisabled(RealTimeStylus sender, RealTimeStylusDisabledData data) {}
        public void RealTimeStylusEnabled(RealTimeStylus sender, RealTimeStylusEnabledData data){}
        public void StylusOutOfRange(RealTimeStylus sender, StylusOutOfRangeData data) {}
        public void StylusInRange(RealTimeStylus sender, StylusInRangeData data) {}

        public void StylusButtonDown(RealTimeStylus sender, StylusButtonDownData data) {}
        public void StylusButtonUp(RealTimeStylus sender, StylusButtonUpData data) {}
        public void CustomStylusDataAdded(RealTimeStylus sender, CustomStylusData data){}
        public void SystemGesture(RealTimeStylus sender, SystemGestureData data){}
        public void InAirPackets(RealTimeStylus sender, InAirPacketsData data){}
        public void TabletAdded(RealTimeStylus sender, TabletAddedData data){}
        public void TabletRemoved(RealTimeStylus sender, TabletRemovedData data) {}
    }
}
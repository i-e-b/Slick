using Microsoft.Ink;
using Microsoft.StylusInput;
using Microsoft.StylusInput.PluginData;
using SlickCommon.Canvas;

namespace SlickWindows.Input
{
    /// <summary>
    /// A real time stylus plugin that demonstrates
    /// custom dynamic rendering.  
    /// </summary>
    public class CanvasDrawingPlugin:IStylusSyncPlugin
    {
        private static readonly object _tileLock = new();

        private readonly Form?          _container;
        private readonly IEndlessCanvas _canvas;
        private readonly IKeyboard      _keyboard;

        private readonly Dictionary<int, TabletDeviceKind> _stylusIdToDeviceKind;
        private readonly Dictionary<int, Queue<DPoint>>    _stylusIdToPoints;


        /// <summary>
        /// The highest pressure value we've seen (initial guess, updated as we collect data)
        /// </summary>
        private static float _maxPressure = 4096.0f;

        /// <summary>
        /// The 'pressure' value used when an input method doesn't supply one (0..1)
        /// </summary>
        public const float DefaultPressure = 0.65f;

        /// <summary>
        /// Constructor for this plugin
        /// </summary>
        /// <param name="container">Window that receives pen input</param>
        /// <param name="g">The graphics object used for dynamic rendering.</param>
        /// <param name="keyboard">Key state helper</param>
        public CanvasDrawingPlugin(Form container, IEndlessCanvas g, IKeyboard keyboard)
        {
            _stylusIdToPoints = new Dictionary<int, Queue<DPoint>>();
            _stylusIdToDeviceKind = new Dictionary<int, TabletDeviceKind>();
            _container = container;
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
        public void Packets(RealTimeStylus? sender, PacketsData? data)
        {
            if (sender == null || data?.Stylus == null) return;
            Queue<DPoint>? ptQ;

            lock (_tileLock)
            {
                if (!_stylusIdToDeviceKind.ContainsKey(data.Stylus.Id)) return; // unmapped
                if (!_stylusIdToPoints.TryGetValue(data.Stylus.Id, out ptQ)) return; // unmapped
            }

            ReadPacketDataToQueue(data, ptQ);
            DrawPointQueue(data.Stylus, ptQ, exhaust: false);
        }

        private void DrawPointQueue(Stylus? stylus, Queue<DPoint>? ptQ, bool exhaust)
        {
            if (stylus == null || ptQ == null) return;

            TabletDeviceKind tabletKind;
            lock (_tileLock)
            {
                tabletKind = _stylusIdToDeviceKind[stylus.Id];
            }

            switch (tabletKind)
            {
                case TabletDeviceKind.Pen:
                case TabletDeviceKind.Mouse:
                    if (_keyboard.IsPanKeyHeld())
                    {
                        Scroll(ptQ);
                    }
                    else
                    {
                        Draw(ptQ, exhaust);
                    }

                    break;

                case TabletDeviceKind.Touch:
                    Scroll(ptQ);
                    break;
            }
        }
        
        private bool IsInkStroke(Stylus? stylus)
        {
            if (stylus == null) return false;

            TabletDeviceKind tabletKind;
            lock (_tileLock) { tabletKind = _stylusIdToDeviceKind[stylus.Id]; }

            switch (tabletKind)
            {
                case TabletDeviceKind.Pen:
                case TabletDeviceKind.Mouse:
                    return !_keyboard.IsPanKeyHeld();

                default:
                    return false;
            }
        }

        private void ReadPacketDataToQueue(StylusDataBase? data, Queue<DPoint>? ptQ)
        {
            if (data?.Stylus == null || ptQ == null) return;

            // Win32 scaling and DPI is a hot mess
            // There's no logic to this, just painful trial and error
            var dpiDiff = (_container?.DeviceDpi ?? 96) / 96.0;
            var effectiveDpi = _canvas.Dpi * dpiDiff;
            effectiveDpi /= 2540.0; // map from ink space to pixel space

            // For each new packet received, extract the x,y data
            // and draw a small circle around the result.
            for (int i = 0; i < data.Count; i += data.PacketPropertyCount)
            {
                // Packet data always has x followed by y followed by the rest
                var point = new DPoint { X = data[i], Y = data[i + 1] };

                // Since the packet data is in Ink Space coordinates, we need to convert to Pixels...
                point.X *= effectiveDpi;
                point.Y *= effectiveDpi;
                var pressure = DefaultPressure;

                if (data.PacketPropertyCount > 2) // Contains pressure info
                {
                    if (data[i + 2] > _maxPressure) _maxPressure = data[i + 2];
                    pressure = data[i + 2] / _maxPressure;
                }

                var thisPt = new DPoint
                {
                    X = point.X,
                    Y = point.Y,
                    Pressure = pressure,
                    IsErase = data.Stylus.Name == "Eraser",
                    StylusId = data.Stylus.Id
                };

                ptQ.Enqueue(thisPt);
            }
        }

        private void Draw(Queue<DPoint> ptQ, bool exhaust)
        {
            while (ptQ.Count > 1)
            {
                var a = ptQ.Dequeue();
                var b = ptQ.Peek();
                _canvas.Ink(a, b);
            }
            if (exhaust && ptQ.Count == 1) {
                var a = ptQ.Dequeue();
                _canvas.Ink(a, a);
            }
        }

        private void Scroll(Queue<DPoint> ptQ)
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
        public DataInterestMask DataInterest =>
        // ReSharper disable BitwiseOperatorOnEnumWithoutFlags
            DataInterestMask.StylusDown | DataInterestMask.StylusUp | DataInterestMask.Packets;
        // ReSharper restore BitwiseOperatorOnEnumWithoutFlags

        public void StylusDown(RealTimeStylus? sender, StylusDownData? data) 
        {
            // make a queue type, and set the stylus type
            if (data?.Stylus == null) return;

            lock (_tileLock)
            {
                // make sure the stylus is mapped
                if (!_stylusIdToDeviceKind.ContainsKey(data.Stylus.Id))
                {
                    var currentTablet = sender?.GetTabletFromTabletContextId(data.Stylus.TabletContextId);
                    if (currentTablet != null)
                    {
                        _stylusIdToDeviceKind.Add(data.Stylus.Id, currentTablet.DeviceKind);
                        _stylusIdToPoints.Add(data.Stylus.Id, new Queue<DPoint>());

                    }
                }
                // use first 'down' point:
                ReadPacketDataToQueue(data, _stylusIdToPoints[data.Stylus.Id]);

                if (IsInkStroke(data.Stylus)) {
                    _canvas.StartStroke();
                }
            }
        }

        public void StylusUp(RealTimeStylus? sender, StylusUpData? data)
        {
            if (data?.Stylus == null) return;
            lock (_tileLock)
            {
                // write out any waiting points. (this prevents us losing single-touch dots)
                DrawPointQueue(data.Stylus, _stylusIdToPoints[data.Stylus.Id], exhaust: true);

                if (_stylusIdToDeviceKind[data.Stylus.Id] != TabletDeviceKind.Touch) {
                    _canvas.EndStroke();
                    _canvas.SaveChanges();
                }

                _stylusIdToDeviceKind.Remove(data.Stylus.Id);
                _stylusIdToPoints.Remove(data.Stylus.Id);
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
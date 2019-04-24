using Microsoft.StylusInput;
using Microsoft.StylusInput.PluginData;

namespace SlickWindows.Input
{
    /// <summary>
    /// Trigger a component when we get input data
    /// </summary>
    public class TouchPointStylusPlugin : IStylusAsyncPlugin
    {
        private readonly ITouchTriggered _sink;
        private readonly int _deviceDpi;

        public TouchPointStylusPlugin(ITouchTriggered sink, int deviceDpi)
        {
            _sink = sink;
            _deviceDpi = deviceDpi;
        }

        public void StylusDown(RealTimeStylus sender, StylusDownData data)
        {
            if (data.Count < 2) return; // no co-ordinates

            var x = data[0] * _deviceDpi / 2540.0F;
            var y = data[1] * _deviceDpi / 2540.0F;
            _sink.Touched(data.Stylus.Id, (int)x, (int)y);
        }

        /// <summary>
        /// Defines the types of notifications the plugin is interested in.
        /// </summary>
        public DataInterestMask DataInterest { get { return DataInterestMask.StylusDown; } }

        // The remaining interface methods are not used.
        public void CustomStylusDataAdded(RealTimeStylus sender, CustomStylusData data) { }
        public void Error(RealTimeStylus sender, ErrorData data) { }
        public void RealTimeStylusDisabled(RealTimeStylus sender, RealTimeStylusDisabledData data) {}
        public void RealTimeStylusEnabled(RealTimeStylus sender, RealTimeStylusEnabledData data){}
        public void StylusOutOfRange(RealTimeStylus sender, StylusOutOfRangeData data) {}
        public void StylusInRange(RealTimeStylus sender, StylusInRangeData data) {}
        public void StylusUp(RealTimeStylus sender, StylusUpData data) { }
        public void Packets(RealTimeStylus sender,  PacketsData data) { }
        public void StylusButtonDown(RealTimeStylus sender, StylusButtonDownData data) {}
        public void StylusButtonUp(RealTimeStylus sender, StylusButtonUpData data) {}
        public void SystemGesture(RealTimeStylus sender, SystemGestureData data){}
        public void InAirPackets(RealTimeStylus sender, InAirPacketsData data){}
        public void TabletAdded(RealTimeStylus sender, TabletAddedData data){}
        public void TabletRemoved(RealTimeStylus sender, TabletRemovedData data) {}
    }
}
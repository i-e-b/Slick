using Microsoft.StylusInput;
using Microsoft.StylusInput.PluginData;

namespace SlickWindows
{
    /// <summary>
    /// Trigger a component when we get input data
    /// </summary>
    public class DataTriggerStylusPlugin : IStylusAsyncPlugin
    {
        private readonly IDataTriggered _sink;

        public DataTriggerStylusPlugin(IDataTriggered sink)
        {
            _sink = sink;
        }


        public void StylusUp(RealTimeStylus sender, StylusUpData data) {
            if (sender == null || data == null) return;
            _sink?.DataCollected(sender);
        }

        public void Packets(RealTimeStylus sender,  PacketsData data) {
            if (sender == null || data == null) return;
            _sink?.DataCollected(sender);
        }


        /// <summary>
        /// Defines the types of notifications the plugin is interested in.
        /// </summary>
        public DataInterestMask DataInterest
        {
            get
            {
                // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
                return DataInterestMask.Packets | DataInterestMask.StylusUp;
            }
        }

        // The remaining interface methods are not used.
        public void CustomStylusDataAdded(RealTimeStylus sender, CustomStylusData data) { }
        public void Error(RealTimeStylus sender, ErrorData data) { }
        public void RealTimeStylusDisabled(RealTimeStylus sender, RealTimeStylusDisabledData data) {}
        public void RealTimeStylusEnabled(RealTimeStylus sender, RealTimeStylusEnabledData data){}
        public void StylusOutOfRange(RealTimeStylus sender, StylusOutOfRangeData data) {}
        public void StylusInRange(RealTimeStylus sender, StylusInRangeData data) {}
        public void StylusDown(RealTimeStylus sender, StylusDownData data) {}
        public void StylusButtonDown(RealTimeStylus sender, StylusButtonDownData data) {}
        public void StylusButtonUp(RealTimeStylus sender, StylusButtonUpData data) {}
        public void SystemGesture(RealTimeStylus sender, SystemGestureData data){}
        public void InAirPackets(RealTimeStylus sender, InAirPacketsData data){}
        public void TabletAdded(RealTimeStylus sender, TabletAddedData data){}
        public void TabletRemoved(RealTimeStylus sender, TabletRemovedData data) {}
    }
}
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

        /// <summary>
        /// Informs the implementing object that user data is available.
        /// </summary>
        /// <param name="sender">The real time stylus associated with the notification</param>
        /// <param name="data">The notification data</param>
        public void CustomStylusDataAdded(RealTimeStylus sender, CustomStylusData data)
        {
            if (sender == null || data == null) return;
            _sink?.DataCollected(sender, data);
        }

        /// <summary>
        /// Called when the current plugin or the ones previous in the list
        /// threw an exception.
        /// </summary>
        /// <param name="sender">The real time stylus associated with the notification</param>
        /// <param name="data">The notification data</param>
        public void Error(RealTimeStylus sender, ErrorData data)
        {
            if (sender == null || data == null) return;
            _sink?.Error(sender, data);
        }

        /// <summary>
        /// Defines the types of notifications the plugin is interested in.
        /// </summary>
        public DataInterestMask DataInterest
        {
            get
            {
                // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
                return DataInterestMask.CustomStylusDataAdded | DataInterestMask.Error;
            }
        }

        // The remaining interface methods are not used in this sample application.
        public void RealTimeStylusDisabled(RealTimeStylus sender, RealTimeStylusDisabledData data) {}
        public void RealTimeStylusEnabled(RealTimeStylus sender, RealTimeStylusEnabledData data){}
        public void StylusOutOfRange(RealTimeStylus sender, StylusOutOfRangeData data) {}
        public void StylusInRange(RealTimeStylus sender, StylusInRangeData data) {}
        public void StylusDown(RealTimeStylus sender, StylusDownData data) {}
        public void StylusUp(RealTimeStylus sender, StylusUpData data) {}
        public void StylusButtonDown(RealTimeStylus sender, StylusButtonDownData data) {}
        public void StylusButtonUp(RealTimeStylus sender, StylusButtonUpData data) {}
        public void SystemGesture(RealTimeStylus sender, SystemGestureData data){}
        public void Packets(RealTimeStylus sender,  PacketsData data) {}
        public void InAirPackets(RealTimeStylus sender, InAirPacketsData data){}
        public void TabletAdded(RealTimeStylus sender, TabletAddedData data){}
        public void TabletRemoved(RealTimeStylus sender, TabletRemovedData data) {}
    }
}
using Microsoft.StylusInput;
using Microsoft.StylusInput.PluginData;

namespace SlickWindows
{
    public interface IDataTriggered
    {
        void DataCollected(RealTimeStylus sender, CustomStylusData data);
        void Error(RealTimeStylus sender, ErrorData data);
    }
}
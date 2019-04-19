using Microsoft.StylusInput;

namespace SlickWindows
{
    public interface IDataTriggered
    {
        void DataCollected(RealTimeStylus sender);
    }
}
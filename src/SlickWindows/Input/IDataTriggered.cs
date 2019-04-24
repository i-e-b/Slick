using Microsoft.StylusInput;

namespace SlickWindows.Input
{
    public interface IDataTriggered
    {
        void DataCollected(RealTimeStylus sender);
    }
}
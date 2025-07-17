using Microsoft.StylusInput;

namespace SlickWindows.Input
{
    /// <summary>
    /// Helper used to store information about a given plugin.
    /// </summary>
    public struct PluginListItem
    {
        /// <summary>
        /// The real time stylus plugin associated with this class.
        /// </summary>
        public IStylusSyncPlugin Plugin;

        /// <summary>
        /// A string containing the description of the plugin.
        /// </summary>
        public string Description;

        /// <summary>
        /// Public constructor
        /// </summary>
        /// <param name="stylusPlugin">The plugin</param>
        /// <param name="pluginDescription">Description of the plugin</param>
        public PluginListItem(IStylusSyncPlugin stylusPlugin, string pluginDescription)
        {
            Plugin = stylusPlugin;
            Description = pluginDescription ?? throw new ArgumentNullException(nameof(pluginDescription));
        }

        /// <summary>
        /// The description of this plugin
        /// </summary>
        /// <returns>String that describes this plugin</returns>
        public override string ToString()
        {
            return Description;
        }
    }
}
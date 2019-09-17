using System;
using System.IO;
using System.Text;
using Windows.Storage;
using SlickUWP.Adaptors;

namespace SlickUWP.CrossCutting
{
    public static class Logging
    {

        // look in a path like C:\Users\...\AppData\Local\Packages\IEB-Slick-UWP_bhcbx7r9h0w0a\LocalState\Logs

        // Appends to a plain text file
        public static void WriteLogMessage(string message) {

            var basePath = ApplicationData.Current?.LocalCacheFolder?.Path;
            if (basePath == null) return; // useless.


            var folder = Sync.Run(()=> StorageFolder.GetFolderFromPathAsync(basePath));
            if (folder == null) { throw new Exception("Path to log file is not available"); }

            var file = Sync.Run(()=> folder.CreateFileAsync("log.txt", CreationCollisionOption.OpenIfExists));
            if (file == null || !file.IsAvailable) { throw new Exception("Failed to open log file"); }

            using (var stream = Sync.Run(() => file.OpenStreamForWriteAsync())) {
                if (stream == null) return;
                stream.Seek(0, SeekOrigin.End);
                stream.WriteByte(0x0d); // CR
                stream.WriteByte(0x0a); // LF
                var bytes = Encoding.UTF8?.GetBytes(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") + message);
                if (bytes != null) stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
            }

        }

        public static void LaunchLogInEditor() {
            var basePath = ApplicationData.Current?.LocalCacheFolder?.Path;
            if (basePath == null) return;

            var folder = Sync.Run(()=> StorageFolder.GetFolderFromPathAsync(basePath));
            if (folder == null) { throw new Exception("Path to log file is not available"); }

            var file = Sync.Run(()=> folder.CreateFileAsync("log.txt", CreationCollisionOption.OpenIfExists));
            if (file == null || !file.IsAvailable) { throw new Exception("Failed to open log file"); }

            Sync.Run(() => Windows.System.Launcher.LaunchFileAsync(file));
        }


        /*

        Logging is yet another total mess in UWP.
        https://github.com/Microsoft/Windows-universal-samples/tree/master/Samples/Logging

        This 'official' way of logging is total crap. It requires you to track disposables in a static context,
        spews enormous files, loses data, and the output format is useless.

        // Use the event viewer and 'open saved log' to read the .etl files.
        public static void WriteError()
        {
            using (var session = new FileLoggingSession("Default"))
            {
                using (var channel = new LoggingChannel("SlickUWPLogging", null)) // null means use default options.
                {
                    session.AddLoggingChannel(channel);
                    //session.LogFileGenerated += Session_LogFileGenerated; // doesn't work

                    //channel.Enabled = true; // can't be set
                    //channel.Level = LoggingLevel.Verbose; // can't be set

                    var fields = new LoggingFields();
                    fields.AddString("IEB_FieldName", "This is the Field value XXXXXXXXXXXXX", LoggingFieldFormat.String);

                    var act = channel.StartActivity("Some event", fields, LoggingLevel.Error);
                    act.LogEvent("Event action");
                    act.StopActivity("Some event");

                    channel.LogMessage("This is a simple sample message", LoggingLevel.Error);

                    channel.LogEvent("Event name", fields, LoggingLevel.Error);

                    session.CloseAndSaveToFileAsync();
                }
            }
        }*/
    }
}

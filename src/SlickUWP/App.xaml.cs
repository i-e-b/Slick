using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using SlickUWP.CrossCutting;

namespace SlickUWP
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    // ReSharper disable once RedundantExtendsListEntry
    sealed partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
            Suspending += OnSuspending;
            UnhandledException += App_UnhandledException;
        }

        private void App_UnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            // define DISABLE_XAML_GENERATED_BREAK_ON_UNHANDLED_EXCEPTION
            // to turn off the default debugger break
            Logging.WriteLogMessage(e?.Exception?.ToString() ?? "Invalid exception in backstop handler");
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            if (Window.Current == null) return;
            if (e == null) return;
            var rootFrame = Window.Current?.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                rootFrame = CreateRootFrame();
                if (rootFrame == null) throw new Exception("Failed to generate root frame");
            }

            if (e.PrelaunchActivated == false)
            {
                ActivateNewMainPage(e, rootFrame);
            }
        }

        private static MainPage ActivateNewMainPage(ILaunchActivatedEventArgs e, Frame rootFrame)
        {
            if (rootFrame == null) throw new Exception("Tried to activate null frame");
            if (rootFrame.Content == null)
            {
                // When the navigation stack isn't restored navigate to the first page,
                // configuring the new page by passing required information as a navigation
                // parameter
                rootFrame.Navigate(typeof(MainPage), e?.Arguments ?? "");
            }

            // Ensure the current window is active
            Window.Current?.Activate();

            return rootFrame.Content as MainPage;
        }

        private Frame CreateRootFrame()
        {
            if (Window.Current == null) return null;

            // Create a Frame to act as the navigation context and navigate to the first page
            var rootFrame = new Frame();

            rootFrame.NavigationFailed += OnNavigationFailed;

            // Place the frame in the current Window
            Window.Current.Content = rootFrame;
            return rootFrame;
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + (e?.SourcePageType?.FullName ?? "<null>"));
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            if (e?.SuspendingOperation == null) return;
            var deferral = e.SuspendingOperation.GetDeferral();
            deferral?.Complete();
        }

        protected override async void OnFileActivated(FileActivatedEventArgs args)
        {
            // TODO: try making a new `Frame` to open another window?
            if (!(Window.Current?.Content is Frame rootFrame)) {
                rootFrame = CreateRootFrame();
                if (rootFrame == null)
                {
                    throw new Exception("Failed to generate root frame");
                }
            }

            if (!(rootFrame.Content is MainPage page))
            {
                page = ActivateNewMainPage(null, rootFrame);
                if (page == null)
                {
                    throw new Exception("Root frame was not of type <MainPage> in `OnFileActivated`");
                }
            }

            await page.LoadActivationFile(args);
        }
    }
}

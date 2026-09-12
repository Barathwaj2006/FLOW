using Xunit;

// Disable parallel test execution within Flow.Windows.Tests to prevent race conditions
// on Windows OS-wide singletons: Clipboard, Foreground Window focus, WASAPI audio, and Win32 hooks.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

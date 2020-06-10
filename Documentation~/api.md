# API

## Tracker API

Performance Tracker are internals for now as their implementation is subject to change. But here are the most importat API the Performance Tracking package is using to access their data.

### Querying Trackers

```CSharp
internal struct EditorPerformanceTracker
{
    public static extern string[] GetAvailableTrackers();
    public static extern bool Exists(string trackerName);
    public static extern void Reset(string trackerName);
    public static extern int GetSampleCount(string trackerName);
    public static extern double GetLastTime(string trackerName);
    public static extern double GetPeakTime(string trackerName);
    public static extern double GetAverageTime(string trackerName);
    public static extern double GetTotalTime(string trackerName);
    public static extern double GetTotalUsage(string trackerName);
    public static extern double GetTimestamp(string trackerName);
    public static extern void LogCallstack(string trackerName);
    public static extern void GetCallstack(string trackerName, Action<string> onCallstackCaptured);

    internal static extern int StartTracker(string trackerName);
    internal static extern void StopTracker(int trackerToken);
}
```

### Using tracker in C# code

You can create a new Performance tracker in C# with a `using` block:

```CSharp
using (new EditorPerformanceTracker("Tracker.PerformNotifications"))
{
    PerformNotifications(now, trackerNames);
}
```

This tracker will then be monitored by the [Performance Tracker Window](performance-tracker-window.html).

## Performance Actions

The Performance Tracking package expose all its performance actions as public API.

```CSharp
public static class PerformanceTrackerActions
{
    public static void StartProfilerRecording(bool editorProfile, bool deepProfile);

    public static void StopProfilerRecording(
        string marker, 
        bool openProfiler = true, 
        Analytics.ActionSource source = Analytics.ActionSource.Scripting);

    public static EditorWindow OpenProfiler(
        string marker = "", 
        Analytics.ActionSource source = Analytics.ActionSource.Scripting);

    public static EditorWindow OpenProfilerData(
        string profileDataPath, 
        string marker = "", 
        Analytics.ActionSource source = Analytics.ActionSource.Scripting);

    public static PerformanceTrackerWindow OpenPerformanceTrackerViewer(
        string marker = "", 
        Analytics.ActionSource source = Analytics.ActionSource.Scripting);

    public static void LogCallstack(
        string marker, 
        Analytics.ActionSource source = Analytics.ActionSource.Scripting);

    public static void GetCallstack(
        string marker, 
        Action<string> handler, 
        bool formatForConsole = false);

    public static void AddNewPerformanceNotification(
        string marker, 
        float threshold = 500, 
        Analytics.ActionSource source = Analytics.ActionSource.Scripting);


    public static void OpenBugReportingTool(
        string marker = "", 
        Analytics.ActionSource source = Analytics.ActionSource.Scripting);
}
```
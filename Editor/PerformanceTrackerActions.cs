using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEditorInternal.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace Unity.PerformanceTracking
{
    internal static class PerformanceTrackerActions
    {
        private const string k_DefaultPerformanceTrackerReportsDirectory = "Assets/EditorTrackerReports";

#if UNITY_2020_1_OR_NEWER
        [WindowAction]
        private static WindowAction StartProfilerRecordingEditorAction()
        {
            var action = WindowAction.CreateWindowMenuItem("StartProfilerRecordingEditor", (window, _action) =>
            {
                // We need to fix this!! See 

                var marker = PerformanceTrackerMonitoringService.GetWindowPaintMarker(window);
                ProfilerHelpers.OpenProfiler(marker, profilerWindow =>
                {
                    ProfilerHelpers.SetRecordingEnabled(profilerWindow, true);
                    ProfilerHelpers.StartProfilerRecording("", true, ProfilerDriver.deepProfiling);
                });
            }, "Window Performance/Start Profiler Recording");
            return action;
        }
        
        [WindowAction]
        private static WindowAction OpenProfilerAction()
        {
            var action = WindowAction.CreateWindowMenuItem("OpenProfilerForWindow", (v, a) => {
                OpenProfiler(PerformanceTrackerMonitoringService.GetWindowPaintMarker(v), Analytics.ActionSource.EditorWindowMenu);
            }, "Window Performance/Open Profiler");
            return action;
        }

        [WindowAction]
        private static WindowAction OpenPerformanceTrackerWindowAction()
        {
            var action = WindowAction.CreateWindowMenuItem("OpenPerformanceTrackerWindow", (v, a) => {
                OpenPerformanceTrackerWindow(v.GetType().Name, Analytics.ActionSource.EditorWindowMenu);
            }, "Window Performance/Open Performance Tracker Window");
            return action;
        }

        [WindowAction]
        private static WindowAction OpenBugReportingToolAction()
        {
            var action = WindowAction.CreateWindowMenuItem("OpenBugReportingTool", (v, a) =>
            {
                OpenBugReportingTool(PerformanceTrackerMonitoringService.GetWindowPaintMarker(v), Analytics.ActionSource.EditorWindowMenu);
            }, "Window Performance/Report Performance Bug");
            return action;
        }
#endif
        public static void OpenProfiler(string marker)
        {
            OpenProfiler(marker, Analytics.ActionSource.Scripting);
        }

        public static void OpenProfiler(string marker = "", Analytics.ActionSource source = Analytics.ActionSource.Scripting)
        {
            Analytics.SendPerformanceActionEvent(new Analytics.PerformanceActionEvent(
                Analytics.ActionType.OpenProfilerOnMarker,
                marker, source));
            ProfilerHelpers.OpenProfiler(marker);
        }

        public static void OpenProfileReport(string profileDataPath, string searchString = "", Analytics.ActionSource source = Analytics.ActionSource.Scripting)
        {
            Analytics.SendPerformanceActionEvent(new Analytics.PerformanceActionEvent(
                Analytics.ActionType.OpenProfilerData,
                searchString, source));
            ProfilerHelpers.OpenProfileReport(profileDataPath, searchString);
        }

        public static void StopProfilerRecordingAndCreateReport(string profileTitle, Analytics.ActionSource source = Analytics.ActionSource.Scripting)
        {
            ProfilerHelpers.StopProfilerRecordingAndCreateReport(profileTitle);

            Analytics.SendPerformanceActionEvent(new Analytics.PerformanceActionEvent(
                ProfilerDriver.deepProfiling ? Analytics.ActionType.DeedProfile : Analytics.ActionType.Profile,
                profileTitle,
                source
                ));
        }

        public static PerformanceTrackerWindow OpenPerformanceTrackerWindow(string marker, Analytics.ActionSource source = Analytics.ActionSource.Scripting)
        {
            Analytics.SendPerformanceActionEvent(new Analytics.PerformanceActionEvent(
                Analytics.ActionType.OpenPerformanceTrackerViewer,
                marker, source));
            var window = EditorWindow.GetWindow<PerformanceTrackerWindow>();
            window.Show();
            window.SetSearchString("");
            window.SetSearchString(marker);
            return window;
        }

        public static void LogCallstack(string marker, Analytics.ActionSource source = Analytics.ActionSource.Scripting)
        {
            Analytics.SendPerformanceActionEvent(new Analytics.PerformanceActionEvent(
                Analytics.ActionType.LogCallstack,
                marker, source));
            GetCallstack(marker, callstack => Debug.Log(callstack), true);
        }

        public static void GetCallstack(string marker, Action<string> handler, bool formatForConsole = false)
        {
            EditorPerformanceTracker.GetCallstack(marker, (callstack =>
            {
                handler(formatForConsole ? FormatCallstackForConsole(callstack) : callstack);
            }));
        }

        public static void AddNewPerformanceNotification(string marker, float threshold = 500, Analytics.ActionSource source = Analytics.ActionSource.Scripting)
        {
            Analytics.SendPerformanceActionEvent(new Analytics.PerformanceActionEvent(
                Analytics.ActionType.AddNotification,
                marker, source));

            var collection = PerformanceTrackerSettings.LoadPerformanceNotificationCollection();
            PerformanceTrackerSettings.AddPerformanceNotification(collection, new PerformanceNotification()
            {
                enabled = true,
                name = marker,
                threshold = threshold
            });
            PerformanceTrackerSettings.SavePerformanceNotificationCollection(collection);
            SettingsService.OpenUserPreferences(PerformanceTrackerSettings.settingsKey);
            SettingsService.NotifySettingsProviderChanged();
        }

#if UNITY_2020_1_OR_NEWER
        public static void OpenBugReportingTool(string marker, Analytics.ActionSource source = Analytics.ActionSource.Scripting)
        {
            Analytics.SendPerformanceActionEvent(new Analytics.PerformanceActionEvent(
                Analytics.ActionType.LogPerformanceBug,
                marker, source));
            var options = new TrackerReportOptions();
            options.showSamples = true;
            options.showPeak = true;
            options.showAvg = true;
            options.showTotal = true;
            options.sort = true;
            options.sortBy = ColumnId.PeakTime;
            options.sortAsc = false;
            var report = PerformanceTrackerReportUtils.GetAllTrackersReport(options);

            var reportFilePath = SavePerformanceTrackerReport(report);

            var descriptionBuilder = new StringBuilder();
            descriptionBuilder.AppendLine("1. What is slow (complete performance tracker report attached)");
            if (!string.IsNullOrEmpty(marker))
            {
                options.trackerFilter = $"^{marker}$";
                var markerReport = PerformanceTrackerReportUtils.GetTrackersReport(options);
                descriptionBuilder.AppendLine();
                descriptionBuilder.AppendLine(markerReport);
            }
            descriptionBuilder.AppendLine();
            descriptionBuilder.Append("2. How we can reproduce it using the example you attached");

            var myAssets = new[]
            {
                GetFullPath(reportFilePath)
            };
            BugReporterUtils.OpenBugReporter(descriptionBuilder.ToString(), myAssets);
        }
#endif

        internal static string FormatCallstackForConsole(string callstack)
        {
            // The callstack we are receiving is not formatted with hyperlinks, nor does
            // it have the format that the console expects to display in the active text box.
            var formattedCallstack = Regex.Replace(callstack, "\\[(\\S+?):(\\d+)\\]", "[<a href=\"$1\" line=\"$2\">$1:$2</a>]");
            return formattedCallstack;
        }

        private static string GetFullPath(string projectRelativePath)
        {
            var fileInfo = new FileInfo(projectRelativePath);
            return fileInfo.FullName;
        }

        private static string SavePerformanceTrackerReport(string report)
        {
            if (!System.IO.Directory.Exists(k_DefaultPerformanceTrackerReportsDirectory))
                System.IO.Directory.CreateDirectory(k_DefaultPerformanceTrackerReportsDirectory);

            var timeId = EditorApplication.timeSinceStartup.ToString(CultureInfo.InvariantCulture).Replace(".", "");
            var reportsFilePath = $"{k_DefaultPerformanceTrackerReportsDirectory}/{timeId}.perf-trackers-report.txt".Replace("\\", "/");
            File.WriteAllText(reportsFilePath, report);

            return reportsFilePath;
        }
    }
}
// #define PERFORMANCE_TRACKING_ANALYTICS_LOGGING
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Analytics;

namespace Unity.PerformanceTracking
{
    public static class Analytics
    {
        [Serializable]
        internal class PerformanceLogEvent
        {
            private DateTime m_StartTime;
            internal long elapsedTimeMs => (long)(DateTime.Now - m_StartTime).TotalMilliseconds;
            public long duration;
            public TrackersReport report;
            public PerformanceLogEvent()
            {
                m_StartTime = DateTime.Now;
            }

            public void Done()
            {
                if (duration == 0)
                    duration = elapsedTimeMs;
            }
        }

        public enum PerformanceTrackingEventType 
        {
            PreferenceChanges,
            Startup,
            Shutdown
        }

        [Serializable]
        internal class PerformanceTrackingEvent
        {
            public float monitoringUpdateSpeed;
            public bool monitoringNeeded;
            public bool notificationEnabled;
            public PerformanceNotification[] notifications;
            public SpikeHighlightStrategy spikeHighlightStrategy;
            public bool spikeHighlightEnabled;
            public float spikeWarningThreshold;
            public float spikeCriticalThreshold;
            public float spikeDuration;

            public PerformanceTrackingEventType trackingEventType;

            public PerformanceTrackingEvent()
            {
                monitoringUpdateSpeed = PerformanceTrackerSettings.monitoringUpdateSpeed;
                monitoringNeeded = PerformanceTrackerSettings.monitoringNeeded;
                notifications = PerformanceTrackerSettings.notifications.items;
                notificationEnabled = PerformanceTrackerSettings.notificationEnabled;
                spikeHighlightStrategy = PerformanceTrackerSettings.spikeHighlightStrategy;
                spikeHighlightEnabled = PerformanceTrackerSettings.spikeHighlightEnabled;
                spikeWarningThreshold = PerformanceTrackerSettings.spikeWarningThreshold;
                spikeCriticalThreshold = PerformanceTrackerSettings.spikeCriticalThreshold;
                spikeDuration = PerformanceTrackerSettings.spikeDuration;
            }
        }

        public enum WindowUsageType
        {
            Startup,
            Sort,
            ChangeColumnLayout,
            ChangeMonitoringSpeed,
            ChangeSortBy,
            Shutdown,
            AddPin,
            RemovePin,
            OpenPreferences,
            ResetSampleCount,
            SetSearchFieldAction
        }

        [Serializable]
        internal class PerformanceWindowUsageEvent
        {
            public string[] pinMarkers;
            public string filter;
            public float updateSpeed;
            public int columns;
            public ColumnId sortBy;
            public bool sortAsc;
            public WindowUsageType usageType;
            public string markerName;
        }

        public enum ActionSource
        {
            PerformanceTrackerWindow,
            EditorWindowMenu,
            MainMenu,
            Scripting,
            OpenAsset,
            PreferencePage
        }

        public enum ActionType
        {
            Profile,
            DeedProfile,
            LogCallstack,
            AddNotification,
            LogPerformanceBug,
            OpenProfilerOnMarker,
            OpenProfilerData,
            OpenPerformanceTrackerViewer
        }

        [Serializable]
        internal class PerformanceActionEvent
        {
            public ActionType actionType;
            public string trackerName;
            public ActionSource actionSource;

            public PerformanceActionEvent(ActionType actionType, string trackerName = null, ActionSource source = ActionSource.Scripting)
            {
                this.actionType = actionType;
                this.trackerName = trackerName;
                actionSource = source;
            }
        }

        internal static string Version;
        private static bool s_Registered;

        static Analytics()
        {
            Version = Utils.GetPerformanceTrackingVersion();

            EditorApplication.quitting += Quit;
        }

        enum EventName
        {
            PerformanceWindowUsageEvent,
            PerformanceLogEvent,
            PerformanceActionEvent,
            PerformanceTrackingEvent
        }

        internal static void SendPerformanceWindowUsageEvent(PerformanceWindowUsageEvent evt)
        {
            Send(EventName.PerformanceWindowUsageEvent, evt);
        }

        internal static void SendPerformanceLogEvent()
        {
            PerformanceLogEvent evt = new PerformanceLogEvent();
            var options = new TrackerReportOptions();
            options.showSamples = true;
            options.showPeak = true;
            options.showAvg = true;
            options.showTotal = true;
            options.sort = true;
            options.sortBy = ColumnId.AvgTime;
            options.sortAsc = false;
            evt.report = PerformanceTrackerReportUtils.GenerateTrackerReport(options);
            evt.Done();

            Send(EventName.PerformanceLogEvent, evt);
        }

        internal static void SendPerformanceActionEvent(PerformanceActionEvent evt)
        {
            Send(EventName.PerformanceActionEvent, evt);
        }

        internal static void SendPerformanceTrackingEvent(PerformanceTrackingEventType eventType)
        {
            Send(EventName.PerformanceTrackingEvent, new PerformanceTrackingEvent() { trackingEventType = eventType });
        }

        private static void Quit()
        {
            SendPerformanceLogEvent();
            SendPerformanceTrackingEvent(PerformanceTrackingEventType.Shutdown);
        }

        private static bool RegisterEvents()
        {
            if (UnityEditorInternal.InternalEditorUtility.inBatchMode)
            {
                return false;
            }

            if (!EditorAnalytics.enabled)
            {
                Console.WriteLine("[Performance Tracking] Editor analytics are disabled");
                return false;
            }

            if (s_Registered)
            {
                return true;
            }

            var allNames = Enum.GetNames(typeof(EventName));
            if (allNames.Any(eventName => !RegisterEvent(eventName)))
            {
                return false;
            }

            s_Registered = true;
            return true;
        }

        private static bool RegisterEvent(string eventName)
        {
            const string vendorKey = "unity.scene-template";
            var result = EditorAnalytics.RegisterEventWithLimit(eventName, 100, 1000, vendorKey);
            switch (result)
            {
                case AnalyticsResult.Ok:
                    {
#if PERFORMANCE_TRACKING_ANALYTICS_LOGGING
                        Debug.Log($"SceneTemplate: Registered event: {eventName}");
#endif
                        return true;
                    }
                case AnalyticsResult.TooManyRequests:
                    // this is fine - event registration survives domain reload (native)
                    return true;
                default:
                    {
                        Console.WriteLine($"[ST] Failed to register analytics event '{eventName}'. Result: '{result}'");
                        return false;
                    }
            }
        }

        private static void Send(EventName eventName, object eventData)
        {
#if PERFORMANCE_TRACKING_ANALYTICS_LOGGING
#else
            if (Utils.IsDeveloperMode())
                return;
#endif

            if (!RegisterEvents())
            {
#if PERFORMANCE_TRACKING_ANALYTICS_LOGGING
                Console.WriteLine($"[ST] Analytics disabled: event='{eventName}', time='{DateTime.Now:HH:mm:ss}', payload={EditorJsonUtility.ToJson(eventData, true)}");
#endif
                return;
            }
            try
            {
                var result = EditorAnalytics.SendEventWithLimit(eventName.ToString(), eventData);
                if (result == AnalyticsResult.Ok)
                {
#if PERFORMANCE_TRACKING_ANALYTICS_LOGGING
                    Console.WriteLine($"[ST] Event='{eventName}', time='{DateTime.Now:HH:mm:ss}', payload={EditorJsonUtility.ToJson(eventData, true)}");
#endif
                }
                else
                {
                    Console.WriteLine($"[ST] Failed to send event {eventName}. Result: {result}");
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

#if PERFORMANCE_TRACKING_ANALYTICS_LOGGING
        [MenuItem("Tools/Single PerformanceLog Events", false, 180005)]
        static void SinglePerformanceLogEvents()
        {
            SendPerformanceLogEvent();
        }

        [MenuItem("Tools/Spam PerformanceLog Events", false, 180005)]
        static void SpamPerformanceLogEvents()
        {
            for(var i = 0; i < 25; ++i)
            {
                SendPerformanceLogEvent();
                Debug.Log($"Send Log event: {i}");
                Thread.Sleep(500);
            }
        }

        static T RandomValue<T>(T[] values)
        {
            var index = UnityEngine.Random.Range(0, values.Length - 1);
            return values[index];
        }

        static T RandomEnum<T>() where T : struct, IConvertible
        {
            var values = Enum.GetValues(typeof(T));
            var indexOfLastElement = values.Length - 1;
            var index = UnityEngine.Random.Range(0, indexOfLastElement);
            return (T)values.GetValue(index);
        }

        static bool RandomBool()
        {
            var index = UnityEngine.Random.Range(0, 1);
            return index == 0;
        }

        static int[] kShowColumns = new []
        {
            1,
            16,
            15,
            11
        };

        static string[] kSearchFields = new string[]
        {
            ".Paint", ".OnInspectorGUI", null, "Editor.", "Scene"
        };

        static string[][] kPins = new[]
        {
            new [] { "WebView.Tick" },
            new [] { "WebView.Tick", "UnityConnect.Tick" },
            new [] { "WebView.Tick", "UnityConnect.Tick", "WinEditorMain" },
            new string [0]
        };

        [MenuItem("Tools/Spam Performance WindowUsage Events", false, 180005)]
        static void SpamPerformanceWindowUsageEvents()
        {
            var trackers = PtModel.BuildTrackerList(new PtInfo[] { }, ColumnId.AvgTime, true, true);

            for (var i = 0; i < 25; ++i)
            {
                var evt = new PerformanceWindowUsageEvent()
                {
                    columns = RandomValue(kShowColumns),
                    sortBy = RandomEnum<ColumnId>(),
                    sortAsc = RandomBool(),
                    filter = RandomValue(kSearchFields),
                    pinMarkers = RandomValue(kPins),
                    updateSpeed = (float)RandomValue(PtModel.RefreshRates).rate,
                    usageType = RandomEnum<WindowUsageType>(),
                    markerName = RandomValue(trackers).name
                };

                SendPerformanceWindowUsageEvent(evt);
                Debug.Log($"Send windowUsage event: {i}");
                Thread.Sleep(500);
            }
        }

        [MenuItem("Tools/Spam PerformanceAction Events", false, 180005)]
        static void SpamPerformanceActionEvents()
        {
            var trackers = PtModel.BuildTrackerList(new PtInfo[] { }, ColumnId.AvgTime, true, true);
            for (var i = 0; i < 25; ++i)
            {
                var evt = new PerformanceActionEvent(
                    RandomEnum<ActionType>(),
                    RandomValue(trackers).name,
                    RandomEnum<ActionSource>()
                    );

                SendPerformanceActionEvent(evt);
                Debug.Log($"Send action event: {i}");
                Thread.Sleep(500);
            }
        }

        [MenuItem("Tools/Spam PerformanceTracking Events", false, 180005)]
        static void SpamPerformanceTrackingEvents()
        {
            for (var i = 0; i < 25; ++i)
            {
                SendPerformanceTrackingEvent(RandomEnum<PerformanceTrackingEventType>());
                Debug.Log($"Send tracking event: {i}");
                Thread.Sleep(500);
            }
        }
#endif
    }

}
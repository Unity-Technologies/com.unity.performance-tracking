// #define PERFORMANCE_TRACKING_DEBUG
using System;
using System.Linq;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace Unity.PerformanceTracking
{
    enum SpikeHighlightStrategy
    {
        LastTime,
        AvgTime
    }

    static class PerformanceTrackerSettings
    {
        private const string k_KeyPrefix = "perftracking";
        public const string settingsKey = "Preferences/Analysis/Performance Tracking";

        public static event Action settingsChanged;
        public static float monitoringUpdateSpeed { get; private set; }
        public static bool monitoringNeeded => notificationEnabled || spikeHighlightEnabled;

        public static bool notificationEnabled { get; private set; }
        public static PerformanceNotificationCollection notifications { get; private set; }

        public static SpikeHighlightStrategy spikeHighlightStrategy = SpikeHighlightStrategy.AvgTime;
        public static bool spikeHighlightEnabled { get; private set; }
        public static bool inspectorSpikeHighlightEnabled { get; private set; }
        public static float spikeWarningThreshold { get; private set; }
        public static float spikeCriticalThreshold { get; private set; }
        public static float spikeDuration { get; private set; }

        public static string[] notificationNames = new string[0];
        public static float[] notificationThresholds = new float[0];

        private static string s_NewNotifName = "";
        private static float s_NewNotifThreshold = 500f;

        private const float k_MinThreshold = 1f;
        private const float k_MaxThreshold = 5000;

        static class Defaults
        {
            public static bool spikeHighlightEnabled = false;
            public static bool notificationEnabled = false;
            public static bool inspectorSpikeHighlightEnabled = false;
            public static float monitoringUpdateSpeed = 5;
            public static float spikeWarningThreshold = 0.050f;
            public static float spikeCriticalThreshold = 0.10f;

            public static SpikeHighlightStrategy spikeHighlightStrategy = SpikeHighlightStrategy.AvgTime;
            public static float spikeDuration = 0.0f;

            public static PerformanceNotificationCollection GetDefaultNotifications()
            {
                var collection = new PerformanceNotificationCollection();
                collection.items = new[]
                {
                    new PerformanceNotification {enabled = true, name = "Application.Tick", threshold = 500},
                    new PerformanceNotification {enabled = true, name = "Application.Reload", threshold = 500},
                };
                return collection;
            }
        }

        static PerformanceTrackerSettings()
        {
            Load();
        }

        public static PerformanceNotificationCollection LoadPerformanceNotificationCollection()
        {
            var notificationsJson = EditorPrefs.GetString(GetKeyName(nameof(notifications)), string.Empty);
            PerformanceNotificationCollection collection;
            if (!string.IsNullOrEmpty(notificationsJson))
                collection = JsonUtility.FromJson<PerformanceNotificationCollection>(notificationsJson);
            else
            {
                collection = Defaults.GetDefaultNotifications();
            }

            return collection;
        }

        public static void SavePerformanceNotificationCollection(PerformanceNotificationCollection collection)
        {
            EditorPrefs.SetString(GetKeyName(nameof(notifications)), JsonUtility.ToJson(collection));
        }

        public static void AddPerformanceNotification(PerformanceNotificationCollection collection, PerformanceNotification notification)
        {
            if (Array.Find(collection.items, i => i.name == notification.name) != null)
                return;

            var newList = new List<PerformanceNotification>(collection.items)
            {
                notification
            };
            collection.items = newList.ToArray();
        }

        private static void BuildCaches()
        {
            notificationNames = notifications.items.Where(n => n.enabled).Select(n => n.name).ToArray();
            notificationThresholds = notifications.items.Where(n => n.enabled).Select(n => n.threshold / 1000f).ToArray();
        }

        private static string GetKeyName(string name)
        {
            return $"{k_KeyPrefix}.{name}";
        }

        private static void Load()
        {
            spikeHighlightEnabled = EditorPrefs.GetBool(GetKeyName(nameof(spikeHighlightEnabled)), Defaults.spikeHighlightEnabled);
            notificationEnabled = EditorPrefs.GetBool(GetKeyName(nameof(notificationEnabled)), Defaults.notificationEnabled);
            inspectorSpikeHighlightEnabled = EditorPrefs.GetBool(GetKeyName(nameof(inspectorSpikeHighlightEnabled)), Defaults.inspectorSpikeHighlightEnabled);
            monitoringUpdateSpeed = EditorPrefs.GetFloat(GetKeyName(nameof(monitoringUpdateSpeed)), Defaults.monitoringUpdateSpeed);
            notifications = LoadPerformanceNotificationCollection();
            spikeWarningThreshold = EditorPrefs.GetFloat(GetKeyName(nameof(spikeWarningThreshold)), Defaults.spikeWarningThreshold);
            spikeCriticalThreshold = EditorPrefs.GetFloat(GetKeyName(nameof(spikeCriticalThreshold)), Defaults.spikeCriticalThreshold);
            
            spikeHighlightStrategy = Defaults.spikeHighlightStrategy;
            spikeDuration = Defaults.spikeDuration;
#if PERFORMANCE_TRACKING_DEBUG
            spikeDuration = EditorPrefs.GetFloat(GetKeyName(nameof(spikeDuration)), Defaults.spikeDuration);
            Enum.TryParse(EditorPrefs.GetString(GetKeyName(nameof(spikeHighlightStrategy)), Defaults.spikeHighlightStrategy.ToString()), out spikeHighlightStrategy);
#endif

            BuildCaches();
        }

        private static void Save()
        {
            EditorPrefs.SetBool(GetKeyName(nameof(spikeHighlightEnabled)), spikeHighlightEnabled);
            EditorPrefs.SetBool(GetKeyName(nameof(notificationEnabled)), notificationEnabled);
            EditorPrefs.SetBool(GetKeyName(nameof(inspectorSpikeHighlightEnabled)), inspectorSpikeHighlightEnabled);
            EditorPrefs.SetFloat(GetKeyName(nameof(monitoringUpdateSpeed)), monitoringUpdateSpeed);
            EditorPrefs.SetFloat(GetKeyName(nameof(spikeWarningThreshold)), spikeWarningThreshold);
            EditorPrefs.SetFloat(GetKeyName(nameof(spikeCriticalThreshold)), spikeCriticalThreshold);
            
            SavePerformanceNotificationCollection(notifications);

#if PERFORMANCE_TRACKING_DEBUG
            EditorPrefs.SetFloat(GetKeyName(nameof(spikeDuration)), spikeDuration);
            EditorPrefs.SetString(GetKeyName(nameof(spikeHighlightStrategy)), spikeHighlightStrategy.ToString());
#endif

            BuildCaches();

            settingsChanged?.Invoke();
        }

        private static void ResetSettings()
        {
            spikeHighlightEnabled = Defaults.spikeHighlightEnabled;
            notificationEnabled = Defaults.notificationEnabled;
            inspectorSpikeHighlightEnabled = Defaults.inspectorSpikeHighlightEnabled;
            monitoringUpdateSpeed = Defaults.monitoringUpdateSpeed;
            notifications = Defaults.GetDefaultNotifications();
            spikeWarningThreshold = Defaults.spikeWarningThreshold;
            spikeCriticalThreshold = Defaults.spikeCriticalThreshold;

            spikeHighlightStrategy = Defaults.spikeHighlightStrategy;
            spikeDuration = Defaults.spikeDuration;
        }

        [UsedImplicitly, SettingsProvider]
        private static SettingsProvider CreateSettings()
        {
            return new SettingsProvider(settingsKey, SettingsScope.User)
            {
                keywords = SettingsProvider.GetSearchKeywordsFromGUIContentProperties<Contents>(),
                activateHandler = (title, rootElement) => Load(),
                guiHandler = searchContext =>
                {
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Space(10);
                        GUILayout.BeginVertical(GUILayout.Width(515));
                        {
                            GUILayout.Space(10);
                            var oldWidth = EditorGUIUtility.labelWidth;
                            EditorGUIUtility.labelWidth = 250;
                            using (var _ = new EditorGUI.ChangeCheckScope())
                            {
                                GUILayout.BeginHorizontal();
                                {
                                    monitoringUpdateSpeed = DrawSlider(Contents.updateSpeedContent.text, monitoringUpdateSpeed, 0.1f, 10);
                                }
                                GUILayout.EndHorizontal();
                                GUILayout.Space(30);

                                notificationEnabled = EditorGUILayout.Toggle(Contents.enablePerformanceTrackingNotificationContent, notificationEnabled);
                                using (new EditorGUI.DisabledScope(!notificationEnabled))
                                {
                                    DrawNotifications();
                                    DrawNewNotificationFields();
                                }

                                GUILayout.Space(30);
                                spikeHighlightEnabled = EditorGUILayout.Toggle(Contents.enableSpikeHighlighContent, spikeHighlightEnabled);
                                using (new EditorGUI.DisabledScope(!spikeHighlightEnabled))
                                {
                                    DrawSpikeHighlightControls();
                                }

                                if (_.changed)
                                    Save();
                            }

                            GUILayout.Space(10);
                            if (GUILayout.Button(Contents.resetSettings, GUILayout.Width(100)))
                            {
                                ResetSettings();
                            }

                            EditorGUIUtility.labelWidth = oldWidth;
                        }
                        GUILayout.EndVertical();
                    }
                    GUILayout.EndHorizontal();
                }
            };
        }



        private static void DrawNewNotificationFields()
        {
            EditorGUILayout.LabelField("Add New", EditorStyles.largeLabel);
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            {
                GUILayout.Space(41);
                s_NewNotifThreshold = EditorGUILayout.Slider(s_NewNotifThreshold, k_MinThreshold, k_MaxThreshold, Styles.sliderLayoutOption);
                s_NewNotifName = GUILayout.TextField(s_NewNotifName, 64, Styles.nameLayoutOption);
                if (GUILayout.Button(Contents.notifAddNewContent, GUILayout.Width(100)) && !string.IsNullOrEmpty(s_NewNotifName))
                {
                    AddPerformanceNotification(notifications, new PerformanceNotification { enabled = true, name = s_NewNotifName, threshold = s_NewNotifThreshold });
                    s_NewNotifName = "";
                    Save();
                }
            }
            GUILayout.EndHorizontal();
        }

        private static void DrawSpikeHighlightControls()
        {
            inspectorSpikeHighlightEnabled = EditorGUILayout.Toggle(Contents.enableInspectorSpikeHighlighContent, inspectorSpikeHighlightEnabled);
#if PERFORMANCE_TRACKING_DEBUG
            spikeHighlightStrategy = (SpikeHighlightStrategy)EditorGUILayout.EnumPopup(new GUIContent("Spike triggers on"), spikeHighlightStrategy);
            spikeDuration = DrawSlider("Spike Duration (seconds)", spikeDuration, 0, 10);
#endif
            spikeWarningThreshold = DrawSlider($"<color={PtStyles.warningHexCode}>Warning threshold (milliseconds)</color>", spikeWarningThreshold * 1000, 1, 50) / 1000;
            spikeCriticalThreshold = DrawSlider($"<color={PtStyles.criticalHexCode}>Critical threshold (milliseconds)</color>", spikeCriticalThreshold * 1000, spikeWarningThreshold * 1000, 100) / 1000;
        }

        private static void DrawNotifications()
        {
            EditorGUILayout.LabelField("Notifications (threshold in milliseconds)", EditorStyles.largeLabel);
            GUILayout.Space(4);
            foreach (var p in notifications.items)
            {
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Space(20);

                    p.enabled = GUILayout.Toggle(p.enabled, Contents.notifEnabledContent, GUILayout.Width(20));

                    using (new EditorGUI.DisabledGroupScope(!p.enabled))
                    {
                        p.threshold = EditorGUILayout.Slider(p.threshold, k_MinThreshold, k_MaxThreshold, Styles.sliderLayoutOption);
                        p.name = GUILayout.TextField(p.name, 64, Styles.nameLayoutOption);
                        if (GUILayout.Button(Contents.notifDeleteContent, GUILayout.Width(100)))
                        {
                            notifications.items = notifications.items.Where(n => n != p).ToArray();
                            Save();
                            GUIUtility.ExitGUI();
                        }
                    }
                }
                GUILayout.EndHorizontal();
            }
        }

        private static float DrawSlider(string label, float value, float leftValue, float rightValue)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, Styles.rtfLabel, GUILayout.Width(EditorGUIUtility.labelWidth));
            GUILayout.Space(20);
            GUILayout.FlexibleSpace();
            var newValue = EditorGUILayout.Slider(value, leftValue, rightValue);
            GUILayout.EndHorizontal();
            return newValue;
        }

        static class Styles
        {
            public static GUIStyle rtfLabel = new GUIStyle(EditorStyles.label)
            {
                richText = true
            };

            public static readonly GUILayoutOption sliderLayoutOption = GUILayout.Width(140);
            public static readonly GUILayoutOption nameLayoutOption = GUILayout.Width(250);
        }

        class Contents
        {
            public static GUIContent resetSettings = new GUIContent("Reset Settings", "Reset all performance tracking settings to defaults.");
            public static GUIContent updateSpeedContent = new GUIContent("Monitoring Update Speed (seconds)");
            public static GUIContent notifEnabledContent = new GUIContent("", "Enable or disable this performance tracking notification.");
            public static GUIContent notifAddNewContent = new GUIContent("Add", "Add a new notifications");
            public static GUIContent notifDeleteContent = new GUIContent("Remove", "Remove this notification");
            public static GUIContent enablePerformanceTrackingNotificationContent = new GUIContent(
                "Enable performance notifications",
                "If performance tracking notifications are enabled, the system will log in the console trackers that exceed a given threshold.");
            public static GUIContent enableSpikeHighlighContent = new GUIContent(
                "Enable spike window highlight",
                "Window border will be highlighted to show when a repaint caused an editor spike.");
            public static GUIContent enableInspectorSpikeHighlighContent = new GUIContent(
                "Enable inspector components highlight",
                "Inspector component will be highlighted to show when a repaint caused an editor spike.");
        }
    }
}
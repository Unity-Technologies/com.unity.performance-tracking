using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.PerformanceTracking
{
    [Serializable]
    class PerformanceNotification
    {
        public string name;
        public bool enabled;
        public float threshold;
    }

    [Serializable]
    class PerformanceNotificationCollection
    {
        public PerformanceNotificationCollection()
        {
            items = new PerformanceNotification[0];
        }

        public PerformanceNotification[] items;
    }

    class WindowBorderState
    {
        public WindowBorderState(VisualElement element)
        {
            bottomColor = element.style.borderBottomColor;
            topColor = element.style.borderTopColor;
            leftColor = element.style.borderLeftColor;
            rightColor = element.style.borderRightColor;
        }

        public static void ApplyToStyle(VisualElement element, WindowBorderState state)
        {
            element.style.borderBottomColor = state.bottomColor;
            element.style.borderTopColor = state.topColor;
            element.style.borderLeftColor = state.leftColor;
            element.style.borderRightColor = state.rightColor;
        }

        public static void ApplyToStyle(VisualElement element, Color color)
        {
            element.style.borderBottomColor = color;
            element.style.borderTopColor = color;
            element.style.borderLeftColor = color;
            element.style.borderRightColor = color;
        }

        public StyleColor bottomColor;
        public StyleColor topColor;
        public StyleColor rightColor;
        public StyleColor leftColor;
    }

    class SpikeWindowInfo
    {
        public SpikeWindowInfo(EditorWindow window, string trackerName, bool isInspectorElement = false, VisualElement spikeOverlayParent = null)
        {
            this.window = window;
            this.trackerName = trackerName;
            this.isInspectorElement = isInspectorElement;
            spikeOverlayExplicitParent = spikeOverlayParent;
            isInspectorWindow = window.GetType().Name == "InspectorWindow";
        }

        public double lastAvgTime;
        public double lastPeakTime;
        public double lastTime;
        public int lastSampleCount;
        public double lastSpikeTime;

        public VisualElement spikeOverlay;
        public bool paintTriggeredByStateChanged;
        public string trackerName;
        public EditorWindow window;
        public bool isInspectorWindow;

        public bool isInspectorElement;
        public bool inUse;

        public VisualElement spikeOverlayParent
        {
            get
            {
                if (spikeOverlayExplicitParent != null)
                    return spikeOverlayExplicitParent;

                if (!isInspectorElement && window && window != null && window.rootVisualElement?.parent != null)
                {
                    return window.rootVisualElement.parent;
                }

                return null;
            }
        }

        public bool isSpiking => spikeOverlay != null;
        public bool supportsFading => !isInspectorElement;

        internal VisualElement spikeOverlayExplicitParent;
    }

    [UsedImplicitly, InitializeOnLoad]
    static class PerformanceTrackerMonitoringService
    {
        private static double s_NextCheck = 0;
        private static double s_NextCacheCleanup = 0;
        private const int k_SpikeBorderWidth = 2;
        private static Dictionary<int, double> s_NotifChecks = new Dictionary<int, double>();
        private static Dictionary<string, SpikeWindowInfo> s_SpikeWindowInfos = new Dictionary<string, SpikeWindowInfo>();
        private static Dictionary<string, SpikeWindowInfo> s_InspectorWindowInfos = new Dictionary<string, SpikeWindowInfo>();

        static PerformanceTrackerMonitoringService()
        {
            EditorApplication.update -= MonitorTrackers;
            EditorApplication.update += MonitorTrackers;

            PerformanceTrackerSettings.settingsChanged -= PreferencesChanged;
            PerformanceTrackerSettings.settingsChanged += PreferencesChanged;

            Analytics.SendPerformanceTrackingEvent(Analytics.PerformanceTrackingEventType.Startup);
        }

        private static void PreferencesChanged()
        {
            if (!PerformanceTrackerSettings.spikeHighlightEnabled)
            {
                foreach (var info in s_SpikeWindowInfos.Values.ToArray())
                {
                    RemoveSpikeOverlay(info);
                }
            }

            if (!PerformanceTrackerSettings.spikeHighlightEnabled || !PerformanceTrackerSettings.inspectorSpikeHighlightEnabled)
            {
                foreach (var info in s_InspectorWindowInfos.Values.ToArray())
                {
                    RemoveSpikeOverlay(info);
                }
            }

            Analytics.SendPerformanceTrackingEvent(Analytics.PerformanceTrackingEventType.PreferenceChanges);
        }

        private static void MonitorTrackers()
        {
            if (!PerformanceTrackerSettings.monitoringNeeded)
                return;

            var now = EditorApplication.timeSinceStartup;
            if (now < s_NextCheck)
                return;

            var trackerNames = EditorPerformanceTracker.GetAvailableTrackers();
            using (new PerformanceTracker("Tracker.PerformNotifications"))
            {
                PerformNotifications(now, trackerNames);
            }

            using (new PerformanceTracker("Tracker.PerformSpikeWindowHighlight"))
            {
                PerformSpikeWindowHighlight(now, trackerNames);
            }
            s_NextCheck = now + PerformanceTrackerSettings.monitoringUpdateSpeed;
        }

        static void PerformNotifications(double now, string[] trackerNames)
        {
            if (!PerformanceTrackerSettings.notificationEnabled)
                return;

            if (now > s_NextCacheCleanup)
            {
                s_NotifChecks.Clear();
                s_NextCacheCleanup = now + 60;
            }

            var notificationNames = PerformanceTrackerSettings.notificationNames;
            var thresholds = PerformanceTrackerSettings.notificationThresholds;
            for (int i = 0; i < notificationNames.Length; ++i)
            {
                var notifName = notificationNames[i];
                foreach (var trackerName in trackerNames)
                {
                    if (!trackerName.Contains(notifName))
                        continue;

                    var checkKey = notifName.GetHashCode();
                    var avgTime = EditorPerformanceTracker.GetAverageTime(trackerName);
                    if (!s_NotifChecks.TryGetValue(checkKey, out var threshold))
                        threshold = thresholds[i];
                    if (avgTime > threshold)
                    {
                        s_NotifChecks[checkKey] = avgTime;
                        var avgString = PtModel.ToEngineeringNotation(avgTime);
                        var thresholdString = PtModel.ToEngineeringNotation(thresholds[i]);
                        Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, $"<b>{trackerName}</b> is slower than expected ({avgString}s > {thresholdString}s)");
                        EditorPerformanceTracker.LogCallstack(trackerName);
                    }
                }
            }
        }

        internal static string GetWindowPaintMarker(EditorWindow window)
        {
            var windowType = window.GetType().Name;
            return $"{windowType}.Paint";
        }

        static void PerformSpikeWindowHighlight(double now, string[] trackerNames)
        {
            if (!PerformanceTrackerSettings.spikeHighlightEnabled)
                return;

            var allEditorWindows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            foreach (var window in allEditorWindows)
            {
                var windowMarker = GetWindowPaintMarker(window);
                if (EditorPerformanceTracker.Exists(windowMarker))
                {
                    if (!s_SpikeWindowInfos.TryGetValue(windowMarker, out var spikeInfo))
                    {
                        spikeInfo = new SpikeWindowInfo(window, windowMarker);
                        s_SpikeWindowInfos.Add(windowMarker, spikeInfo);
                    }
                    UpdateSpikeWindow(now, spikeInfo);

                    if (spikeInfo.isInspectorWindow && PerformanceTrackerSettings.inspectorSpikeHighlightEnabled)
                    {
                        using (new PerformanceTracker("Tracker.UpdateInspectors"))
                        {
                            UpdateInspectors(now, spikeInfo);
                        }
                    }
                }
                else if (s_SpikeWindowInfos.TryGetValue(windowMarker, out var spikeInfo))
                {
                    RemoveSpikeOverlay(spikeInfo);
                }
            }

            // Window is hidden or closed: clean up all spikeOverlay
            var infos = s_SpikeWindowInfos.Values.ToArray();
            foreach (var info in infos)
            {
                if (!info.window)
                {
                    RemoveSpikeOverlay(info);
                }
            }
        }

        static void UpdateInspectors(double now, SpikeWindowInfo inspectorInfo)
        {
            var editorsList = inspectorInfo.window.rootVisualElement.Q(null, "unity-inspector-editors-list");
            if (editorsList != null)
            {
                foreach (var editorElement in editorsList.Children())
                {
                    var name = editorElement.name;
                    var markerName = $"Editor.{name}.OnInspectorGUI";
                    s_InspectorWindowInfos.TryGetValue(markerName, out var componentInfo);
                    if (!EditorPerformanceTracker.Exists(markerName))
                    {
                        if (componentInfo != null)
                            componentInfo.inUse = false;
                        continue;
                    }

                    if (componentInfo == null)
                    {
                        componentInfo = new SpikeWindowInfo(inspectorInfo.window, markerName, true, editorElement);
                        s_InspectorWindowInfos.Add(markerName, componentInfo);
                    }
                    else if (editorElement != componentInfo.spikeOverlayParent)
                    {
                        InvalidateParent(componentInfo, editorElement);
                    }

                    componentInfo.inUse = true;
                    UpdateSpikeWindow(now, componentInfo);
                }
            }

            var inspectorInfos = s_InspectorWindowInfos.Values.ToArray();
            foreach (var info in inspectorInfos)
            {
                if (!info.inUse)
                {
                    RemoveSpikeOverlay(info);
                }
            }
        }

        static void UpdateSpikeWindow(double now, SpikeWindowInfo info)
        {
            var avgTime = EditorPerformanceTracker.GetAverageTime(info.trackerName);
            var trackerLastTime = EditorPerformanceTracker.GetLastTime(info.trackerName);
            var sampleCount = EditorPerformanceTracker.GetSampleCount(info.trackerName);
            var peakTime = EditorPerformanceTracker.GetPeakTime(info.trackerName);
            var spikeDuration = info.lastSpikeTime == 0 ? 0 : now - info.lastSpikeTime;

            var currentTime = PerformanceTrackerSettings.spikeHighlightStrategy == SpikeHighlightStrategy.LastTime ? trackerLastTime : avgTime;
            var infoLastTime = PerformanceTrackerSettings.spikeHighlightStrategy == SpikeHighlightStrategy.LastTime ? info.lastTime : info.lastAvgTime;

            if (info.isSpiking && info.spikeOverlay.parent != info.spikeOverlayParent)
            {
                // Spike Overlay is not attached to the right window: probably a window that got docked or undocked.
                InvalidateParent(info);
            }

            if (!info.isSpiking && sampleCount == info.lastSampleCount)
            {
                // Nothing to do
            }
            else if (info.isSpiking && sampleCount == info.lastSampleCount)
            {
                // Check if needs to fade out
                if (spikeDuration > PerformanceTrackerSettings.spikeDuration)
                {
                    ResetSpikeOverlay(info);
                }
                // Do not fade if we are already long to redraw
                else if (currentTime < PerformanceTrackerSettings.spikeCriticalThreshold && info.supportsFading)
                {
                    // Try to fade out gently:
                    var alphaFading = (float)((PerformanceTrackerSettings.spikeDuration - spikeDuration) / PerformanceTrackerSettings.spikeDuration);
                    var color = info.spikeOverlay.style.borderBottomColor.value;
                    color.a = alphaFading;
                    ApplySpikeOverlay(info, color);
                }
            }
            else
            {
                // We just had an update of sample count:
                if (info.paintTriggeredByStateChanged)
                {
                    // Discard this Paint event since it was caused by one of our state change
                    info.paintTriggeredByStateChanged = false;
                }
                else if (currentTime > PerformanceTrackerSettings.spikeCriticalThreshold)
                {
                    if (!info.isSpiking)
                    {
                        ApplySpikeOverlay(info, ComputeSpikeColor((float)currentTime, PerformanceTrackerSettings.spikeCriticalThreshold, 10.0f, PtStyles.criticalColor, Color.black));
                    }
                    info.lastSpikeTime = now;
                }
                else if (currentTime > PerformanceTrackerSettings.spikeWarningThreshold)
                {
                    if (!info.isSpiking)
                    {
                        ApplySpikeOverlay(info, ComputeSpikeColor((float)currentTime, PerformanceTrackerSettings.spikeWarningThreshold, PerformanceTrackerSettings.spikeCriticalThreshold, PtStyles.warningColor, PtStyles.criticalColor));
                    }
                    info.lastSpikeTime = now;
                }
                else if (infoLastTime > PerformanceTrackerSettings.spikeWarningThreshold)
                {
                    ResetSpikeOverlay(info);
                }
            }

            info.lastAvgTime = avgTime;
            info.lastTime = trackerLastTime;
            info.lastSampleCount = sampleCount;
            info.lastPeakTime = peakTime;
        }

        static Color ComputeSpikeColor(float time, float lowerTime, float upperTime, Color lowerColor, Color upperColor)
        {
            return Color.Lerp(lowerColor, upperColor, (time - lowerTime) / (upperTime - lowerTime));
        }

        static VisualElement CreateSpikeOverlay()
        {
            var spikeOverlay = new VisualElement();
            spikeOverlay.pickingMode = PickingMode.Ignore;
            spikeOverlay.focusable = false;
            spikeOverlay.style.position = Position.Absolute;
            spikeOverlay.style.borderBottomWidth = k_SpikeBorderWidth;
            spikeOverlay.style.borderTopWidth = k_SpikeBorderWidth;
            spikeOverlay.style.borderLeftWidth = k_SpikeBorderWidth;
            spikeOverlay.style.borderRightWidth = k_SpikeBorderWidth;
            spikeOverlay.style.height = new StyleLength(new Length(100, LengthUnit.Percent));
            spikeOverlay.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            spikeOverlay.AddToClassList("perf-spike-overlay");
            return spikeOverlay;
        }

        static void InvalidateParent(SpikeWindowInfo info, VisualElement parent = null)
        {
            info.spikeOverlayExplicitParent = parent;
            // We will be processing the event so do not prevent it.
            ResetSpikeOverlay(info, false);
        }

        static void ApplySpikeOverlay(SpikeWindowInfo info, Color spikeColor)
        {
            var spikeOverlayParent = info.spikeOverlayParent;
            if (info.isInspectorElement)
            {
                if (info.spikeOverlay == null)
                {
                    info.spikeOverlay = CreateSpikeOverlay();
                    info.spikeOverlay.name = info.trackerName;
                    spikeOverlayParent.Add(info.spikeOverlay);
                }
                
                var color = spikeColor;
                color.a = 0.05f;
                info.spikeOverlay.style.backgroundColor = color;
            }
            else
            {
                if (spikeOverlayParent == null)
                    return;

                if (info.spikeOverlay == null)
                {
                    info.spikeOverlay = CreateSpikeOverlay();
                    spikeOverlayParent.Add(info.spikeOverlay);
                }
                else
                {
                    // Check if it is still the last element:
                    var childCount = spikeOverlayParent.childCount;
                    if (childCount > 0 && spikeOverlayParent.ElementAt(childCount - 1) == info.spikeOverlay)
                    {
                        // All is good, perf Overlay is the last item. Nothing to do.
                    }
                    else
                    {
                        // Ensure it is last:
                        if (spikeOverlayParent.Contains(info.spikeOverlay))
                        {
                            spikeOverlayParent.Remove(info.spikeOverlay);
                        }
                        spikeOverlayParent.Add(info.spikeOverlay);
                    }
                }
                WindowBorderState.ApplyToStyle(info.spikeOverlay, spikeColor);
            }

            info.paintTriggeredByStateChanged = true;
            if (PerformanceTrackerSettings.spikeHighlightStrategy == SpikeHighlightStrategy.AvgTime)
            {
                EditorPerformanceTracker.Reset(info.trackerName);
            }
        }

        static void ResetSpikeOverlay(SpikeWindowInfo info, bool ignoreNextEvent = true)
        {
            info.spikeOverlay?.RemoveFromHierarchy();
            info.spikeOverlay = null;
            info.lastSpikeTime = 0;
            info.lastTime = 0;
            if (ignoreNextEvent)
                info.paintTriggeredByStateChanged = true;

            if (info.window)
                info.window.Repaint();

            if (PerformanceTrackerSettings.spikeHighlightStrategy == SpikeHighlightStrategy.AvgTime)
            {
                EditorPerformanceTracker.Reset(info.trackerName);
            }
        }

        static void RemoveSpikeOverlay(SpikeWindowInfo info)
        {
            ResetSpikeOverlay(info);
            if (info.isInspectorElement)
            {
                s_InspectorWindowInfos.Remove(info.trackerName);
            }
            else
            {
                s_SpikeWindowInfos.Remove(info.trackerName);
                if (info.isInspectorWindow)
                {
                    var inspectorInfos = s_InspectorWindowInfos.Values.ToArray();
                    foreach (var inspectorInfo in inspectorInfos)
                    {
                        RemoveSpikeOverlay(inspectorInfo);
                    }
                }
            }
        }
    }
}
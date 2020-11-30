using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.IMGUI.Controls;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;
using UnityEditorInternal.Profiling;

#if UNITY_2021_1_OR_NEWER
using CPUModule = UnityEditorInternal.Profiling.CPUOrGPUProfilerModule;
#else
using CPUModule = UnityEditorInternal.Profiling.CPUorGPUProfilerModule;
#endif

[assembly: InternalsVisibleTo("Unity.PerformanceTracking.Editor.Tests")]

namespace Unity.PerformanceTracking
{
    public static class ProfilerHelpers
    {
        public const string k_DefaultProfileSaveDirectory = "Assets/Profiles";
        private const string kProfilerEnabledSessionKey = "ProfilerEnabled";
        private static string m_MarkerFilter;

        static ProfilerHelpers()
        {
            Init();
        }

        public static event Action<bool> profileEnabledChanged;

        public static void ToggleProfilerRecording(bool deepProfiler)
        {
            if (!ProfilerDriver.enabled)
            {
                StartProfilerRecording(true, deepProfiler);
            }
            else
            {
                var currentMarkerFilter = GetCurrentMarkerFilter();
                StopProfilerRecordingAndCreateReport(string.IsNullOrEmpty(currentMarkerFilter) ? "ToggleProfile" : currentMarkerFilter);
            }
        }

        public static bool RecordWithProfileSample(string sampleName, Action toBenchmark, bool editorProfile = true, bool deepProfile = true, int count = 1, Action<EditorWindow> recordingDone = null)
        {
            return StartProfilerRecording(editorProfile, deepProfile, () =>
            {
                for (var i = 0; i < count; ++i)
                {
                    Profiler.BeginSample(sampleName);
                    toBenchmark();
                    Profiler.EndSample();
                }
                StopProfilerRecordingAndCreateReport("", sampleName, recordingDone);
            });
        }

        public static bool RecordWithMarkerFilter(string markerName, Action toBenchmark, bool editorProfile = true, bool deepProfile = true, int count = 1, Action<EditorWindow> recordingDone = null)
        {
            if (!SupportsMarkerFiltering())
                return false;

            #if UNITY_2020_1_OR_NEWER
            if (!EditorPerformanceTracker.Exists(markerName))
            {
                // Create Marker once so Profiler.SetMarkerFiltering will work
                using (new EditorPerformanceTracker(markerName))
                {

                }
            }
            #endif

            return StartProfilerRecording(markerName, editorProfile, deepProfile, () =>
            {
                for (var i = 0; i < count; ++i)
                    toBenchmark();
                StopProfilerRecordingAndCreateReport(markerName, "", recordingDone);
            });
        }

        public static bool StartProfilerRecording(bool editorProfile, bool deepProfile, Action onProfileEnabled = null)
        {
            return StartProfilerRecording("", editorProfile, deepProfile, onProfileEnabled);
        }

        public static bool StartProfilerRecording(string markerFilter, bool editorProfile, bool deepProfile, Action onProfileEnabled = null)
        {
            if (ProfilerDriver.deepProfiling != deepProfile)
            {
                if (deepProfile)
                    Debug.LogWarning("Enabling deep profiling. Domain reload will occur. Please restart Profiling.");
                else
                    Debug.LogWarning("Disabling deep profiling. Domain reload will occur. Please restart Profiling.");

                SetProfilerDeepProfile(deepProfile);
                return false;
            }

            var editorProfileStr = editorProfile ? "editor" : "playmode";
            var deepProfileStr = deepProfile ? " - deep profile" : "";
            var hasMarkerFilter = !string.IsNullOrEmpty(markerFilter) && SupportsMarkerFiltering();
            var markerStr = hasMarkerFilter ? $"- MarkerFilter: {markerFilter}" : "";
            Debug.Log($"Start profiler recording: {editorProfileStr} {deepProfileStr} {markerStr}...");

            EnableProfiler(false);

            EditorApplication.delayCall += () =>
            {
                ProfilerDriver.ClearAllFrames();
                ProfilerDriver.profileEditor = editorProfile;
                ProfilerDriver.deepProfiling = deepProfile;
                if (hasMarkerFilter)
                {
                    SetMarkerFiltering(markerFilter);
                }
                
                EditorApplication.delayCall += () =>
                {
                    EnableProfiler(true);
                    if (onProfileEnabled != null)
                    {
                        EditorApplication.delayCall += () =>
                        {
                            onProfileEnabled();
                        };
                    }
                };
            };

            return true;
        }

        public static void StopProfilerRecording(Action toProfilerStopped = null)
        {
            if (SupportsMarkerFiltering())
                SetMarkerFiltering("");

            EnableProfiler(false);
            Debug.Log($"Stop profiler recording.");

            if (toProfilerStopped != null)
            {
                EditorApplication.delayCall += () =>
                {
                    toProfilerStopped();
                };
            };
        }

        public static void StopProfilerRecordingAndOpenProfiler(string searchString = "", Action<EditorWindow> onProfilerOpened = null)
        {
            StopProfilerRecording(() =>
            {
                OpenProfiler(searchString, onProfilerOpened);
            });
        }

        public static void StopProfilerRecordingAndCreateReport(string profileTitle, string searchString = "", Action<EditorWindow> onReportOpened = null)
        {
            StopProfilerRecording(() =>
            {
                var profileSaveFilePath = SaveProfileReport(profileTitle);
                OpenProfileReport(profileSaveFilePath, searchString, onReportOpened);
            });
        }

        public static string SaveProfileReport(string reportTitle)
        {
            if (!Directory.Exists(k_DefaultProfileSaveDirectory))
                Directory.CreateDirectory(k_DefaultProfileSaveDirectory);
            var timeId = DateTime.Now.ToString("s").Replace(":", "_");
            var formattedId = reportTitle.ToLowerInvariant().Replace(".", "_");
            if (ProfilerDriver.deepProfiling)
                formattedId += "_deep";
            var profileSaveFilePath = $"{k_DefaultProfileSaveDirectory}/{formattedId}_{timeId}.profile.data".Replace("\\", "/");
            ProfilerDriver.SaveProfile(profileSaveFilePath);
            AssetDatabase.ImportAsset(profileSaveFilePath);
            Debug.Log("Saved profiling trace at <a file=\"" + profileSaveFilePath + "\">" + profileSaveFilePath + "</a>");
            return profileSaveFilePath;
        }

        public static void OpenProfiler(string searchString = "", Action<EditorWindow> onOpenProfiler = null)
        {
            var profilerWindow = OpenProfilerWindow();
            EditorApplication.delayCall += () =>
            {
                EditorApplication.delayCall += () =>
                {
                    if (!string.IsNullOrEmpty(searchString))
                    {
                        SetSearchField(profilerWindow, searchString);
                        profilerWindow.Repaint();
                    }
                    onOpenProfiler?.Invoke(profilerWindow);
                };
            };
        }

        public static void OpenProfileReport(string profileDataPath, string searchString = "", Action<EditorWindow> onProfileReportLoaded = null)
        {
            var profilerWindow = OpenProfilerWindow();
            EditorApplication.delayCall += () =>
            {
                EnableProfiler(false);
                ProfilerDriver.ClearAllFrames();
                if (ProfilerDriver.LoadProfile(profileDataPath, false))
                {
                    profilerWindow.titleContent.text = System.IO.Path.GetFileNameWithoutExtension(profileDataPath);
                }

                SwitchToCPUView(profilerWindow);
                profilerWindow.Repaint();
                if (!string.IsNullOrEmpty(searchString))
                {
                    EditorApplication.delayCall += () =>
                    {
                        // Wait till switch to CPU before setting the search field.
                        SetSearchField(profilerWindow, searchString);
                        Debug.Log("Profiler report ready");
                        profilerWindow.Repaint();
                    };
                }
                onProfileReportLoaded?.Invoke(profilerWindow);
            };
        }

        [UsedImplicitly, OnOpenAsset(1)]
        private static bool OpenProfileData(int instanceID, int line)
        {
            var assetPath = AssetDatabase.GetAssetPath(EditorUtility.InstanceIDToObject(instanceID));
            if (System.IO.Path.GetExtension(assetPath) != ".data")
                return false;

            OpenProfileReport(assetPath);
            return true;
        }

        public struct BenchmarkResult
        {
            public int sampleCount;
            public double avgInMs;
            public double totalInSecond;
            public double peakInMs;
            public double minInMs;
            public double medianInMs;
        }

        public static BenchmarkResult BenchmarkMarker(string markerName, Action action, int count = 100)
        {
            var peak = 0.0;
            var min = 100000000.0;
            var samples = new double[count];

            EditorPerformanceTracker.Reset(markerName);
            var sampleCount = EditorPerformanceTracker.GetSampleCount(markerName);
            for (var i = 0; i < count; ++i)
            {
                action();
                sampleCount = EditorPerformanceTracker.GetSampleCount(markerName);
                
                var elapsed = EditorPerformanceTracker.GetLastTime(markerName) * 1000.0;
                samples[i] = elapsed;
                if (elapsed > peak)
                {
                    peak = elapsed;
                }
                if (elapsed < min)
                {
                    min = elapsed;
                }
            }

            Array.Sort(samples);

            var result = new BenchmarkResult();
            result.sampleCount = count;
            result.avgInMs = EditorPerformanceTracker.GetAverageTime(markerName) * 1000.0;
            result.totalInSecond = EditorPerformanceTracker.GetTotalTime(markerName);
            result.peakInMs = peak;
            result.minInMs = min;
            result.medianInMs = samples[count / 2];
            return result;
        }

        public static BenchmarkResult Benchmark(Action action, int count = 100)
        {
            var clock = new System.Diagnostics.Stopwatch();
            var peak = 0L;
            var total = 0L;
            var min = 1000000L;
            var samples = new long[count];

            for (var i = 0; i < count; ++i)
            {
                clock.Start();
                action();
                clock.Stop();

                var elapsed = clock.ElapsedTicks;
                samples[i] = elapsed;
                if (elapsed > peak)
                {
                    peak = elapsed;
                }
                if (elapsed < min)
                {
                    min = elapsed;
                }

                total += elapsed;
                clock.Reset();
            }

            var secondsPerTick = 1.0 / Stopwatch.Frequency;
            var msPerTick = 1000.0 / Stopwatch.Frequency;

            var avg = (double)(total) / count;
            Array.Sort(samples);

            var result = new BenchmarkResult();
            result.sampleCount = count;
            result.avgInMs = avg * msPerTick;
            result.totalInSecond = total * secondsPerTick;
            result.peakInMs = peak * msPerTick;
            result.minInMs = min * msPerTick;
            result.medianInMs = samples[count / 2] * msPerTick;
            return result;
        }

        #region Menu
        [MenuItem("Edit/Toggle Profiler Recording #E")]
        public static void ToggleProfilerRecording()
        {
            ToggleProfilerRecording(ProfilerDriver.deepProfiling);
        }
        #endregion

        #region CompatibilityNoInternalAccess

        static Assembly m_UnityEditorAssembly;
        static System.Type[] m_Types;
        static System.Type m_ProfilerWindowType;

        internal static void EnableProfiler(bool enable)
        {
            ProfilerDriver.enabled = enable;
            SessionState.SetBool(kProfilerEnabledSessionKey, enable);
            profileEnabledChanged?.Invoke(enable);
        }

        internal static void Init()
        {
            m_UnityEditorAssembly = typeof(Selection).Assembly;
            m_Types = m_UnityEditorAssembly.GetTypes().ToArray();
            m_ProfilerWindowType = GetUnityEditorType("ProfilerWindow");
        }

        internal static EditorWindow OpenProfilerWindow()
        {
            var profilerWindow = EditorWindow.CreateWindow<ProfilerWindow>();
            SwitchToCPUView(profilerWindow);
            profilerWindow.Show();
            return profilerWindow;
        }

        #if UNITY_2019_3
        internal static void GetCpuModule(EditorWindow profiler, out System.Type cpuModuleType, out object cpuModule)
        {
            var m_ProfilerModulesField = m_ProfilerWindowType.GetField("m_ProfilerModules", BindingFlags.Instance | BindingFlags.NonPublic);
            var m_ProfilerModules = m_ProfilerModulesField.GetValue(profiler) as Array;
            cpuModule = m_ProfilerModules.GetValue((int)ProfilerArea.CPU);
            cpuModuleType = GetUnityEditorType("CPUorGPUProfilerModule");
        }
        #endif

        internal static void SetProfilerDeepProfile(bool deepProfile)
        {
            ProfilerWindow.SetEditorDeepProfiling(deepProfile);
        }

        internal static void RequestScriptReload()
        {
            EditorUtility.RequestScriptReload();
        }

        internal static void SetRecordingEnabled(EditorWindow profiler, bool enableRecording)
        {
            ((ProfilerWindow)profiler).SetRecordingEnabled(enableRecording);
        }

        internal static bool SupportsMarkerFiltering()
        {
            return true;
        }

        internal static string GetCurrentMarkerFilter()
        {
            return m_MarkerFilter;
        }

        internal static void SetMarkerFiltering(string markerName)
        {
            if (SupportsMarkerFiltering())
                m_MarkerFilter = markerName;

            ProfilerDriver.SetMarkerFiltering(markerName);
        }

        internal static void SwitchToCPUView(EditorWindow profilerWindow)
        {
            var profiler = (ProfilerWindow)profilerWindow;
            var cpuProfilerModule = profiler.GetProfilerModule<CPUProfilerModule>(ProfilerArea.CPU);
            cpuProfilerModule.ViewType = ProfilerViewType.Hierarchy;
        }

        internal static void SetSearchField(EditorWindow profilerWindow, string searchString)
        {
            #if UNITY_2021_1_OR_NEWER
            var profiler = (ProfilerWindow)profilerWindow;
            var cpuModule = profiler.GetProfilerModule<CPUModule>(ProfilerArea.CPU);
            cpuModule.ClearSelection();
            if (!string.IsNullOrEmpty(searchString))
                cpuModule.FrameDataHierarchyView.treeView.searchString = searchString;
            #else
            var profiler = (ProfilerWindow)profilerWindow;
            var cpuModule = profiler.GetProfilerModule<CPUModule>(ProfilerArea.CPU);
            cpuModule.FrameDataHierarchyView.SetSelectionFromLegacyPropertyPath("");
            if (!String.IsNullOrEmpty(searchString))
                cpuModule.FrameDataHierarchyView.treeView.searchString = searchString;
            #endif
        }

        internal static void SetTreeViewSearchString(System.Type typeOwningTreeView, object objOwningTreeView, string searchString)
        {
            var treeViewField = typeOwningTreeView.GetProperty("treeView", BindingFlags.Instance | BindingFlags.Public);
            var treeView = treeViewField.GetValue(objOwningTreeView) as TreeView;
            treeView.searchString = searchString;
        }

        public static System.Type GetUnityEditorType(string typeName)
        {
            return m_Types.First(t => t.Name == typeName);
        }

        public static IEnumerable<System.Type> GetUnityEditorTypesImplementing(System.Type baseType)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            return assemblies.SelectMany(a => a.GetTypes()).Where(t => t.IsSubclassOf(baseType));
        }

        #endregion
    }
}

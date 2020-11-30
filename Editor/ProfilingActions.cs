using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace Unity.PerformanceTracking
{
    public static class ProfilingActions
    {
        [UsedImplicitly, ProfilingAction("Profile")]
        static void ProfileSnippet(object preExecutePayload, ProfilingSnippet snippet, ProfilingSnippetOptions options)
        {
            ProfileSnippet(preExecutePayload, snippet, options, false);
        }

        [UsedImplicitly, ProfilingAction("Profile - Deep")]
        static void ProfileSnippetDeep(object preExecutePayload, ProfilingSnippet snippet, ProfilingSnippetOptions options)
        {
            ProfileSnippet(preExecutePayload, snippet, options, true);
        }

        static void ProfileSnippet(object preExecutePayload, ProfilingSnippet snippet, ProfilingSnippetOptions options, bool deepProfile)
        {
            var s = snippet.GetSnippetAction(preExecutePayload, options);
            ProfilerHelpers.RecordWithProfileSample(snippet.sampleName, s, true, deepProfile, options.count);
        }

        [UsedImplicitly, ProfilingAction("Profile Marker")]
        static void ProfileMarker(object preExecutePayload, ProfilingSnippet snippet, ProfilingSnippetOptions options)
        {
            ProfileSnippetMarker(preExecutePayload, snippet, options, false);
        }

        [UsedImplicitly, ProfilingAction("Profile Marker Deep")]
        static void ProfileMarkerDeep(object preExecutePayload, ProfilingSnippet snippet, ProfilingSnippetOptions options)
        {
            ProfileSnippetMarker(preExecutePayload, snippet, options, true);
        }

        static void ProfileSnippetMarker(object preExecutePayload, ProfilingSnippet snippet, ProfilingSnippetOptions options, bool deepProfile)
        {
            var markerName = snippet.GetValidMarkerName();
            var action = ProfilingSnippetUtils.GetMarkerEnclosedSnippetAction(preExecutePayload, snippet, options);
            ProfilerHelpers.RecordWithMarkerFilter(markerName, action, true, deepProfile, options.count);
        }

        [UsedImplicitly, ProfilingAction("Benchmark")]
        static void BenchmarkSnippet(object preExecutePayload, ProfilingSnippet snippet, ProfilingSnippetOptions options)
        {
            var snippetAction = snippet.GetSnippetAction(preExecutePayload, options);
            #if UNITY_2020_1_OR_NEWER
            if (!snippet.ranOnce && options.warmup)
            {
                snippetAction(); // This execution pass should JIT the code first.
                EditorApplication.CallDelayed(() => {
                    var result = ProfilerHelpers.Benchmark(snippetAction, options.count);
                    LogBenchmark(snippet.label, result, options);
                }, 0);
            }
            else
            #endif
            {
                var result = ProfilerHelpers.Benchmark(snippetAction, options.count);
                LogBenchmark(snippet.label, result, options);
            }
        }

        [UsedImplicitly, ProfilingAction("Benchmark Marker")]
        static void BenchmarMarker(object preExecutePayload, ProfilingSnippet snippet, ProfilingSnippetOptions options)
        {
            var markerName = snippet.GetValidMarkerName();
            var action = ProfilingSnippetUtils.GetMarkerEnclosedSnippetAction(preExecutePayload, snippet, options);
            var result = ProfilerHelpers.BenchmarkMarker(markerName, action, options.count);
            LogBenchmark($"Benchmark marker {markerName}", result, options);
        }

        private static void LogBenchmark(string title, ProfilerHelpers.BenchmarkResult result, ProfilingSnippetOptions options)
        {
            Debug.Log(ProfilingSnippetUtils.FormatBenchmarkResult(title, result));
            options.Log(ProfilingSnippetUtils.FormatBenchmarkResult(title, result, options.csvLog));
        }
    }

}

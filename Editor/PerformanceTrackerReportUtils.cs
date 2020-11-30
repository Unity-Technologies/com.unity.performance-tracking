using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor.Profiling;
using UnityEngine;

namespace Unity.PerformanceTracking
{
    [Serializable]
    internal  struct TrackerReportOptions
    {
        public string trackerFilter;
        public bool showSamples;
        public bool showPeak;
        public bool showAvg;
        public bool showTotal;
        public bool sort;
        public ColumnId sortBy;
        public bool sortAsc;
    }

    [Serializable]
    internal struct TrackerInfo
    {
        public int samples;
        public float peakTime;
        public float avgTime;
        public float totalTime;
        public string trackerName;
    }

    [Serializable]
    internal struct TrackersReport
    {
        public List<TrackerInfo> trackers;
    }

    internal static class PerformanceTrackerReportUtils
    {
        public static string GetTrackersReport(TrackerReportOptions options)
        {
            var trackerRx = new Regex(options.trackerFilter, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var trackers = PtModel.BuildTrackerList(new PtInfo[] { }, options.sortBy, options.sortAsc, options.sort);
            var filteredTrackers = trackers.Where(tracker => trackerRx.IsMatch(tracker.name)).ToList();
            return GetTrackersReport(filteredTrackers, options);
        }

        public static string GetAllTrackersReport(TrackerReportOptions options)
        {
            options.trackerFilter = ".+";
            return GetTrackersReport(options);
        }

        public static TrackersReport GenerateTrackerReport(TrackerReportOptions options)
        {
            if (string.IsNullOrEmpty(options.trackerFilter))
            {
                options.trackerFilter = ".+";
            }
            var trackerRx = new Regex(options.trackerFilter, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var trackers = PtModel.BuildTrackerList(new PtInfo[] { }, options.sortBy, options.sortAsc, options.sort);
            var filteredTrackers = trackers.Where(tracker => trackerRx.IsMatch(tracker.name)).ToList();
            var report = new TrackersReport()
            {
                trackers = new List<TrackerInfo>()
            };

            foreach(var tracker in filteredTrackers)
            {
                TrackerInfo info = new TrackerInfo
                {
                    avgTime = (float)tracker.avgTime,
                    totalTime = (float)tracker.totalTime,
                    samples = tracker.sampleCount,
                    peakTime = (float)tracker.peakTime,
                    trackerName = tracker.name
                };
                report.trackers.Add(info);
            }

            return report;
        }

        public static string GetTrackersReport(IEnumerable<string> trackerNames, TrackerReportOptions options)
        {
            if (!ValidateReportOptions(options))
                return null;

            var sb = new StringBuilder();
            var maxTrackerNameLength = trackerNames.Max(name => name.Length);

            var moreThanOneLine = false;
            foreach (var trackerName in trackerNames)
            {
                if (!EditorPerformanceTracker.Exists(trackerName))
                    continue;

                var shownTokens = new List<string>();
                if (options.showSamples)
                    shownTokens.Add($"{EditorPerformanceTracker.GetSampleCount(trackerName)} samples");
                if (options.showPeak)
                    shownTokens.Add($"Peak {PtModel.ToEngineeringNotation(EditorPerformanceTracker.GetPeakTime(trackerName))}s");
                if (options.showAvg)
                    shownTokens.Add($"Avg. {PtModel.ToEngineeringNotation(EditorPerformanceTracker.GetAverageTime(trackerName))}s");
                if (options.showTotal)
                    shownTokens.Add($"Total {PtModel.ToEngineeringNotation(EditorPerformanceTracker.GetTotalTime(trackerName))}s");

                if (moreThanOneLine)
                    sb.AppendLine();

                sb.AppendFormat($"{{0,{maxTrackerNameLength}}}: ", trackerName);
                sb.Append(string.Join(", ", shownTokens));
                moreThanOneLine = true;
            }

            return sb.ToString();
        }

        public static string GetTrackersReport(IEnumerable<PtInfo> trackers, TrackerReportOptions options)
        {
            if (!ValidateReportOptions(options))
                return null;

            var sb = new StringBuilder();
            var maxTrackerNameLength = trackers.Max(tracker => tracker.name.Length);

            var moreThanOneLine = false;
            foreach (var tracker in trackers)
            {
                var shownTokens = new List<string>();
                if (options.showSamples)
                    shownTokens.Add($"{tracker.sampleCount} samples");
                if (options.showPeak)
                    shownTokens.Add($"Peak {PtModel.ToEngineeringNotation(tracker.peakTime)}s");
                if (options.showAvg)
                    shownTokens.Add($"Avg. {PtModel.ToEngineeringNotation(tracker.avgTime)}s");
                if (options.showTotal)
                    shownTokens.Add($"Total {PtModel.ToEngineeringNotation(tracker.totalTime)}s");

                if (moreThanOneLine)
                    sb.AppendLine();

                sb.AppendFormat($"{{0,{maxTrackerNameLength}}}: ", tracker.name);
                sb.Append(string.Join(", ", shownTokens));
                moreThanOneLine = true;
            }

            return sb.ToString();
        }

        private static bool ValidateReportOptions(TrackerReportOptions options)
        {
            var showSomething = options.showSamples || options.showPeak || options.showAvg || options.showTotal;
            if (!showSomething)
            {
                Debug.LogError("You should have at least one shown value in the report");
                return false;
            }

            return true;
        }
    }
}
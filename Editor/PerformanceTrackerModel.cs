using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.Profiling;
using System.Diagnostics;

namespace Unity.PerformanceTracking
{
    [DebuggerDisplay("name={name}")]
    internal struct PtInfo
    {
        public string name;
        public int sampleCount;
        public double peakTime;
        public double avgTime;
        public double totalTime;
        public double lastTime;
        public double usage;
        public double timestamp;
        public int dtSampleCount;
        public double dtPeakTime;
        public double dtAvgTime;
        public double dtLastTime;
        public bool updated;
    }

    internal struct RefreshRateInfo
    {
        public double rate;
        public string label;
    }

    internal enum ColumnId
    {
        Name,
        SampleCount,
        Age,
        PeakTime,
        AvgTime,
        LastTime,
        TotalTime,
        Actions
    }

    internal struct ColumnDescriptor
    {
        public ColumnDescriptor(ColumnId id, string label, int maskId = 0)
        {
            columnId = id;
            columnsSelectorMaskId = maskId;
            this.label = label;
            labelAsc = label + " \u21d3";
            labelDesc = label + " \u21d1";
        }

        public ColumnId columnId;
        public bool supportsHiding => columnsSelectorMaskId > 0;
        public int columnsSelectorMaskId;
        public string label;
        public string labelAsc;
        public string labelDesc;
    }

    internal static class PtStyles
    {
        public static bool isDarkTheme => EditorGUIUtility.isProSkin;
        public static Color normalColor = isDarkTheme ? new Color(196 / 255f, 196 / 255f, 196 / 255f) : new Color(32 / 255f, 32 / 255f, 32 / 255f);
        public static Color warningColor = isDarkTheme ? new Color(255 / 255f, 204 / 255f, 0 / 255f) : new Color(240 / 255f, 105 / 255f, 53 / 255f);
        public static Color criticalColor = new Color(204 / 255f, 51 / 255f, 0 / 255f);

        public static string criticalHexCode = Utils.ColorToHexCode(criticalColor);
        public static string warningHexCode = Utils.ColorToHexCode(warningColor);

        public static Color fasterColor = isDarkTheme ? new Color(153 / 255f, 204 / 255f, 51 / 255f) : new Color(1 / 255f, 169 / 255f, 87 / 255f);
        public static Color slowerColor = isDarkTheme ? new Color(255 / 255f, 153 / 255f, 102 / 255f) : criticalColor;
        public static Color oddRowColor = isDarkTheme ? new Color(56 / 255f, 56 / 255f, 56 / 255f) : new Color(202 / 255f, 202 / 255f, 202 / 255f);
        public static Color evenRowColor = isDarkTheme ? new Color(63 / 255f, 63 / 255f, 63 / 255f) : new Color(194 / 255f, 194 / 255f, 194 / 255f);
        public static int itemHeight = 23;
    }

    internal static class PtModel
    {
        public static RefreshRateInfo[] RefreshRates =
        {
            new RefreshRateInfo { rate = 0.5, label = "0.5 second" },
            new RefreshRateInfo { rate = 1, label = "1 second" },
            new RefreshRateInfo { rate = 2, label = "2 seconds" },
            new RefreshRateInfo { rate = 5, label = "5 seconds" },
            new RefreshRateInfo { rate = 10, label = "10 seconds" },
            new RefreshRateInfo { rate = 1000000000, label = "No refresh" }
        };

        public static ColumnDescriptor[] ColumnDescriptors =
        {
            new ColumnDescriptor(ColumnId.Name, "Tracker"),
            new ColumnDescriptor(ColumnId.SampleCount, "Sample Count", 1),
            new ColumnDescriptor(ColumnId.Age, "Age", 2),
            new ColumnDescriptor(ColumnId.PeakTime, "Peak Time", 4),
            new ColumnDescriptor(ColumnId.AvgTime, "Average Time", 8),
            new ColumnDescriptor(ColumnId.LastTime, "Last Time", 16),
            new ColumnDescriptor(ColumnId.TotalTime, "Total Time", 32),
            new ColumnDescriptor(ColumnId.Actions, ""),
        };

        public static int showAllColumns;

        static PtModel()
        {
            foreach (var desc in ColumnDescriptors)
            {
                showAllColumns |= desc.columnsSelectorMaskId;
            }
        }

        public static ColumnDescriptor GetColumnDescriptor(ColumnId id)
        {
            return ColumnDescriptors[(int)id];
        }

        public static PtInfo[] BuildTrackerList(PtInfo[] previousTrackers, ColumnId sortBy, bool sortAsc, bool sort = true)
        {
            var trackerNames = EditorPerformanceTracker.GetAvailableTrackers();
            var trackers = new PtInfo[trackerNames.Length];
            for (int i = 0; i < trackerNames.Length; ++i)
            {
                var trackerName = trackerNames[i];
                if (!EditorPerformanceTracker.Exists(trackerName))
                    continue;
                trackers[i].name = trackerName;
                trackers[i].sampleCount = EditorPerformanceTracker.GetSampleCount(trackerName);
                trackers[i].peakTime = EditorPerformanceTracker.GetPeakTime(trackerName);
                trackers[i].avgTime = EditorPerformanceTracker.GetAverageTime(trackerName);
                trackers[i].totalTime = EditorPerformanceTracker.GetTotalTime(trackerName);
                trackers[i].lastTime = EditorPerformanceTracker.GetLastTime(trackerName);
                trackers[i].usage = EditorPerformanceTracker.GetTotalUsage(trackerName);
                trackers[i].timestamp = EditorPerformanceTracker.GetTimestamp(trackerName);
                trackers[i].updated = false;

                // Tracker previous changes
                var pti = FindTrackerIndex(trackerName, previousTrackers);
                if (pti == -1)
                    continue;

                var ppt = previousTrackers[pti];
                trackers[i].dtSampleCount = trackers[i].sampleCount - ppt.sampleCount;
                trackers[i].dtPeakTime = trackers[i].peakTime - ppt.peakTime;
                trackers[i].dtLastTime = trackers[i].lastTime - ppt.lastTime;
                trackers[i].dtAvgTime = trackers[i].avgTime - ppt.avgTime;
                trackers[i].updated = trackers[i].dtSampleCount > 0;
            }

            if (sort)
            {
                Sort(trackers, sortBy, sortAsc);
            }

            return trackers;
        }

        public static void Sort(PtInfo[] trackers, ColumnId sortBy, bool sortAsc)
        {
            int dirm = sortAsc ? 1 : -1;
            Array.Sort(trackers, (x, y) =>
            {
                switch (sortBy)
                {
                    case ColumnId.AvgTime: return dirm * x.avgTime.CompareTo(y.avgTime);
                    case ColumnId.SampleCount: return dirm * x.sampleCount.CompareTo(y.sampleCount);
                    case ColumnId.PeakTime: return dirm * x.peakTime.CompareTo(y.peakTime);
                    case ColumnId.LastTime: return dirm * x.lastTime.CompareTo(y.lastTime);
                    case ColumnId.Age: return dirm * x.timestamp.CompareTo(y.timestamp);
                    case ColumnId.TotalTime: return dirm * x.usage.CompareTo(y.usage);
                }

                return dirm * String.Compare(x.name, y.name, StringComparison.Ordinal);
            });
        }

        public static int FindTrackerIndex(string name, PtInfo[] trackers)
        {
            for (int i = 0; i < trackers.Length; ++i)
            {
                if (trackers[i].name == name)
                    return i;
            }

            return -1;
        }

        public static string FormatAge(PtInfo t, double currentTime)
        {
            return t.updated ? $"{ToEngineeringNotation(currentTime - t.timestamp)}s" : "---";
        }

        public static string FormatAgeToolTip(PtInfo t)
        {
            return $"Occurred {t.timestamp} second(s) after startup";
        }

        public static string FormatTimeChange(double time, double dt)
        {
            return $"{ToEngineeringNotation(time)}s ({ToEngineeringNotation(dt, true)}s)";
        }

        public static string FormatTime(double time, double dt)
        {
            return $"{ToEngineeringNotation(time)}s ({ToEngineeringNotation(dt, true)}s)";
        }

        public static string FormatTimeRate(double time, double rate)
        {
            return $"{ToEngineeringNotation(time)}s ({rate:0.00} %)";
        }

        public static string ToEngineeringNotation(double d, bool printSign = false)
        {
            var sign = !printSign || d < 0 ? "" : "+";
            if (Math.Abs(d) >= 1)
                return $"{sign}{d.ToString("###.0", System.Globalization.CultureInfo.InvariantCulture)}";

            if (Math.Abs(d) > 0)
            {
                double exponent = Math.Log10(Math.Abs(d));
                switch ((int)Math.Floor(exponent))
                {
                    case -1:
                    case -2:
                    case -3:
                        return $"{sign}{(d * 1e3):###.0} m";
                    case -4:
                    case -5:
                    case -6:
                        return $"{sign}{(d * 1e6):###.0} µ";
                    case -7:
                    case -8:
                    case -9:
                        return $"{sign}{(d * 1e9):###.0} n";
                    case -10:
                    case -11:
                    case -12:
                        return $"{sign}{(d * 1e12):###.0} p";
                    case -13:
                    case -14:
                    case -15:
                        return $"{sign}{(d * 1e15):###.0} f";
                    case -16:
                    case -17:
                    case -18:
                        return $"{sign}{(d * 1e15):###.0} a";
                    case -19:
                    case -20:
                    case -21:
                        return $"{sign}{(d * 1e15):###.0} z";
                    default:
                        return $"{sign}{(d * 1e15):###.0} y";
                }
            }

            return "0";
        }
    }
}
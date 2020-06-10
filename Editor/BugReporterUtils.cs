#if UNITY_2020_1_OR_NEWER
using System.Collections.Generic;
using UnityEditor.BugReporting;

namespace Unity.PerformanceTracking
{
    internal static class BugReporterUtils
    {
        public static void OpenBugReporter(string description, IEnumerable<string> additionalAttachments)
        {
            var additionalArguments = new List<string>();

            // Set performance type
            additionalArguments.Add("--bugtype");
            additionalArguments.Add("performance");

            // Set custom description
            additionalArguments.Add("--description");
            additionalArguments.Add(description);

            // Add attachments
            foreach (var attachment in additionalAttachments)
            {
                additionalArguments.Add("--attach");
                additionalArguments.Add(attachment);
            }

            // Open the reporter
            BugReportingTools.LaunchBugReporter(BugReportMode.ManualOpen, additionalArguments.ToArray());
        }
    }
}
#endif
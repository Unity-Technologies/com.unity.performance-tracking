
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Unity.PerformanceTracking.Tests;
using Unity.PerformanceTracking;

public class PerformanceTrackerActionsTests
{
    [UnityTest]
    public IEnumerator OpenPerformanceTrackerWindow()
    {
        var perfTrackerWindow = PerformanceTrackerActions.OpenPerformanceTrackerWindow(ProfilerHelpersTests.k_ApplicationTickMarker);
        Assert.IsNotNull(perfTrackerWindow);

        // Give the window some time to update
        var currentRefreshRate = perfTrackerWindow.GetRefreshRate();
        yield return TestUtils.WaitForTime(currentRefreshRate.rate);

        var listView = perfTrackerWindow.rootVisualElement.Q<ListView>(PerformanceTrackerWindow.k_TrackerList);
        var sourceItems = listView.itemsSource as List<PtInfo>;
        Assert.IsNotNull(sourceItems);
        Assert.IsNotEmpty(sourceItems);
        Assert.IsTrue(sourceItems.All(item => item.name.StartsWith("Application.Tick")));
        Assert.AreEqual(ProfilerHelpersTests.k_ApplicationTickMarker, sourceItems[0].name);

        perfTrackerWindow.Close();
    }

    [UnityTest]
    public IEnumerator GetCallstack()
    {
        var receivedData = false;
        PerformanceTrackerActions.GetCallstack(ProfilerHelpersTests.k_ApplicationTickMarker, callstack =>
        {
            receivedData = true;
            Assert.IsFalse(string.IsNullOrEmpty(callstack));
        });
        while (!receivedData)
        {
            yield return null;
        }
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using Unity.PerformanceTracking;
using Unity.PerformanceTracking.Tests;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.TestTools;

public class ProfilerHelpersTests
{
    public const string k_ApplicationTickMarker = "Application.Tick";

    [OneTimeSetUp]
    public void Init()
    {
        TestUtils.CreateFolder(TestUtils.testGeneratedFolder);
    }

    [OneTimeTearDown]
    public void Cleanup()
    {
        TestUtils.DeleteFolder(TestUtils.testGeneratedFolder);
    }

    [UnityTest]
    public IEnumerator StartProfilerRecording()
    {
        ProfilerHelpers.StartProfilerRecording("Application.Tick", true, ProfilerDriver.deepProfiling);

        // Wait for two delay call since we are doing two in the function
        yield return TestUtils.WaitForDelayCall();
        yield return TestUtils.WaitForDelayCall();

        Assert.IsTrue(ProfilerDriver.profileEditor);
        Assert.IsTrue(ProfilerDriver.enabled);

        ProfilerDriver.enabled = false;
    }

    [UnityTest]
    public IEnumerator StopProfilerRecording()
    {
        ProfilerHelpers.StartProfilerRecording("Application.Tick", true, false);
        // Wait for two delay call since we are doing two in the function
        yield return TestUtils.WaitForDelayCall();
        yield return TestUtils.WaitForDelayCall();

        ProfilerHelpers.StopProfilerRecording();

        Assert.IsFalse(ProfilerDriver.enabled);
    }

    [UnityTest]
    public IEnumerator OpenProfiler()
    {
        EditorWindow theProfilerWindow = null;
        ProfilerHelpers.OpenProfiler(k_ApplicationTickMarker, profilerWindow =>
        {
            theProfilerWindow = profilerWindow;
        });
        yield return TestUtils.WaitForDelayCall();
        yield return TestUtils.WaitForDelayCall();

        Assert.IsNotNull(theProfilerWindow);

        theProfilerWindow.Close();
    }

    [UnityTest]
    public IEnumerator OpenProfilerReport()
    {
        ProfilerDriver.ClearAllFrames();
        ProfilerDriver.profileEditor = true;
        yield return null;
        ProfilerDriver.enabled = true;
        yield return null;
        ProfilerDriver.enabled = false;
        yield return null;

        var profileSaveFilePath = ProfilerHelpers.SaveProfileReport(k_ApplicationTickMarker);
        yield return null;

        EditorWindow profilerWindow = null;
        ProfilerHelpers.OpenProfileReport(profileSaveFilePath, k_ApplicationTickMarker, win =>
        {
            profilerWindow = win;
        });
        yield return TestUtils.WaitForDelayCall();
        yield return TestUtils.WaitForDelayCall();

        Assert.IsNotNull(profilerWindow);

        File.Delete(profileSaveFilePath);

        profilerWindow.Close();
    }
}

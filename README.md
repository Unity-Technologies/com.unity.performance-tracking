# About Performance Tracking

This package expose a bunch of workflows and utilities to track, monitor and help profile the general performance of Unity Editor.

Quick [video](https://www.youtube.com/watch?feature=player_embedded&v=dtUKEBn5iuA) recapping the features of the package:

<a href="https://www.youtube.com/watch?feature=player_embedded&v=dtUKEBn5iuA" target="_blank"><img src="https://img.youtube.com/vi/dtUKEBn5iuA/0.jpg"
alt="IMAGE ALT TEXT HERE" width="240" height="180" border="10" /></a>

The core of these monitoring utilities is in the Performance Tracker Window (available in version 2020.1):

![Performance Tracker](https://files.slack.com/files-pri/T06AF9667-FNRD6JJ1L/image.png?pub_secret=cf9533c0e4)

And in the Profiling Snippet Window and API:

![snippet](Documentation~/images/profiling_snippet_window.gif)

For more information see our documentation:

* [About Performance Tracking](Documentation~/index.md)
* [Performance Tracker Window](Documentation~/performance-tracker-window.md)
* [Performance Monitoring Actions](Documentation~/performance-window-actions.md)
* [Monitoring](Documentation~/monitoring.md)
* [Profiling Snippet Window](Documentation~/profiling-snippet-window.md)
* [API](Documentation~/api.md)

# Installing Package

To install this package, follow the instructions in the [Package Manager documentation](https://docs.unity3d.com/Packages/com.unity.package-manager-ui@latest/index.html).

Note that Performance tracker viewer is a *Preview Package* and that you need to use the [Advanced button](https://docs.unity3d.com/Packages/com.unity.package-manager-ui@2.1/manual/index.html#advanced) to have access to that kind of packages.

## Requirements

This version of Performance Tracker is compatible with the following versions of the Unity Editor:

* 2018.3: for ProfilerHelpers API and Profiling Snippet Window
* 2020.1 and later for Performance Tracker Window, Monitoring Service and Performance Bug Reporting

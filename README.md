# About Performance Tracking

This package expose a bunch of workflows and utilities to track, monitor and help profile the general performance of Unity Editor.

The core of these monitoring utilities is in the Performance Tracker Window (available in version 2019.3):

![task manager](Documentation~/images/performance-tracker-window.png)

And in the Profiling Snippet Window and API:

![snippet](Documentation~/images/profiling_snippet_window.gif)

For more information see our documentation:

* [About Performance Tracking](Documentation~/index.md)
* [Performance Tracker Window](Documentation~/performance-tracker-window.md)
* [Performance Monitoring Actions](Documentation~/performance-window-actions.md)
* [Monitoring](Documentation~/monitoring.md)
* [Profiling Snippet Window](Documentation~/profiling-snippet-window.md)
* [API](Documentation~/api.md)

## Installation

### Git URL
Use the package manager:
- Click the plus icon
- Click **Add package from git URL**

![install](Documentation~/images/git-url.png)

- Enter `https://github.com/Unity-Technologies/com.unity.performance-tracking.git`

### Local Installation
- Download the package. Unzip it somewhere on your disk.
![download](Documentation~/images/download.png)
- Either **embed** the package itself in your project by copying the package in the Packages folder of your project.
![embed](Documentation~/images/emded.png)

- Or Use the package manager:
    - Click the plus icon
    - Click **Add package from disk**    
    ![install](Documentation~/images/add-package.png)
    - Select the package.json file in the com.unity.performance-tracking-master folder.
    ![install](Documentation~/images/select-package.png)

    
## Requirements

This version of Performance Tracker is compatible with the following versions of the Unity Editor:

* 2019.4 and later for Performance Tracker Window, Monitoring Service and Performance Bug Reporting

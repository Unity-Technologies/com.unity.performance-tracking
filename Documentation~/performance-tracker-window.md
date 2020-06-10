# Performance Tracker Window

The Performance Tracker Window monitors all [Performance Trackers](api.html#tracker-api) similar to how *Task Manager* monitors Windows processes:

![task manager](images/task-manager.png)

![task manager](images/performance-tracker-window.png)

Note that this window is available in Unity 2020.1 and onward.

## Window configuration

The Performance Tracker Window is mostly about monitoring but it can be customized to make performance gathering more efficient. The toolbar allows to configure how we report performance data and which tracker are to be displayed.

![config](images/window-configuration-toolbar.png)

- **Udpate Speed**: Speed at which we refresh all the tracker and the view itself. 
- **Columns Selector**: Decide which columns to show. Tracker name and Action columns are always visible.
- **Sorty By**: Which columns is used for sorting. *Alternatively you can click a column header to sort all tracker according to this column.*
- **Cog Icon**: Clicking this button will open up the Performance tracking preferences.

## Tracker Categories

A lot of different Editor workflows have been instrumented with Performance Tracker. Some trackers have name that have common rprefix/suffix to identify common workflows:

- `WinEditorMain.*` : Unity initialization sequence
- `Application.*` : (mostly) Unity initialization sequence
  - `Application.DomainReload`: you know what it is
- `<Any EditorWindow>.Paint` : Paint sequence for a given EditorWindow
  - `<Any EditorWindow>.OnGUI.repaint`
  - `<Any EditorWindow>.OnGUI.mouseMove`
  - `<Any EditorWindow>.OnGUI.mouseLeaveWindow`
  - `<Any EditorWindow>.OnGUI.mouseenterWindow`
  - `<Any EditorWindow>.OnGUI.layout`
- `*.Tick`: all the various Tick Timers of the Editor


## Tracker Columns
- **Pin**: can be used to pin a marker so it stays always on top
- **Sample Count**: number of time this tracker was hit. Clicking on the Sample Count resets it to zero (and trigger an average computation)
- **Age**: how much time since that trakcer was last hit
- **Peak Time**: Highest time for this tracker
- **Average Time**:  Average time for all Samples
- **Last Time**: last tracker time
- **Actions**: See below for more information on which actions are possible for a specific Tracker.

All the columns can be sorted.

## Pins

Using the first column you can *pin* tracker you want to keep on top of the view for closer monitoring. When sorting all pinned tracker are sorted together and all the rest of the trackers are sorted separately.

![pin](images/pin-workflow.png)

## Action Column

The last column has an action button allowing you to execute a specific set of actions on a specific tracker:

![actions](images/tracker-window-actions.png)

- **set as search filter**: take the current tracker name and set it as the filter of the Window (effectively keeping this tracker the only one on screen)
- **Add performance notification**: Add a [performance notification](monitoring.html#performance-notifications) for this tracker
![add notif](images/tracker-window-add-notification-action.gif)
- **Profile...**: start the profiler (without popping the window) and filter all samples **so we only keep the data relative to this tracker**.
- **Deep Profile...**: start the profiler in deep profile mode (without popping the window) and filter all samples *so we only keep the data relative to this tracker*.

For both profiling action, the action button will change to a *Stop profiling* button. Pressing this button will stop recording AND will open up the Profiler window.

![profiler](images/tracker-window-profiler.gif)
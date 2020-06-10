# Performance Monitoring

When the Performance Tracking package is included in a project, it can track and automatically reports Performance spikes in the editor.

Performance monitoring happens in the background and can be scheduled using the Performance Tracking preference page:

![monitor](images/performance-tracking-monitoring.png)

By default we monitor performance events each 5 seconds.

## Performance Notifications

You can setup notifications that will trigger when a specific Tracker (or a filtered) Tracker has its *Average Time* value be over a specific threshold. This can be great to track under the hood events that might stall the editor (Application.Reload, Application.Tick...).

If you open the Performance Tracking Preferences page you can tweak which Tracker will fire Performance notification:

![pref-notif](images/preference-notification.png)

Notice how using a *partial* tracker name (ex: `.Tick`) will track **ALL** markers containing this partial name. In this example all these *ticking* trackers would be monitored:

![track](images/notification-partial-tracking-tick.png)

When a performance notification is triggered, it will be printed in the console:

![notif](images/performance-notification-in-console.png)

## Editor Window Paint Spike Highlight

If you enable *Spike Window Highlight* from the Performance tracking preference page, each Editor Window will will be monitored for performance spike happening while repainting the window (repaints include all OnGUI, Layout, and painting operations). A spike happens when the `Average Time` of a *Paint* Tracker is over a specific customizable threshold.

When a spike happens, the EditorWindow will be highlighted either in yellow (*warning*) or in red (*critical*).

You can also enable Inspector Components highlight and we will monitor if any *Inspector Editor* is taking too much time in their `OnInspectorGUI` function.

![spike highlight](images/preference-spike-highlight.png)

This editor is being warned! :

![spike highlight](images/all-warnings.png)
# Analytics

When using the Performance Tracking package Analytics will be sent to Unity so we can aggregate statistics on the editor performance. Each time a user closes Unity we send a report of all Performance Tracker statistics.

This report can also be consulted in the editor log file of Unity (Editor log file can be found at `<User Folder>/AppData/Local/Unity/Editor/Editor.log` on Windows).

The report looks like this and can be found at the end of the Editor.log after Unity has closed:

```
WinEditorMain : 1 samples, Peak.  1.45 s (1.0x), Avg.  1.45 s, Total. 1.446 s (0.2%)
WinEditorMain.ConfigureCrashHandler: 1 samples, Peak.  20.1 us (1.0x), Avg.  20.1 us, Total. 20.10 us (0.0%)
WinEditorMain.CurlRequestInitialize: 1 samples, Peak.  2.12 ms (1.0x), Avg.  2.12 ms, Total. 2.123 ms (0.0%)
WinEditorMain.SetupLogFile: 1 samples, Peak.  1.33 ms (1.0x), Avg.  1.33 ms, Total. 1.334 ms (0.0%)
WinEditorMain.RunNativeTestsIfRequiredAndExit: samples, Peak.  2.80 us (1.0x), Avg.  2.80 us, Total. 2.800 us (0.0%)
CurlRequestCheck: 19 samples, Peak.  92.1 ms (10.7x), Avg.  8.64 ms, Total. 164.1 ms (0.0%)
PackageManager::RunRequestSynchronously: 10 samples, Peak.  11.8 ms (3.0x), Avg.  3.96 ms, Total. 39.56 ms (0.0%)
....
```


# Profiling Snippet Window

The goal of this window is to help benchmark or profile specific snippet of code in a more deterministic fashion.

Here is a quick example of the Window in action:

![images](images/profiling_snippet_window.gif)

As you can see there are mainly 2 workflows:

- **Profiling**: execute the snippet of code while profiling it in the background. We then open the profiler ready to investigate the snippet: the frame is already selected and the filter search field is already populated.
- **Benchmark**: execute the snippet of code and benchmark its execution. It then logs the result in file (and at the console).

## Snippet List View

This list view shows all snippets registered in your project. By default we are populated the list with 2 types of snippet:

- Repaint: for each EditorWindow in the Editor, we generate a snippet that will be able to open the Window and benchmark its `RepaintImmediatly` function.

![repaint](images/repaint_snippet.png)

- Static: we extract all static C# API with no parameters. You can then execute those snippet and run benchmarks.

![static](images/static_snippet.png)

### Registering custom Snippet

A user can also register its own Snippet using the `ProfilingSnippet` C# Attribute:

```CSharp
[ProfilingSnippetAction("Test_something_long_id", 
    "Test something long", 
    "Test Category", 
    "Test_something_long_sample")]
static void TestProfilingSnippetActionAttr(object preExecutePayload, ProfilingSnippet snippet, ProfilingSnippetOptions options)
{
    DoSomethingLong();
}
```

Alternatively you can register a function that will create a new instance of `ProfilingSnippet` if you want more control over the Snippet execution. To do so you use the `ProfilingSnippet` attribute:

```CSharp
[ProfilingSnippet]
static ProfilingSnippet TestProfilingSnippetAttr()
{
    var snippet = new ProfilingSnippet("Test_something_else_long_id", "Test something else long");
    // Set the snippet category
    snippet.category = "Test";

    // Set the sample name that will be used when profiling the snippet
    snippet.sampleName = "Test_something_else_long_sample";

    // Set the performance tracker name that will be used when profiling 
    // with marker filtering.
    snippet.marker = "Test_something_else_long_marker";
    snippet.executeFunc = (preExecutePayload, s, options) =>
    {
        DoSomethingLong();
    };
    return snippet;
}
```

## Snippet Profiling Actions

There are 6 (4 if 18.4->19.3) different actions available with Snippet. Four actions can be used for profiling and 2 for benchmarking.

![profiling](images/profiling_actions.png)

### Profile (Deep)

The Profile action, uses the profiler in the background to profile a Snippet. Basically it does the following:

```CSharp
Profiler.BeginSample(snippetSampleName);
executeMySnippet();
Profiler.EndSample();
```

![](images/profiler-workflow.gif)

Notice how the profiler opens already at the right frame with the proper sample name already populated in the the profiler search field.

The profiling trace into the `Assets/Profiles` folder and open the profile log in the profiler:

![](images/profiler-console-trace.png)

If you chose `Profile - Deep` it record the trace in Deep Profile mode. Note that **the first time** you activate `Profiler - Deep` Unity needs to domain reload all script. You will then need to press the `Profile - Deep` button again.

### Profiling Performance Tracker (for Unity 2020.1+)

`Profile Marker` is similar to profile but instead of creating a Profiler Sample it uses Marker Filtering to record an already existing Performance tracker. In the case of `Repaint` snippet, each EditorWindow rrepaing is already instrumented with a performance tracker named `<ViewName>.Paint`.

Marker filtering is great because it filers out all the data that is not directly part of the callstack of the marker:

![](images/profiler-marker.png)

Marker filtering **is only available for Unity 2020.1 and onward**.

### Benchmark

Benchmark execute the snippet a given amount of times (see Options below) and record the total time, peak time, min time, average and medium time. Those benchmarks result are logged in the Console Window as well as in the `Assets/Profiles/ProfilingSnippet.log` file:

![](images/benchmark-log.png)

### Benchmark Marker (for Unity 2020.1+)

If you use `Benchmark Marker` we actually track Performance Tracker samples invoked in the sample. We effectively reset the samples count then execute the snippet a given amount of time and report about the Performance Marker usage.

See [Performance Tracker Window](performance-tracker-window.md) for more information on Performance Tracker.

## Options

The options box contains various options that can be used by Profiling Actions:

![](images/profiling-options.png)

- Maximize Window: use by **Repaint Snippets**. If this is toggled it will maximize the window before executing repainting.
- Standalone Window: use by **Repaint Snippets**. If this is toggled it will close ay Editor Windowe corresponding to the Snippet and open a new window undocked.
- Nb Iterations: use by both **Benchmark** actions and **profile** actions. This is the number of time we execute the snippet.
- Log File: where Benchmark results are logged.
- Clear: clear the log file.
- CSV: log the benchmark in CSV mode

Here is an example of CSV logging:
```
100,0.121397,1.21397,1.171,2.5508,0.9343
100,0.120124,1.20124,1.1082,3.784,0.9271
```





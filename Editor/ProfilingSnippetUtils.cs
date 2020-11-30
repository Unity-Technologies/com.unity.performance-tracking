// #define SNIPPET_DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEditor.Profiling;

namespace Unity.PerformanceTracking
{
    public static class ProfilingSnippetUtils
    {
        static MethodInfo m_RepaintImmediatly;
        static object[] m_NoArgs = new object[0];

        static ProfilingSnippetUtils()
        {
            m_RepaintImmediatly = typeof(EditorWindow).GetMethod("RepaintImmediately", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        public static void RepaintImmediatlyWindow(EditorWindow window)
        {
            m_RepaintImmediatly.Invoke(window, m_NoArgs);
        }

        public static string FormatBenchmarkResult(string title, ProfilerHelpers.BenchmarkResult result, bool csv = false)
        {
            if (csv)
                return $"{result.sampleCount},{result.totalInSecond},{result.avgInMs},{result.medianInMs},{result.peakInMs},{result.minInMs}";
            else
                return $"{title} samples: {result.sampleCount} - total: {FormatNumber(result.totalInSecond)}s - avg: {FormatNumber(result.avgInMs)}ms - median: {FormatNumber(result.medianInMs)}ms - peak: {FormatNumber(result.peakInMs)}ms - min: {FormatNumber(result.minInMs)}ms";
        }

        static string FormatNumber(double d)
        {
            return d.ToString("F4");
        }

        public static IEnumerator OpenStandaloneWindowMaximized(string viewTypeName)
        {
            var windowType = ProfilerHelpers.GetUnityEditorType(viewTypeName);
            var window = EditorWindow.GetWindow(windowType);
            if (window)
                window.Close();
            yield return null;
            window = EditorWindow.GetWindow(windowType);
            yield return null;
        }

        public static EditorWindow OpenStandaloneWindow(string viewTypeName)
        {
            var windowType = ProfilerHelpers.GetUnityEditorType(viewTypeName);
            return OpenStandaloneWindow(windowType);
        }

        public static EditorWindow OpenStandaloneWindow(System.Type windowType)
        {
            var window = EditorWindow.GetWindow(windowType);
            if (window)
                window.Close();
            return EditorWindow.GetWindow(windowType);
        }

        public static void MaximizeWindow(EditorWindow window)
        {
            var m_ParentField = window.GetType().GetField("m_Parent", BindingFlags.Instance | BindingFlags.NonPublic);
            var parent = m_ParentField.GetValue(window);
            var windowProperty = parent.GetType().GetProperty("window", BindingFlags.Instance | BindingFlags.Public);
            var containerWindow = windowProperty.GetValue(parent);
            var ToggleMaximizeMethod = containerWindow.GetType().GetMethod("ToggleMaximize", BindingFlags.Instance | BindingFlags.Public);
            ToggleMaximizeMethod.Invoke(containerWindow, new object[0]);
        }

        public static EditorWindow SetupWindow(System.Type editorWindowType, ProfilingSnippetOptions options)
        {
            EditorWindow window = null;
            window = options.standaloneWindow ? OpenStandaloneWindow(editorWindowType) : EditorWindow.GetWindow(editorWindowType);

            if (options.maximizeWindow)
            {
                MaximizeWindow(window);
            }

            return window;
        }

        static ProfilingSnippet[] m_AllSnippets;

        public static ProfilingSnippet[] FetchAllSnippets()
        {
            if (m_AllSnippets == null)
            {
                var repaintSnippets = ProfilerHelpers.GetUnityEditorTypesImplementing(typeof(EditorWindow)).OrderBy(t => t.Name)
                    .Select(type => CreateSnippetFromEditorWindowType(type));

                bool isDevBuild = UnityEditor.Unsupported.IsDeveloperBuild();
                var staticMethods = AppDomain.CurrentDomain.GetAllStaticMethods(isDevBuild);
                var staticSnippets = staticMethods
                    .Select(mi => CreateSnippetFromStaticMethod(mi));

                var menuSnippets = GetMenuSnippets();

                var profilingSnippetActions = Utils.GetAllMethodsWithAttribute<ProfilingSnippetActionAttribute>().Select(methodInfo =>
                {
                    return CreateSnippetFromProfilingSnippetActionAttr(methodInfo);
                });

                var profilingSnippets = Utils.GetAllMethodsWithAttribute<ProfilingSnippetAttribute>().Select(methodInfo =>
                {
                    return CreateSnippetFromProfilingSnippetAttr(methodInfo);
                });


                m_AllSnippets = repaintSnippets
                    .Concat(staticSnippets)
                    .Concat(menuSnippets)
                    .Concat(profilingSnippetActions)
                    .Concat(profilingSnippets)
                    .Where(snippet => snippet != null)
                    .ToArray();
            }

            return m_AllSnippets;
        }

#if SNIPPET_DEBUG
        [MenuItem("ProfilerHelpers/Refresh Snippets")]
        public static void RefreshSnippets()
        {
            m_AllSnippets = null;
            FetchAllSnippets();
        }
#endif

        public static ProfilingSnippet CreateSnippetFromEditorWindowType(System.Type editorWindowType)
        {
            var newSnippet = new ProfilingSnippet($"repaint_{editorWindowType.FullName}", $"{editorWindowType.Name} ({editorWindowType.Namespace})");
            newSnippet.category = "Repaint";
            newSnippet.preExecuteFunc = (snippet, options) => SetupWindow(editorWindowType, options);
            newSnippet.executeFunc = (editorWindowObj, snippet, options) =>
            {
                var editorWindow = editorWindowObj as EditorWindow;
                RepaintImmediatlyWindow(editorWindow);
            };
            newSnippet.sampleName = $"{editorWindowType.Name}_Paint";
            newSnippet.markerName = $"{editorWindowType.Name}.Paint";

            return newSnippet;
        }

        static Dictionary<string, MethodInfo> staticCache = new Dictionary<string, MethodInfo>();

        static IEnumerable<ProfilingSnippet> GetMenuSnippets()
        {
            var outItemNames = new List<string>();
            var outItemDefaultShortcuts = new List<string>();
            Assembly assembly = typeof(Menu).Assembly;
            var managerType = assembly.GetTypes().First(t => t.Name == "Menu");
            var method = managerType.GetMethod("GetMenuItemDefaultShortcuts", BindingFlags.NonPublic | BindingFlags.Static);
            var arguments = new object[] { outItemNames, outItemDefaultShortcuts };
            method.Invoke(null, arguments);

            return outItemNames.Select(menuItemName =>
            {
                var snippet = new ProfilingSnippet($"menu_{menuItemName}", menuItemName, menuItemName.Replace("/", "_"));
                snippet.category = "Menu";
                snippet.executeFunc = (preExecutePayload, s, options) =>
                {
                    EditorApplication.ExecuteMenuItem(s.label);
                };

                return snippet;
            });
        }

        public static ProfilingSnippet CreateSnippetFromStaticMethod(MethodInfo mi)
        {
            var fullName = $"{mi.Name} ({mi.DeclaringType.FullName})";
            var newSnippet = new ProfilingSnippet($"{mi.DeclaringType.FullName}_{mi.Name}_{mi.GetHashCode()}", fullName);
            newSnippet.category = "Static";
            newSnippet.executeFunc = (dummy, snippet, options) =>
            {
                mi.Invoke(null, null);
            };
            newSnippet.sampleName = $"static_{mi.Name}";

            return newSnippet;
        }

        const string kSignatureMismatch = "Signature mismatch: [ProfilingSnippetAction] should be void MySnippet(object preExecutePayload, ProfilingSnippet snippet, ProfilingSnippetOptions options) or void MySnippet()";

        public static ProfilingSnippet CreateSnippetFromProfilingSnippetActionAttr(MethodInfo mi)
        {
            var attr = mi.GetCustomAttributes(typeof(ProfilingSnippetActionAttribute), false).Cast<ProfilingSnippetActionAttribute>().First();
            var snippet = new ProfilingSnippet(attr.idStr ?? mi.Name, attr.label, attr.sampleName, attr.markerName);
            try
            {
                snippet.category = attr.category;
                var inputParams = mi.GetParameters();
                if (mi.GetParameters().Length == 3)
                {
                    snippet.executeFunc = Delegate.CreateDelegate(typeof(Action<object, ProfilingSnippet, ProfilingSnippetOptions>), mi) as
                        Action<object, ProfilingSnippet, ProfilingSnippetOptions>;
                }
                else if (mi.GetParameters().Length == 0)
                {
                    var noParamAction = Delegate.CreateDelegate(typeof(Action), mi) as Action;
                    snippet.executeFunc = (payload, _snippet, options) => noParamAction();
                }
                else
                {
                    Debug.LogError($"Error while creating ProfilingSnippet {snippet.label}. {kSignatureMismatch}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error while creating ProfilingSnippet {snippet.label}. {kSignatureMismatch} - {ex.Message}");
                return null;
            }
            return snippet;
        }

        public static ProfilingSnippet CreateSnippetFromProfilingSnippetAttr(MethodInfo mi)
        {
            var attr = mi.GetCustomAttributes(typeof(ProfilingSnippetAttribute), false).Cast<ProfilingSnippetAttribute>().First();
            try
            {
                var snippet = mi.Invoke(null, null) as ProfilingSnippet;
                return snippet;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error while creating ProfilingSnippet: {ex.Message}");
                return null;
            }

        }

        public static Action GetMarkerEnclosedSnippetAction(object preExecutePayload, ProfilingSnippet snippet, ProfilingSnippetOptions options)
        {
            var snippetAction = snippet.GetSnippetAction(preExecutePayload, options);
            if (!snippet.isValidMarker)
            {
                // Generate marker:
                var markerName = snippet.GetValidMarkerName();
                return () =>
                {
                    using (new PerformanceTracker(markerName))
                    {
                        snippetAction();
                    }
                };
            }

            return snippetAction;
        }

        #region TestProfilingSnippetAttribute
        static void DoSomethingLong()
        {
            var str = "";
            for (var i = 0; i < 100; ++i)
            {
                str += GUID.Generate().ToString();
            }
        }

        [ProfilingSnippetAction("Test_something_long_id", "Test something long", "Test", "Test_something_long_sample")]
        static void TestProfilingSnippetActionAttr(object preExecutePayload, ProfilingSnippet snippet, ProfilingSnippetOptions options)
        {
            DoSomethingLong();
        }

        [ProfilingSnippetAction("Test_something_long_no_param_id", "Test something long (no param)", "Test")]
        static void TestProfilingSnippetActionAttrNoParam()
        {
            DoSomethingLong();
        }

        [ProfilingSnippet]
        static ProfilingSnippet TestProfilingSnippetAttr()
        {
            var snippet = new ProfilingSnippet("Test_something_else_long_id", "Test something else long");
            snippet.category = "Test";
            snippet.sampleName = "Test_something_else_long_sample";
            snippet.executeFunc = (preExecutePayload, s, options) =>
            {
                DoSomethingLong();
            };
            return snippet;
        }
        #endregion
    }

    public class ProfilingAction
    {
        public string name;
        public Func<object, ProfilingSnippet, ProfilingSnippetOptions, object> action;
        public bool defaultSet;
    }

    public class ProfilingActionAttribute : Attribute
    {
        public string name;
        public ProfilingActionAttribute(string name)
        {
            this.name = name;
        }
    }

    public class ProfilingSnippet
    {
        string m_SampleName;
        string m_Category;

        public ProfilingSnippet(string idStr, string label = null, string sampleName = null, string markerName = null)
        {
            this.idStr = idStr;
            this.label = label ?? idStr;
            this.sampleName = sampleName;
            this.markerName = markerName;
        }

        public string label;
        public string idStr;
        public int id => idStr.GetHashCode();

        internal bool ranOnce { get; set; }

        public string category
        {
            get
            {
                if (string.IsNullOrEmpty(m_Category))
                {
                    return "General";
                }

                return m_Category;
            }
            set => m_Category = value;
        }

        public string sampleName
        {
            get
            {
                if (string.IsNullOrEmpty(m_SampleName))
                {
                    return label.Replace(" ", "_");
                }

                return m_SampleName;
            }
            set => m_SampleName = value;
        }

        public string markerName;
        public bool isValidMarker => !string.IsNullOrEmpty(markerName);

        public string GetValidMarkerName()
        {
            return isValidMarker ? markerName : sampleName;
        }

        public Func<ProfilingSnippet, ProfilingSnippetOptions, object> preExecuteFunc;
        public Action<object, ProfilingSnippet, ProfilingSnippetOptions> executeFunc;
        public Action<object, ProfilingSnippet, ProfilingSnippetOptions> postExecuteFunc;

        public object PreExecute(ProfilingSnippetOptions options)
        {
            return preExecuteFunc?.Invoke(this, options);
        }

        public Action GetSnippetAction(object preExecutePayload, ProfilingSnippetOptions options)
        {
            return () => { executeFunc(preExecutePayload, this, options); ranOnce = true; };
        }

        public void PostExecute(object executePayload, ProfilingSnippetOptions options)
        {
            postExecuteFunc?.Invoke(executePayload, this, options);
        }

        public override int GetHashCode()
        {
            return id;
        }
    }

    public class ProfilingSnippetAttribute : Attribute
    {
        public ProfilingSnippetAttribute()
        {
        }
    }

    public class ProfilingSnippetActionAttribute : Attribute
    {
        public string idStr;
        public string label;
        public string sampleName;
        public string markerName;
        public string category;
        public ProfilingSnippetActionAttribute(string idStr = null, string label = null, string category = "User", string sampleName = null, string markerName = null)
        {
            this.idStr = idStr;
            this.label = label;
            this.category = category;
            this.sampleName = sampleName;
            this.markerName = markerName;
        }
    }

    public class ProfilingSnippetOptions
    {
        public int count;
        public bool maximizeWindow;
        public bool standaloneWindow;
        public bool warmup;
        public bool csvLog;
        public string logFile;

        public void Log(string str)
        {
            using (var sw = File.AppendText(logFile))
            {
                sw.WriteLine(str);
            }
        }
    }
}
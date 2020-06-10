using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Reflection;
using UnityEditor.IMGUI.Controls;
using static UnityEditor.EditorGUI;
using UnityEditorInternal;

namespace Unity.PerformanceTracking
{
    public class ProfilingSnippetWindow : EditorWindow
    {
        const string k_KeyPrefix = "profilingsnippetwindow";
        const string k_LogFile = "Library/ProfilingSnippet.log";

        ProfilingSnippet[] m_AllSnippets;
        List<ProfilingAction> m_Actions;
        string m_SearchValue;
        Vector2 m_ActionScroll = new Vector2();
        Vector2 m_HistoryScroll = new Vector2();
        bool m_InitialListSize;
        SnippetListView m_SnippetListView;
        ProfilingSnippetOptions m_Options;
        List<HistoryItem> m_History;
        int m_HistoryCursor;
        float m_SplitterWidth = 5;
        Rect m_SplitterRect;
        bool m_DraggingSplitter;

        [SerializeField] float m_SplitterPos = 200f;

        class HistoryItem
        {
            public string search;
            public string idStr;

            public bool IsValid()
            {
                return !string.IsNullOrEmpty(search) && idStr != null;
            }

            public override string ToString()
            {
                if (!IsValid())
                {
                    return "invalid";
                }

                return $"{search}:{idStr}";
            }
        }

        static class Styles
        {
            public static GUIStyle box = new GUIStyle("OL box")
            {
                padding = new RectOffset(0, 0, 5, 0),
                margin = new RectOffset(2, 2, 0, 0),
            };

            public static readonly GUIStyle link = new GUIStyle(EditorStyles.label)
            {
                name = "snippet-link-label",
                normal = new GUIStyleState { textColor = new Color(79 / 255f, 128 / 255f, 248 / 255f) },
                margin = new RectOffset(4, 4, 2, 2),
                padding = new RectOffset(2, 2, 1, 1)
            };

            public static readonly GUIStyle clearButton = new GUIStyle("button")
            {
                margin = new RectOffset(4, 4, 2, 2),
                padding = new RectOffset(2, 2, 1, 1)
            };

            public static readonly GUIStyle navBtn = new GUIStyle(EditorStyles.miniButton)
            {
                name = "snippet-nav-btn",
                fixedWidth = 25
            };

            public static readonly GUIStyle actionBtn = new GUIStyle("button")
            {
                fixedWidth = 200,
                margin = new RectOffset(3, 3, 0, 1)
            };

            public static readonly GUIContent warmupLabelContent = new GUIContent("Warm up", "Execute the snippet action a few times before benchmarking");
            public static readonly GUIContent deepProfileLabelContent = new GUIContent("Deep Profile");
            public static readonly GUIContent maximizeWindowContent = new GUIContent("Maximize Window");
            public static readonly GUIContent standaloneWindowContent = new GUIContent("Standalone Window");
            public static readonly GUIContent countContent = new GUIContent("Nb Iterations");
            public static readonly GUIContent logContent = new GUIContent("Log File");
        }

        [UsedImplicitly, MenuItem("Window/Analysis/Snippet Profiling")]
        private static void ShowWindow()
        {
            var perfWindow = GetWindow<ProfilingSnippetWindow>();
            perfWindow.Show();
            perfWindow.m_SplitterPos = perfWindow.position.width * 0.6f;
        }

        private static IEnumerable<ProfilingAction> GetProfilingSnippetActions()
        {
            return Utils.GetAllMethodsWithAttribute<ProfilingActionAttribute>().Select(methodInfo =>
            {
                var action = new ProfilingAction();
                action.name = methodInfo.GetCustomAttributes(typeof(ProfilingActionAttribute), false).Cast<ProfilingActionAttribute>().First().name;
                try
                {
                    if (methodInfo.ReturnType == typeof(void))
                    {
                        var d = Delegate.CreateDelegate(typeof(Action<object, ProfilingSnippet, ProfilingSnippetOptions>), methodInfo) as Action<object, ProfilingSnippet, ProfilingSnippetOptions>;
                        action.action = (o, s, options) =>
                        {
                            d(o, s, options);
                            return null;
                        };
                    }
                    else
                    {
                        action.action = Delegate.CreateDelegate(typeof(Func<object, ProfilingSnippet, ProfilingSnippetOptions, object>), methodInfo) as Func<object, ProfilingSnippet, ProfilingSnippetOptions, object>;
                    }

                }
                catch (Exception ex)
                {
                    Debug.LogError($"Cannot create delegate for ProfilingAction: {action.name}: {ex.Message}");
                    return null;
                }

                return action;
            }).Where(a => a != null);
        }

        void OnEnable()
        {
            titleContent = new GUIContent("Profiling");

            m_AllSnippets = ProfilingSnippetUtils.FetchAllSnippets();
            m_History = new List<HistoryItem>();
            m_Actions = GetProfilingSnippetActions().ToList();

            foreach (var action in m_Actions)
                action.defaultSet = EditorPrefs.GetBool(GetKeyName($"action_{action.name}_default_set"), false);

            if (!Directory.Exists(ProfilerHelpers.k_DefaultProfileSaveDirectory))
                Directory.CreateDirectory(ProfilerHelpers.k_DefaultProfileSaveDirectory);
            if (!File.Exists(k_LogFile))
            {
                CreateLogFile();
            }

            m_SnippetListView = new SnippetListView(m_AllSnippets);
            m_SnippetListView.doubleClicked += HandleSnippetListDoubleClick;

            m_Options = new ProfilingSnippetOptions()
            {
                logFile = k_LogFile,
                count = 1
            };

            m_Options.warmup = EditorPrefs.GetBool(GetKeyName($"options_{nameof(m_Options.warmup)}"), m_Options.warmup);
            m_Options.maximizeWindow = EditorPrefs.GetBool(GetKeyName($"options_{nameof(m_Options.maximizeWindow)}"), m_Options.maximizeWindow);
            m_Options.standaloneWindow = EditorPrefs.GetBool(GetKeyName($"options_{nameof(m_Options.standaloneWindow)}"), m_Options.standaloneWindow);
            m_Options.count = EditorPrefs.GetInt(GetKeyName($"options_{nameof(m_Options.count)}"), m_Options.count);
            m_Options.csvLog = EditorPrefs.GetBool(GetKeyName($"options_{nameof(m_Options.csvLog)}"), m_Options.csvLog);

            m_SearchValue = EditorPrefs.GetString(GetKeyName(nameof(m_SearchValue)));
            var currentSnippetIdStr = EditorPrefs.GetString(GetKeyName("CurrentSnippetId"));
            SetCurrentFromId(currentSnippetIdStr);

            var historyStr = EditorPrefs.GetString(GetKeyName(nameof(m_History)));
            if (!string.IsNullOrEmpty(historyStr))
            {
                var historyItemTokens = historyStr.Split(';');
                foreach (var historyItemToken in historyItemTokens)
                {
                    var itemTokens = historyItemToken.Split(':');
                    if (itemTokens.Length != 2)
                        continue;
                    var possibleSnippetId = itemTokens[1];
                    var possibleSnippet = m_AllSnippets.FirstOrDefault(snippet => snippet.idStr == possibleSnippetId);
                    if (possibleSnippet != null)
                        PushHistory(itemTokens[0], possibleSnippet);
                }
            }
        }

        void OnDisable()
        {
            EditorPrefs.SetString(GetKeyName(nameof(m_SearchValue)), m_SearchValue);
            var currentSnippet = CurrentSnippet();
            EditorPrefs.SetString(GetKeyName("CurrentSnippetId"), currentSnippet?.idStr);

            // Persist history as a big string
            var toPersistHistoryItems = m_History;
            if (toPersistHistoryItems.Count > 10)
            {
                toPersistHistoryItems = new List<HistoryItem>(10);
                for (var i = m_History.Count - 10; i < m_History.Count; ++i)
                    toPersistHistoryItems.Add(m_History[i]);
            }

            var historyStr = string.Join(";", toPersistHistoryItems.Select(item => item.ToString()));
            EditorPrefs.SetString(GetKeyName(nameof(m_History)), historyStr);

            EditorPrefs.SetBool(GetKeyName($"options_{nameof(m_Options.warmup)}"), m_Options.warmup);
            EditorPrefs.SetBool(GetKeyName($"options_{nameof(m_Options.maximizeWindow)}"), m_Options.maximizeWindow);
            EditorPrefs.SetBool(GetKeyName($"options_{nameof(m_Options.standaloneWindow)}"), m_Options.standaloneWindow);
            EditorPrefs.SetInt(GetKeyName($"options_{nameof(m_Options.count)}"), m_Options.count);
            EditorPrefs.SetBool(GetKeyName($"options_{nameof(m_Options.csvLog)}"), m_Options.csvLog);

            foreach (var action in m_Actions)
                EditorPrefs.SetBool(GetKeyName($"action_{action.name}_default_set"), action.defaultSet);
                
        }

        void OnGUI()
        {
            if (Event.current.type == EventType.KeyDown &&
                Event.current.keyCode == KeyCode.DownArrow &&
                GUI.GetNameOfFocusedControl() == "SearchField")
            {
                m_SnippetListView.SetFocusAndEnsureSelectedItem();
                Event.current.Use();
            }
            else if (Event.current.type == EventType.KeyDown &&
                Event.current.keyCode == KeyCode.UpArrow &&
                m_SnippetListView.HasFocus() &&
                m_SnippetListView.IsFirstItemSelected())
            {
                EditorGUI.FocusTextInControl("SearchField");
                Event.current.Use();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawList();

                // Splitter
                GUILayout.Box("",
                    GUILayout.Width(m_SplitterWidth),
                    GUILayout.MaxWidth(m_SplitterWidth),
                    GUILayout.MinWidth(m_SplitterWidth),
                    GUILayout.ExpandHeight(true));
                m_SplitterRect = GUILayoutUtility.GetLastRect();
                EditorGUIUtility.AddCursorRect(m_SplitterRect, MouseCursor.ResizeHorizontal);

                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                {
                    DrawOptions();
                    GUILayout.Space(5);
                    DrawActions();
                    // Used only for debugging.
                    // DrawHistoryView();
                }
            }

            HandleSplitterEvents();
        }

        void HandleSnippetListDoubleClick(ProfilingSnippet snippet)
        {
            foreach (var action in m_Actions)
            {
                if (!action.defaultSet)
                    continue;
                EditorApplication.delayCall += () => ExecuteSnippet(action, snippet);
            }
        }

        void HandleSplitterEvents()
        {
            switch (Event.current.rawType)
            {
                case EventType.MouseDown:
                    if (m_SplitterRect.Contains(Event.current.mousePosition))
                        m_DraggingSplitter = true;
                    break;
                case EventType.MouseDrag:
                    if (m_DraggingSplitter)
                    {
                        m_SplitterPos += Event.current.delta.x;
                        m_SnippetListView.ResizeColumn(m_SplitterPos);
                        Repaint();
                    }
                    break;
                case EventType.MouseUp:
                    if (m_DraggingSplitter)
                        m_DraggingSplitter = false;
                    break;
            }
        }

        void DrawList()
        {
            GUILayout.BeginVertical(GUILayout.Width(m_SplitterPos), GUILayout.MaxWidth(m_SplitterPos), GUILayout.MinWidth(m_SplitterPos));
            using (new GUILayout.HorizontalScope())
            {
                using (new DisabledScope(!BackHistoryAvailable()))
                    if (GUILayout.Button("<", Styles.navBtn))
                    {
                        BackHistory();
                    }

                using (new DisabledScope(!ForwardHistoryAvailable()))
                    if (GUILayout.Button(">", Styles.navBtn))
                    {
                        ForwardHistory();
                    }
                GUILayout.Label("Snippets");
            }

            var currentSnippet = CurrentSnippet();
            EditorGUI.BeginChangeCheck();
            GUI.SetNextControlName("SearchField");
            m_SnippetListView.searchString = m_SearchValue = SearchField(m_SearchValue);
            if (EditorGUI.EndChangeCheck() && string.IsNullOrEmpty(m_SearchValue) && currentSnippet != null)
            {
                // User has cleaned up the search field if we have a current type focus on it:
                m_SnippetListView.FrameItem(currentSnippet.id);
            }

            var rect = EditorGUILayout.GetControlRect(false, GUILayout.ExpandHeight(true));

            if (!m_InitialListSize && Event.current.type == EventType.Repaint)
            {
                m_SnippetListView.ResizeColumn(rect.width);
                m_InitialListSize = true;
            }

            m_SnippetListView.OnGUI(rect);
            GUILayout.EndVertical();
        }

        void DrawOptions()
        {
            GUILayout.Space(5);
            using (new GUILayout.VerticalScope(Styles.box))
            {
                #if UNITY_2020_1_OR_NEWER
                m_Options.warmup = GUILayout.Toggle(m_Options.warmup, Styles.warmupLabelContent);
                #endif
                EditorGUI.BeginChangeCheck();
                ProfilerDriver.deepProfiling = GUILayout.Toggle(ProfilerDriver.deepProfiling, Styles.deepProfileLabelContent);
                if (EditorGUI.EndChangeCheck())
                    ProfilerHelpers.SetProfilerDeepProfile(ProfilerDriver.deepProfiling);
                m_Options.maximizeWindow = GUILayout.Toggle(m_Options.maximizeWindow, Styles.maximizeWindowContent);
                m_Options.standaloneWindow = GUILayout.Toggle(m_Options.standaloneWindow, Styles.standaloneWindowContent);
                m_Options.count = EditorGUILayout.IntSlider(Styles.countContent, m_Options.count, 1, 101);
                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Profiling Logs", Styles.link))
                        ClickLogFile();
                    var linkRect = GUILayoutUtility.GetLastRect();
                    EditorGUIUtility.AddCursorRect(linkRect, MouseCursor.Link);

                    if (GUILayout.Button("Clear", Styles.clearButton, GUILayout.Width(55)))
                        ClearLogFile();
                    m_Options.csvLog = GUILayout.Toggle(m_Options.csvLog, "CSV");
                }
            }
        }

        void DrawActions()
        {
            var currentSnippet = CurrentSnippet();
            using (new DisabledScope(currentSnippet == null))
            {
                m_ActionScroll = GUILayout.BeginScrollView(m_ActionScroll, Styles.box, GUILayout.ExpandHeight(true));

                var currentSnippetStr = currentSnippet == null ? "No snippet selected" : currentSnippet.label;
                GUILayout.Label($"Snippet: {currentSnippetStr}");

                if (currentSnippet != null && ProfilerHelpers.SupportsMarkerFiltering())
                {
                    var markerName = currentSnippet.GetValidMarkerName();
                    if (!currentSnippet.isValidMarker)
                        markerName += " (*)";
                    GUILayout.Label(new GUIContent($"Marker: {markerName}", !currentSnippet.isValidMarker ? "Marker name generated" : ""));
                }

                GUILayout.Space(5);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUILayout.VerticalScope(GUILayout.ExpandHeight(true)))
                    {
                        foreach (var action in m_Actions)
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                action.defaultSet = GUILayout.Toggle(action.defaultSet, "", GUILayout.MaxWidth(14f));
                                if (GUILayout.Button(action.name, Styles.actionBtn))
                                {
                                    PushHistory(m_SearchValue, currentSnippet);
                                    EditorApplication.delayCall += () => ExecuteSnippet(action, currentSnippet);
                                }
                            }
                        }
                    }
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndScrollView();
            }
        }

        void ExecuteSnippet(ProfilingAction action, ProfilingSnippet snippet)
        {
            var preExecutePayload = snippet.PreExecute(m_Options);
            var executePayload = action.action(preExecutePayload, snippet, m_Options);
            snippet.PostExecute(executePayload, m_Options);
        }

        void DrawHistoryView()
        {
            m_HistoryScroll = GUILayout.BeginScrollView(m_HistoryScroll, Styles.box, GUILayout.Height(150));
            if (m_History.Count > 0)
            {
                for (var i = m_History.Count - 1; i >= 0; --i)
                {
                    var item = m_History[i];

                    var searchLabel = m_HistoryCursor == i ? "-> " : "";
                    var snippet = m_AllSnippets.FirstOrDefault(s => s.idStr == item.idStr);
                    GUILayout.Label($"{searchLabel} Search: {item.search}  Snippet: {snippet.label}");
                }
            }
            else
            {
                GUILayout.Label("No history");
            }
            GUILayout.EndScrollView();
        }

        void ForwardHistory()
        {
            if (!ForwardHistoryAvailable())
                return;
            ++m_HistoryCursor;
            SetCurrentFromHistory(m_History[m_HistoryCursor]);
        }

        void BackHistory()
        {
            if (!BackHistoryAvailable())
                return;
            --m_HistoryCursor;
            SetCurrentFromHistory(m_History[m_HistoryCursor]);
        }

        void SetCurrentFromHistory(HistoryItem item)
        {
            m_SearchValue = item.search;
            SetCurrentFromId(item.idStr);
        }

        void SetCurrentFromSnippet(ProfilingSnippet currentSnippet)
        {
            var selectedItems = new List<int>();
            if (currentSnippet != null)
            {
                selectedItems.Add(currentSnippet.id);
            }
            m_SnippetListView.SetSelection(selectedItems);
            
            if (currentSnippet != null)
                m_SnippetListView.FrameItem(currentSnippet.id);
        }

        void SetCurrentFromId(string snippetId)
        {
            var snippet = m_AllSnippets.FirstOrDefault(s => s.idStr == snippetId);
            SetCurrentFromSnippet(snippet);
        }

        bool BackHistoryAvailable()
        {
            return m_History.Count > 0 && m_HistoryCursor != 0;
        }

        bool ForwardHistoryAvailable()
        {
            return m_HistoryCursor + 1 < m_History.Count;
        }

        void PushHistory(string search, ProfilingSnippet snippet)
        {
            if (m_History.Count > 0 && m_History.Last().search == search && m_History.Last().idStr == snippet.idStr)
                return;

            m_History.Add(new HistoryItem()
            {
                search = search,
                idStr = snippet.idStr
            });
            m_HistoryCursor = m_History.Count - 1;
        }

        private ProfilingSnippet CurrentSnippet()
        {
            ProfilingSnippet selectedSnippet = null;
            var selection = m_SnippetListView.GetSelection();
            if (selection.Count > 0)
            {
                selectedSnippet = m_AllSnippets.FirstOrDefault(snippet => snippet.id == selection[0]);
            }

            return selectedSnippet;
        }

        private void ClickLogFile()
        {
            var logFile = GetLogFile();
            EditorUtility.OpenWithDefaultApp(logFile);
        }

        private void ClearLogFile()
        {
            var logFile = GetLogFile();
            File.WriteAllText(logFile, "");
        }

        private string GetLogFile()
        {
            if (string.IsNullOrEmpty(m_Options.logFile))
                return "";

            if (!File.Exists(m_Options.logFile))
                CreateLogFile();

            return m_Options.logFile;
        }

        private static void CreateLogFile()
        {
            File.WriteAllText(k_LogFile, "");
        }

        private static string GetKeyName(string name)
        {
            return $"{k_KeyPrefix}.{name}";
        }

        static MethodInfo ToolbarSearchField;
        private static string SearchField(string value, params GUILayoutOption[] options)
        {
            if (ToolbarSearchField == null)
            {
                ToolbarSearchField = typeof(EditorGUILayout).GetMethods(BindingFlags.NonPublic | BindingFlags.Static).First(mi => mi.Name == "ToolbarSearchField" && mi.GetParameters().Length == 2);
            }

            return ToolbarSearchField.Invoke(null, new[] { value, (object)options }) as string;
        }
    }
}
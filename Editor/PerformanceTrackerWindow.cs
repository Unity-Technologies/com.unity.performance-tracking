using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Profiling;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace Unity.PerformanceTracking
{
    public class PerformanceTrackerWindow : EditorWindow
    {
        class RowUserData
        {
            public int modelIndex;
            public PtInfo tracker;
        }

        const string k_KeyPrefix = "preftrackingwindow";

        internal const string k_TrackerList = k_KeyPrefix + "_trackerlist";
        internal const string k_PinnedTrackerList = k_KeyPrefix + "_pinnedtrackerlist";

        private ColumnId m_SortBy;
        private bool m_SortAsc;
        [SerializeField] private string m_FilterText;
        [SerializeField] private int m_UpdateSpeedIndex = -1;
        [SerializeField] int m_ShownColumn = -1;
        [SerializeField] private List<string> m_PinnedTrackerNames;

        private PtInfo[] m_AllTrackers = new PtInfo[0];
        private List<PtInfo> m_FilteredTrackers = new List<PtInfo>();
        private List<PtInfo> m_PinnedTrackers = new List<PtInfo>();
        private double m_NextRefreshTime = 0;
        private ListView m_TrackersView;
        private ListView m_PinnedTrackersView;
        private VisualElement m_HeaderRow;
        private EnumField m_SortBySelector;
        private ToolbarSearchField m_SearchField;
        private Delayer m_RefreshFromSearch;
        private bool m_NeedsUpdate;
        private string m_CurrentProfileTag = String.Empty;

        [UsedImplicitly, MenuItem("Window/Analysis/Performance Trackers _%&7")]
        private static void ShowWindow()
        {
            var perfWindow = GetWindow<PerformanceTrackerWindow>() as PerformanceTrackerWindow;
            perfWindow.Show();
            perfWindow.SendAnalytics(Analytics.WindowUsageType.Startup);
        }

        [UsedImplicitly]
        private void OnEnable()
        {
            m_SortBy = (ColumnId)EditorPrefs.GetInt(GetKeyName(nameof(m_SortBy)), (int)ColumnId.Name);
            m_SortAsc = EditorPrefs.GetBool(GetKeyName(nameof(m_SortAsc)), true);

            if (m_FilterText == null)
            {
                m_FilterText = EditorPrefs.GetString(GetKeyName(nameof(m_FilterText)), "");
            }

            if (m_UpdateSpeedIndex == -1)
            {
                m_UpdateSpeedIndex = EditorPrefs.GetInt(GetKeyName(nameof(m_UpdateSpeedIndex)), 0);
            }

            if (m_ShownColumn == -1)
            {
                m_ShownColumn = EditorPrefs.GetInt(GetKeyName(nameof(m_ShownColumn)), (int)PtModel.showAllColumns);
            }

            if (m_PinnedTrackerNames == null)
            {
                var pinnedTrackerStr = EditorPrefs.GetString(GetKeyName(nameof(m_PinnedTrackerNames)), "");
                m_PinnedTrackerNames = pinnedTrackerStr.Split(';').ToList();
            }

            ProfilerHelpers.profileEnabledChanged -= CheckIfProfilingMarker;
            ProfilerHelpers.profileEnabledChanged += CheckIfProfilingMarker;
            
            SetupUI();
            UpdateTrackers();
        }

        private void OnDisable()
        {
            ProfilerHelpers.profileEnabledChanged -= CheckIfProfilingMarker;

            EditorPrefs.SetInt(GetKeyName(nameof(m_SortBy)), (int) m_SortBy);
            EditorPrefs.SetBool(GetKeyName(nameof(m_SortAsc)), m_SortAsc);
            EditorPrefs.SetString(GetKeyName(nameof(m_FilterText)), m_FilterText);
            EditorPrefs.SetInt(GetKeyName(nameof(m_UpdateSpeedIndex)), m_UpdateSpeedIndex);
            EditorPrefs.SetInt(GetKeyName(nameof(m_ShownColumn)), m_ShownColumn);
            EditorPrefs.SetString(GetKeyName(nameof(m_PinnedTrackerNames)), string.Join(";", m_PinnedTrackerNames));

            m_NeedsUpdate = false;
            SendAnalytics(Analytics.WindowUsageType.Shutdown);
        }

        private void CheckIfProfilingMarker(bool profileEnabled)
        {
            if (!profileEnabled && !string.IsNullOrEmpty(m_CurrentProfileTag))
            {
                // Stopping the profiling has happened outside the PerfTrackerwindow: refresh to remove the Stop Profiling button:
                m_CurrentProfileTag = null;
                RefreshTrackers();
            }
        }

        private static string GetKeyName(string name)
        {
            return $"{k_KeyPrefix}.{name}";
        }

        private void SendAnalytics(Analytics.WindowUsageType eventType, string markerName = null)
        {
            var evt = new Analytics.PerformanceWindowUsageEvent()
            {
                columns = m_ShownColumn,
                sortBy = m_SortBy,
                sortAsc = m_SortAsc,
                filter = m_SearchField != null ? m_SearchField.value : "",
                pinMarkers = m_PinnedTrackerNames.ToArray(),
                updateSpeed = (float)PtModel.RefreshRates[m_UpdateSpeedIndex].rate,
                usageType = eventType,
                markerName = markerName
            };
            Analytics.SendPerformanceWindowUsageEvent(evt);
        }

        private void SetupUI()
        {
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>($"{Utils.packageFolderName}/Editor/StyleSheets/PerformanceTrackerWindow.uss");
            rootVisualElement.styleSheets.Add(styleSheet);

            m_RefreshFromSearch = Delayer.Throttle(RefreshFromSearch);

            var toolbar = new UnityEditor.UIElements.Toolbar();
            toolbar.AddToClassList("perf-toolbar");
            toolbar.style.height = PtStyles.itemHeight;

            var searchBox = new VisualElement();
            searchBox.AddToClassList("perf-search-box");
            AddSelectorLabel(toolbar, "Tracker");
            m_SearchField = new ToolbarSearchField();
            m_SearchField.AddToClassList("perf-search-tracker");
            m_SearchField.value = m_FilterText;
            m_SearchField.RegisterCallback<ChangeEvent<string>>((evt) =>
            {
                m_RefreshFromSearch.Execute(evt.newValue);
            });
            searchBox.Add(m_SearchField);
            toolbar.Add(searchBox);

            var resetAllCountersButton = new Button(ResetAllCounters);
            resetAllCountersButton.text = "Reset all counters";
            resetAllCountersButton.AddToClassList("perf-tracker-toolbar-button");
            toolbar.Add(resetAllCountersButton);

            AddSelectorLabel(toolbar, "Update Speed");
            var choices = PtModel.RefreshRates.Select(r => r.label).ToList();
            var updateSpeedSelector = new UnityEditor.UIElements.PopupField<string>(choices, 0);
            updateSpeedSelector.value = PtModel.RefreshRates[m_UpdateSpeedIndex].label;
            updateSpeedSelector.AddToClassList("perf-update-speed-selector");
            updateSpeedSelector.RegisterCallback<ChangeEvent<string>>((evt) =>
            {
                m_UpdateSpeedIndex = Array.FindIndex(PtModel.RefreshRates, (r) => r.label == evt.newValue);
                ScheduleNextRefresh();
                SendAnalytics(Analytics.WindowUsageType.ChangeMonitoringSpeed);
            });
            toolbar.Add(updateSpeedSelector);

            AddSelectorLabel(toolbar, "Columns");
            var columnsChoice = PtModel.ColumnDescriptors.Where(desc => desc.columnsSelectorMaskId > 0).Select(desc => desc.label).ToList();
            var columnsSelector = new MaskField(columnsChoice, m_ShownColumn);
            columnsSelector.RegisterCallback<ChangeEvent<int>>((evt) =>
            {
                m_ShownColumn = evt.newValue;
                CreateHeaders();
                RefreshTrackers();
                SendAnalytics(Analytics.WindowUsageType.ChangeColumnLayout);
            });
            columnsSelector.AddToClassList("perf-columns-selector");
            toolbar.Add(columnsSelector);

            AddSelectorLabel(toolbar, "Sort By");
            m_SortBySelector = new EnumField(ColumnId.Name);
            m_SortBySelector.value = m_SortBy;
            m_SortBySelector.AddToClassList("perf-sort-by-selector");
            m_SortBySelector.RegisterCallback<ChangeEvent<Enum>>((evt) =>
            {
                ChangeSortOrder((ColumnId)evt.newValue);
                SendAnalytics(Analytics.WindowUsageType.ChangeSortBy);
            });
            toolbar.Add(m_SortBySelector);

            var settingsBtn = new Button(() =>
            {
                SettingsService.OpenUserPreferences(PerformanceTrackerSettings.settingsKey);
                SendAnalytics(Analytics.WindowUsageType.OpenPreferences);
            });
            settingsBtn.style.backgroundImage = Icons.settings;
            settingsBtn.style.width = PtStyles.itemHeight -3;
            settingsBtn.style.height = PtStyles.itemHeight -3;

            toolbar.Add(settingsBtn);

            rootVisualElement.Add(toolbar);

            // Set List View Header:
            m_HeaderRow = new VisualElement();
            m_HeaderRow.AddToClassList("perf-header-row-container");
            m_HeaderRow.style.flexDirection = FlexDirection.Row;
            rootVisualElement.Add(m_HeaderRow);
            CreateHeaders();

            m_PinnedTrackersView = new ListView(m_PinnedTrackers, PtStyles.itemHeight, MakeItem, BindPinnedItem);
            m_PinnedTrackersView.name = k_PinnedTrackerList;
            m_PinnedTrackersView.AddToClassList("perf-tracker-list");
            m_PinnedTrackersView.selectionType = SelectionType.Multiple;
            m_PinnedTrackersView.style.flexGrow = 0;
            m_PinnedTrackersView.Q<VisualElement>(null, "unity-scroll-view__content-viewport").RegisterCallback<GeometryChangedEvent>(SyncHeaderAndListsGeometry);
            rootVisualElement.Add(m_PinnedTrackersView);

            m_TrackersView = new ListView(m_FilteredTrackers, PtStyles.itemHeight, MakeItem, BindUnpinnedItem);
            m_TrackersView.name = k_TrackerList;
            m_TrackersView.AddToClassList("perf-tracker-list");
            m_TrackersView.selectionType = SelectionType.Multiple;
            m_TrackersView.style.flexGrow = 1.0f;

            m_TrackersView.Q<VisualElement>(null, "unity-scroll-view__content-viewport").RegisterCallback<GeometryChangedEvent>(SyncHeaderAndListsGeometry);

            rootVisualElement.Add(m_TrackersView);

            m_NeedsUpdate = true;
            ScheduleNextRefresh();
            UpdateTrackers();
        }

        private IEnumerable<PtInfo> GetAllVisibleTrackers()
        {
            return m_FilteredTrackers.Concat(m_PinnedTrackers);
        }

        private IEnumerable<string> GetAllVisibleTrackerNames()
        {
            return GetAllVisibleTrackers().Select(t => t.name);
        }

        private void ResetAllCounters()
        {
            foreach (var trackerName in GetAllVisibleTrackerNames())
                EditorPerformanceTracker.Reset(trackerName);
            RefreshTrackers();
        }

        private void ChangeSortOrder(ColumnId newSortOrder)
        {
            m_SortBy = newSortOrder;
            foreach (var header in m_HeaderRow.Children())
            {
                ColumnId colId = (ColumnId)Convert.ToInt32(header.userData);
                UpdateHeaderTitle(colId, header as Label);
            }
            RefreshTrackers();
        }

        private static void AddSelectorLabel(VisualElement container, string title, params string[] classes)
        {
            var selectorLabel = new Label(title);
            selectorLabel.AddToClassList("perf-selector-label");
            foreach (var c in classes)
                selectorLabel.AddToClassList(c);
            container.Add(selectorLabel);
        }

        private void CreateHeaders()
        {
            m_HeaderRow.Clear();

            AddHeader(m_HeaderRow, m_SortBySelector, ColumnId.Name, "perf-tracker-name");
            AddHeader(m_HeaderRow, m_SortBySelector, ColumnId.SampleCount, "perf-sample-count");
            AddHeader(m_HeaderRow, m_SortBySelector, ColumnId.Age, "perf-age");
            AddHeader(m_HeaderRow, m_SortBySelector, ColumnId.PeakTime, "perf-peak-time");
            AddHeader(m_HeaderRow, m_SortBySelector, ColumnId.AvgTime, "perf-avg-time");
            AddHeader(m_HeaderRow, m_SortBySelector, ColumnId.LastTime, "perf-last-time");
            AddHeader(m_HeaderRow, m_SortBySelector, ColumnId.TotalTime, "perf-total-time");
            AddHeader(m_HeaderRow, m_SortBySelector, ColumnId.Actions, "perf-actions");
        }

        private void UpdateHeaderTitle(ColumnId id, Label header)
        {
            var desc = PtModel.GetColumnDescriptor(id);
            var columnTitle = desc.label;
            if (id == m_SortBy)
            {
                columnTitle = m_SortAsc ? desc.labelAsc : desc.labelDesc;
            }

            header.text = columnTitle;
        }

        private bool IsColumnVisible(ColumnId id)
        {
            var columnDesc = PtModel.GetColumnDescriptor(id);
            return !columnDesc.supportsHiding || (columnDesc.columnsSelectorMaskId & m_ShownColumn) > 0;
        }

        private void AddHeader(VisualElement container, EnumField sortBySelector, ColumnId sortBy, params string[] classes)
        {
            if (!IsColumnVisible(sortBy))
                return;

            var header = new Label();
            UpdateHeaderTitle(sortBy, header);
            header.userData = sortBy;
            header.AddToClassList("perf-header");
            foreach (var c in classes)
                header.AddToClassList(c);
            header.RegisterCallback<MouseUpEvent>((evt) =>
            {
                m_SortAsc = !m_SortAsc;
                if ((ColumnId)sortBySelector.value == sortBy)
                {
                    ChangeSortOrder(sortBy);
                }
                else
                {
                    sortBySelector.value = sortBy;
                }

                SendAnalytics(Analytics.WindowUsageType.Sort);
            });
            container.Add(header);
        }

        private void RefreshFromSearch(object context)
        {
            m_FilterText = context as string;
            RefreshTrackers();
        }

        private VisualElement MakeItem()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.AddToClassList("perf-row");

            var pinIcon = new VisualElement();
            pinIcon.AddToClassList("perf-pin");
            row.Add(pinIcon);

            var trackerName = new TextField {isReadOnly = true};
            VisualElement column = trackerName;
            column.AddToClassList("perf-tracker-name");
            row.Add(column);

            if (IsColumnVisible(ColumnId.SampleCount))
            {
                column = new Button();
                column.RegisterCallback<MouseUpEvent>(evt =>
                {
                    var tracker = GetTrackerInRowAtEvt(evt);
                    EditorPerformanceTracker.Reset(tracker.name);
                    RefreshTrackers();
                    SendAnalytics(Analytics.WindowUsageType.ResetSampleCount, tracker.name);
                });

                column.AddToClassList("perf-sample-count");
                row.Add(column);
            }

            if (IsColumnVisible(ColumnId.Age))
            {
                column = new Label();
                column.AddToClassList("perf-age");
                row.Add(column);
            }

            if (IsColumnVisible(ColumnId.AvgTime))
            {
                column = new Label();
                column.AddToClassList("perf-avg-time");
                row.Add(column);
            }

            if (IsColumnVisible(ColumnId.PeakTime))
            {
                column = new Label();
                column.AddToClassList("perf-peak-time");
                row.Add(column);
            }

            if (IsColumnVisible(ColumnId.LastTime))
            {
                column = new Label();
                column.AddToClassList("perf-last-time");
                row.Add(column);
            }

            if (IsColumnVisible(ColumnId.TotalTime))
            {
                column = new Label();
                column.AddToClassList("perf-total-time");
                row.Add(column);
            }

            var actionBtn = new Button {text = "..."};
            actionBtn.RegisterCallback<MouseUpEvent>(evt =>
            {
                var btn = evt.target as Button;
                var tracker = GetTrackerInRowAtEvt(evt);
                if (tracker.name == m_CurrentProfileTag)
                {
                    EndTrackerProfiling(evt.target as Button, tracker.name);
                }
                else
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Set as search filter"), false, () => {
                        m_SearchField.value = tracker.name;
                        SendAnalytics(Analytics.WindowUsageType.SetSearchFieldAction, tracker.name);
                    });
                    menu.AddItem(new GUIContent("Add performance notification"), false, () => PerformanceTrackerActions.AddNewPerformanceNotification(tracker.name, (float)tracker.avgTime, Analytics.ActionSource.PerformanceTrackerWindow));
                    menu.AddItem(new GUIContent("LogCallstack"), false, () => PerformanceTrackerActions.LogCallstack(tracker.name, Analytics.ActionSource.PerformanceTrackerWindow));
                    if (String.IsNullOrEmpty(m_CurrentProfileTag))
                    {
                        menu.AddSeparator("");
                        menu.AddItem(new GUIContent("Editmode Profile..."), false, () => StartTrackerProfiling(btn, tracker.name, false, true));
                        menu.AddItem(new GUIContent("Editmode Deep profile..."), false, () => StartTrackerProfiling(btn, tracker.name, true, true));
                        menu.AddSeparator("");
                        menu.AddItem(new GUIContent("Playmode Profile..."), false, () => StartTrackerProfiling(btn, tracker.name, false, false));
                        menu.AddItem(new GUIContent("Playmode Deep profile..."), false, () => StartTrackerProfiling(btn, tracker.name, true, false));
                    }
                    menu.ShowAsContext();
                }
            });
            actionBtn.AddToClassList("perf-actions");
            row.Add(actionBtn);

            return row;
        }

        private PtInfo GetTrackerInRowAtEvt(EventBase evt)
        {
            var btn = evt.target as VisualElement;
            var btnRow = btn.parent;
            return ((RowUserData)btnRow.userData).tracker;
        }

        private void StartTrackerProfiling(Button btn, string trackerId, bool deepProfile, bool editorProfile)
        {
            if (!ProfilerHelpers.StartProfilerRecording(trackerId, editorProfile, deepProfile))
                return;

            m_CurrentProfileTag = trackerId;
            UpdateActionButton(btn, trackerId);
        }

        private void EndTrackerProfiling(Button btn, string trackerId)
        {
            m_CurrentProfileTag = String.Empty;
            UpdateActionButton(btn, trackerId);
            PerformanceTrackerActions.StopProfilerRecordingAndCreateReport(trackerId, Analytics.ActionSource.PerformanceTrackerWindow);
        }

        private void UpdateActionButton(Button btn, string trackerId)
        {
            if (trackerId == m_CurrentProfileTag)
            {
                btn.style.width = 100f;
                btn.text = "Stop profiling...";
            }
            else
            {
                btn.text = "...";
                btn.style.width = 30f;
            }
        }

        private void BindUnpinnedItem(VisualElement element, int modelIndex)
        {
            var tracker = m_FilteredTrackers[modelIndex];
            BindTracker(element, tracker, modelIndex, m_FilteredTrackers);
        }

        private void BindPinnedItem(VisualElement element, int modelIndex)
        {
            var tracker = m_PinnedTrackers[modelIndex];
            BindTracker(element, tracker, modelIndex, m_PinnedTrackers);
        }

        private void BindTracker(VisualElement element, PtInfo tracker, int modelIndex, List<PtInfo> trackerList)
        {
            element.style.backgroundColor = (modelIndex % 2) == 0 ? PtStyles.evenRowColor : PtStyles.oddRowColor;
            element.userData = new RowUserData
            {
                tracker = tracker,
                modelIndex = modelIndex
            };

            var columnIndex = 0;
            var pinIcon = element.ElementAt(columnIndex++);
            if (pinIcon != null)
            {
                if (pinIcon.userData != null)
                    pinIcon.UnregisterCallback((EventCallback<MouseDownEvent>)pinIcon.userData);
                EventCallback<MouseDownEvent> mouseDownCallback = evt => { TogglePinnedItem(modelIndex, trackerList); };
                pinIcon.userData = mouseDownCallback;
                pinIcon.RegisterCallback(mouseDownCallback);

                if (IsPinnedTracker(tracker))
                {
                    pinIcon.EnableInClassList("perf-pin-off", false);
                    pinIcon.EnableInClassList("perf-pin-on", true);
                }
                else
                {
                    pinIcon.EnableInClassList("perf-pin-off", true);
                    pinIcon.EnableInClassList("perf-pin-on", false);
                }
            }

            // Implement trackerName as a TextField to support text selection.
            var trackerName = element.ElementAt(columnIndex++) as TextField;
            if (trackerName.value != tracker.name)
                trackerName.value = tracker.name;

            if (IsColumnVisible(ColumnId.SampleCount))
            {
                var sampleCount = element.ElementAt(columnIndex++) as Button;
                sampleCount.text = tracker.sampleCount.ToString();
            }

            if (IsColumnVisible(ColumnId.Age))
            {
                var age = element.ElementAt(columnIndex++) as Label;
                age.text = PtModel.FormatAge(tracker, EditorApplication.timeSinceStartup);
                age.style.color = tracker.updated ? PtStyles.warningColor : PtStyles.normalColor;
            }

            if (IsColumnVisible(ColumnId.PeakTime))
            {
                var peakTime = element.ElementAt(columnIndex++) as Label;
                peakTime.text = PtModel.FormatTime(tracker.peakTime, tracker.dtPeakTime);
                peakTime.style.color = GetLabelTimeColor(tracker.peakTime, 0.5, 1.0);
            }

            if (IsColumnVisible(ColumnId.AvgTime))
            {
                var avgTime = element.ElementAt(columnIndex++) as Label;
                avgTime.text = PtModel.FormatTime(tracker.avgTime, tracker.dtAvgTime);
                avgTime.style.color = GetLabelTimeColor(tracker.avgTime, 0.1, 0.5);
            }

            if (IsColumnVisible(ColumnId.LastTime))
            {
                var lastTime = element.ElementAt(columnIndex++) as Label;
                lastTime.text = PtModel.FormatTimeChange(tracker.lastTime, tracker.dtLastTime);
                lastTime.style.color = GetLabelTimeChangeColor(tracker.dtLastTime);
            }

            if (IsColumnVisible(ColumnId.TotalTime))
            {
                var totalTime = element.ElementAt(columnIndex++) as Label;
                totalTime.text = PtModel.FormatTimeRate(tracker.totalTime, tracker.usage);
                totalTime.style.color = GetLabelTimeColor(tracker.totalTime, 5, 10.0);
            }

            var actionBtn = element.ElementAt(columnIndex++) as Button;
            UpdateActionButton(actionBtn, tracker.name);
        }

        private static Color GetLabelTimeColor(double time, double warningLimit, double errorLimit)
        {
            if (time >= errorLimit)
                return PtStyles.criticalColor;

            if (time >= warningLimit)
                return PtStyles.warningColor;

            return PtStyles.normalColor;
        }

        private static Color GetLabelTimeChangeColor(double dt)
        {
            if (Math.Abs(dt) < 0.00075)
                return PtStyles.normalColor;
            return dt < 0 ? PtStyles.fasterColor : PtStyles.slowerColor;
        }

        private void BuildTrackers(out bool numberTrackersChanged)
        {
            using (new PerformanceTracker("PerformanceTrackerWindow.BuildTrackers"))
            {
                var oldTrackerCount = m_AllTrackers.Length;
                m_AllTrackers = PtModel.BuildTrackerList(m_AllTrackers, m_SortBy, m_SortAsc);
                var pinnedSortedTrackers = new List<PtInfo>();
                var unpinnedSortedTrackers = new List<PtInfo>();
                foreach (var tracker in m_AllTrackers)
                {
                    if (IsPinnedTracker(tracker))
                        pinnedSortedTrackers.Add(tracker);
                    else
                        unpinnedSortedTrackers.Add(tracker);
                }
                numberTrackersChanged = oldTrackerCount != m_AllTrackers.Length;
                var oldCount = m_FilteredTrackers.Count;
                m_PinnedTrackers.Clear();
                m_PinnedTrackers.AddRange(pinnedSortedTrackers);
                m_PinnedTrackersView.style.height = Math.Min(10.0f, pinnedSortedTrackers.Count) * PtStyles.itemHeight;
                m_FilteredTrackers.Clear();
                if (!string.IsNullOrEmpty(m_FilterText))
                {
                    var filterTokens = m_FilterText.Split(',').Select(f => f.Trim()).Where(f => f.Length > 0).ToArray();
                    if (filterTokens.Length == 1)
                    {
                        m_FilteredTrackers.AddRange(unpinnedSortedTrackers.Where(t => t.name.IndexOf(filterTokens[0], StringComparison.OrdinalIgnoreCase) != -1));
                    }
                    else
                    {
                        m_FilteredTrackers.AddRange(unpinnedSortedTrackers.Where(t => filterTokens.Any(f => t.name.IndexOf(f, StringComparison.OrdinalIgnoreCase) != -1)));
                    }
                }
                else
                {
                    m_FilteredTrackers.AddRange(unpinnedSortedTrackers);
                }
                numberTrackersChanged = numberTrackersChanged || m_FilteredTrackers.Count != oldCount;
                titleContent = new GUIContent($"Performance Trackers ({m_FilteredTrackers.Count})");
            }
        }

        private void RefreshTrackers()
        {
            // Update Model and fully repaint, recreate the view.
            using (new PerformanceTracker("PerformanceTrackerWindow.RefreshTrackers"))
            {
                BuildTrackers(out _);
                m_PinnedTrackersView?.Refresh();
                m_TrackersView?.Refresh();
            }
        }

        private void SyncHeaderAndListsGeometry(GeometryChangedEvent evt)
        {
            var pinnedTrackerListScrollVisible = m_PinnedTrackersView.Q<ScrollView>(null, "unity-scroll-view").verticalScroller.visible;
            var filteredTrackerListScrollVisible = m_TrackersView.Q<ScrollView>(null, "unity-scroll-view").verticalScroller.visible;
            m_HeaderRow.style.marginRight = (pinnedTrackerListScrollVisible || filteredTrackerListScrollVisible) ? 13 : 0;

            // Update filtered list view
            if (pinnedTrackerListScrollVisible && !filteredTrackerListScrollVisible)
            {
                m_TrackersView.style.marginRight = 13;
            }
            else
            {
                m_TrackersView.style.marginRight = 0;
            }

            // Update pinned list view
            if (filteredTrackerListScrollVisible && !pinnedTrackerListScrollVisible)
            {
                m_PinnedTrackersView.style.marginRight = 13;
            }
            else
            {
                m_PinnedTrackersView.style.marginRight = 0;
            }
        }

        private void ScheduleNextRefresh(double currentTime = 0)
        {
            m_NextRefreshTime = currentTime == 0 ? EditorApplication.timeSinceStartup : currentTime + PtModel.RefreshRates[m_UpdateSpeedIndex].rate;
        }

        private void UpdateTrackers(double currentTime = 0)
        {
            if (currentTime == 0 || m_NextRefreshTime <= currentTime)
            {
                using (new PerformanceTracker("PerformanceTrackerWindow.UpdateTrackers"))
                {
                    ScheduleNextRefresh(currentTime);
                    BuildTrackers(out var needRefreshView);
                    UpdateTrackersView(m_PinnedTrackersView, BindPinnedItem, needRefreshView);
                    UpdateTrackersView(m_TrackersView, BindUnpinnedItem, needRefreshView);
                }
            }

            if (m_NeedsUpdate)
                EditorApplication.delayCall += () => UpdateTrackers(EditorApplication.timeSinceStartup);
        }

        private static void UpdateTrackersView(ListView trackerView, Action<VisualElement, int> bindItem, bool needsRefresh)
        {
            if (needsRefresh)
            {
                // Actual numbers of trackers has changed in between 2 updates. Need to refresh the whole view.
                trackerView?.Refresh();
            }
            else
            {
                foreach (var row in trackerView.Q("unity-content-container").Children())
                {
                    var modelIndex = Convert.ToInt32(((RowUserData)row.userData).modelIndex);
                    bindItem(row, modelIndex);
                }
            }
        }

        [UsedImplicitly, OnOpenAsset(1)]
        private static bool OpenProfileData(int instanceID, int line)
        {
            var assetPath = AssetDatabase.GetAssetPath(EditorUtility.InstanceIDToObject(instanceID));
            if (System.IO.Path.GetExtension(assetPath) != ".data")
                return false;
            PerformanceTrackerActions.OpenProfileReport(assetPath, "", Analytics.ActionSource.OpenAsset);
            return true;
        }

        internal void SetSearchString(string searchString)
        {
            var searchField = rootVisualElement.Q<ToolbarSearchField>(null, "perf-search-tracker");
            if (searchField == null)
                return;
            searchField.value = searchString;
        }

        internal RefreshRateInfo GetRefreshRate()
        {
            if (m_UpdateSpeedIndex < 0 || m_UpdateSpeedIndex >= PtModel.RefreshRates.Length)
                return PtModel.RefreshRates.First();
            return PtModel.RefreshRates[m_UpdateSpeedIndex];
        }

        private void TogglePinnedItem(int itemIndex, List<PtInfo> trackerList)
        {
            var tracker = trackerList[itemIndex];
            if (IsPinnedTracker(tracker))
            {
                m_PinnedTrackerNames.Remove(tracker.name);
                SendAnalytics(Analytics.WindowUsageType.RemovePin, tracker.name);
            }
            else
            {
                m_PinnedTrackerNames.Add(tracker.name);
                SendAnalytics(Analytics.WindowUsageType.AddPin, tracker.name);
            }

            RefreshTrackers();
        }

        private bool IsPinnedTracker(PtInfo tracker)
        {
            return m_PinnedTrackerNames.Contains(tracker.name);
        }
    }
}
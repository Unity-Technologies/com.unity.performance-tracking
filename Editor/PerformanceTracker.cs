using System;
using System.Runtime.CompilerServices;

namespace Unity.PerformanceTracking
{
    public struct PerformanceTracker : IDisposable
    {
        private bool m_Disposed;
        private readonly int m_WatchHandle;

        public PerformanceTracker(string name)
        {
            m_Disposed = false;
            m_WatchHandle = UnityEditor.Profiling.EditorPerformanceTracker.StartTracker(name);
        }

        public void Dispose()
        {
            if (m_Disposed)
                return;
            m_Disposed = true;
            UnityEditor.Profiling.EditorPerformanceTracker.StopTracker(m_WatchHandle);
        }
    }
}
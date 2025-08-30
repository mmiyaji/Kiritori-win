using System;
using System.Runtime.InteropServices.WindowsRuntime; // AsTask()
using System.Threading.Tasks;
using Windows.Foundation;

namespace Kiritori.Helpers
{
    internal static class WinRtAwait
    {
        public static Task Await(IAsyncAction action)
            => action == null ? Task.CompletedTask : action.AsTask();

        public static Task<T> Await<T>(IAsyncOperation<T> op)
            => op == null ? Task.FromResult(default(T)) : op.AsTask();
    }
}
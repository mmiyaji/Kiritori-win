using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

public static class StartupManager
{
    private const string TaskId = "KiritoriStartup";

    private static Type TStartupTask =>
        Type.GetType("Windows.ApplicationModel.StartupTask, Windows, ContentType=WindowsRuntime");

    public static async Task<bool> IsEnabledAsync()
    {
        if (!PackagedHelper.IsPackaged()) return false;
        var tStartupTask = TStartupTask;
        if (tStartupTask == null) return false;

        var getAsync = tStartupTask.GetMethod("GetAsync", BindingFlags.Public | BindingFlags.Static);
        var op = getAsync.Invoke(null, new object[] { TaskId });
        var startupTask = await AwaitIAsyncOperation(op); // StartupTask インスタンス

        var propState = tStartupTask.GetProperty("State");
        int stateVal = Convert.ToInt32(propState.GetValue(startupTask));

        // Async: Disabled=0, EnabledByUser=1, Enabled=2, DisabledByUser=3? / EnabledByPolicy=4（環境差あり）
        return stateVal == 2 /*Enabled*/ || stateVal == 4 /*EnabledByPolicy*/ || stateVal == 1 /*EnabledByUser*/;
    }

    public static async Task<bool> EnableAsync()
    {
        if (!PackagedHelper.IsPackaged()) return false;
        var tStartupTask = TStartupTask;
        if (tStartupTask == null) return false;

        var getAsync = tStartupTask.GetMethod("GetAsync", BindingFlags.Public | BindingFlags.Static);
        var op = getAsync.Invoke(null, new object[] { TaskId });
        var startupTask = await AwaitIAsyncOperation(op);

        var propState = tStartupTask.GetProperty("State");
        int stateVal = Convert.ToInt32(propState.GetValue(startupTask));

        if (stateVal == 0 /*Disabled*/)
        {
            var reqEnable = tStartupTask.GetMethod("RequestEnableAsync");
            var op2 = reqEnable.Invoke(startupTask, null);
            var res = await AwaitIAsyncOperation(op2); // 戻りは StartupTaskState
            stateVal = Convert.ToInt32(res);
        }

        return stateVal == 2 || stateVal == 4 || stateVal == 1;
    }

    public static async Task DisableAsync()
    {
        if (!PackagedHelper.IsPackaged()) return;
        var tStartupTask = TStartupTask;
        if (tStartupTask == null) return;

        var getAsync = tStartupTask.GetMethod("GetAsync", BindingFlags.Public | BindingFlags.Static);
        var op = getAsync.Invoke(null, new object[] { TaskId });
        var startupTask = await AwaitIAsyncOperation(op);

        var disable = tStartupTask.GetMethod("Disable");
        disable.Invoke(startupTask, null);
    }

    private static async Task<object> AwaitIAsyncOperation(object iasyncOp)
    {
        if (iasyncOp == null)
            throw new InvalidOperationException("IAsyncOperation object is null.");

        var opType = iasyncOp.GetType();
        Debug.WriteLine($"[Await] opType={opType.FullName}, asm={opType.Assembly?.FullName}");

        // 1) __ComObject が実装している IAsyncOperation<T> を探す
        var iAsyncOperationOpen = Type.GetType("Windows.Foundation.IAsyncOperation`1, Windows, ContentType=WindowsRuntime");
        if (iAsyncOperationOpen != null)
        {
            var iAsyncIface = opType
                .GetInterfaces()
                .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == iAsyncOperationOpen);

            Debug.WriteLine($"[Await] has IAsyncOperation<T> = { (iAsyncIface != null) }");

            if (iAsyncIface != null)
            {
                // TResult を取得（例: Windows.ApplicationModel.StartupTask / StartupTaskState）
                var resultType = iAsyncIface.GetGenericArguments()[0];
                Debug.WriteLine($"[Await] TResult = {resultType.FullName}");

                // 2) System.WindowsRuntimeSystemExtensions.AsTask<TResult>(IAsyncOperation<TResult>) を反射で呼ぶ
                var extType = Type.GetType("System.WindowsRuntimeSystemExtensions, System.Runtime.WindowsRuntime");
                if (extType == null)
                    throw new InvalidOperationException("System.Runtime.WindowsRuntime not found.");

                var asTaskGen = extType
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "AsTask"
                                    && m.IsGenericMethodDefinition
                                    && m.GetGenericArguments().Length == 1
                                    && m.GetParameters().Length == 1
                                    && m.GetParameters()[0].ParameterType.IsInterface);
                if (asTaskGen == null)
                    throw new InvalidOperationException("AsTask generic method not found.");

                var asTaskClosed = asTaskGen.MakeGenericMethod(resultType);

                // パラメータの型は "IAsyncOperation<TResult>"。__ComObject はそれを実装しているのでそのまま渡せる。
                var taskObj = asTaskClosed.Invoke(null, new object[] { iasyncOp }); // Task<TResult>
                var task = (Task)taskObj;
                await task.ConfigureAwait(false);

                var taskType = taskObj.GetType(); // Task<TResult>
                var propResult = taskType.GetProperty("Result");
                var res = propResult?.GetValue(taskObj);

                Debug.WriteLine("[Await] Completed via AsTask");
                return res;
            }
        }

        // 3) 最後の保険：Status/GetResults を直接ポーリング（対応環境限定）
        var propStatus = opType.GetProperty("Status");
        var getResults = opType.GetMethod("GetResults");
        var propError  = opType.GetProperty("ErrorCode");

        Debug.WriteLine($"[Await] fallback: propStatus={(propStatus!=null)} getResults={(getResults!=null)}");
        if (propStatus == null || getResults == null)
            throw new InvalidOperationException("Object is not a valid IAsyncOperation.");

        while (true)
        {
            int status = Convert.ToInt32(propStatus.GetValue(iasyncOp, null)); // 0 Started, 1 Completed, 2 Canceled, 3 Error
            if (status == 1) return getResults.Invoke(iasyncOp, null);
            if (status == 2) throw new TaskCanceledException("IAsyncOperation was canceled.");
            if (status == 3)
            {
                var ex = propError?.GetValue(iasyncOp, null) as Exception;
                if (ex != null)
                    throw new InvalidOperationException($"IAsyncOperation failed. HResult=0x{ex.HResult:X8} Message={ex.Message}", ex);
                throw new InvalidOperationException("IAsyncOperation failed.");
            }
            await Task.Delay(20).ConfigureAwait(false);
        }
    }




}

using global::System;
using global::System.Diagnostics;
using global::System.IO;
using global::System.IO.Pipes;
using global::System.Runtime.InteropServices;
using global::System.Security.Principal;
using global::System.Threading;
using global::System.Threading.Tasks;

namespace Workflow_Timer.Services.System
{
    public sealed class SingleInstanceService : IDisposable
    {
        private readonly Mutex _mutex;
        private readonly string _pipeName;
        private readonly CancellationTokenSource _shutdown = new();
        private bool _ownsMutex;
        private bool _disposed;
        private Task? _listener;

        public SingleInstanceService()
        {
            // Scope to the same Windows user and interactive session, not an install path.
            using var identity = WindowsIdentity.GetCurrent();
            using var process = Process.GetCurrentProcess();
            var scope = $"WorkflowTimer.SingleInstance.v1.{identity.User!.Value}.{process.SessionId}";
            _pipeName = scope;
            _mutex = new Mutex(false, @"Local\" + scope);
        }

        public bool TryAcquire()
        {
            try { _ownsMutex = _mutex.WaitOne(0); }
            catch (AbandonedMutexException) { _ownsMutex = true; }
            return _ownsMutex;
        }

        public void StartListening(Action activate)
        {
            if (!_ownsMutex || _listener != null)
                throw new InvalidOperationException("Only the primary instance may listen.");
            _listener = Task.Run(() => ListenAsync(activate, _shutdown.Token));
        }

        private async Task ListenAsync(Action activate, CancellationToken stopping)
        {
            while (!stopping.IsCancellationRequested)
            {
                try
                {
                    using var pipe = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await pipe.WaitForConnectionAsync(stopping).ConfigureAwait(false);
                    using var request = CancellationTokenSource.CreateLinkedTokenSource(stopping);
                    request.CancelAfter(TimeSpan.FromSeconds(3));

                    // Tell the launching process whom to grant foreground permission to.
                    await pipe.WriteAsync(BitConverter.GetBytes(Environment.ProcessId), request.Token).ConfigureAwait(false);
                    var signal = new byte[1];
                    await pipe.ReadExactlyAsync(signal, request.Token).ConfigureAwait(false);
                    if (signal[0] == 1 && !stopping.IsCancellationRequested)
                    {
                        activate();
                        await pipe.WriteAsync(new byte[] { 1 }, request.Token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (stopping.IsCancellationRequested) { break; }
                catch (OperationCanceledException) { }
                catch (IOException)
                {
                    try { await Task.Delay(50, stopping).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                }
                catch (UnauthorizedAccessException)
                {
                    // Avoid a tight retry loop if creating the endpoint is temporarily blocked.
                    try { await Task.Delay(250, stopping).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                }
            }
        }

        public bool RequestActivation()
        {
            if (_ownsMutex) return false;
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(6));
                return RequestActivationAsync(timeout.Token).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException) { return false; }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        private async Task<bool> RequestActivationAsync(CancellationToken token)
        {
            using var pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(token).ConfigureAwait(false);
            var pid = new byte[4];
            await pipe.ReadExactlyAsync(pid, token).ConfigureAwait(false);
            int primaryPid = BitConverter.ToInt32(pid);
            if (primaryPid > 0)
                AllowSetForegroundWindow((uint)primaryPid);
            await pipe.WriteAsync(new byte[] { 1 }, token).ConfigureAwait(false);
            var reply = new byte[1];
            await pipe.ReadExactlyAsync(reply, token).ConfigureAwait(false);
            return reply[0] == 1;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _shutdown.Cancel();
            // Listener never waits for the UI dispatcher; cancellation can finish on a worker.
            try { _listener?.GetAwaiter().GetResult(); }
            finally
            {
                _shutdown.Dispose();
                if (_ownsMutex) _mutex.ReleaseMutex();
                _mutex.Dispose();
            }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllowSetForegroundWindow(uint dwProcessId);
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
#if NET40
using Kzone.Semaphore;
#endif
namespace Kzone.Signal
{
    public class Semaphore : IDisposable
    {
#if NET40
        private AsyncSemaphore _mutex = new(1);
#else
        private SemaphoreSlim _mutex = new(1, 1);
#endif
        public void Wait()
            => _mutex.Wait();

        public void Wait(CancellationToken token)
            => _mutex.Wait(token);
        public Task WaitAsync()
           => _mutex.WaitAsync();
        public Task WaitAsync(CancellationToken token)
            => _mutex.WaitAsync(token);

        public void Release()
           => _mutex.Release();

        public void Dispose()
        {
#if NET40
            _mutex = null;
#else
            _mutex.Dispose();
#endif
        }
    }
}


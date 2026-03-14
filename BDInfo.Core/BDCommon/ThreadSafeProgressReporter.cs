using System;
using System.Threading;

namespace BDCommon
{
    public class ThreadSafeProgressReporter
    {
        private readonly long _totalBytes;
        private long _finishedBytes;
        private readonly DateTime _timeStarted;
        private long _lastReportTicks;

        public ThreadSafeProgressReporter(long totalBytes)
        {
            _totalBytes = totalBytes;
            _timeStarted = DateTime.Now;
            _lastReportTicks = 0;
        }

        public void ReportFileCompleted(long fileBytes)
        {
            Interlocked.Add(ref _finishedBytes, fileBytes);
            RenderProgress();
        }

        private void RenderProgress()
        {
            long now = Environment.TickCount64;
            long last = Interlocked.Read(ref _lastReportTicks);
            if (now - last < 500) return;
            if (Interlocked.CompareExchange(ref _lastReportTicks, now, last) != last) return;

            try
            {
                long finishedBytes = Interlocked.Read(ref _finishedBytes);
                double progress = _totalBytes > 0 ? (double)finishedBytes / _totalBytes : 0;
                double progressValue = Math.Clamp(100 * progress, 0, 100);

                TimeSpan elapsedTime = DateTime.Now.Subtract(_timeStarted);
                TimeSpan remainingTime;
                if (progress > 0 && progress < 1)
                {
                    remainingTime = new TimeSpan(
                        (long)((double)elapsedTime.Ticks / progress) - elapsedTime.Ticks);
                }
                else
                {
                    remainingTime = new TimeSpan(0);
                }

                Console.Write($"\rScanning");
                Console.Write($" | Progress: {progressValue,6:F2}%");
                Console.Write($" | Elapsed: {elapsedTime.Hours:D2}:{elapsedTime.Minutes:D2}:{elapsedTime.Seconds:D2}");
                Console.Write($" | Remaining: {remainingTime.Hours:D2}:{remainingTime.Minutes:D2}:{remainingTime.Seconds:D2}");
            }
            catch
            {
                // Suppress progress reporting errors
            }
        }

        public void RenderFinal()
        {
            try
            {
                TimeSpan elapsedTime = DateTime.Now.Subtract(_timeStarted);
                Console.Write($"\rScanning");
                Console.Write($" | Progress: {100.00,6:F2}%");
                Console.Write($" | Elapsed: {elapsedTime.Hours:D2}:{elapsedTime.Minutes:D2}:{elapsedTime.Seconds:D2}");
                Console.Write($" | Remaining: 00:00:00");
            }
            catch
            {
                // Suppress progress reporting errors
            }
        }
    }
}

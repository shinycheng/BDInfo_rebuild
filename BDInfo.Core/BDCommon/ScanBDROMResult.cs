using System;
using System.Collections.Concurrent;

namespace BDCommon
{
  public class ScanBDROMResult
  {
    public Exception ScanException = new Exception("Scan has not been run.");
    public ConcurrentDictionary<string, Exception> FileExceptions = new ConcurrentDictionary<string, Exception>();
  }
}
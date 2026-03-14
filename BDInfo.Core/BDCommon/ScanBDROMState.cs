using System.Collections.Generic;

namespace BDCommon
{
  public class ScanBDROMState
  {
    public long TotalBytes;
    public Dictionary<string, List<TSPlaylistFile>> PlaylistMap = new Dictionary<string, List<TSPlaylistFile>>();
  }
}
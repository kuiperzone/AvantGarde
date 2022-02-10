// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
// -----------------------------------------------------------------------------

using System;

namespace AvantGarde.Settings
{
    public class RecentFile : IComparable, IComparable<RecentFile>
    {
        public RecentFile()
        {
            Path = string.Empty;
        }

        public RecentFile(string path)
        {
            Path = path;
            Update();
        }

        public string Path { get; set; }

        public long Timestamp { get; set; }

        public void Update()
        {
            Timestamp = DateTime.UtcNow.Ticks;
        }

        public int CompareTo(RecentFile? other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            return other.Timestamp.CompareTo(Timestamp);
        }

        public int CompareTo(object? other)
        {
            return CompareTo(other as RecentFile);
        }

    }
}
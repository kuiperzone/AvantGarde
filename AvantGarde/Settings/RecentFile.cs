// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas (C) 2022-23
// LICENSE   : GPL-3.0-or-later
// HOMEPAGE  : https://github.com/kuiperzone/AvantGarde
//
// Avant Garde is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, either version 3 of the License, or (at your option) any later version.
//
// Avant Garde is distributed in the hope that it will be useful, but WITHOUT
// ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
// FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License along
// with Avant Garde. If not, see <https://www.gnu.org/licenses/>.
// -----------------------------------------------------------------------------

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
            ArgumentNullException.ThrowIfNull(other);
            return other.Timestamp.CompareTo(Timestamp);
        }

        public int CompareTo(object? other)
        {
            return CompareTo(other as RecentFile);
        }

    }
}
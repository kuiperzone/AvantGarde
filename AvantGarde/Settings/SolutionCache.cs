// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas (C) 2022-25
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

using System.Diagnostics;
using AvantGarde.Projects;

namespace AvantGarde.Settings
{
    /// <summary>
    /// Json friendly cache of solution properties.
    /// </summary>
    public class SolutionCache : JsonSettings
    {
        private readonly List<SolutionCacheItem> _recent = new();
        public int _maxCount = 25;

        public List<SolutionCacheItem> Recent
        {
            get { return _recent; }

            set
            {
                _recent.Clear();
                _recent.AddRange(value);
            }
        }

        public int MaxCount
        {
            get { return _maxCount; }

            set
            {
                value = Math.Max(value, 1);

                if (_maxCount != value)
                {
                    _maxCount = value;
                    SortAndCap();
                }
            }
        }


        public IEnumerable<string> GetPathHistory(int max = 10)
        {
            var temp = new List<string>();

            foreach (var item in _recent)
            {
                if (temp.Count == max)
                {
                    return temp;
                }

                temp.Add(item.FullName);
            }

            return temp;
        }

        public void Clear()
        {
            _recent.Clear();
        }

        public bool AssignTo(DotnetSolution solution)
        {
            Debug.WriteLine($"{nameof(SolutionCache)}.{nameof(AssignTo)}");

            foreach (var item in _recent)
            {
                if (item.AssignTo(solution))
                {
                    Debug.WriteLine("Matched: " + item.FullName);
                    return true;
                }
            }

            Debug.WriteLine("No match");
            return false;
        }

        public void Upsert(DotnetSolution solution)
        {
            Debug.WriteLine($"{nameof(SolutionCache)}.{nameof(Upsert)}");

            foreach (var item in _recent)
            {
                if (item.AssignFrom(solution))
                {
                    Debug.WriteLine("Matched: " + item.FullName);
                    SortAndCap();
                    return;
                }
            }

            Debug.WriteLine("Insert new: " + solution.FullName);
            _recent.Add(new SolutionCacheItem(solution));
            SortAndCap();
        }

        /// <summary>
        /// Implements.
        /// </summary>
        public override bool Read()
        {
            return ReadInternal();
        }

        private bool ReadInternal()
        {
            Debug.WriteLine($"{nameof(SolutionCache)}.{nameof(ReadInternal)}");
            var temp = Read<SolutionCache>();

            if (temp != null)
            {
                _recent.Clear();
                _recent.AddRange(temp.Recent);

                Debug.WriteLine("RecentCount: " + _recent.Count);
                return true;
            }

            return false;
        }

        private void SortAndCap()
        {
            _recent.Sort();

            while (_recent.Count > MaxCount)
            {
                _recent.RemoveAt(_recent.Count - 1);
            }
        }

    }
}
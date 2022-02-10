// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
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
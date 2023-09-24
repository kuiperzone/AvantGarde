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

using System.Diagnostics;
using AvantGarde.Projects;

namespace AvantGarde.Settings
{
    /// <summary>
    /// Holds a copy of solution and project settings for JSON storage.
    /// </summary>
    public class SolutionCacheItem : IComparable, IComparable<SolutionCacheItem>
    {
        private readonly List<ProjectProperties> _projects = new();

        public SolutionCacheItem()
        {
        }

        public SolutionCacheItem(DotnetSolution solution)
        {
            AssignFrom(solution, true);
        }

        public string FullName { get; set; } = string.Empty;

        public long Timestamp { get; set; }

        public SolutionProperties Properties { get; set; }= new();

        public List<ProjectProperties> Projects
        {
            get { return _projects; }

            set
            {
                _projects.Clear();

                foreach (var item in value)
                {
                    if (!string.IsNullOrEmpty(item.ProjectName))
                    {
                        _projects.Add(item);
                    }
                }
            }
        }

        public void Update()
        {
            Timestamp = DateTime.UtcNow.Ticks;
        }

        public bool AssignFrom(DotnetSolution solution)
        {
            return AssignFrom(solution, false);
        }

        public bool AssignTo(DotnetSolution solution)
        {
            Debug.WriteLine($"{nameof(SolutionCacheItem)}.{nameof(AssignTo)}");

            if (FullName.Equals(solution.FullName, PathItem.PlatformComparison))
            {
                Debug.WriteLine("Matched solution: " + FullName);
                Debug.WriteLine("Project cache count: " + Projects.Count);
                Debug.WriteLine("Solution project count: " + solution.Projects.Count);

                Update();
                solution.Properties.AssignFrom(Properties);

                foreach (var item in Projects)
                {
                    if (solution.Projects.TryGetValue(item.ProjectName, out DotnetProject? project))
                    {
                        Debug.WriteLine("Matched project: " + item.ProjectName);
                        project.Properties.AssignFrom(item);
                    }
                }

                return true;
            }

            return false;
        }

        public int CompareTo(SolutionCacheItem? other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            return other.Timestamp.CompareTo(Timestamp);
        }

        public int CompareTo(object? other)
        {
            return CompareTo(other as SolutionCacheItem);
        }

        private bool AssignFrom(DotnetSolution solution, bool force)
        {
            if (force || FullName.Equals(solution.FullName, PathItem.PlatformComparison))
            {
                Debug.WriteLine($"{nameof(SolutionCacheItem)}.{nameof(AssignFrom)}");

                Update();
                FullName = solution.FullName;
                Properties.AssignFrom(solution.Properties);

                _projects.Clear();

                foreach (var item in solution.Projects.Values)
                {
                    var clone = new ProjectProperties();
                    clone.AssignFrom(item.Properties);
                    _projects.Add(clone);
                }

                return true;
            }

            return false;
        }

    }
}
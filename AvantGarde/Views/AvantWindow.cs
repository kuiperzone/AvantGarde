// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas (C) 2022
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

using Avalonia.Controls;
using AvantGarde.ViewModels;

namespace AvantGarde.Views
{
    /// <summary>
    /// A base class for application windows.
    /// </summary>
    public class AvantWindow<T> : Window
        where T : AvantViewModel
    {
        /// <summary>
        /// Constructor assigns model to DataContext.
        /// </summary>
        public AvantWindow(T model)
        {
            Model = model;
            DataContext = Model;
        }

        /// <summary>
        /// Gets the view model.
        /// </summary>
        protected T Model;

        /// <summary>
        /// Called by this class. Can be overridden, but call base.
        /// </summary>
        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            ScaleSize();
            // this.SetCenterFix();  // TBD
        }

        private void ScaleSize()
        {
            double f = GlobalModel.Global.AppFontSize / GlobalModel.DefaultFontSize;
            var w = Width * f;
            var h = Height * f;
            var mw = MinWidth * f;
            var xw = MaxWidth * f;
            var mh = MinHeight * f;
            var xh = MaxHeight * f;

            MinWidth = mw;
            MaxWidth = xw;
            MinHeight = mh;
            MaxHeight = xh;

            Width = w;
            Height = h;

            if (!CanResize)
            {
                if (double.IsFinite(w))
                {
                    MinWidth = w;
                    MaxWidth = w;
                }

                if (double.IsFinite(h))
                {
                    MinHeight = h;
                    MaxHeight = h;
                }
            }
        }

    }

    /// <summary>
    /// A non-generic variant with default constructor.
    /// </summary>
    public class AvantWindow : AvantWindow<AvantViewModel>
    {
        public AvantWindow()
            : base( new AvantViewModel())
        {
        }
    }

}
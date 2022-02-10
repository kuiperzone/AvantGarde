// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
// -----------------------------------------------------------------------------

using System;
using Avalonia.Controls;
using AvantGarde.Utility;
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
            this.SetCenterFix();
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
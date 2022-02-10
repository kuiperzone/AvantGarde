// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
// -----------------------------------------------------------------------------

using Avalonia.Media.Imaging;
using AvantGarde.Projects;

namespace AvantGarde.Loading
{
    /// <summary>
    /// Class used to return preview bitmap and related data.
    /// </summary>
    public class PreviewPayload
    {
        /// <summary>
        /// Gets the filename (short).
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the text content.
        /// </summary>
        public string? Text { get; set; }

        /// <summary>
        /// Gets the item path kind.
        /// </summary>
        public PathKind ItemKind { get; set; } = PathKind.AnyFile;

        /// <summary>
        /// Gets whether the item is project header.
        /// </summary>
        public bool IsProjectHeader { get; set; }

        /// <summary>
        /// Gets or sets if the preview is of a window.
        /// </summary>
        public bool IsWindow { get; set; }

        /// <summary>
        /// Gets or sets the window title.
        /// </summary>
        public string? WindowTitle { get; set; }

        /// <summary>
        /// Gets or sets the window icon. The value applies only if <see cref="IsWindow"/> is true.
        /// </summary>
        public Bitmap? WindowIcon { get; set; }

        /// <summary>
        /// Gets or sets whether the window can resize. The value applies only if <see cref="IsWindow"/> is true.
        /// </summary>
        public bool WindowCanResize { get; set; } = true;

        /// <summary>
        /// Gets the main preview bitmap. The initial value and value on failure is null.
        /// </summary>
        public Bitmap? Source { get; set; }

        /// <summary>
        /// Gets or sets the design width. NaN if unspecified.
        /// Used for local in-process preview only.
        /// </summary>
        public double DesignWidth { get; set; } = double.NaN;

        /// <summary>
        /// Gets or sets the design height. NaN if unspecified.
        /// Used for local in-process preview only.
        /// </summary>
        public double DesignHeight { get; set; } = double.NaN;

        /// <summary>
        /// Gets or sets width dimension.
        /// </summary>
        public ControlDimension Width { get; set; } = ControlDimension.Empty;

        /// <summary>
        /// Gets or sets height dimension.
        /// </summary>
        public ControlDimension Height { get; set; } = ControlDimension.Empty;

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public PreviewError? Error { get; set; }

        /// <summary>
        /// Gets or sets the process stdout (inc stderr).
        /// </summary>
        public string? Output { get; set; }

        /// <summary>
        /// Creates a clone. The <see cref="Source"/> and <see cref="WindowIcon"/>
        /// are referenced copied.
        /// </summary>
        public PreviewPayload Clone()
        {
            var clone = new PreviewPayload();

            clone.Name = Name;
            clone.Text = Text;
            clone.ItemKind = ItemKind;
            clone.IsProjectHeader = IsProjectHeader;
            clone.IsWindow = IsWindow;
            clone.WindowTitle = WindowTitle;
            clone.WindowIcon = WindowIcon;
            clone.WindowCanResize = WindowCanResize;
            clone.Source = Source;
            clone.DesignWidth = DesignWidth;
            clone.DesignHeight = DesignHeight;
            clone.Width = Width;
            clone.Height = Height;
            clone.Error = Error;
            clone.Output = Output;

            return clone;
        }


    }
}
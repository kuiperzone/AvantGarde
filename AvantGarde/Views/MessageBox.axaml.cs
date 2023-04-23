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
using Avalonia.Markup.Xaml;
using AvantGarde.Utility;
using AvantGarde.ViewModels;

namespace AvantGarde.Views
{
    /// <summary>
    /// A MessageBox class. It can be shown by calling on the static ShowDialog() methods, or by constucting
    /// an instance and calling the non-static <see cref="Window.ShowDialog(Window)"/> which returns the value
    /// type <see cref="MessageBox.BoxResult"/>. The default window title is set from the application name.
    /// </summary>
    public partial class MessageBox : AvantWindow
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public MessageBox()
        {
            InitializeComponent();
            Title = App.Current?.Name ?? string.Empty;
        }

        /// <summary>
        /// Button option type.
        /// </summary>
        [Flags]
        public enum BoxButtons
        {
            None = 0x0000,
            Ok = 0x0001,
            Yes = 0x0002,
            YesAll = 0x0004,
            No = 0x0008,
            NoAll = 0x0010,
            Cancel = 0x0020,
            Abort = 0x0040,
        }

        /// <summary>
        /// Gets or sets the message text.
        /// </summary>
        public string Message
        {
            get { return MessageText.Text ?? ""; }
            set { MessageText.Text = value; }
        }

        /// <summary>
        /// Gets or sets the buttons shown.
        /// </summary>
        public BoxButtons Buttons { get; set; } = BoxButtons.Ok;

        /// <summary>
        /// Shows an instance of <see cref="MessageBox"/>. A default is used for the window title.
        /// The result comprises a single <see cref="BoxButtons"/> flag value pertaining to the button clicked.
        /// </summary>
        public static Task<BoxButtons> ShowDialog(Window owner, string msg, BoxButtons btns = BoxButtons.Ok)
        {
            return ShowDialog(owner, msg, string.Empty, btns);
        }

        /// <summary>
        /// Shows an instance of <see cref="MessageBox"/>. The result comprises a single <see cref="BoxButtons"/>
        /// flag value pertaining to the button clicked.
        /// </summary>
        public static Task<BoxButtons> ShowDialog(Window owner, string msg, string title, BoxButtons btns = BoxButtons.Ok)
        {
            var box = new MessageBox();
            box.Message = msg;
            box.Buttons = btns;

            if (!string.IsNullOrWhiteSpace(title))
            {
                box.Title = title;
            }

            return box.ShowDialog<BoxButtons>(owner);
        }

        /// <summary>
        /// Shows an instance of <see cref="MessageBox"/> with exception information. If stack is true,
        /// the full error stack is shown, whereas only the message is shown if false. If null, the
        /// stack is shown only where DEBUG is defined. The result comprises a single <see cref="BoxButtons"/>
        /// flag value pertaining to the button clicked.
        /// </summary>
        public static Task<BoxButtons> ShowDialog(Window owner, Exception error, bool? stack = null)
        {
#if DEBUG
            stack ??= true;
#endif
            var msg = error.Message;

            if (stack == true)
            {
                msg += "\n\n";
                msg += error.ToString();
            }

            return ShowDialog(owner, msg, error.GetType().Name);
        }

        protected override void OnOpened(EventArgs e)
        {
            if (Buttons.HasFlag(BoxButtons.Ok))
            {
                AddButton("Ok", BoxButtons.Ok);
            }

            if (Buttons.HasFlag(BoxButtons.Yes))
            {
                AddButton("Yes", BoxButtons.Yes);
            }

            if (Buttons.HasFlag(BoxButtons.YesAll))
            {
                AddButton("Yes All", BoxButtons.YesAll);
            }

            if (Buttons.HasFlag(BoxButtons.No))
            {
                AddButton("No", BoxButtons.No);
            }

            if (Buttons.HasFlag(BoxButtons.NoAll))
            {
                AddButton("NoAll", BoxButtons.NoAll);
            }

            if (Buttons.HasFlag(BoxButtons.Cancel))
            {
                AddButton("Cancel", BoxButtons.Cancel);
            }

            if (Buttons.HasFlag(BoxButtons.Abort))
            {
                AddButton("Abort", BoxButtons.Abort);
            }

            this.SizeToContent = SizeToContent.WidthAndHeight;
            base.OnOpened(e);
            this.CanResize = false;
        }

        private void AddButton(string caption, BoxButtons rslt)
        {
            var btn = new Button();
            btn.Content = caption;
            btn.MinWidth = GlobalModel.Global.MinStdButtonWidth * 0.75;
            btn.MinHeight = GlobalModel.Global.MinStdButtonHeight;

            if (rslt == BoxButtons.Ok)
            {
                btn.IsDefault = true;
            }

            if (rslt == BoxButtons.Cancel)
            {
                btn.IsCancel = true;
            }

            btn.Click += (_, __) => { this.Close(rslt); };

            ButtonsPanel.Children.Add(btn);
        }
    }

}
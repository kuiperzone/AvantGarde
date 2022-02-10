// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
// -----------------------------------------------------------------------------

using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AvantGarde.Loading;
using AvantGarde.Projects;
using AvantGarde.Utility;
using AvantGarde.ViewModels;

namespace AvantGarde.Views
{
    /// <summary>
    /// XAML text with seconding output tab.
    /// </summary>
    public partial class XamlCodeControl : UserControl
    {
        private readonly CodeTextBox _codeBox;
        private readonly CodeTextBox _outputBox;
        private readonly XamlTextControlViewModel _model = new();

        /// <summary>
        /// Constructor.
        /// </summary>
        public XamlCodeControl()
        {
            DataContext = _model;
            AvaloniaXamlLoader.Load(this);

            _codeBox = this.FindOrThrow<CodeTextBox>("CodeBox");
            _outputBox = this.FindOrThrow<CodeTextBox>("OutputBox");
        }

        /// <summary>
        /// Gets whether payload kind is XAML.
        /// </summary>
        public bool HasXaml { get; private set; }

        /// <summary>
        /// Gets or sets process output text, so that text may be modified after update.
        /// </summary>
        public string? OutputText
        {
            get { return _model.OutputText; }

            set
            {
                if (_model.OutputText != value)
                {
                    _model.OutputText = value;
                    _outputBox.CaretIndex = int.MaxValue;
                }
            }
        }

        /// <summary>
        /// Updates. Returns true if <see cref="HasXaml"/> changes.
        /// </summary>
        public bool Update(PreviewPayload? payload)
        {
            bool temp = HasXaml;
            HasXaml = payload?.ItemKind == PathKind.Xaml;
            _model.CodeText = payload?.Text;
            OutputText = payload?.Output;
            return HasXaml != temp;
        }

        public string? GetSelectedText(bool focusedOnly = true)
        {
            return GetCheckedBox(focusedOnly)?.SelectedText;
        }

        public int GetCaretIndex(bool focusedOnly = true)
        {
            return GetCheckedBox(focusedOnly)?.CaretIndex ?? -1;
        }

        public Tuple<int, int>? GetCaretPos(bool focusedOnly = true)
        {
            return GetCheckedBox(focusedOnly)?.GetCaretPos();
        }

        public string? GetCaretLabel(bool focusedOnly = true)
        {
            return GetCheckedBox(focusedOnly)?.GetCaretLabel();
        }

        public void SetCaretPos(int line, int col, bool focus = true)
        {
            var box = GetCheckedBox(false);

            if (box != null)
            {
                box.SetCaretPos(line, col);

                if (focus)
                {
                    box.Focus();
                }
            }
        }

        public bool SelectAll()
        {
            var box = GetCheckedBox(false);

            if (box != null)
            {
                box.SelectAll();
                return true;
            }

            return false;
        }

        private CodeTextBox? GetCheckedBox(bool focusedOnly)
        {
            if (IsVisible)
            {
                CodeTextBox? rslt = null;

                if (_codeBox.IsVisible)
                {
                    rslt = _codeBox;
                }
                else
                if (_outputBox.IsVisible)
                {
                    rslt = _outputBox;
                }

                if (rslt != null && (rslt.IsFocused || !focusedOnly))
                {
                    return rslt;
                }
            }

            return null;
        }


    }
}
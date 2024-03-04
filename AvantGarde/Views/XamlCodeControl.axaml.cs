// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas (C) 2022-24
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
using AvantGarde.Loading;
using AvantGarde.Projects;
using AvantGarde.Utility;
using AvantGarde.ViewModels;

namespace AvantGarde.Views;

/// <summary>
/// XAML text with seconding output tab.
/// </summary>
public partial class XamlCodeControl : UserControl
{
    private readonly XamlTextControlViewModel _model = new();

    /// <summary>
    /// Constructor.
    /// </summary>
    public XamlCodeControl()
    {
        DataContext = _model;
        InitializeComponent();
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
                OutputBox.CaretIndex = int.MaxValue;
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

            if (CodeBox.IsVisible)
            {
                rslt = CodeBox;
            }
            else
            if (OutputBox.IsVisible)
            {
                rslt = OutputBox;
            }

            if (rslt != null && (rslt.IsFocused || !focusedOnly))
            {
                return rslt;
            }
        }

        return null;
    }


}
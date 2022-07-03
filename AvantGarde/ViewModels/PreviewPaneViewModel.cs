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

using AvantGarde.Views;
using ReactiveUI;

namespace AvantGarde.ViewModels
{
    public class PreviewPaneViewModel : PreviewOptionsViewModel
    {
        private string? _caretText;

        public PreviewPane? Owner;

        public string? StatusText
        {
            get { return IsDisableEventsChecked ? "Events Disabled" : null; }
        }

        public string? CaretText
        {
            get { return _caretText; }
            set { this.RaiseAndSetIfChanged(ref _caretText, value, nameof(CaretText)); }
        }

        public void CopyCommand()
        {
            Owner?.CopyToClipboard();
        }

        public void RestartCommand()
        {
            Owner?.OnRestart();
        }

        public void ToggleCommand()
        {
            if (Owner != null)
            {
                Owner.IsXamlViewOpen = !Owner.IsXamlViewOpen;
            }
        }

        protected override void OnFlagChanged(bool invoke)
        {
            this.RaisePropertyChanged(nameof(StatusText));
            base.OnFlagChanged(invoke);
        }
    }
}

// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
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

// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
// -----------------------------------------------------------------------------

using ReactiveUI;

namespace AvantGarde.ViewModels
{
    public class XamlTextControlViewModel : AvantViewModel
    {
        private const double DefButtonWidth = 80;

        private bool _isCodeChecked = true;
        private bool _isOutputChecked;
        private string? _codeText;
        private string? _outputText;

        public bool IsCodeChecked
        {
            get { return _isCodeChecked; }

            set
            {
                if (_isCodeChecked != value)
                {
                    _isCodeChecked = value;
                    IsOutputChecked = !value;
                    this.RaisePropertyChanged(nameof(IsCodeChecked));
                }
            }
        }

        public bool IsOutputChecked
        {
            get { return _isOutputChecked; }

            set
            {
                if (_isOutputChecked != value)
                {
                    _isOutputChecked = value;
                    IsCodeChecked = !value;
                    this.RaisePropertyChanged(nameof(IsOutputChecked));
                }
            }
        }

        public double MinButtonWidth
        {
            get { return DefButtonWidth *  Global.Scale; }
        }

        public string? CodeText
        {
            get { return _codeText; }
            set { this.RaiseAndSetIfChanged(ref _codeText, value, nameof(CodeText)); }
        }

        public string? OutputText
        {
            get { return _outputText; }
            set { this.RaiseAndSetIfChanged(ref _outputText, value, nameof(OutputText)); }
        }

        /// <summary>
        /// Override.
        /// </summary>
        protected override void ColorChangedHandler()
        {
            base.ColorChangedHandler();
        }

        /// <summary>
        /// Override.
        /// </summary>
        protected override void FontChangedHandler()
        {
            base.FontChangedHandler();
            this.RaisePropertyChanged(nameof(MinButtonWidth));
        }

    }
}

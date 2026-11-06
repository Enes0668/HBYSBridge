using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HBYSBridge.Models
{
    public class CommandMapping : INotifyPropertyChanged
    {
        private string _assignedInput = "— atanmadı —";
        private string _assignedAction = "";

        public string CommandId { get; set; } = "";
        public string CommandName { get; set; } = "";
        public string Category { get; set; } = ""; // "Kayıt" veya "Dinleme"
        public string DefaultShortcutText { get; set; } = "";
        public bool IsHoldCommand { get; set; } // Basılı tut komutu mu?
        public string VirtualKeyCombo { get; set; } = ""; // örn: "Ctrl+Alt+F7"

        public string AssignedInput
        {
            get => _assignedInput;
            set
            {
                if (_assignedInput != value)
                {
                    _assignedInput = value;
                    OnPropertyChanged();
                }
            }
        }

        public string AssignedAction
        {
            get => _assignedAction;
            set
            {
                if (_assignedAction != value)
                {
                    _assignedAction = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

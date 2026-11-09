using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HBYSBridge.Models
{
    public class CommandMapping : INotifyPropertyChanged
    {
        private string _assignedInput = "— atanmadı —";
        private string _assignedAction = "";

        public bool IsHeader { get; set; } = false;
        public string CommandId { get; set; } = "";
        public string CommandName { get; set; } = "";
        public string Category { get; set; } = "";
        public string DefaultShortcutText { get; set; } = "";
        public bool IsHoldCommand { get; set; }
        public string VirtualKeyCombo { get; set; } = "";

        public string AssignedInput
        {
            get => _assignedInput;
            set
            {
                if (_assignedInput != value)
                {
                    _assignedInput = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayAssignedText));
                    OnPropertyChanged(nameof(AssignedTextColor));
                }
            }
        }

        public string DisplayAssignedText
        {
            get
            {
                if (IsHeader) return "";
                if (string.IsNullOrWhiteSpace(AssignedInput) || AssignedInput == "— atanmadı —")
                {
                    return "— atanmadı —";
                }
                if (!string.IsNullOrWhiteSpace(DefaultShortcutText))
                {
                    return $"{DefaultShortcutText} ({AssignedInput})";
                }
                return AssignedInput;
            }
        }

        public string AssignedTextColor => (DisplayAssignedText == "— atanmadı —") ? "#888888" : "#222222";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

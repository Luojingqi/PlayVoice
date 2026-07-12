using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Controls;

namespace PlayVoice.Pages.Preset
{
    public class MenuItemModel : INotifyPropertyChanged
    {
        private string _title;
        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }

        private Page _view;
        public Page View
        {
            get => _view;
            set { _view = value; OnPropertyChanged(); }
        }

        public MenuItemModel(string title, Page view)
        {
            Title = title;
            View = view;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}

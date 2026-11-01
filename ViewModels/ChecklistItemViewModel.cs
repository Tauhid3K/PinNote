using System;
using System.Windows.Input;
using PinNote.Models;

namespace PinNote.ViewModels
{
    public class ChecklistItemViewModel : ViewModelBase
    {
        private readonly ChecklistItemModel _model;
        private readonly Action<ChecklistItemViewModel> _deleteAction;
        private readonly Action<ChecklistItemViewModel> _moveUpAction;
        private readonly Action<ChecklistItemViewModel> _moveDownAction;
        private readonly Action _onChangedAction;

        public ChecklistItemViewModel(
            ChecklistItemModel model,
            Action<ChecklistItemViewModel> deleteAction,
            Action<ChecklistItemViewModel> moveUpAction,
            Action<ChecklistItemViewModel> moveDownAction,
            Action onChangedAction)
        {
            _model = model;
            _deleteAction = deleteAction;
            _moveUpAction = moveUpAction;
            _moveDownAction = moveDownAction;
            _onChangedAction = onChangedAction;

            DeleteCommand = new RelayCommand(_ => _deleteAction(this));
            MoveUpCommand = new RelayCommand(_ => _moveUpAction(this));
            MoveDownCommand = new RelayCommand(_ => _moveDownAction(this));
        }

        public Guid Id => _model.Id;

        public string Text
        {
            get => _model.Text;
            set
            {
                if (_model.Text != value)
                {
                    _model.Text = value ?? string.Empty;
                    OnPropertyChanged();
                    _onChangedAction();
                }
            }
        }

        public bool IsChecked
        {
            get => _model.IsChecked;
            set
            {
                if (_model.IsChecked != value)
                {
                    _model.IsChecked = value;
                    OnPropertyChanged();
                    _onChangedAction();
                }
            }
        }

        public int Order
        {
            get => _model.Order;
            set
            {
                if (_model.Order != value)
                {
                    _model.Order = value;
                    OnPropertyChanged();
                    _onChangedAction();
                }
            }
        }

        public ICommand DeleteCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }

        public ChecklistItemModel GetModel() => _model;
    }
}

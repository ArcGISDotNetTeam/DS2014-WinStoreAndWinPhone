using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interactivity;

namespace DevSummitDemo
{
    public class EventToCommand : TriggerAction<DependencyObject>
    {
        public ICommand Command
        {
            get { return (ICommand)GetValue(CommandProperty); }
            set { SetValue(CommandProperty, value); }
        }

        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register("Command", typeof(ICommand), typeof(EventToCommand), new PropertyMetadata(null));

        public string CommandName { get; set; }

        public object CommandParameter
        {
            get { return (object)GetValue(CommandParameterProperty); }
            set { SetValue(CommandParameterProperty, value); }
        }

        public static readonly DependencyProperty CommandParameterProperty =
            DependencyProperty.Register("CommandParameter", typeof(object), typeof(Object), new PropertyMetadata(null));

        protected override void Invoke(object parameter)
        {
            if (Command != null && Command.CanExecute(parameter))
                Command.Execute(CommandParameter ?? parameter);
        }
        protected override void OnAttached()
        {
            base.OnAttached();
        }
        protected override void OnDetaching()
        {
            base.OnDetaching();
        }
    }
}

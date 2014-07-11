using System;
using System.Windows.Input;

namespace DevSummitDemo
{
	/// <summary>
	/// ICommand implementation that supports executing a simple Action
	/// </summary>
	internal class DelegateCommand : ICommand
	{
		Action m_Command;
		Action<object> m_CommandWithParameter;

		/// <summary>
		/// Initializes a new instance of the <see cref="DelegateCommand"/> class.
		/// </summary>
		/// <param name="command">The command.</param>
		public DelegateCommand(Action command)
		{
			this.m_Command = command;
		}
		/// <summary>
		/// Initializes a new instance of the <see cref="DelegateCommand"/> class.
		/// </summary>
		/// <param name="command">The command.</param>
		public DelegateCommand(Action<object> command)
		{
			m_CommandWithParameter = command;
		}
		/// <summary>
		/// Determines whether this instance can execute the specified parameter.
		/// </summary>
		/// <param name="parameter">The parameter.</param>
		/// <returns></returns>
		public bool CanExecute(object parameter)
		{
			return (m_CommandWithParameter != null || m_Command != null);
		}

		/// <summary>
		/// Occurs when changes occur that affect whether the command should execute.
		/// </summary>
		public event EventHandler CanExecuteChanged;

		/// <summary>
		/// Executes the specified parameter.
		/// </summary>
		/// <param name="parameter">The parameter.</param>
		public void Execute(object parameter)
		{
			if (m_CommandWithParameter != null)
				m_CommandWithParameter(parameter);
			else
				m_Command();
		}
	}
}

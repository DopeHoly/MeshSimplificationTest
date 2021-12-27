using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MeshSimplificationTest
{
    /// <summary>
    /// Класс реализация ICommand, позволяющий выполнять действие и управлять возможностью его выполнения в данынй момент (работает CanExecute).
    /// </summary>
    public class DelegateCommand : ICommand
    {
        private Action<object> execute;
        private Func<object, bool> canExecute;

        /// <summary>
        /// Вызывается при изменении условия CanExecute. (Система сама вызывает.)
        /// </summary>
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// Конструктор, задающий действие и условие возможности его выполнения.
        /// </summary>
        /// <param name="execute">Делегат с одним аргументом типа object (параметром команды) - действие которое выполнится при вызове команды.</param>
        /// <param name="canExecute">Делегат, возвращающий булево значение и принимающий параметр команды типа object - проверка условия возможности выполнить команду.</param>
        public DelegateCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        /// <summary>
        /// Условие возможности выполнения команды. Система использует сама.
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public bool CanExecute(object parameter)
        {
            return this.canExecute == null || this.canExecute(parameter);
        }

        /// <summary>
        /// Исполнение команды. Система использует сама.
        /// </summary>
        /// <param name="parameter"></param>
        public void Execute(object parameter)
        {
            this.execute(parameter);
        }
    }
}

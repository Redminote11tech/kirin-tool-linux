// Licensed to Kethily Daniel & NDXCode under one or more agreements.
// Kethily Daniel & NDXCode licenses this file to you under the Business Source License 1.1.
// See the LICENSE file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Threading;
using Kirin_Tool.Models;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Kirin_Tool.UI
{
    public partial class ProgressDialog : Window
    {
        public ObservableCollection<ProgressItemViewModel> ProgressItems { get; }
        private bool operationComplete;

        public ProgressDialog()
        {
            InitializeComponent();
        }

        public ProgressDialog(ObservableCollection<ProgressItemViewModel> items)
        {
            InitializeComponent();
            ProgressItems = items;
            DataContext = this;
        }

        public void UpdateOverallStatus(string status)
        {
            Dispatcher.UIThread.InvokeAsync(() => OverallStatusTextBlock.Text = status);
        }

        public void ShowCloseButton(bool isSuccess = true, string title = null)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                operationComplete = true;
                CloseButton.IsVisible = true;
                Title = title ?? (isSuccess ? "Operation Complete" : "Operation Failed");
            });
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            if (!operationComplete) e.Cancel = true;
            base.OnClosing(e);
        }

        private void CloseButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close();
        }

        public void EnableOKButton(bool isSuccess = true)
        {
            ShowCloseButton(isSuccess);
        }
    }
}

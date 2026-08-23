// Licensed to Kethily Daniel & NDXCode under one or more agreements.
// Kethily Daniel & NDXCode licenses this file to you under the Business Source License 1.1.
// See the LICENSE file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace Kirin_Tool.UI
{
    public partial class FrpStepDialog : Window
    {
        public FrpStepDialog()
        {
            InitializeComponent();
        }

        public void UpdateStepStatus(int stepNumber, bool success)
        {
            Dispatcher.UIThread.InvokeAsync(() => UpdateStepStatusInternal(stepNumber, success));
        }

        private void UpdateStepStatusInternal(int stepNumber, bool success)
        {
            var icon = stepNumber switch
            {
                1 => Step1Icon,
                2 => Step2Icon,
                3 => Step3Icon,
                _ => null
            };

            if (icon != null)
            {
                icon.Text = success ? "\u2713" : "\u2717";
                icon.Foreground = success ? Brushes.Green : Brushes.Red;
            }
        }

        public void UpdateOverallStatus(string status)
        {
            Dispatcher.UIThread.InvokeAsync(() => OverallStatusTextBlock.Text = status);
        }

        public void ShowCloseButton(bool isSuccess = true)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                CloseButton.IsVisible = true;
                Title = isSuccess ? "FRP Bypass Complete" : "FRP Bypass Failed";
            });
        }

        private void CloseButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close();
        }
    }
}

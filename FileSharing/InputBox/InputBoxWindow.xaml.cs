using System;
using System.Windows;

namespace InputBox
{
    public partial class InputBoxWindow : Window
    {
        public InputBoxWindow(string title, string question, string defaultAnswer = "")
        {
            InitializeComponent();

            Title = title;
            Question.Content = question;
            UserAnswer.Text = defaultAnswer;
        }

        public string Answer => UserAnswer.Text;

        private void OnOkClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void OnContentRendered(object sender, EventArgs e)
        {
            UserAnswer.SelectAll();
            UserAnswer.Focus();
        }
    }
}
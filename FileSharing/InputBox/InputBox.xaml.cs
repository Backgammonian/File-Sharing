using System;
using System.Windows;

namespace FileSharing.InputBox
{
    public partial class InputBox : Window
    {
        public InputBox(string title, string question, string defaultAnswer = "")
        {
            InitializeComponent();

            Title = title;
            _question.Content = question;
            _answer.Text = defaultAnswer;
        }

        public string Answer => _answer.Text;

        private void OkClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void OnContentRendered(object sender, EventArgs e)
        {
            _answer.SelectAll();
            _answer.Focus();
        }
    }
}
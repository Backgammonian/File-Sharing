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
            lblQuestion.Content = question;
            txtAnswer.Text = defaultAnswer;
        }

        public string Answer => txtAnswer.Text;

        private void btnDialogOk_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            txtAnswer.SelectAll();
            txtAnswer.Focus();
        }
    }
}
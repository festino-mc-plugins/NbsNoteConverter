using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace NoteConverter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<List<string>> Books = new List<List<string>>();
        private int Book = 0;
        private int Page = 0;
        private readonly List<RadioButton> BookButtons = new List<RadioButton>();
        private readonly List<Button> PageButtons = new List<Button>();

        public MainWindow()
        {
            InitializeComponent();
            SetPage(0); // to block buttons
        }
        public void Convert(string path)
        {
            FilePathBox.Text = path;
            try
            {
                Books = Converter.Convert(path);
            }
            catch (Exception e)
            {
                PageTextBox.Foreground = Brushes.Red;
                PageTextBox.Text = e.Message;
                return;
            }
            PageTextBox.Foreground = Brushes.Black;
            RecreateBooks();
        }

        public void SetBook(int n)
        {
            if (n < 0 || n >= Books.Count)
                return;
            Book = n;
            BookButtons[n].IsChecked = true;
            RecreatePages();
        }

        public void SetPage(int n)
        {
            if (Book < 0 || Book > Books.Count - 1
                    || n < 0 || n > Books[Book].Count - 1)
            {
                CopyButton.IsEnabled = false;
                BackButton.IsEnabled = false;
                NextButton.IsEnabled = false;
                return;
            }
            CopyButton.IsEnabled = true;
            if (n == 0)
                BackButton.IsEnabled = false;
            else
                BackButton.IsEnabled = true;
            if (n == Books[Book].Count - 1)
                NextButton.IsEnabled = false;
            else
                NextButton.IsEnabled = true;

            if (0 <= Page && Page < PageButtons.Count)
                PageButtons[Page].IsEnabled = true;
            Page = n;
            PageButtons[n].IsEnabled = false;
            PageTextBox.Text = Books[Book][Page];
            // set page button
        }

        private void NextButton_Click(object sender, RoutedEventArgs e) // TODO separate logic to incapsulate
        {
            SetPage(Page + 1);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            SetPage(Page - 1);
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(PageTextBox.Text);
            if (NextCopyCheckBox.IsChecked.HasValue && NextCopyCheckBox.IsChecked.Value)
                SetPage(Page + 1);
        }

        private void RecreateBooks()
        {
            BookButtons.Clear();
            BookPanel.Children.Clear();
            for (int i = 0; i < Books.Count; i++)
            {
                RadioButton button = new RadioButton();
                button.Margin = new Thickness(3, 0, 3, 0);
                button.GroupName = "books";
                button.Content = (i + 1).ToString();
                button.Checked += BookButton_Checked;
                BookButtons.Add(button);
                BookPanel.Children.Add(button);
            }
            SetBook(0);
        }

        private void RecreatePages()
        {
            PageButtons.Clear();
            PagePanel.Children.Clear();
            for (int i = 0; i < Books[Book].Count; i++)
            {
                Button button = new Button();
                button.Width = button.Height = 25;
                button.Margin = new Thickness(2);
                button.Content = (i + 1).ToString();
                button.Click += PageButton_Click;
                PageButtons.Add(button);
                PagePanel.Children.Add(button);
            }
            SetPage(0);
        }

        private void BookButton_Checked(object sender, RoutedEventArgs e)
        {
            RadioButton button = (RadioButton)sender;
            int num = int.Parse((string)button.Content);
            SetBook(num - 1);
        }

        private void PageButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            int num = int.Parse((string)button.Content);
            SetPage(num - 1);
        }

        private void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "OpenNBS файлы (*.nbs)|*.nbs";
            //openFileDialog.Filter = "OpenNBS файлы (*.nbs)|*.nbs|Все файлы (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
                Convert(openFileDialog.FileName);
        }

        private void ConvertButton_Click(object sender, RoutedEventArgs e)
        {
            Convert(FilePathBox.Text);
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            // https://www.codeproject.com/Questions/514592/DragplusandplusdropplusWPFplusC-23plusgettingplusf
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];

                // Assuming you have one file that you care about, pass it off to whatever
                // handling code you have defined.
                Convert(files[0]);

            }
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            // https://stackoverflow.com/questions/724774/wpf-filedrop-event-just-allow-a-specific-file-extension
            bool dropEnabled = true;
            if (e.Data.GetDataPresent(DataFormats.FileDrop, true))
            {
                string[] filenames = e.Data.GetData(DataFormats.FileDrop, true) as string[];

                foreach (string filename in filenames)
                {
                    if (System.IO.Path.GetExtension(filename).ToUpperInvariant() != ".NBS")
                    {
                        dropEnabled = false;
                        break;
                    }
                }
            }
            else
            {
                dropEnabled = false;
            }

            if (!dropEnabled)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
            }
        }
    }
}

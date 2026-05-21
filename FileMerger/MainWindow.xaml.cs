using FileMerger.ViewModels;
using System.Windows;


namespace FileMerger;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private void Window_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var filePaths = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (DataContext is MainViewModel vm)
            {
                await vm.AddFilesFromPathsAsync(filePaths);
            }
        }
    }
}

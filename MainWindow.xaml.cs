using Microsoft.Win32;
using System;
using System.Collections.Generic;
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
using Media_Organizer.Classes;
using System.IO;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.ComponentModel;
using System.Threading;

namespace Media_Organizer
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
			InitializeComponent();
		}

		public BackgroundWorker bw = new BackgroundWorker();
		public string target;
		public string destination;

		private void Button_Click_BrowseTarget(object sender, RoutedEventArgs e)
		{
			CommonOpenFileDialog dialog = new CommonOpenFileDialog();
			dialog.InitialDirectory = "";
			dialog.IsFolderPicker = true;
			if(dialog.ShowDialog() == CommonFileDialogResult.Ok)
			{
				targetDirectoryTextBox.Text = dialog.FileName;
			}
		}

		private void Button_Click_BrowseDestination(object sender, RoutedEventArgs e)
		{
			CommonOpenFileDialog dialog = new CommonOpenFileDialog();
			dialog.InitialDirectory = "";
			dialog.IsFolderPicker = true;
			if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
			{
				destinationDirectoryTextBox.Text = dialog.FileName;
			}
		}

		private void Button_Click_Organize(object sender, RoutedEventArgs e)
		{
			bw.WorkerReportsProgress = true;
			bw.WorkerSupportsCancellation = true;
			bw.DoWork += bw_DoWork;
			bw.ProgressChanged += bw_ProgressChanged;
			bw.RunWorkerCompleted += bw_RunWorkerCompleted;
			bw.RunWorkerAsync();
		}

		private void bw_DoWork(object sender, DoWorkEventArgs e)
		{
			this.Dispatcher.Invoke(() =>
			{
				target = targetDirectoryTextBox.Text;
				destination = destinationDirectoryTextBox.Text;
			});
			try
			{
				while (!bw.CancellationPending)
				{
					//target and destination must exist AND destination cannot be within the target directory
					if (Directory.Exists(target) && Directory.Exists(destination) && target != destination)
					{
						RecursiveFileProcessor rfp = new RecursiveFileProcessor(target, destination);
						this.Dispatcher.Invoke(() =>
						{
							filesToProcess.Text = System.IO.Directory.GetFiles(target, "*", SearchOption.AllDirectories).Length.ToString();
						});

						rfp.Run(bw);
						Thread.Sleep(1000);
						MessageBox.Show("Organization Complete!");
					}
					else
					{
						MessageBox.Show("Directories must exist and cannot be the same.");
					}
					bw.CancelAsync();
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
				bw.CancelAsync();
			}
		}

		private void bw_ProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			filesProcessed.Text = e.ProgressPercentage.ToString();
			int percent = e.ProgressPercentage * 100 / Convert.ToInt32(filesToProcess.Text);
			fileProgress.Value = percent;
			filesProgressPercent.Text = percent.ToString()+"%";
		}

		private void bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			filesProcessed.Text = "0";
			filesToProcess.Text = "0";
			fileProgress.Value = 0;
			filesProgressPercent.Text = "";
		}
	}
}

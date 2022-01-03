using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using MetadataExtractor;
using System.Linq;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.QuickTime;
using System.Globalization;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Media_Organizer.Classes
{
	class RecursiveFileProcessor
	{
		#region Constants

		const string DEFAULTMETADATAID = "File - File Modified Date = ";

		public static readonly string[] JPGMETADATADATEIDS =
			{
				"Exif IFD0 - Date/Time = ", //jpeg first choice
				"Exif SubIFD - Date/Time Original = " //jpeg second choice
			};
		public static readonly string[] PNGMETADATADATEIDS =
			{
				"QuickTime Track Header - Created = " //png first choice
			};
		public static readonly string[] MOVMETADATADATEIDS =
			{
				"QuickTime Metadata Header - Creation Date = ", //mov first choice
				"QuickTime Track Header - Created = " //mov second choice
			};
		public static readonly string[] MP4METADATADATEIDS =
			{
				"QuickTime Track Header - Created = " //mp4 first choice
			};

		#endregion Constants

		public RecursiveFileProcessor(string targetDirectory, string destinationDirectory)
		{
			DestinationDirectory = destinationDirectory;
			TargetDirectory = targetDirectory;
		}

		public string TargetDirectory { get; set; }
		public string DestinationDirectory { get; set; }

		public int FilesProcessed { get; set; }
		public int FilesToProcess { get; set; }

		static object x = new object();

		public void Run(BackgroundWorker bw)
		{
			if (TargetDirectory == DestinationDirectory)
			{
				return;
			}

			if (File.Exists(TargetDirectory))
			{
				// This path is a file
				ProcessFile(TargetDirectory, DestinationDirectory);
			}
			else if (System.IO.Directory.Exists(TargetDirectory))
			{
				// This path is a directory
				FilesToProcess = System.IO.Directory.GetFiles(TargetDirectory, "*", SearchOption.AllDirectories).Length;
				ProcessDirectory(TargetDirectory, DestinationDirectory, bw);
			}
		}

		#region Main Methods

		public void ProcessFile(string targetFilePath, string destinationDirectory)
		{
			FileInfo fileInfo = new FileInfo(targetFilePath);
			if (fileInfo.Extension.ToLower() == ".ds_store" || fileInfo.Extension.ToLower() == ".db")
			{
				return;
			}

			List<string> metadataList = new List<string>(GetMetaDataList(targetFilePath));
			DateTime? dateTime = GetCreatedDateFromMetadata(metadataList,fileInfo.Extension);

			if (dateTime != null)
			{
				string newFileName = dateTime?.ToString("yyyy-MM-dd HH-mm-ss");
				string subFolderPath = CreateMonthDirectoryFromFileName(destinationDirectory, newFileName);
				string newFullFilePath = Path.Combine(subFolderPath, newFileName) + fileInfo.Extension;

				//Add #'s to end of file if duplicate name exists
				int count = 0;
				lock (x)
				{
					while (File.Exists(newFullFilePath) && !FilesAreEqual(fileInfo, new FileInfo(newFullFilePath)))
					{
						count++;
						newFileName = newFileName.Substring(0, 19) + " #" + count.ToString();
						newFullFilePath = Path.Combine(subFolderPath, newFileName) + fileInfo.Extension;
					}
				}
				if (!File.Exists(newFullFilePath))
				{
					File.Copy(targetFilePath, newFullFilePath);
				}
			}
			else //if no dateTime is found, it goes to an Errors folder
			{
				string errorDirectory = Path.Combine(destinationDirectory, "Errors");
				if (!System.IO.Directory.Exists(errorDirectory))
				{
					System.IO.Directory.CreateDirectory(errorDirectory);
				}
				File.Copy(targetFilePath, Path.Combine(errorDirectory, fileInfo.Name));
			}
		}

		public void ProcessDirectory(string targetDirectory, string destinationDirectory, BackgroundWorker bw)
		{
			// Process the list of files found in the directory.
			string[] fileEntries = System.IO.Directory.GetFiles(targetDirectory);

			_ = Parallel.ForEach(fileEntries, fileName =>
			  {
				  ProcessFile(fileName, destinationDirectory);
				  FilesProcessed++;
				  bw.ReportProgress(FilesProcessed);
			  });

			// Recurse into subdirectories of this directory.
			string[] subdirectoryEntries = System.IO.Directory.GetDirectories(targetDirectory);
			foreach (string subdirectory in subdirectoryEntries)
			{
				if(subdirectory != destinationDirectory)
				{
					ProcessDirectory(subdirectory, destinationDirectory, bw);
				}
			}
			bw.ReportProgress(FilesProcessed);
		}
		#endregion Main Methods

		#region Metadata Processing
		public static List<string> GetMetaDataList(string targetFilePath)
		{
			IEnumerable<MetadataExtractor.Directory> metadataDirectories = ImageMetadataReader.ReadMetadata(targetFilePath);
			List<string> metadataList = new List<string>();
			foreach (var directory in metadataDirectories)
				foreach (var tag in directory.Tags)
					metadataList.Add($"{directory.Name} - {tag.Name} = {tag.Description}");
			return metadataList;
		}

		public DateTime? GetCreatedDateFromMetadata(List<string> metadataList, string extension)
		{
			DateTime? dateTime = null;
			string dateTimeRaw;

			if (extension.ToLower() == ".jpg" || extension.ToLower() == ".jpeg")
			{
				dateTimeRaw = metadataList.Find(x => x.Contains(JPGMETADATADATEIDS[0]));
				if (dateTimeRaw != null)
				{
					dateTime = ExtractDateTimeFromExifIFD0Date(dateTimeRaw);
				}
				else
				{
					dateTimeRaw = metadataList.Find(x => x.Contains(JPGMETADATADATEIDS[1]));
					if (dateTimeRaw != null)
					{
						dateTime = ExtractDateTimeFromExifSubIFDDate(dateTimeRaw);
					}
				}
			}

			else if (extension.ToLower() == ".png")
			{
				dateTimeRaw = metadataList.Find(x => x.Contains(PNGMETADATADATEIDS[0]));
				if (dateTimeRaw != null)
				{
					dateTime = ExtractDateTimeFromQuicktimeTrackHeaderDate(dateTimeRaw);
				}
			}

			else if (extension.ToLower() == ".mov")
			{
				dateTimeRaw = metadataList.Find(x => x.Contains(MOVMETADATADATEIDS[0]));
				if (dateTimeRaw != null)
				{
					dateTime = ExtractDateTimeFromQuicktimeMetadataHeaderDate(dateTimeRaw);
				}
				else
				{
					dateTimeRaw = metadataList.Find(x => x.Contains(MOVMETADATADATEIDS[1]));
					if (dateTimeRaw != null)
					{
						dateTime = ExtractDateTimeFromQuicktimeTrackHeaderDate(dateTimeRaw);
					}
				}
			}

			else if (extension.ToLower() == ".mp4")
			{
				dateTimeRaw = metadataList.Find(x => x.Contains(MP4METADATADATEIDS[0]));
				if (dateTimeRaw != null)
				{
					dateTime = ExtractDateTimeFromQuicktimeTrackHeaderDate(dateTimeRaw);
				}
			}

			if (dateTime == null || dateTime.Value.Year < 1995)
			{
				dateTimeRaw = metadataList.Find(x => x.Contains(DEFAULTMETADATAID));
				if (dateTimeRaw != null)
				{
					dateTime = ExtractDateTimeFromModDate(dateTimeRaw);
				}
			}
			return dateTime;
		}
		#endregion Metadata Processing

		#region Mapping DateTime Methods
		public DateTime? ExtractDateTimeFromModDate(string dateTimeRaw)
		{
			DateTime? dateTime = null;
			if (dateTimeRaw != null)
			{
				var temp = dateTimeRaw.Split(' ', ':');
				int year = Convert.ToInt32(temp[14]);
				int month = Convert.ToInt32(DateTime.ParseExact(temp[7], "MMM", CultureInfo.CurrentCulture).Month);
				int day = Convert.ToInt32(DateTime.ParseExact(temp[8], "dd", CultureInfo.CurrentCulture).Day);
				int hour = Convert.ToInt32(temp[9]);
				int minute = Convert.ToInt32(temp[10]);
				int second = Convert.ToInt32(temp[11]);

				dateTime = new DateTime(year, month, day, hour, minute, second);
			}
			return dateTime;
		}

		public DateTime? ExtractDateTimeFromExifIFD0Date(string dateTimeRaw)
		{
			DateTime? dateTime = null;
			if (dateTimeRaw != null)
			{
				var temp = dateTimeRaw.Split(' ', ':');
				int year = Convert.ToInt32(temp[5]);
				int month = Convert.ToInt32(temp[6]);
				int day = Convert.ToInt32(temp[7]);
				int hour = Convert.ToInt32(temp[8]);
				int minute = Convert.ToInt32(temp[9]);
				int second = Convert.ToInt32(temp[10]);

				dateTime = new DateTime(year, month, day, hour, minute, second);
			}
			return dateTime;
		}

		public DateTime? ExtractDateTimeFromExifSubIFDDate(string dateTimeRaw)
		{
			DateTime? dateTime = null;
			if (dateTimeRaw != null)
			{
				var temp = dateTimeRaw.Split(' ', ':');
				int year = Convert.ToInt32(temp[6]);
				int month = Convert.ToInt32(temp[7]);
				int day = Convert.ToInt32(temp[8]);
				int hour = Convert.ToInt32(temp[9]);
				int minute = Convert.ToInt32(temp[10]);
				int second = Convert.ToInt32(temp[11]);

				dateTime = new DateTime(year, month, day, hour, minute, second);
			}
			return dateTime;
		}

		public DateTime? ExtractDateTimeFromQuicktimeMetadataHeaderDate(string dateTimeRaw)
		{
			DateTime? dateTime = null;
			if (dateTimeRaw != null)
			{
				var temp = dateTimeRaw.Split(' ', ':');
				int year = Convert.ToInt32(temp[15]);
				int month = Convert.ToInt32(DateTime.ParseExact(temp[8], "MMM", CultureInfo.CurrentCulture).Month);
				int day = Convert.ToInt32(DateTime.ParseExact(temp[9], "dd", CultureInfo.CurrentCulture).Day);
				int hour = Convert.ToInt32(temp[10]);
				int minute = Convert.ToInt32(temp[11]);
				int second = Convert.ToInt32(temp[12]);

				dateTime = new DateTime(year, month, day, hour, minute, second);
			}
			return dateTime;
		}

		public DateTime? ExtractDateTimeFromQuicktimeTrackHeaderDate(string dateTimeRaw)
		{
			DateTime? dateTime = null;
			if (dateTimeRaw != null)
			{
				var temp = dateTimeRaw.Split(' ', ':');
				int year = Convert.ToInt32(temp[12]);
				int month = Convert.ToInt32(DateTime.ParseExact(temp[7], "MMM", CultureInfo.CurrentCulture).Month);
				int day = Convert.ToInt32(DateTime.ParseExact(temp[8], "dd", CultureInfo.CurrentCulture).Day);
				int hour = Convert.ToInt32(temp[9]);
				int minute = Convert.ToInt32(temp[10]);
				int second = Convert.ToInt32(temp[11]);

				dateTime = new DateTime(year, month, day, hour, minute, second);
			}
			return dateTime;
		}
		#endregion Mapping DateTime Methods

		#region Tools
		public string CreateMonthDirectoryFromFileName(string destinationPath, string fileName)
		{
			var cleanedFileName = fileName.Split('-', ' ');
			string directoryName = $"{cleanedFileName[0]}-{cleanedFileName[1]}";
			string directoryFullPath = Path.Combine(destinationPath, directoryName);
			if (!System.IO.Directory.Exists(directoryFullPath))
			{
				System.IO.Directory.CreateDirectory(directoryFullPath);
			}
			return directoryFullPath;
		}

		public bool FilesAreEqual(FileInfo first, FileInfo second)
		{
			//const int BYTES_TO_READ = sizeof(Int64);

			if (first.Length != second.Length)
				return false;

			//if (string.Equals(first.FullName, second.FullName, StringComparison.OrdinalIgnoreCase))
			//	return true;

			//int iterations = (int)Math.Ceiling((double)first.Length / BYTES_TO_READ);

			//using (FileStream fs1 = first.OpenRead())
			//using (FileStream fs2 = second.OpenRead())
			//{
			//	byte[] one = new byte[BYTES_TO_READ];
			//	byte[] two = new byte[BYTES_TO_READ];

			//	for (int i = 0; i < iterations; i++)
			//	{
			//		fs1.Read(one, 0, BYTES_TO_READ);
			//		fs2.Read(two, 0, BYTES_TO_READ);

			//		if (BitConverter.ToInt64(one, 0) != BitConverter.ToInt64(two, 0))
			//			return false;
			//	}
			//}

			return true;
		}
		#endregion Tools
	}
}

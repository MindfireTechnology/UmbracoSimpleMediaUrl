using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using Umbraco.Core;
using Umbraco.Core.Events;
using Umbraco.Core.Models;
using Umbraco.Core.Services;

namespace UmbracoSimpleMediaUrl
{
	public sealed class SimpleUrlMediaService : ApplicationEventHandler
	{
		protected override void ApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
		{
			base.ApplicationStarted(umbracoApplication, applicationContext);

			MediaService.Saving += MediaService_Saving;
			MediaService.Moved += MediaService_Moved;
		}

		private void MediaService_Moved(IMediaService sender, MoveEventArgs<IMedia> e)
		{
			foreach (var mediaItem in e.MoveInfoCollection)
				sender.Save(mediaItem.Entity);
		}

		private void MediaService_Saving(IMediaService sender, SaveEventArgs<IMedia> e)
		{
			foreach (var mediaItem in e.SavedEntities)
				RelocateFile(sender, mediaItem);
		}

		private void RelocateFile(IMediaService sender, IMedia mediaItem)
		{
			if (!mediaItem.Properties.Contains("umbracoFile"))
				return;

			// Get file and extension data
			string file = (string)mediaItem.Properties["umbracoFile"].Value;
			//string ext = mediaItem.Properties["umbracoExtension"].Value;

			string parentPath = GetParentPath(sender, mediaItem);

			if (!string.IsNullOrWhiteSpace(file))
			{
				// Website name, node path
				string newpath = "/" + Path.Combine("media", parentPath, Path.GetFileName(file)).Replace('\\', '/');

				if (!newpath.Equals(file, StringComparison.CurrentCultureIgnoreCase))
				{

					MoveFile(file, newpath);
					MoveFile(AppendFileName(file, "_thumb"), AppendFileName(newpath, "_thumb"));
					MoveFile(AppendFileName(file, "_big-thumb"), AppendFileName(newpath, "_big-thumb"));

					// Change the path
					mediaItem.Properties["umbracoFile"].Value = newpath;
					if (mediaItem.Properties.Contains("localPath"))
						mediaItem.Properties["localPath"].Value = newpath;

					sender.Save(mediaItem);
				}
			}
		}

		private string AppendFileName(string file, string value)
		{
			return Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file) + value + Path.GetExtension(file));
		}

		private static void MoveFile(string oldRelativePath, string newRelativePath)
		{
			// Move the file & delete the old file / paths
			string newFullPath = HttpContext.Current.Server.MapPath(newRelativePath);
			string oldFullPath = HttpContext.Current.Server.MapPath(oldRelativePath);

			Directory.CreateDirectory(Path.GetDirectoryName(newFullPath));
			if (System.IO.File.Exists(newFullPath))
				System.IO.File.Delete(newFullPath);

			if (System.IO.File.Exists(oldFullPath))
			{
				System.IO.File.Copy(oldFullPath, newFullPath);
				System.IO.File.Delete(oldFullPath);
			}

			DeleteOldPathIfEmpty(oldFullPath);
		}

		private static void DeleteOldPathIfEmpty(string oldFullPath)
		{
			// Delete empty directories (these are the 1001, etc directories left behind from the old path)
			string oldDirName = Path.GetDirectoryName(oldFullPath);
			if (Directory.Exists(oldDirName) &&
				!Directory.EnumerateFiles(oldDirName, "*.*", SearchOption.AllDirectories).Any())
				Directory.Delete(oldDirName, true);
		}

		private string GetParentPath(IMediaService sender, IMedia mediaItem)
		{
			string result = string.Empty;
			IMedia current = mediaItem;
			IMedia parent = null;
			do
			{
				parent = sender.GetParent(current);
				if (parent != null)
					result = "\\" + parent.Name + result;
				
				current = parent;

			} while (parent != null);

			return result.TrimStart('\\');
		}
	}
}

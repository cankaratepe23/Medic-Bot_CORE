using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using DSharpPlus.Entities;

namespace MedicBot
{
    [Serializable]
    public class AudioEntry : IComparable
    {
        public enum AudioType
        {
            File,
            Url,
            Youtube,
            Attachment
        }
        public string Name { get; set; }
        private string _extension;
        public string Extension
        {
            get
            {
                if (_extension == null)
                {
                    _extension = ".opus";
                }
                return _extension;
            }
            set => _extension = value;
        }
        public string FileName
        {
            get
            {
                return Name + Extension;
            }
        }
        public AudioType Type { get; set; }
        private string _path;
        public string Path
        {
            get
            {
                if (this.Type == AudioType.File)
                {
                    return System.IO.Path.Combine(AudioHelper.ResDirectory, FileName);
                }
                else
                {
                    return _path;
                }
            }
            set
            {
                if (Type == AudioType.File)
                {
                    throw new InvalidOperationException("Path cannot be set when type is File. Path is always computed inside the getter.");
                }
                else
                {
                    _path = value;
                }
            }
        }
        public DateTime CreationDate { get; set; }
        public ulong OwnerId { get; set; }
        public string DownloadedFrom { get; set; }
        public string SecondaryPath { get; set; }
        public List<string> Aliases { get; set; }
        public List<string> Collections { get; set; }

        public AudioEntry(string name, string extension, AudioType type, ulong ownerId, string downloadedFrom)
        {
            Name = name;
            Extension = extension;
            Type = type;
            OwnerId = ownerId;
            DownloadedFrom = downloadedFrom;
            CreationDate = File.GetLastWriteTimeUtc(Path);
            Aliases = new List<string>();
            Collections = new List<string>();
        }

        public AudioEntry(Uri uri)
        {
            Aliases = null;
            Collections = null;

            if (uri.Host == "www.youtube.com" || uri.Host == "youtu.be" || uri.Host == "youtube.com")
            {
                ProcessStartInfo ydlStartInfo = new ProcessStartInfo()
                {
                    FileName = "youtube-dl",
                    Arguments = "-f bestaudio --get-title " + uri.ToString(),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                Process youtubeDl = Process.Start(ydlStartInfo);
                youtubeDl.WaitForExit();
                string stderr = youtubeDl.StandardError.ReadToEnd();
                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    throw new Exception(stderr);
                }
                string name = youtubeDl.StandardOutput.ReadToEnd();
                ydlStartInfo.Arguments = "-f bestaudio -g " + uri.ToString();
                youtubeDl = Process.Start(ydlStartInfo);
                youtubeDl.WaitForExit();
                stderr = youtubeDl.StandardError.ReadToEnd();
                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    throw new Exception(stderr);
                }
                string path = youtubeDl.StandardOutput.ReadToEnd();
                youtubeDl.Dispose();

                Name = name;
                Type = AudioType.Youtube;
                Path = path;
                SecondaryPath = uri.ToString();
            }
            else
            {
                Name = uri.PathAndQuery;
                Type = AudioType.Url;
                Path = uri.OriginalString;
                SecondaryPath = null;
            }
        }

        public AudioEntry(DiscordAttachment attachment)
        {
            Name = attachment.FileName;
            Type = AudioType.Attachment;
            Path = attachment.Url;
            SecondaryPath = null;
            Aliases = null;
            Collections = null;
        }

        public void AddAlias(string aliasName)
        {
            Aliases.Add(aliasName);
            AudioHelper.AddToAlias(aliasName, this);
            AudioHelper.Save();
        }

        public void AddAliases(IEnumerable<string> aliases)
        {
            foreach (string alias in aliases)
            {
                Aliases.Add(alias);
                AudioHelper.AddToAlias(alias, this);
            }
            AudioHelper.Save();
        }

        public int CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }
            if (obj is AudioEntry otherAudioEntry)
            {
                return Name.CompareTo(otherAudioEntry.Name);
            }
            else
            {
                throw new ArgumentException("Comparison object is not an AudioEntry.");
            }
        }

        public override string ToString()
        {
            return Name;
        }
    }
}

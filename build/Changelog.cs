using NuGet.Versioning;
using Nuke.Common.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Stormancer.Build
{
    class ChangeLog
    {
        public List<ChangeLogRelease> Versions { get;  } = new List<ChangeLogRelease>();

        private ChangeLog(string[] lines)
        {
            string? id = null;
            List<string> descriptionLines = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.StartsWith("--"))//New section found.
                {
                    if (id != null)//Section was in progress
                    {
                        descriptionLines.RemoveAt(descriptionLines.Count - 1);//Remove last, because it's the id of the new section

                        Versions.Add(new ChangeLogRelease(id, descriptionLines.Join(Environment.NewLine)));
                        descriptionLines.Clear();

                    }
                    id = lines[i - 1];


                }
                else if(id != null)
                {
                    descriptionLines.Add(line);
                }
            }
            if (id != null)
            {
                Versions.Add(new ChangeLogRelease(id, descriptionLines.Join(Environment.NewLine)));
            }
        }

        public static ChangeLog? ReadFrom(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            return new ChangeLog(File.ReadAllLines(path));
        }
    }

    class ChangeLogRelease
    {
        public ChangeLogRelease(string id, string description)
        {
            Id = id;
            var spaceIndex = id.Trim().IndexOf(' ');
           
            if (spaceIndex > 0)
            {
                if (NuGetVersion.TryParse(id.Trim().Substring(0, spaceIndex), out var v))
                {
                    Version = v;
                }
              
            }
            else
            {
                if(NuGetVersion.TryParse(Id,out var v))
                {
                    Version = v;
                }
              
            }
            Description = description;
        }
        public string Id { get; }
        public NuGetVersion? Version { get; }
        public string Description { get; }
    }
}

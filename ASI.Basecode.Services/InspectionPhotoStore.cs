using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ASI.Basecode.Services
{
    /// <summary>
    /// Inspection photos live outside wwwroot so they are never served as public static files;
    /// staff read them through a scoped controller action instead.
    /// </summary>
    public static class InspectionPhotoStore
    {
        private static readonly Regex FileNamePattern = new(@"^photo_[1-6]\.(jpg|jpeg|png|webp)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static string Root => Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "uploads", "inspections");

        public static string DirectoryFor(long inspectionId) => Path.Combine(Root, inspectionId.ToString());

        /// <summary>Full path for a stored photo, or null when the name isn't one this store writes (no path traversal).</summary>
        public static string PathFor(long inspectionId, string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || !FileNamePattern.IsMatch(fileName))
            {
                return null;
            }

            var path = Path.Combine(DirectoryFor(inspectionId), fileName);
            return File.Exists(path) ? path : null;
        }

        public static IReadOnlyList<string> List(long inspectionId)
        {
            var dir = DirectoryFor(inspectionId);
            return Directory.Exists(dir)
                ? Directory.GetFiles(dir).Select(Path.GetFileName).Where(n => FileNamePattern.IsMatch(n)).OrderBy(n => n).ToList()
                : Array.Empty<string>();
        }

        public static string ContentTypeFor(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg",
        };

        /// <summary>One-time move of photos saved before this change from the public wwwroot folder.</summary>
        public static int MigrateFromPublicFolder()
        {
            var legacy = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "inspections");
            if (!Directory.Exists(legacy))
            {
                return 0;
            }

            var moved = 0;
            foreach (var dir in Directory.GetDirectories(legacy))
            {
                var target = Path.Combine(Root, Path.GetFileName(dir));
                Directory.CreateDirectory(target);
                foreach (var file in Directory.GetFiles(dir))
                {
                    var dest = Path.Combine(target, Path.GetFileName(file));
                    if (!File.Exists(dest))
                    {
                        File.Move(file, dest);
                        moved++;
                    }
                }
            }

            Directory.Delete(legacy, recursive: true);
            return moved;
        }
    }
}

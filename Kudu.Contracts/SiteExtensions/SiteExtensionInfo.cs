using System;
using System.Collections.Generic;
using System.Linq;
using NuGet;

namespace Kudu.Contracts.SiteExtensions
{
    // This is equivalent to NuGet.IPackage
    public class SiteExtensionInfo
    {
        public string Id
        {
            get;
            set;
        }

        public string Title 
        { 
            get; 
            set; 
        }

        public string Description
        {
            get;
            set;
        }

        public string Version
        {
            get;
            set;
        }

        public bool IsLatestVersion
        {
            get;
            set;
        }

        public Uri ProjectUrl
        {
            get;
            set;
        }

        public Uri IconUrl
        {
            get; 
            set;
        }

        public IEnumerable<string> Authors
        {
            get;
            set;
        }

        public DateTimeOffset? PublishedDateTime
        {
            get;
            set;
        }

        public Uri LicenseUrl
        {
            get;
            set;
        }

        public int DownloadCount
        {
            get;
            set;
        }

        public string AppPath
        {
            get;
            set;
        }

        public SiteExtensionInfo LatestInfo
        {
            get; 
            set;
        }

        public DateTimeOffset? InstalledDateTime
        {
            get;
            set;
        }

        public static SiteExtensionInfo ConvertFrom(IPackage package)
        {
            return new SiteExtensionInfo
            {
                Id = package.Id,
                Title = package.Title,
                Description = package.Description,
                Version = package.Version.ToString(),
                ProjectUrl = package.ProjectUrl,
                IconUrl = package.IconUrl,
                LicenseUrl = package.LicenseUrl,
                Authors = package.Authors,
                PublishedDateTime = package.Published,
                IsLatestVersion = package.IsLatestVersion,
                DownloadCount = package.DownloadCount,
                LatestInfo = null,
                AppPath = null,
                InstalledDateTime = null,
            };
        }
    }
}

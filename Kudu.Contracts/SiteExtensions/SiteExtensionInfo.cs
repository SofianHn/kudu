using System;
using System.Collections.Generic;

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

        public string LocalPath
        {
            get;
            set;
        }

        public DateTimeOffset? InstalledDateTime
        {
            get;
            set;
        }
    }
}

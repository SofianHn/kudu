using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Kudu.Contracts.SiteExtensions;
using Kudu.Core.Infrastructure;
using NuGet;

namespace Kudu.Core.SiteExtensions
{
    public class SiteExtensionManager : ISiteExtensionManager
    {
        private static readonly Uri _remoteSource = new Uri("http://siteextensions.azurewebsites.net/api/v2/");
        private readonly IPackageRepository _remoteRepository = new DataServicePackageRepository(_remoteSource);
        private readonly IPackageRepository _localRepository;

        public SiteExtensionManager(IEnvironment environment)
        {
            _localRepository = new LocalPackageRepository(environment.RootPath + "\\SiteExtensions");
        }

        public IEnumerable<SiteExtensionInfo> GetRemoteExtensions(string filter, bool allowPrereleaseVersions = false)
        {
            if (String.IsNullOrEmpty(filter))
            {
                return _remoteRepository.GetPackages()
                                        .Where(p => p.IsLatestVersion)
                                        .OrderByDescending(f => f.DownloadCount)
                                        .AsEnumerable()
                                        .Select(ConvertPackageToSiteExtensionInfo);
            }

            return _remoteRepository.Search(filter, allowPrereleaseVersions)
                                    .Where(p => p.IsLatestVersion)
                                    .AsEnumerable()
                                    .Select(ConvertPackageToSiteExtensionInfo);
        }

        public SiteExtensionInfo GetRemoteExtension(string id, string version)
        {
            var semanticVersion = version == null ? null : new SemanticVersion(version);
            return ConvertPackageToSiteExtensionInfo(_remoteRepository.FindPackage(id, semanticVersion));
        }

        public IEnumerable<SiteExtensionInfo> GetLocalExtensions(string filter, bool latestInfo = false)
        {
            var siteExtensionInfoList = _localRepository.Search(filter, false)
                                                        .AsEnumerable()
                                                        .Select(ConvertLocalPackageToSiteExtensionInfo);

            if (latestInfo)
            {
                siteExtensionInfoList = siteExtensionInfoList.Select(info => info.LatestInfo = GetLatestInfo(info.Id));
            }

            return siteExtensionInfoList;
        }

        public SiteExtensionInfo GetLocalExtension(string id, bool latestInfo = false)
        {
            var info = ConvertPackageToSiteExtensionInfo(_localRepository.FindPackage(id));

            if (latestInfo)
            {
                info.LatestInfo = GetLatestInfo(info.Id);
            }

            return info;
        }

        public SiteExtensionInfo InstallExtension(SiteExtensionInfo info)
        {
            return InstallExtension(info.Id);
        }

        public SiteExtensionInfo InstallExtension(string id)
        {
            IPackage package = _remoteRepository.FindPackage(id);

            // Directory where _localRepository.AddPackage would use.
            string installationDirectory = GetInstallationDirectory(id);

            OperationManager.Attempt(() =>
            {
                if (FileSystemHelpers.DirectoryExists(installationDirectory))
                {
                    FileSystemHelpers.DeleteDirectorySafe(installationDirectory);
                }

                // Copy nupkg file for package list/lookup
                FileSystemHelpers.CreateDirectory(installationDirectory);
                var packageFilePath = Path.Combine(installationDirectory, String.Format("{0}.{1}.nupkg", package.Id, package.Version));
                using (Stream readStream = package.GetStream(), writeStream = FileSystemHelpers.OpenWrite(packageFilePath))
                {
                    readStream.CopyTo(writeStream);
                }

                foreach (var file in package.GetContentFiles())
                {
                    // It is necessary to place applicationHost.xdt under site extension root.
                    string contentFilePath = file.Path.Substring("content/".Length);
                    var fullPath = Path.Combine(installationDirectory, contentFilePath);
                    FileSystemHelpers.CreateDirectory(Path.GetDirectoryName(fullPath));
                    using (Stream writeStream = FileSystemHelpers.OpenWrite(fullPath), readStream = file.GetStream())
                    {
                        readStream.CopyTo(writeStream);
                    }
                }
            });

            // If there is no xdt file, generate default.
            string xdtPath = Path.Combine(installationDirectory, "applicationHost.xdt");
            if (!File.Exists(xdtPath))
            {
                var xdtTemplate = CreateDefaultXdtTemplate(package.Id);
                xdtTemplate.Save(xdtPath);
            }

            return ConvertPackageToSiteExtensionInfo(package);
        }

        public bool UninstallExtension(string id)
        {
            string installationDirectory = GetInstallationDirectory(id);
            FileSystemHelpers.DeleteDirectorySafe(installationDirectory);
            return !FileSystemHelpers.DirectoryExists(installationDirectory);
        }

        public string GetInstallationDirectory(string id)
        {
            return Path.Combine(_localRepository.Source, id);
        }

        private SiteExtensionInfo GetLatestInfo(string id)
        {
            return ConvertPackageToSiteExtensionInfo(_remoteRepository.FindPackage(id));
        }

        private static XElement CreateDefaultXdtTemplate(string id)
        {
            XNamespace xdt = "http://schemas.microsoft.com/XML-Document-Transform";

            return new XElement("configuration", new XAttribute(XNamespace.Xmlns + "xdt", xdt),
                new XElement("system.applicationHost",
                    new XElement("sites",
                        new XElement("site", new XAttribute("name", "%XDT_SCMSITENAME%"), new XAttribute(xdt + "Locator", "Match(name)"),
                            new XElement("application", new XAttribute("path", "/" + id),
                                new XAttribute(xdt + "Locator", "Match(path)"),
                                new XAttribute(xdt + "Transform", "Remove")),
                           new XElement("application", new XAttribute("path", "/" + id),
                               new XAttribute("applicationPool", "%XDT_APPPOOLNAME%"),
                               new XAttribute(xdt + "Transform", "Insert"),
                               new XElement("virtualDirectory", new XAttribute("path", "/"),
                                   new XAttribute("physicalPath", "%XDT_EXTENSIONPATH%")))))));
        }

        public static SiteExtensionInfo ConvertPackageToSiteExtensionInfo(IPackage package)
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

        public SiteExtensionInfo ConvertLocalPackageToSiteExtensionInfo(IPackage package)
        {
            SiteExtensionInfo info = ConvertPackageToSiteExtensionInfo(package);
            IPackage latestPackage = _remoteRepository.FindPackage(info.Id);
            info.IsLatestVersion = package.Version == latestPackage.Version;
            return info;
        }
    }
}

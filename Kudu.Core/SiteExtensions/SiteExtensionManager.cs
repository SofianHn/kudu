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
                                        .Select(SiteExtensionInfo.ConvertFrom);
            }

            return _remoteRepository.Search(filter, allowPrereleaseVersions)
                                    .Where(p => p.IsLatestVersion)
                                    .AsEnumerable()
                                    .Select(SiteExtensionInfo.ConvertFrom);
        }

        public SiteExtensionInfo GetRemoteExtension(string id, string version)
        {
            var semanticVersion = version == null ? null : new SemanticVersion(version);
            return SiteExtensionInfo.ConvertFrom(_remoteRepository.FindPackage(id, semanticVersion));
        }

        public IEnumerable<SiteExtensionInfo> GetLocalExtensions(string filter, bool latestInfo = false)
        {
            var siteExtensionInfoList = _localRepository.Search(filter, false)
                                                        .AsEnumerable()
                                                        .Select(SiteExtensionInfo.ConvertFrom);

            if (latestInfo)
            {
                siteExtensionInfoList = siteExtensionInfoList.Select(info => info.LatestInfo = GetLatestInfo(info.Id));
            }

            return siteExtensionInfoList;
        }

        public SiteExtensionInfo GetLocalExtension(string id, bool latestInfo = false)
        {
            var info = SiteExtensionInfo.ConvertFrom(_localRepository.FindPackage(id));

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
            var installationDirectory = GetInstallationDirectory(package);

            OperationManager.Attempt(() =>
            {
                foreach (var file in package.GetFiles())
                {
                    // It is necessary to place applicationHost.xdt under site extension root.
                    string pathWithoutContextPrefix = file.Path.Substring("content/".Length);
                    var fullPath = Path.Combine(installationDirectory, pathWithoutContextPrefix);
                    FileSystemHelpers.CreateDirectory(Path.GetDirectoryName(fullPath));
                    using (Stream writeStream = File.OpenWrite(fullPath), 
                        readStream = file.GetStream())
                    {
                        readStream.CopyTo(writeStream);
                    }
                }

                // For package list/lookup
                _localRepository.AddPackage(package);
            });

            // If there is no xdt file, generate a default one
            string xdtPath = Path.Combine(installationDirectory, "applicationHost.xdt");
            if (!File.Exists(xdtPath))
            {
                var xdtTemplate = CreateDefaultXdtTemplate(package.Id);
                xdtTemplate.Save(xdtPath);
            }

            return SiteExtensionInfo.ConvertFrom(package);
        }

        public bool UninstallExtension(string id)
        {
            IPackage package = _localRepository.FindPackage(id);
            string directory = GetInstallationDirectory(package);
            FileSystemHelpers.DeleteDirectorySafe(directory);
            return FileSystemHelpers.DirectoryExists(directory);
        }

        private SiteExtensionInfo GetLatestInfo(string id)
        {
            return SiteExtensionInfo.ConvertFrom(_remoteRepository.FindPackage(id));
        }

        private string GetInstallationDirectory(IPackageName package)
        {
            return _localRepository.Source + "\\" + package.Id + "." + package.Version;
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
    }
}

﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Kudu.Contracts.SiteExtensions;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using NuGet;

namespace Kudu.Core.SiteExtensions
{
    public class SiteExtensionManager : ISiteExtensionManager
    {
        private static readonly Uri _remoteSource = new Uri("http://siteextensions.azurewebsites.net/api/v2/");
        private readonly IPackageRepository _remoteRepository = new DataServicePackageRepository(_remoteSource);
        private readonly IPackageRepository _localRepository;
        private readonly ITraceFactory _traceFactory;

        public SiteExtensionManager(IEnvironment environment, ITraceFactory traceFactory)
        {
            _localRepository = new LocalPackageRepository(environment.RootPath + "\\SiteExtensions");
            _traceFactory = traceFactory;
        }

        public IEnumerable<SiteExtensionInfo> GetRemoteExtensions(string filter, bool allowPrereleaseVersions = false)
        {
            IQueryable<IPackage> packages;

            if (String.IsNullOrEmpty(filter))
            {
                packages = _remoteRepository.GetPackages()
                    .Where(p => p.IsLatestVersion)
                    .OrderByDescending(p => p.DownloadCount);
            }
            else
            {
                packages = _remoteRepository.Search(filter, allowPrereleaseVersions)
                    .Where(p => p.IsLatestVersion);
            }

            return packages.AsEnumerable()
                           .Select(ConvertPackageToSiteExtensionInfo);
        }

        public SiteExtensionInfo GetRemoteExtension(string id, string version)
        {
            var semanticVersion = version == null ? null : new SemanticVersion(version);
            IPackage package = _remoteRepository.FindPackage(id, semanticVersion);
            if (package == null)
            {
                return null;
            }

            return ConvertPackageToSiteExtensionInfo(package);
        }

        public IEnumerable<SiteExtensionInfo> GetLocalExtensions(string filter, bool checkLatest = true)
        {
            return _localRepository.Search(filter, false)
                                                        .AsEnumerable()
                                                        .Select(info => ConvertLocalPackageToSiteExtensionInfo(info, checkLatest));
        }

        public SiteExtensionInfo GetLocalExtension(string id, bool checkLatest = true)
        {
            IPackage package = _localRepository.FindPackage(id);
            if (package == null)
            {
                return null;
            }

            return ConvertLocalPackageToSiteExtensionInfo(package, checkLatest);
        }

        public SiteExtensionInfo InstallExtension(SiteExtensionInfo info)
        {
            return info == null ? null : InstallExtension(info.Id);
        }

        public SiteExtensionInfo InstallExtension(string id)
        {
            if (String.IsNullOrEmpty(id))
            {
                return null;
            }

            IPackage package = _remoteRepository.FindPackage(id);
            if (package == null)
            {
                return null;
            }

            // Directory where _localRepository.AddPackage would use.
            string installationDirectory = GetInstallationDirectory(id);

            bool success = InstallExtension(package, installationDirectory);

            if (success)
            {
                return ConvertPackageToSiteExtensionInfo(package);
            }

            return null;
        }

        public bool InstallExtension(IPackage package, string installationDirectory)
        {
            try
            {
                if (FileSystemHelpers.DirectoryExists(installationDirectory))
                {
                    FileSystemHelpers.DeleteDirectorySafe(installationDirectory);
                }

                foreach (IPackageFile file in package.GetContentFiles())
                {
                    // It is necessary to place applicationHost.xdt under site extension root.
                    string contentFilePath = file.Path.Substring("content/".Length);
                    string fullPath = Path.Combine(installationDirectory, contentFilePath);
                    FileSystemHelpers.CreateDirectory(Path.GetDirectoryName(fullPath));
                    using (Stream writeStream = FileSystemHelpers.OpenWrite(fullPath), readStream = file.GetStream())
                    {
                        OperationManager.Attempt(() => readStream.CopyTo(writeStream));
                    }
                }

                // If there is no xdt file, generate default.
                string xdtPath = Path.Combine(installationDirectory, "applicationHost.xdt");
                if (!File.Exists(xdtPath))
                {
                    XElement xdtTemplate = CreateDefaultXdtTemplate(package.Id);
                    OperationManager.Attempt(() => xdtTemplate.Save(xdtPath));
                }

                // Copy nupkg file for package list/lookup
                FileSystemHelpers.CreateDirectory(installationDirectory);
                string packageFilePath = Path.Combine(installationDirectory,
                    String.Format("{0}.{1}.nupkg", package.Id, package.Version));
                using (
                    Stream readStream = package.GetStream(), writeStream = FileSystemHelpers.OpenWrite(packageFilePath))
                {
                    OperationManager.Attempt(() => readStream.CopyTo(writeStream));
                }
            }
            catch (Exception ex)
            {
                ITracer tracer = _traceFactory.GetTracer();
                tracer.TraceError(ex);
                FileSystemHelpers.DeleteDirectorySafe(installationDirectory);
                return false;
            }

            return true;
        }

        public bool UninstallExtension(string id)
        {
            if (String.IsNullOrEmpty(id))
            {
                return true;
            }

            string installationDirectory = GetInstallationDirectory(id);

            OperationManager.Attempt(() => FileSystemHelpers.DeleteDirectorySafe(installationDirectory));
            
            return !FileSystemHelpers.DirectoryExists(installationDirectory);
        }

        public string GetInstallationDirectory(string id)
        {
            if (String.IsNullOrEmpty(id))
            {
                return null;
            }

            return Path.Combine(_localRepository.Source, id);
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
                LocalPath = null,
                InstalledDateTime = null,
            };
        }

        public SiteExtensionInfo ConvertLocalPackageToSiteExtensionInfo(IPackage package, bool checkLatest = true)
        {
            SiteExtensionInfo info = ConvertPackageToSiteExtensionInfo(package);

            info.LocalPath = GetInstallationDirectory(info.Id);

            info.InstalledDateTime = FileSystemHelpers.GetLastWriteTimeUtc(info.LocalPath);

            if (checkLatest)
            {
                IPackage latestPackage = _remoteRepository.FindPackage(info.Id);
                info.IsLatestVersion = package.Version == latestPackage.Version;
            }

            return info;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
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
        private readonly IEnvironment _environment;

        public SiteExtensionManager(IEnvironment environment)
        {
            _environment = environment;
            _localRepository = new LocalPackageRepository(_environment.RootPath + "\\SiteExtensions");
        }

        public async Task<IEnumerable<SiteExtensionInfo>> GetRemoteExtensions(string filter, bool allowPrereleaseVersions = false)
        {
            if (String.IsNullOrEmpty(filter))
            {
                return await Task.Run(() => _remoteRepository.GetPackages()
                                        .Where(p => p.IsLatestVersion)
                                        .OrderByDescending(f => f.DownloadCount)
                                        .AsEnumerable()
                                        .Select(SiteExtensionInfo.ConvertFrom));
            }

            return await Task.Run(() => _remoteRepository.Search(filter, allowPrereleaseVersions)
                                                         .AsEnumerable()
                                                         .Select(SiteExtensionInfo.ConvertFrom));
        }

        public async Task<SiteExtensionInfo> GetRemoteExtension(string id, string version)
        {
            var semanticVersion = version == null ? null : new SemanticVersion(version);
            return await Task.Run(() => SiteExtensionInfo.ConvertFrom(_remoteRepository.FindPackage(id, semanticVersion)));
        }

        public async Task<IEnumerable<SiteExtensionInfo>> GetLocalExtensions(string filter, bool latestInfo = false)
        {
            return await Task.Run(async () =>
            {
                var siteExtensionInfoList = _localRepository.Search(filter, true).AsEnumerable()
                    .Select(SiteExtensionInfo.ConvertFrom);

                if (latestInfo)
                {
                    foreach (var info in siteExtensionInfoList)
                    {
                        info.LatestInfo = await GetLatestInfo(info.Id);
                    }
                }

                return siteExtensionInfoList;
            });
        }

        public async Task<SiteExtensionInfo> GetLocalExtension(string id, bool latestInfo = false)
        {

            return await Task.Run(async () =>
            {
                var info = SiteExtensionInfo.ConvertFrom(_localRepository.FindPackage(id));
                if (latestInfo)
                {
                    info.LatestInfo = await GetLatestInfo(info.Id);
                }
                return info;
            });
        }

        public async Task<SiteExtensionInfo> InstallExtension(SiteExtensionInfo info)
        {
            return await InstallExtension(info.Id);
        }
        public async Task<SiteExtensionInfo> InstallExtension(string id)
        {
            return await Task.Run(() =>
            {
                IPackage package = _remoteRepository.FindPackage(id);
                    
                // Directory where _localRepository.AddPackage would use.
                var installationDirectory = GetInstallationDirectory(package);

                foreach (var file in package.GetFiles())
                {
                    var fullPath = Path.Combine(installationDirectory, file.Path);
                    FileSystemHelpers.CreateDirectory(Path.GetDirectoryName(fullPath));
                    using (Stream writeStream = File.OpenWrite(fullPath),
                        readStream = file.GetStream())
                    {
                        readStream.CopyTo(writeStream);
                    }
                }

                // For package list/lookup
                _localRepository.AddPackage(package);

                return SiteExtensionInfo.ConvertFrom(package);
            });
        }

        public async Task<bool> UninstallExtension(string id)
        {
            return await Task.Run(() =>
            {
                IPackage package = _localRepository.FindPackage(id);
                FileSystemHelpers.DeleteDirectoryContentsSafe(GetInstallationDirectory(package));
                return true;
            });
        }

        private async Task<SiteExtensionInfo> GetLatestInfo(string id)
        {
            return await Task.Run(() => SiteExtensionInfo.ConvertFrom(_remoteRepository.FindPackage(id)));
        }

        private string GetInstallationDirectory(IPackageName package)
        {
            return _localRepository.Source + "\\" + package.Id + "." + package.Version;
        }
    }
}

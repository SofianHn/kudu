using System;
using System.Collections.Generic;
using System.Security.Policy;
using System.Threading.Tasks;

namespace Kudu.Contracts.SiteExtensions
{
    public interface ISiteExtensionManager
    {
        Task<IEnumerable<SiteExtensionInfo>> GetRemoteExtensions(string filter, bool allowPrereleaseVersions);

        Task<SiteExtensionInfo> GetRemoteExtension(string id, string version);

        Task<IEnumerable<SiteExtensionInfo>> GetLocalExtensions(string filter, bool latestInfo);

        Task<SiteExtensionInfo> GetLocalExtension(string id, bool latestInfo);

        Task<SiteExtensionInfo> InstallExtension(SiteExtensionInfo info);

        Task<bool> UninstallExtension(string id);
    }
}

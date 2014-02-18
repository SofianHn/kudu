using System;
using System.Collections.Generic;
using System.Security.Policy;
using System.Threading.Tasks;

namespace Kudu.Contracts.SiteExtensions
{
    public interface ISiteExtensionManager
    {
        IEnumerable<SiteExtensionInfo> GetRemoteExtensions(string filter, bool allowPrereleaseVersions);

        SiteExtensionInfo GetRemoteExtension(string id, string version);

        IEnumerable<SiteExtensionInfo> GetLocalExtensions(string filter, bool latestInfo);

        SiteExtensionInfo GetLocalExtension(string id, bool latestInfo);

        SiteExtensionInfo InstallExtension(SiteExtensionInfo info);

        bool UninstallExtension(string id);
    }
}

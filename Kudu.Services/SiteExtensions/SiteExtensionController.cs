using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Http;
using Kudu.Contracts.SiteExtensions;

namespace Kudu.Services.SiteExtensions
{
    public class SiteExtensionController : ApiController
    {
        private readonly ISiteExtensionManager _manager;

        public SiteExtensionController(ISiteExtensionManager manager)
        {
            _manager = manager;
        }

        [HttpGet]
        public async Task<IEnumerable<SiteExtensionInfo>> GetRemoteExtensions(string filter = null, bool allowPrereleaseVersions = false)
        {
            return await _manager.GetRemoteExtensions(filter, allowPrereleaseVersions);
        }

        [HttpGet]
        public async Task<SiteExtensionInfo> GetRemoteExtension(string id, string version = null)
        {
            return await _manager.GetRemoteExtension(id, version);
        }

        [HttpGet]
        public async Task<IEnumerable<SiteExtensionInfo>> GetLocalExtensions(string filter = null, bool latestInfo = false)
        {
            return await _manager.GetLocalExtensions(filter, latestInfo);
        }

        [HttpGet]
        public async Task<SiteExtensionInfo> GetLocalExtension(string id, bool latestInfo = false)
        {
            return await _manager.GetLocalExtension(id, latestInfo);
        }

        [HttpPost]
        public async Task<SiteExtensionInfo> InstallExtension(SiteExtensionInfo info)
        {
            return await _manager.InstallExtension(info);
        }

        [HttpDelete]
        public async Task<bool> UninstallExtension(string id)
        {
            return await _manager.UninstallExtension(id);
        }
    }
}

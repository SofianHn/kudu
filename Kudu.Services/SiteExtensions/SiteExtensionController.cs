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
        public IEnumerable<SiteExtensionInfo> GetRemoteExtensions(string filter = null, bool allowPrereleaseVersions = false)
        {
            return _manager.GetRemoteExtensions(filter, allowPrereleaseVersions);
        }

        [HttpGet]
        public SiteExtensionInfo GetRemoteExtension(string id, string version = null)
        {
            return _manager.GetRemoteExtension(id, version);
        }

        [HttpGet]
        public IEnumerable<SiteExtensionInfo> GetLocalExtensions(string filter = null, bool latestInfo = false)
        {
            return _manager.GetLocalExtensions(filter, latestInfo);
        }

        [HttpGet]
        public SiteExtensionInfo GetLocalExtension(string id, bool latestInfo = false)
        {
            return _manager.GetLocalExtension(id, latestInfo);
        }

        [HttpPost]
        public SiteExtensionInfo InstallExtension(SiteExtensionInfo info)
        {
            return _manager.InstallExtension(info);
        }

        [HttpDelete]
        public bool UninstallExtension(string id)
        {
            return _manager.UninstallExtension(id);
        }
    }
}

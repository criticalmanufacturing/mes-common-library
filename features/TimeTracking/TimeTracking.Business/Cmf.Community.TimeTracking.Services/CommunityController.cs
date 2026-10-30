using Microsoft.AspNetCore.Mvc;

namespace Cmf.Community.TimeTracking.Services
{
    /// <summary>
    /// Community Services
    /// </summary>
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class CommunityController : ControllerBase
    {
        private const string OBJECT_TYPE_NAME = "Cmf.Community.TimeTracking.Services.CommunityManagement";
    }
}
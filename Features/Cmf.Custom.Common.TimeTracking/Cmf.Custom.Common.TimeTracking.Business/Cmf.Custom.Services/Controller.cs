using Microsoft.AspNetCore.Mvc;

namespace Cmf.Custom.Common.TimeTracking.Services
{
    /// <summary>
    ///  Services
    /// </summary>
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class Controller : ControllerBase
    {
        private const string OBJECT_TYPE_NAME = "Cmf.Custom.Common.TimeTracking.Services.Management";
    }
}
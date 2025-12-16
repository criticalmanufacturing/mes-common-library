using Cmf.Foundation.Common;
using Cmf.Foundation.Common.LocalizationService;
using System;
using System.Runtime.Serialization;
using System.Threading;

namespace Cmf.Custom.Common.TimeTracking.Common
{
    [DataContract(Name = "CommonException")]
    public class CommonException : CmfBaseException
    {
        /// <summary>
        /// The default constructor by passing a LocalizationService and the  localizedMessage name
        /// </summary>
        /// <param name="localizationService">The instance for LocalizationService</param>
        /// <param name="localizedMessageName">The name of the LocalizedMessage</param>
        /// <param name="parameters">Message Parameters</param>
        public CommonException(ILocalizationService localizationService, string localizedMessageName, params object[] parameters)
                      : base("100000",
                   Convert.ToInt64("100000"),
                   string.Format(localizationService.Localize(Thread.CurrentThread.CurrentCulture.Name, localizedMessageName), parameters),
                   string.Format(localizationService.Localize(Thread.CurrentThread.CurrentCulture.Name, localizedMessageName), parameters))
        {
        }

    }
}

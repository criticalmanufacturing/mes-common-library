using Cmf.Foundation.Common;
using Cmf.Foundation.Common.LocalizationService;
using System;
using System.Runtime.Serialization;
using System.Threading;

namespace Cmf.Community.TimeTracking.Common
{
    [DataContract(Name = "CommunityException")]
    public class CommunityException : CmfBaseException
    {
        public CommunityException(ILocalizationService localizationService, string localizedMessageName, params object[] parameters)
            : base("100000",
                Convert.ToInt64("100000"),
                string.Format(localizationService.Localize(Thread.CurrentThread.CurrentCulture.Name, localizedMessageName), parameters),
                string.Format(localizationService.Localize(Thread.CurrentThread.CurrentCulture.Name, localizedMessageName), parameters))
        {
        }
    }
}

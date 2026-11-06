using Cmf.Foundation.Common;
using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Foundation.Common.Base;

namespace Cmf.Custom.Common.SD.Utilities.Extensions;

public static class EmailExtensions
{
    /// <summary>Sends an email to the specified recipient.</summary>
    /// <param name="receiver">The recipient.</param>
    /// <param name="subject">The message subject.</param>
    /// <param name="message">The message body.</param>
    /// <param name="IsBodyHtml">Whether the body is HTML.</param>
    public static void SendEmail(string receiver, string subject, string message, bool IsBodyHtml = false)
    {
        CmfMail emailItem = new()
        {
            To = receiver,
            Subject = subject,
            Message = message,
            IsBodyHtml = true
        };
        emailItem.SendMail();
    }
}

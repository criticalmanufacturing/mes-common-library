using Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;
using Cmf.Foundation.Common;
using Cmf.Foundation.Common.Abstractions;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Cmf.Custom.Common.SD.Utilities.Helpers
{
    /// <inheritdoc />
    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public class ServiceContextUtilities(IUtilities utilities) : IServiceContextUtilities
    {
        /// <inheritdoc/>
        public TOut RunWithStartAndEndMethod<TIn, TOut>(string callerClassFullName, TIn input, Func<TIn, TOut> method, [CallerMemberName] string callerMethodName = null)
        {
            // Open service
            utilities.StartMethod(callerClassFullName,
                                   callerMethodName,
                                   new KeyValuePair<string, object>(typeof(TIn).Name, input));

            // Invoke method
            TOut output = method(input);

            // Close service
            utilities.EndMethod(-1,
                                 -1,
                                 new KeyValuePair<String, Object>(typeof(TIn).Name, input),
                                 new KeyValuePair<String, Object>(typeof(TOut).Name, output));

            return output;
        }
    }
}

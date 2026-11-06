using Cmf.Foundation.Common;
using System;
using System.Runtime.CompilerServices;

namespace Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions
{
    /// <summary>
    /// Utility services for handling context-related operations
    /// </summary>
    public interface IServiceContextUtilities
    {
        /// <summary>
        /// Encapsulates the execution of code that requires StartMethod and EndMethod calls
        /// </summary>
        /// <typeparam name="TIn">Type of the input class</typeparam>
        /// <typeparam name="TOut">Type of the output class</typeparam>
        /// <param name="callerClassFullName">The namespace and class of the executing class <para/> Usually referred to as OBJECT_TYPE_NAME constant in controller and orchestration classes</param>
        /// <param name="input">Input object instance of type <typeparamref name="TIn"/></param>
        /// <param name="method">Function representing the code to be executed in between StartMethod and EndMethod calls</param>
        /// <param name="callerMethodName">Name of the method starting and ending, automatically captured due to CallerMemberName attribute</param>
        /// <returns>Result of <paramref name="method"/> execution</returns>
        /// <exception cref="CmfBaseException">Any exception occurred while executing <paramref name="method"/> code</exception>
        TOut RunWithStartAndEndMethod<TIn, TOut>(string callerClassFullName, TIn input, Func<TIn, TOut> method, [CallerMemberName] string callerMethodName = null);
    }
}

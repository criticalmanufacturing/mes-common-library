using Cmf.Navigo.BusinessObjects.Abstractions;

namespace Cmf.Custom.Common.TimeTracking.Common.Abstractions
{
    public interface IUtilities
    {

        /// <summary>
        ///     Gets the Employee for the currently logged in User
        /// </summary>
        /// <param name="throwErrorIfNotExists">
        ///     Indicates if an error should be thrown if the User doesn't have Employee
        /// </param>
        /// <returns>
        ///     Employee matching user, null otherwise
        /// </returns>
        /// <exception cref="CommonException">
        ///     If `throwErrorIfNotExists` is set to `true` and if User doesn't have Employee
        /// </exception>
        public IEmployee GetCurrentUserEmployee(bool throwErrorIfNotExists = false);
    }
}

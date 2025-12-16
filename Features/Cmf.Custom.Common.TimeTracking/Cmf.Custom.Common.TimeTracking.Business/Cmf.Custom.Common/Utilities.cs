using Cmf.Foundation.Common.Abstractions;
using Cmf.Foundation.Common.LocalizationService;
using Cmf.Foundation.Security.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;

namespace Cmf.Custom.Common.TimeTracking.Common
{

    public class Utilities : Cmf.Custom.Common.TimeTracking.Common.Abstractions.IUtilities
    {

        #region Employee handling

        private readonly IEntityFactory _entityFactory;
        private readonly ILocalizationService _localizationService;

        public Utilities(IEntityFactory entityFactory, ILocalizationService localizationService)
        {
            _entityFactory = entityFactory;
            _localizationService = localizationService;
        }

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
        public IEmployee GetCurrentUserEmployee(bool throwErrorIfNotExists = false)
        {
            string userAccount = Foundation.Common.Utilities.DomainUserName;
            IUser user = _entityFactory.Create<IUser>();
            user.Load(userAccount);

            IEmployee employee = _entityFactory.Create<IEmployee>();
            employee.LoadByUserAccount(userAccount);

            if (throwErrorIfNotExists && !employee.ObjectExists())
            {
                throw new CommonException(_localizationService, Constants.CustomNoEmployeeForUser, userAccount);
            }

            return employee;
        }

        #endregion

    }
}

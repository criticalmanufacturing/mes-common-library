using Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;
using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessOrchestration.Abstractions;
using Cmf.Foundation.BusinessOrchestration.ChangeSetManagement.InputObjects;
using Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.InputObjects;
using Cmf.Foundation.Common;
using Cmf.Foundation.Common.Abstractions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Cmf.Custom.Common.SD.Utilities.Helpers
{
    ///<inheritdoc/>
    public class EntityHelper : IEntityHelper
    {
        #region Private Variables
        private readonly IEntityFactory _entityFactory;
        private readonly IGenericServiceOrchestration _genericServiceOrchestration;
        private readonly IChangeSetOrchestration _changeSetOrchestration;
        private readonly ITableHelper _tableHelper;

        #endregion Private Variables

        /// <summary>
        /// Initializes a new instance of the <see cref="EntityHelper"/> class.
        /// </summary>
        [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
        public EntityHelper(IEntityFactory entityFactory, IGenericServiceOrchestration genericServiceOrchestration,
            IChangeSetOrchestration changeSetOrchestration, ITableHelper tableHelper)
        {
            this._entityFactory = entityFactory;
            this._genericServiceOrchestration = genericServiceOrchestration;
            this._changeSetOrchestration = changeSetOrchestration;
            this._tableHelper = tableHelper;
        }







        #region ChangeSet



        ///<inheritdoc/>
        public IChangeSet CreateChangeSet(bool isEffectiveOnApproval = true)
        {
            IChangeSet changeSet = CreateChangeSetStructure();
            changeSet.MakeEffectiveOnApproval = isEffectiveOnApproval;
            changeSet.Create();
            return changeSet;
        }

        ///<inheritdoc/>
        public IChangeSet CreateChangeSetStructure()
        {
            IChangeSet changeSet = this._entityFactory.Create<IChangeSet>();
            changeSet.Name = string.Format("AutomaticChangeSet_{0}_{1}", DateTime.Now.ToString("yyyyMMddHHmmss"), Guid.NewGuid().ToString().Replace("-", string.Empty));
            changeSet.Type = "Generic";

            _tableHelper.AddValueToLookupTable("ChangeSetType", changeSet.Type, changeSet.Type);

            return changeSet;
        }

        ///<inheritdoc/>
        public void CreateNewEntityVersion(IEntityVersion entity, bool requestApproval = true, bool isEffectiveOnApproval = true)
        {

            if (entity.ChangeSet == null)
            {
                entity.ChangeSet = CreateChangeSet(isEffectiveOnApproval);
            }

            this._genericServiceOrchestration.CreateObjectVersion(new CreateObjectVersionInput
            {
                Object = entity
            });

            this._changeSetOrchestration.RequestChangeSetApproval(new RequestChangeSetApprovalInput
            {
                ChangeSet = entity.ChangeSet
            });
        }

        ///<inheritdoc/>
        public void CreateNewEntitiesVersion(IEntityVersionCollection<IEntityVersion> entities, string lookUpChangeSetType, bool requestApproval = true, bool isEffectiveOnApproval = true)
        {
            IChangeSet changeSet = CreateChangeSet(isEffectiveOnApproval: isEffectiveOnApproval);

            Dictionary<object, string> objects = new Dictionary<object, string>();

            foreach (IEntityVersion entity in entities)
            {
                if (entity.ChangeSet == null)
                {
                    entity.ChangeSet = changeSet;
                }

                objects[entity] = string.Empty;
            }

            _genericServiceOrchestration.CreateObjectVersions(new CreateObjectVersionsInput
            {
                Objects = objects
            });

            _changeSetOrchestration.RequestChangeSetApproval(new RequestChangeSetApprovalInput
            {
                ChangeSet = changeSet
            });
        }

        #endregion ChangeSet
    }
}

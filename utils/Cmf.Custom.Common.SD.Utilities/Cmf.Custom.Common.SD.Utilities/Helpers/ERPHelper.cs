using Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;
using Cmf.Custom.Common.SD.Utilities.Exceptions;
using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessObjects.QueryObject;
using Cmf.Foundation.Common;
using Cmf.Foundation.Common.Abstractions;
using System;
using System.Data;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Cmf.Custom.Common.SD.Utilities.Helpers
{
    ///<inheritdoc/>
    public class ERPHelper : IERPHelper
    {
        #region Private Variables
        private readonly IEntityFactory _entityFactory = null;

        #endregion Private Variables

        /// <summary>
        /// Initializes a new instance of the <see cref="ERPHelper"/> class.
        /// </summary>
        [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
        public ERPHelper(IEntityFactory entityFactory)
        {
            this._entityFactory = entityFactory;
        }

        ///<inheritdoc/>
        public IIntegrationEntry CreateIntegrationEntry(string sourceSystem, string targetSystem, string messageType, string eventName, string messageName, string message, string batchId = null, string name = null, bool isBatchIdQueueingEnable = false)
        {
            if (string.IsNullOrEmpty(sourceSystem))
                throw new ArgumentNullCmfException(nameof(sourceSystem));
            if (string.IsNullOrEmpty(targetSystem))
                throw new ArgumentNullCmfException(nameof(targetSystem));
            if (string.IsNullOrEmpty(eventName))
                throw new ArgumentNullCmfException(nameof(eventName));
            if (string.IsNullOrEmpty(messageName))
                throw new ArgumentNullCmfException(nameof(messageName));
            if (string.IsNullOrEmpty(messageType))
                throw new ArgumentNullCmfException(nameof(messageType));
            if (string.IsNullOrEmpty(message))
                throw new ArgumentNullCmfException(nameof(message));
            if (isBatchIdQueueingEnable && string.IsNullOrEmpty(batchId))
                throw new ArgumentNullCmfException(nameof(batchId));

            bool failIntegrationEntry = false;

            if (string.IsNullOrWhiteSpace(name))
            {
                name = Guid.NewGuid().ToString();
            }

            IIntegrationEntry integrationEntry = _entityFactory.Create<IIntegrationEntry>();

            integrationEntry.Name = name;
            integrationEntry.EventName = eventName;
            integrationEntry.SourceSystem = sourceSystem;
            integrationEntry.TargetSystem = targetSystem;
            integrationEntry.MessageType = messageType;
            integrationEntry.IntegrationMessage = _entityFactory.Create<IIntegrationMessage>();
            integrationEntry.IntegrationMessage.Message = Encoding.Default.GetBytes(message);
            integrationEntry.SystemState = IntegrationEntrySystemState.Received;
            integrationEntry.MessageName = messageName;
            integrationEntry.MessageDate = DateTime.Now;

            if (!string.IsNullOrEmpty(batchId))
            {
                integrationEntry.BatchId = batchId;

                if (isBatchIdQueueingEnable)
                {
                    integrationEntry.ParentIntegrationEntry = GetLastIntegrationEntryByBatchId(batchId);
                    failIntegrationEntry = integrationEntry.ParentIntegrationEntry != null && integrationEntry.ParentIntegrationEntry.SystemState == IntegrationEntrySystemState.Failed;
                }
            }

            integrationEntry.Create();

            if (failIntegrationEntry)
            {
                // If we are going to fail the IE at creation, we need to set it as retriable, otherwise it is not going to be picked up if the parent gets processed later
                integrationEntry.IsRetriable = true;
                integrationEntry.NumberOfRetries = -1;
                integrationEntry.Fail($"Parent Integration Entry {integrationEntry.ParentIntegrationEntry.Name} has failed.");
            }

            return integrationEntry;
        }

        /// <summary>
        /// Integration entry states for query <see cref="GetLastIntegrationEntryByBatchId"/>
        /// </summary>
        private static readonly string[] ParentIntegrationEntryStateFilterValues = [
            ((int)IntegrationEntrySystemState.Received).ToString(),
            ((int) IntegrationEntrySystemState.Processing).ToString(),
            ((int) IntegrationEntrySystemState.Failed).ToString()
        ];

        /// <summary>
        /// Gets the last Integration Entry not processed for a given Batch Id.
        /// </summary>
        /// <param name="batchId">BatchId to find IEs.</param>
        /// <returns>Integration Entry instance or null.</returns>
        private IIntegrationEntry GetLastIntegrationEntryByBatchId(string batchId)
        {
            IIntegrationEntry parentIntegrationEntry = null;

            IQueryObject query = _entityFactory.Create<IQueryObject>();
            query.EntityTypeName = Constants.IntegrationEntry;
            query.Query = new Query
            {
                Distinct = true,
                Filters = new FilterCollection() {
                        new Filter()
                        {
                            Name = nameof(IIntegrationEntry.BatchId),
                            ObjectName = Constants.IntegrationEntry,
                            ObjectAlias = Constants.IntegrationEntry,
                            Operator = Cmf.Foundation.Common.FieldOperator.IsEqualTo,
                            Value = batchId,
                            LogicalOperator = Cmf.Foundation.Common.LogicalOperator.AND,
                            FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal,
                        },
                        new Filter()
                        {
                            Name = Constants.SystemState,
                            ObjectName = Constants.IntegrationEntry,
                            ObjectAlias = Constants.IntegrationEntry,
                            Operator = Cmf.Foundation.Common.FieldOperator.In,
                            Value = ParentIntegrationEntryStateFilterValues,
                            LogicalOperator = Cmf.Foundation.Common.LogicalOperator.AND,
                            FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal,
                        },
                        new Filter()
                        {
                            Name = Constants.UniversalState,
                            ObjectName = Constants.IntegrationEntry,
                            ObjectAlias = Constants.IntegrationEntry,
                            Operator = Cmf.Foundation.Common.FieldOperator.IsNotEqualTo,
                            Value = Cmf.Foundation.Common.Base.UniversalState.Terminated,
                            LogicalOperator = Cmf.Foundation.Common.LogicalOperator.Nothing,
                            FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal,
                        }
                    },
                Fields = new FieldCollection() {
                        new Field()
                        {
                            Alias = Constants.Id,
                            ObjectName = Constants.IntegrationEntry,
                            ObjectAlias = Constants.IntegrationEntry,
                            IsUserAttribute = false,
                            Name = Constants.Id,
                            Position = 0,
                            Sort = Cmf.Foundation.Common.FieldSort.NoSort
                        },
                        new Field()
                        {
                            Alias = Constants.CreatedOn,
                            ObjectName = Constants.IntegrationEntry,
                            ObjectAlias = Constants.IntegrationEntry,
                            IsUserAttribute = false,
                            Name = Constants.CreatedOn,
                            Position = 1,
                            Sort = Cmf.Foundation.Common.FieldSort.Descending
                        }
                    },
                Relations = new RelationCollection(),
                Top = 1
            };

            DataSet result = query.Execute(false, null);

            if (result.HasData())
            {
                long ieId = (long)result.Tables[0].Rows[0][Constants.Id];

                parentIntegrationEntry = _entityFactory.Create<IIntegrationEntry>();
                parentIntegrationEntry.Load(ieId);
            }

            return parentIntegrationEntry;
        }


    }
}

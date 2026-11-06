using Cmf.Foundation.BusinessObjects;
using Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;
using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Foundation.BusinessOrchestration.Abstractions;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessObjects.QueryObject;
using Cmf.Foundation.BusinessOrchestration.EntityTypeManagement;
using Cmf.Foundation.BusinessOrchestration.EntityTypeManagement.InputObjects;
using Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.InputObjects;
using Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.OutputObjects;
using Cmf.Foundation.Common;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Foundation.Common.Base;
using Cmf.Foundation.Common.LocalizationService;
using Cmf.Foundation.Configuration;
using Cmf.Foundation.Configuration.Abstractions;
using Cmf.Foundation.Security;
using Cmf.Foundation.Security.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using Cmf.Navigo.BusinessOrchestration.Abstractions;
using Cmf.Navigo.BusinessOrchestration.LaborManagement.InputObjects;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Cmf.Custom.Common.SD.Utilities.Helpers
{
    public class GenericHelper : IGenericHelper
    {
        private readonly ILocalizationService _localizationService;
        private readonly ILaborOrchestration _laborOrchestration;
        private readonly IGenericServiceOrchestration _genericServiceOrchestration;
        private readonly IEntityTypeOrchestration _entityTypeOrchestration;
        private readonly IEntityFactory _entityFactory;

        [ActivatorUtilitiesConstructor]
        public GenericHelper(
            ILocalizationService localizationService,
            ILaborOrchestration laborOrchestration,
            IGenericServiceOrchestration genericServiceOrchestration,
            IEntityTypeOrchestration entityTypeOrchestration,
            IEntityFactory entityFactory)
        {
            _localizationService = localizationService;
            _laborOrchestration = laborOrchestration;
            _genericServiceOrchestration = genericServiceOrchestration;
            _entityTypeOrchestration = entityTypeOrchestration;
            _entityFactory = entityFactory;
        }

        public void ThrowLocalizedException(string key, params string[] parameters)
        {
            string exceptionMessage = GetLocalizedMessage(key, parameters);
            throw new CmfBaseException(exceptionMessage);
        }

        public IEmployee GetCurrentEmployee()
        {
            IUser currentUser = new User();
            currentUser.Load(Cmf.Foundation.Common.Utilities.DomainUserName);

            return _laborOrchestration.GetEmployeeByUserAccount(new GetEmployeeByUserAccountInput
            {
                UserAccount = currentUser.UserAccount
            }).Employee;
        }


        public string GetLocalizedMessage(string localizedMessageKey, params object[] parameters)
        {
            string message = _localizationService.Localize(System.Threading.Thread.CurrentThread.CurrentCulture.Name, localizedMessageKey);
            return string.Format(message, parameters);
        }

        public Collection<object> GetObjectsByIds(List<long> ids, object type)
        {
            Collection<object> objects = new();

            if (ids != null && ids.Any())
            {
                GetObjectsByFilterOutput output = _genericServiceOrchestration.GetObjectsByFilter(new GetObjectsByFilterInput
                {
                    Type = type,
                    Filter = new FilterCollection
                    {
                        new Filter
                        {
                            Name = "Id",
                            Operator = FieldOperator.In,
                            Value = ids,
                            LogicalOperator = LogicalOperator.AND
                        }
                    }
                });

                if (output.Instance != null && output.Instance.Count > 0)
                {
                    objects = output.Instance;
                }
            }

            return objects;
        }

        public Collection<object> GetVersionableObjectsByIds(List<long> ids, object type)
        {
            Collection<object> objects = new();

            GetObjectsByFilterOutput output = _genericServiceOrchestration.GetObjectsByFilter(new GetObjectsByFilterInput
            {
                Type = type,
                Filter = new FilterCollection
                {
                    new Filter
                    {
                        Name = "DefinitionId",
                        Operator = FieldOperator.In,
                        Value = ids,
                        LogicalOperator = LogicalOperator.AND
                    },
                    new Filter
                    {
                        Name = "UniversalState",
                        Operator = FieldOperator.IsEqualTo,
                        Value = (int)UniversalState.Effective,
                        LogicalOperator = LogicalOperator.AND
                    },
                    new Filter
                    {
                        Name = "Version",
                        Operator = FieldOperator.GreaterThan,
                        Value = 0,
                        LogicalOperator = LogicalOperator.AND
                    }
                }
            });

            if (output.Instance != null && output.Instance.Count > 0)
            {
                objects = output.Instance;
            }

            return objects;
        }




        public void LoadByIDs<T>(IEntityCollection<T> instances, Collection<long> idsToLoad) where T : IEntity
        {
            LoadByIDs(instances, idsToLoad.ToList());
        }

        public void LoadByIDs<T>(ICoreBaseCollection<T> instances, List<long> idsToLoad) where T : ICoreBase
        {
            if (instances != null && idsToLoad != null)
            {
                instances.Clear();

                List<long> validIds = idsToLoad.Distinct().Where(id => id > 0).ToList();
                if (validIds.Count > 0)
                {
                    Collection<object> foundObjects = _genericServiceOrchestration.GetObjectsByFilter(new GetObjectsByFilterInput
                    {
                        Filter = new FilterCollection
                        {
                            new Filter
                            {
                                Name = "Id",
                                Operator = FieldOperator.In,
                                Value = validIds
                            }
                        },
                        Type = _entityFactory.Create<T>()
                    }).Instance;

                    if (foundObjects != null && foundObjects.Count > 0)
                    {
                        foreach (object loadedObject in foundObjects)
                        {
                            instances.Add((T)loadedObject);
                        }
                    }
                }
            }
        }

        public void AddAttachments(IEntityInstance entityInstance, IEntityDocumentationCollection attachments, string tempFileLocation)
        {
            FullUpdateEntityAttachmentsInput input = new()
            {
                AttachmentsToAdd = new List<IEntityDocumentationFullFileNamePair>(),
                AttachmentsToRemove = new EntityDocumentationCollection(),
                Entity = entityInstance
            };

            foreach (IEntityDocumentation attachment in attachments)
            {
                bool isToHandleInternalAttachment =
                    !string.IsNullOrWhiteSpace(attachment.Filename) &&
                    attachment.StorageType == ContentStorageType.Internal;

                IEntityDocumentationFullFileNamePair pair = new EntityDocumentationFullFileNamePair
                {
                    EntityDocumentation = new EntityDocumentation
                    {
                        Description = attachment.Description,
                        Type = attachment.Type,
                        StorageType = attachment.StorageType,
                        Url = attachment.Url,
                        ContentURL = attachment.ContentURL
                    }
                };

                if (isToHandleInternalAttachment)
                {
                    string fileName = string.Format("{0}.{1}", Guid.NewGuid().ToString(), attachment.Filename.Split('.').Last());
                    tempFileLocation = string.Format("{0}\\{1}", tempFileLocation, fileName);
                    File.Copy(attachment.ContentLocation, tempFileLocation);

                    pair.EntityDocumentation.Filename = attachment.Filename;
                    pair.FullFileName = tempFileLocation;
                }

                input.AttachmentsToAdd.Add(pair);
            }

            _entityTypeOrchestration.FullUpdateEntityAttachments(input);
        }



        public bool IsConfigEnabled(string configPath)
        {
            return ConfigExtensions.GetConfigValue<bool>(configPath);
        }
    }
}

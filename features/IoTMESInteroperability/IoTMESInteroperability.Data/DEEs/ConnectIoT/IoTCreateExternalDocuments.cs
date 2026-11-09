using Cmf.Common.CustomActionUtilities;
using Cmf.Common.CustomActionUtilities.Abstractions;
using Cmf.Navigo.BusinessObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using Cmf.Foundation.Common;
using Cmf.Foundation.BusinessObjects.SmartTables;
using Cmf.Community.IoTMESInteroperability.Common;
using Cmf.Community.IoTMESInteroperability.Utilities;
using Cmf.Foundation.BusinessObjects;
using System.Data;
using Cmf.Community.IoTMESInteroperability.Common.Objects;
using Newtonsoft.Json;
using Cmf.Foundation.BusinessOrchestration.TableManagement;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessOrchestration.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Cmf.Foundation.BusinessOrchestration.Abstractions;

namespace Cmf.Community.IoTMESInteroperability.Actions
{
    public class IoTCreateExternalDocuments : DeeDevBase
    {
        public override bool DeeTestCondition(Dictionary<string, object> Input)
        {
            //---Start DEE Condition Code---

            #region Info

            /// <summary>
            /// Summary text
            ///     DEE To be invoked by IoT to Create External Documents
            /// Action Groups:
            /// Depends On:
            /// Is Dependency For:
            /// Exceptions:
            /// </summary>

            #endregion

            string contextParameter_Material = "IoTCreateExternalDocuments_Material";
            string contextParameter_FilesInformation = "IoTCreateExternalDocuments_FilesInformation";
            string contextParameter_Folder = "IoTCreateExternalDocuments_Folder";
            bool isToExecute = false;

            #region Service Provider

            // Get services provider information
            IServiceProvider serviceProvider = (IServiceProvider)Input["ServiceProvider"];
            IEntityFactory entityFactory = serviceProvider.GetService<IEntityFactory>();
            IDEEHelper deeHelper = serviceProvider.GetService<IDEEHelper>();

            #endregion

            if (Input.Keys.Contains("MaterialName") && Input.Keys.Contains("FilesInformation"))
            {
                string materialName = Input["MaterialName"] as string;
                List<FileInformation> filesInformation = JsonConvert.DeserializeObject<List<FileInformation>>(Input["FilesInformation"].ToJsonString());
                string folderName = Input.Keys.Contains("Folder") ? Input["Folder"] as string : Constants.DocumentsDefaultFolder;

                // Validate that the material exists
                IMaterial material = entityFactory.Create<IMaterial>();
                material.Name = materialName;
                material.Load();
                if (!material.ObjectExists())
                {
                    GeneralUtilities.ThrowLocalizedException(IoTUtilitiesMessages.InvalidMaterial);
                }

                if (filesInformation.Any())
                {
                    deeHelper.SetContextParameter(contextParameter_Material, material);
                    deeHelper.SetContextParameter(contextParameter_FilesInformation, filesInformation);
                    deeHelper.SetContextParameter(contextParameter_Folder, folderName);
                    isToExecute = true;
                }
            }
            else
            {
                GeneralUtilities.ThrowLocalizedException(IoTUtilitiesMessages.InvalidInputs);
            }

            return isToExecute;

            //---End DEE Condition Code---

        }

        public override Dictionary<string, object> DeeActionCode(Dictionary<string, object> Input)
        {
            //---Start DEE Code---

            // System
            UseReference("", "System.Data");

            // CORE
            UseReference("", "Cmf.Foundation.BusinessObjects.Abstractions");

            // MES
            UseReference("Cmf.Navigo.BusinessObjects.dll", "Cmf.Navigo.BusinessObjects");
            UseReference("Cmf.Navigo.BusinessOrchestration.dll", "Cmf.Foundation.BusinessOrchestration.Abstractions");

            // Custom
            UseReference("Cmf.Community.IoTMESInteroperability.Common.dll", "Cmf.Community.IoTMESInteroperability");
            UseReference("Cmf.Community.IoTMESInteroperability.Common.dll", "Cmf.Community.IoTMESInteroperability.Common");
            UseReference("Cmf.Community.IoTMESInteroperability.Common.dll", "Cmf.Community.IoTMESInteroperability.Common.Objects");

            // Common
            UseReference("Cmf.Common.CustomActionUtilities.dll", "Cmf.Common.CustomActionUtilities");

            // 3rd Party
            UseReference("Newtonsoft.Json.dll", "Newtonsoft.Json");

            string contextParameter_Material = "IoTCreateExternalDocuments_Material";
            string contextParameter_FilesInformation = "IoTCreateExternalDocuments_FilesInformation";
            string contextParameter_Folder = "IoTCreateExternalDocuments_Folder";

            #region Service Provider

            // Get services provider information
            IServiceProvider serviceProvider = (IServiceProvider)Input["ServiceProvider"];
            IEntityFactory entityFactory = serviceProvider.GetService<IEntityFactory>();

            ITableOrchestration _tableOrchestration = serviceProvider.GetService<ITableOrchestration>();
            IDEEHelper deeHelper = serviceProvider.GetService<IDEEHelper>();
            IEntityHelper entityHelper = serviceProvider.GetService<IEntityHelper>();

            #endregion

            IMaterial material = deeHelper.GetContextParameter<IMaterial>(contextParameter_Material);
            List<FileInformation> filesInformation = deeHelper.GetContextParameter<List<FileInformation>>(contextParameter_FilesInformation);
            string folderName = deeHelper.GetContextParameter<string>(contextParameter_Folder);

            ISmartTable smartTable = entityFactory.Create<ISmartTable>();
            smartTable.Name = IoTUtilitiesConstants.STMaterialDocumentContext;
            smartTable.Load();

            DataTable dataTable = new DataTable();
            dataTable.Columns.Add("Material", typeof(string));
            dataTable.Columns.Add("Document", typeof(string));

            #region Validate and Create Folder

            bool folderExist = IoTUtilities.FolderExists(folderName);

            IFolder folder = entityFactory.Create<IFolder>();
            folder.Name = folderName;

            if (!folderExist)
            {
                IFolder parentFolder = entityFactory.Create<IFolder>();
                parentFolder.Load(Constants.DocumentsDefaultFolder);

                folder.Name = folderName;
                folder.ParentFolder = parentFolder;
                folder.Create();
            }
            folder.Load();

            #endregion

            #region Create Documents

            IDocumentCollection documentsToCreate = entityFactory.CreateCollection<IDocumentCollection>();
            IChangeSetCollection changeSetCollection = entityFactory.CreateCollection<IChangeSetCollection>();

            Cmf.Foundation.Security.User userCmNavigo = new Foundation.Security.User();
            string userAccount = Cmf.Foundation.Common.Utilities.DomainUserName;
            userCmNavigo.Load(userAccount);

            foreach (FileInformation fileInfo in filesInformation)
            {
                IChangeSet changeSet = entityHelper.CreateChangeSet();
                changeSetCollection.Add(changeSet);

                IDocument document = entityFactory.Create<IDocument>();
                document.Name = fileInfo.FileName;
                document.Type = IoTUtilitiesConstants.DefaultDocumentType;
                document.ContentStorageType = ContentStorageType.External;
                document.ContentURL = fileInfo.FileLocation;
                document.Folder = folder;
                document.Author = userCmNavigo.Name;
                document.CreationDate = DateTime.Now;
                document.ChangeSet = changeSet;

                documentsToCreate.Add(document);

                DataRow dataRow = dataTable.NewRow();
                dataRow["Material"] = material.Name;
                dataRow["Document"] = document.Name;
                dataTable.Rows.Add(dataRow);
            }

            documentsToCreate.CreateEntity();
            foreach (IChangeSet changeSet in changeSetCollection)
            {
                changeSet.RequestApproval();
            }
            documentsToCreate.Load();
            documentsToCreate.MakeEffective();

            #endregion

            #region Create Material Document Contexts

            DataSet dataSet = new DataSet();
            dataSet.Tables.Add(dataTable);
            Foundation.BusinessOrchestration.TableManagement.InputObjects.FullUpdateSmartTableDataInput fullUpdateSmartTableDataInput = new Foundation.BusinessOrchestration.TableManagement.InputObjects.FullUpdateSmartTableDataInput()
            {
                SmartTable = smartTable,
                RowsToAddOrUpdate = NgpDataSet.FromDataSet(dataSet)
            };
            _tableOrchestration.FullUpdateSmartTableData(fullUpdateSmartTableDataInput);

            #endregion

            return Input;

            //---End DEE Code---
        }
    }
}

using Cmf.Common.CustomActionUtilities.Abstractions;
using Cmf.Community.TimeTracking.Actions.TimeTracking;
using Cmf.Community.TimeTracking.UnitTests.Utilities;
using Cmf.Foundation.BaseTestsUtils;
using Cmf.Foundation.BaseTestsUtils.Extensions;
using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using Cmf.Navigo.BusinessOrchestration.Abstractions;
using Cmf.Navigo.BusinessOrchestration.LaborManagement.InputObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace Cmf.Community.TimeTracking.UnitTests.Actions.TimeTracking
{
    public class CustomAutoCheckOutWhenCheckInToNewResourceTests : ActionBaseTests
    {
        private readonly CustomAutoCheckOutWhenCheckInToNewResource _actionUnderTest = new();

        private readonly Mock<IEntityFactory> _mockEntityFactory;
        private readonly Mock<IDEEHelper> _deeHelperMock;
        private readonly Mock<ILaborOrchestration> _laborOrchestrationMock;
        private readonly Mock<IEmployee> _employeeMock = new();
        private readonly Mock<IResource> _resourceMock = new();
        private readonly Mock<ICheckInEmployeeParameters> _checkInEmployeeParametersMock = new();
        private readonly Mock<ICertification> _certificationMock = new();
        private readonly Mock<IAttributeCollection> _resourceAttribuemock = new();
        private readonly MesCollectionsBuilder _collectionBuilder;
        private readonly Mock<IResourceEmployee> _resourceEmployeeMock = new();
        private readonly Mock<IEmployeeCollection> _employeeCollectionMock = new();
        private readonly Mock<IUtilities> _mockUtilities = new();

        public CustomAutoCheckOutWhenCheckInToNewResourceTests()
        {
            _mockEntityFactory = AddMockToActionInput<IEntityFactory>();
            _deeHelperMock = AddMockToActionInput<IDEEHelper>();
            _laborOrchestrationMock = AddMockToActionInput<ILaborOrchestration>();

            _collectionBuilder = new MesCollectionsBuilder(_mockUtilities, _mockEntityFactory);
        }

        //TestCondition Tests

        [Fact]
        public void DeeTestCondition_InputIsSpecialCheckInEmployeesInput_ReturnsTrue()
        {
            //employee
            _mockEntityFactory.Setup(x => x.Create<IEmployee>()).Returns(_employeeMock.Object);
            _employeeMock.Setup(x => x.ObjectExists()).Returns(true);
            //resource
            _mockEntityFactory.Setup(x => x.Create<IResource>()).Returns(_resourceMock.Object);
            Dictionary<string, object> resourceAttributeList = new Dictionary<string, object>();
            resourceAttributeList.Add("IsEmployeeExclusive", true);
            _resourceAttribuemock.MockCollectionWithDictionary(resourceAttributeList);
            _resourceMock.SetupGet(x => x.Attributes).Returns(_resourceAttribuemock.Object);
            //input
            Dictionary<IResource, ICertification> resourceCertification = new();
            resourceCertification.Add(_resourceMock.Object, _certificationMock.Object);
            _checkInEmployeeParametersMock.Setup(x => x.ResourcesCertification).Returns(resourceCertification);
            Dictionary<IEmployee, ICheckInEmployeeParameters> employee = new();
            employee.Add(_employeeMock.Object, _checkInEmployeeParametersMock.Object);
            SpecialCheckInEmployeesInput specialCheckInEmployeesInput = new SpecialCheckInEmployeesInput();
            specialCheckInEmployeesInput.Employees = employee;
            ActionInput["SpecialCheckInEmployeesInput"] = specialCheckInEmployeesInput;
            var result = _actionUnderTest.DeeTestCondition(ActionInput);
            result.Should().BeTrue();
            Mock.Verify(
               _mockEntityFactory,
               _deeHelperMock,
               _resourceMock,
               _employeeMock
             );
        }

        [Fact]
        public void DeeTestCondition_CheckInEmployeesInput_ReturnsTrue()
        {
            //employee
            _mockEntityFactory.Setup(x => x.Create<IEmployee>()).Returns(_employeeMock.Object);
            _employeeMock.Setup(x => x.ObjectExists()).Returns(true);
            //resource
            _mockEntityFactory.Setup(x => x.Create<IResource>()).Returns(_resourceMock.Object);
            Dictionary<string, object> resourceAttributeList = new Dictionary<string, object>();
            resourceAttributeList.Add("IsEmployeeExclusive", true);
            _resourceAttribuemock.MockCollectionWithDictionary(resourceAttributeList);
            _resourceMock.SetupGet(x => x.Attributes).Returns(_resourceAttribuemock.Object);
            //input
            Dictionary<IResource, ICertification> resourceCertification = new();
            resourceCertification.Add(_resourceMock.Object, _certificationMock.Object);
            _checkInEmployeeParametersMock.Setup(x => x.ResourcesCertification).Returns(resourceCertification);
            Dictionary<IEmployee, ICheckInEmployeeParameters> employee = new();
            employee.Add(_employeeMock.Object, _checkInEmployeeParametersMock.Object);
            CheckInEmployeesInput specialCheckInEmployeesInput = new CheckInEmployeesInput();
            specialCheckInEmployeesInput.Employees = employee;
            ActionInput["CheckInEmployeesInput"] = specialCheckInEmployeesInput;
            var result = _actionUnderTest.DeeTestCondition(ActionInput);
            result.Should().BeTrue();
            Mock.Verify(
              _mockEntityFactory,
              _deeHelperMock,
              _resourceMock,
              _employeeMock
           );
        }

        [Fact]
        public void DeeTestCondition_ReturnFalse()
        {
            //employee
            _mockEntityFactory.Setup(x => x.Create<IEmployee>()).Returns(_employeeMock.Object);
            //resource
            _mockEntityFactory.Setup(x => x.Create<IResource>()).Returns(_resourceMock.Object);

            var result = _actionUnderTest.DeeTestCondition(ActionInput);
            result.Should().BeFalse();
            Mock.Verify(
           _mockEntityFactory,
           _deeHelperMock
         );
        }

        [Fact]
        public void DeeActionCode_ReturnTrue()
        {
            _deeHelperMock.Setup(x => x.GetContextParameter("CustomAutoCheckOutEmployee_Employee")).Returns(_employeeMock.Object);
            _resourceEmployeeMock.Setup(x => x.SourceEntity).Returns(_resourceMock.Object);
            _resourceEmployeeMock.Setup(x => x.TargetEntity).Returns(_employeeMock.Object);
            _mockEntityFactory.Setup(x => x.CreateCollection<IEmployeeCollection>()).Returns(_employeeCollectionMock.Object);

            EntityRelationCollection<IEntityRelation> entityRelationCol = _collectionBuilder.NewEntityRelationCollection(_resourceEmployeeMock.Object);
            CmfEntityRelationCollection cmfEntityRelationCol = new CmfEntityRelationCollection(_mockEntityFactory.Object)
          {
              { Navigo.Common.Constants.ResourceEmployee, entityRelationCol }
          };

            _employeeMock.Setup(x => x.LoadRelations(Navigo.Common.Constants.ResourceEmployee)).Callback(() =>
            {
                _employeeMock.SetupProperty(r => r.RelationCollection, cmfEntityRelationCol);
            });
            var output = _actionUnderTest.DeeActionCode(ActionInput);
            ActionInput.Should().BeSameAs(output);
            Mock.Verify(
               _mockEntityFactory,
               _deeHelperMock,
               _resourceMock,
               _employeeMock
             );
        }
    }
}
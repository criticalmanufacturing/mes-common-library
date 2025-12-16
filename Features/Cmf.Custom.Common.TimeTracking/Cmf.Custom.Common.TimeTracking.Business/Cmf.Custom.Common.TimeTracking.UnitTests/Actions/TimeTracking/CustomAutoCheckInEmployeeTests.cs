using Cmf.Common.CustomActionUtilities.Abstractions;
using Cmf.Custom.Actions.TimeTracking;
using Cmf.Foundation.BaseTestsUtils;
using Cmf.Foundation.BaseTestsUtils.Extensions;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using Cmf.Navigo.BusinessOrchestration.Abstractions;
using Cmf.Navigo.BusinessOrchestration.LaborManagement.InputObjects;
using FluentAssertions;
using Moq;
using Xunit;
using System.Runtime.Serialization;
using Cmf.Custom.Common.TimeTracking.Common;
using Cmf.Foundation.Common.LocalizationService;

namespace Cmf.Custom.Common.TimeTracking.UnitTests.Actions.TimeTracking
{
    public class CustomAutoCheckInEmployeeTests : ActionBaseTests
    {
        private readonly CustomAutoCheckInEmployee _actionUnderTest = new();

        private readonly Mock<IEntityFactory> _mockEntityFactory;
        private readonly Mock<IDEEHelper> _deeHelperMock;
        private readonly Mock<ILaborOrchestration> _laborOrchestrationMock;
        private readonly Mock<IEmployee> _employeeMock = new();
        private readonly Mock<IResource> _resourceMock = new();
        private readonly Mock<IResource> _otherResourceMock = new();
        private readonly Mock<ICheckInEmployeeParameters> _checkInEmployeeParametersMock = new();
        private readonly Mock<ICertification> _certificationMock = new();
        private readonly Mock<IAttributeCollection> _resourceAttributemock = new();
        private readonly Mock<IResourceEmployee> _resourceEmployeeMock = new();
        private readonly Mock<IEmployeeCollection> _employeeCollectionMock = new();
        private readonly Mock<IResourceCollection> _mockResourceCollection = new();
        private readonly Mock<Cmf.Foundation.Common.Abstractions.IUtilities> _mockUtilities = new();
        private readonly Mock<IExtensibleDataObject> _mockExtensibleDataObject = new();
        private readonly Mock<ICheckInEmployeeParameters> _mockCheckInEmployeeParameters = new();
        private readonly Mock<Cmf.Custom.Common.TimeTracking.Common.Abstractions.IUtilities> _mockCommonUtilities;
        private readonly Mock<ILocalizationService> _mockLocalizationService;


        public CustomAutoCheckInEmployeeTests()
        {
            _mockEntityFactory = AddMockToActionInput<IEntityFactory>();
            _deeHelperMock = AddMockToActionInput<IDEEHelper>();
            _laborOrchestrationMock = AddMockToActionInput<ILaborOrchestration>();
            _mockCommonUtilities = AddMockToActionInput<Cmf.Custom.Common.TimeTracking.Common.Abstractions.IUtilities>();
            _mockLocalizationService = AddMockToActionInput<ILocalizationService>();
            _mockUtilities = AddMockToActionInput<Cmf.Foundation.Common.Abstractions.IUtilities>();

        }

        #region DeeTestCondition Tests

        [Fact]
        public void DeeTestCondition_ReturnFalse()
        {
            // Arrange

            //resource
            _mockEntityFactory.Setup(x => x.Create<IResource>()).Returns(_resourceMock.Object);
            Dictionary<string, object> resourceAttributeList = new Dictionary<string, object>();
            resourceAttributeList.Add("IsToCheckInAtTrackIn", false);
            _resourceAttributemock.MockCollectionWithDictionary(resourceAttributeList);
            _resourceMock.SetupGet(x => x.Attributes).Returns(_resourceAttributemock.Object);
            ActionInput["Resource"] = _resourceMock.Object;

            // Act

            var result = _actionUnderTest.DeeTestCondition(ActionInput);

            // Assert

            result.Should().BeFalse();

            Mock.Verify(
               _mockEntityFactory,
               _resourceAttributemock,
               _resourceMock,
               _deeHelperMock
            );
        }

        [Fact]
        public void DeeTestCondition_MissingResourceAttribute_ReturnFalse()
        {
            // Arrange

            //resource
            _mockEntityFactory.Setup(x => x.Create<IResource>()).Returns(_resourceMock.Object);
            Dictionary<string, object> resourceAttributeList = new Dictionary<string, object>();
            // No attribute is defined
            _resourceMock.SetupGet(x => x.Attributes).Returns(_resourceAttributemock.Object);
            ActionInput["Resource"] = _resourceMock.Object;

            // Act

            var result = _actionUnderTest.DeeTestCondition(ActionInput);

            // Assert

            result.Should().BeFalse();

            Mock.Verify(
               _mockEntityFactory,
               _resourceAttributemock,
               _resourceMock,
               _deeHelperMock
            );
        }

        [Fact]
        public void DeeTestCondition_ReturnTrue()
        {
            // Arrange

            //resource
            _mockEntityFactory.Setup(x => x.Create<IResource>()).Returns(_resourceMock.Object);
            Dictionary<string, object> resourceAttributeList = new Dictionary<string, object>();
            resourceAttributeList.Add("IsToCheckInAtTrackIn", true);
            _resourceAttributemock.MockCollectionWithDictionary(resourceAttributeList);
            _resourceMock.SetupGet(x => x.Attributes).Returns(_resourceAttributemock.Object);
            ActionInput["Resource"] = _resourceMock.Object;

            // Act

            var result = _actionUnderTest.DeeTestCondition(ActionInput);

            // Assert

            result.Should().BeTrue();

            Mock.Verify(
               _mockEntityFactory,
               _resourceAttributemock,
               _resourceMock,
               _deeHelperMock
            );
        }

        #endregion

        #region DeeActionCode Tests

        [Fact]
        public void DeeActionCode_NoCheckedInResource_ReturnTrue()
        {
            // Arrange

            // Employee is not checked in any Resource
            ArrangeForActionCode(isEmployeeAlreadyCheckedIn : false);

            // Act

            var output = _actionUnderTest.DeeActionCode(ActionInput);

            // Assert

            // Verify that CheckInEmployees was called once
            _laborOrchestrationMock.Verify(x => x.CheckInEmployees(It.IsAny<CheckInEmployeesInput>()), Times.Once);

            ActionInput.Should().BeSameAs(output);

            Mock.Verify(
               _mockEntityFactory,
               _deeHelperMock,
               _mockResourceCollection,
               _mockCommonUtilities,
               _resourceMock,
               _otherResourceMock,
               _employeeMock,
               _laborOrchestrationMock
             );
        }

        [Fact]
        public void DeeActionCode_AlreadyCheckedIn_SameResource_ReturnTrue()
        {
            // Arrange

            // Employee is already checked in the SAME Resource
            ArrangeForActionCode(isEmployeeAlreadyCheckedIn: true, isCheckedInSameResource: true, isCheckedInOtherResource: false);

            // Act

            var output = _actionUnderTest.DeeActionCode(ActionInput);

            // Assert

            // Verify that CheckInEmployees was NEVER called
            _laborOrchestrationMock.Verify(x => x.CheckInEmployees(It.IsAny<CheckInEmployeesInput>()), Times.Never);

            ActionInput.Should().BeSameAs(output);

            Mock.Verify(
               _mockEntityFactory,
               _deeHelperMock,
               _mockResourceCollection,
               _mockCommonUtilities,
               _resourceMock,
               _otherResourceMock,
               _employeeMock,
               _laborOrchestrationMock
             );
        }

        [Fact]
        public void DeeActionCode_AlreadyCheckedIn_OtherResource_ReturnTrue()
        {
            // Arrange

            // Employee is already checked in the OTHER Resource
            ArrangeForActionCode(isEmployeeAlreadyCheckedIn: true, isCheckedInSameResource: false, isCheckedInOtherResource: true);

            // Act

            var output = _actionUnderTest.DeeActionCode(ActionInput);

            // Assert

            // Verify that CheckInEmployees was called once
            _laborOrchestrationMock.Verify(x => x.CheckInEmployees(It.IsAny<CheckInEmployeesInput>()), Times.Once);

            ActionInput.Should().BeSameAs(output);

            Mock.Verify(
               _mockEntityFactory,
               _deeHelperMock,
               _mockResourceCollection,
               _mockCommonUtilities,
               _resourceMock,
               _otherResourceMock,
               _employeeMock,
               _laborOrchestrationMock
             );
        }

        [Fact]
        public void DeeActionCode_AlreadyCheckedIn_MixedResource_ReturnTrue()
        {
            // Arrange

            // Employee is already checked in the MIXED Resources
            ArrangeForActionCode(isEmployeeAlreadyCheckedIn: true, isCheckedInSameResource: true, isCheckedInOtherResource: true);

            // Act

            var output = _actionUnderTest.DeeActionCode(ActionInput);

            // Assert

            // Verify that CheckInEmployees was NEVER called
            _laborOrchestrationMock.Verify(x => x.CheckInEmployees(It.IsAny<CheckInEmployeesInput>()), Times.Never);

            ActionInput.Should().BeSameAs(output);

            Mock.Verify(
               _mockEntityFactory,
               _deeHelperMock,
               _mockResourceCollection,
               _mockCommonUtilities,
               _resourceMock,
               _otherResourceMock,
               _employeeMock,
               _laborOrchestrationMock
             );
        }

        [Fact]
        public void DeeActionCode_EmployeeNotExist_ThrowError()
        {
            // Arrange

            //resource
            _mockEntityFactory.Setup(x => x.Create<IResource>()).Returns(_resourceMock.Object);
            ActionInput["Resource"] = _resourceMock.Object;

            // employee
            _mockEntityFactory.Setup(x => x.Create<IEmployee>()).Returns(_employeeMock.Object);
            _mockCommonUtilities.Setup(x => x.GetCurrentUserEmployee(false)).Returns(_employeeMock.Object);
            _employeeMock.Setup(x => x.ObjectExists()).Returns(false);

            _mockUtilities.Setup(x => x.DomainUserName).Returns("TestUserName");

            string expectedMessage = $"Error Message";
            _mockLocalizationService.Setup(x => x.Localize(It.IsAny<string>(), "CustomNoEmployeeForUser")).Returns(expectedMessage).Verifiable();

            // Act & Assert
            var exception = Xunit.Assert.Throws<CommonException>(() => _actionUnderTest.DeeActionCode(ActionInput));
            exception.Message.Should().Be(expectedMessage);

            // Verify that CheckInEmployees was NEVER called
            _laborOrchestrationMock.Verify(x => x.CheckInEmployees(It.IsAny<CheckInEmployeesInput>()), Times.Never);

            Mock.Verify(
               _mockEntityFactory,
               _deeHelperMock,
               _mockResourceCollection,
               _mockCommonUtilities,
               _resourceMock,
               _otherResourceMock,
               _employeeMock,
               _laborOrchestrationMock
             );
        }

        #endregion

        private void ArrangeForActionCode(bool isEmployeeAlreadyCheckedIn = false, bool isCheckedInSameResource = false, bool isCheckedInOtherResource = false)
        {
            //resource
            _mockEntityFactory.Setup(x => x.Create<IResource>()).Returns(_resourceMock.Object);
            ActionInput["Resource"] = _resourceMock.Object;
            _resourceMock.Setup(x => x.Id).Returns(123456789);

            //other resource
            _mockEntityFactory.Setup(x => x.Create<IResource>()).Returns(_otherResourceMock.Object);
            _otherResourceMock.Setup(x => x.Id).Returns(987654321);

            List<IResource> checkedInResourceList = new List<IResource>{};

            if (isEmployeeAlreadyCheckedIn)
            {
                if (isCheckedInSameResource)
                {
                    checkedInResourceList.Add(_resourceMock.Object);
                }

                if (isCheckedInOtherResource)
                {
                    checkedInResourceList.Add(_otherResourceMock.Object);
                }               
            }         

            _mockResourceCollection.MockCollectionWithList(checkedInResourceList);

            // employee
            _mockEntityFactory.Setup(x => x.Create<IEmployee>()).Returns(_employeeMock.Object);
            _mockCommonUtilities.Setup(x => x.GetCurrentUserEmployee(false)).Returns(_employeeMock.Object);
            _employeeMock.Setup(x => x.GetCheckedInResources(false)).Returns(_mockResourceCollection.Object);
            _employeeMock.Setup(x => x.Name).Returns("TestCurrentEmployee");
            _employeeMock.Setup(x => x.ObjectExists()).Returns(true);
        }
    }
}
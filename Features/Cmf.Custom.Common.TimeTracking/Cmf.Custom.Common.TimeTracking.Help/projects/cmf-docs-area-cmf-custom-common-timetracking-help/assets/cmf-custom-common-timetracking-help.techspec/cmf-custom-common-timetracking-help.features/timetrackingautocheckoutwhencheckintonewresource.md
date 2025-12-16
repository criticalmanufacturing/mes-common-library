# [Time Tracking] Auto Check-out when Check-in to new resource

## Requirement Specification

This feature ensures that an operator can only be actively working on one order at a time by preventing multiple simultaneous check-ins across different resources in the MES.

## Design Specification

A new DEE Action will be created that checks out the employee from other resources when checking into an exclusive resource.

### Relevant Artifacts

The table below describes the properties for this entity type:

| Name                                       | Type               | New?  | Description                                                                                 |
| :----------------------------------------- | :----------------- | :---: | :------------------------------------------------------------------------------------------ |
| CustomAutoCheckOutWhenCheckInToNewResource | DEE Action         |  Yes  | DEE to check-out the employee from other resources when checking into an exclusive resource |
| IsEmployeeExclusive                        | Resource Attribute |  Yes  | Attribute used to define if the Resource is exclusive                                       |

### How it works

If `IsEmployeeExclusive` is set as true in the Target Resource, the Employee must be checked-out automatically from the previous resources. If the attribute is set s false, the Employee must not be checked-out from other resources.

### Assumptions

## Work items

The table below describes de user stories that affect the current functionality

| User Story | Type | Title | Description |

| User Story | Type       | Title                                                        | Description                                                  |
| :--------- | :--------- | :----------------------------------------------------------- | :----------------------------------------------------------- |
| 462761     | User Story | [Time Tracking] Auto Check-out when Check-in to new resource | [Time Tracking] Auto Check-out when Check-in to new resource |

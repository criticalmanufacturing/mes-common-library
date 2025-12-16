# [Time Tracking] Auto Check-In when Track-In Material

## Requirement Specification

This feature ensures that an Employee is automatically checked-in when tracking-in a Material to a Resource.

## Design Specification

A new DEE Action will be created that when an Employee is tracking-in a Material to a Resource, DEE Action validates prerequisites, and if the Employee is not already checked-in to the current Resource, it will perform the check-in.

### Relevant Artifacts

The table below describes the properties for this entity type:

| Name                                                                                                                                                                                              | Type               | New?  | Description                                                                                |
| :------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | :----------------- | :---: | :----------------------------------------------------------------------------------------- |
| [CustomAutoCheckInEmployee](\cmf-custom-common-timetracking-help.techspec\cmf-custom-common-timetracking-help.artifacts>cmf-custom-common-timetracking-help.deeactions>customautocheckinemployee) | DEE Action         |  Yes  | DEE to automatically check-in Employee when tracking-in a Material to a Resource           |
| IsToCheckInAtTrackIn                                                                                                                                                                              | Resource Attribute |  Yes  | Attribute to enable Employee automatic check-in feature when Material track-in to Resource |

### How it works

If `IsToCheckInAtTrackIn` Resource Attribute is set as true, automatic check-in feature is enabled when a Material is tracked-in to this Resource. If this Attribute is set to false, the DEE Action will end without action.

If the Employee is already checked-in to the Resource where the Material is being tracked-in, the DEE Action will end without action. Otherwise the DEE Action automatically check-in the Employee to the Resource.

If the operator is not an existing Employee in MES, the DEE Action will throw a `CustomNoEmployeeForUser` Error.

### Assumptions

- `IsToCheckInAtTrackIn` Resource Attribute exists.
- `Require Check-In` flag should be set to true for every Resource in order to force Employee check-in to be able to process Materials.
- Employee `Require Clock-In` flag should be set to true in order to do not allow Employees to do check-in without clocked-in to MES.

## Work items

The table below describes de user stories that affect the current functionality

| User Story | Type       | Title                                                | Description                                                                    |
| :--------- | :--------- | :--------------------------------------------------- | :----------------------------------------------------------------------------- |
| 462780     | User Story | [Time Tracking] Auto Check-In when Track-In Material | Employee is automatically checked-in when tracking-in a Material to a Resource |

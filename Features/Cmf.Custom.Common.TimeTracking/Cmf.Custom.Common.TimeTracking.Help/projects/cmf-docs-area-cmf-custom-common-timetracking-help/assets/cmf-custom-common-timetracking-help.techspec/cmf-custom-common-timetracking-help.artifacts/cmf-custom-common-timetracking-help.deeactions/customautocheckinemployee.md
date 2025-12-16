# CustomAutoCheckInEmployee

## Overview

DEE Action to Automatically CheckIn an employee into a Resource before a TrackIn.

## Action Groups

* BusinessObjects.MaterialCollection.TrackIn.Pre

## Pre Conditions

* `IsToCheckInAtTrackIn` Resource Attribute exists.
* `Require Check-In` flag should be set to true for every Resource in order to force Employee check-in to be able to process Materials.
* Employee `Require Clock-In` flag should be set to true in order to do not allow Employees to do check-in without clocked-in to MES.

## Action

* Automatically CheckIn an employee into a Resource before a TrackIn

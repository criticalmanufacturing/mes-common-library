import { System } from "@criticalmanufacturing/connect-iot-controller-engine";
import Cmf from "cmf-lbos";
import QueryObject = System.LBOS.Cmf.Foundation.BusinessObjects.QueryObject;

/**
 * Helper class to create MES query objects for ISA-95 hierarchy resolution.
 */
export class Queries {

    /**
     * Create a query to resolve the ISA-95 hierarchy starting from a Material name.
     */
    static getIsa95QueryFromMaterial(materialName: string): QueryObject.QueryObject {
        const filterCollection: Cmf.Foundation.BusinessObjects.QueryObject.FilterCollection =
            new Cmf.Foundation.BusinessObjects.QueryObject.FilterCollection();

        const filter_0 = new Cmf.Foundation.BusinessObjects.QueryObject.Filter();
        filter_0.Name = "Name";
        filter_0.ObjectName = "Material";
        filter_0.ObjectAlias = "Material_1";
        filter_0.Operator = Cmf.Foundation.Common.FieldOperator.IsEqualTo;
        filter_0.Value = materialName;
        filter_0.LogicalOperator = Cmf.Foundation.Common.LogicalOperator.Nothing;
        filter_0.FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal;

        const makeAlwaysTrue = (objectName: string, objectAlias: string): Cmf.Foundation.BusinessObjects.QueryObject.Filter => {
            const f = new Cmf.Foundation.BusinessObjects.QueryObject.Filter();
            f.ObjectName = objectName;
            f.ObjectAlias = objectAlias;
            f.Value = null;
            f.LogicalOperator = Cmf.Foundation.Common.LogicalOperator.AND;
            f.FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.AlwaysTrue;
            return f;
        };

        filterCollection.push(filter_0);
        filterCollection.push(makeAlwaysTrue("Resource", "Material_LastProcessedResource_2"));
        filterCollection.push(makeAlwaysTrue("Resource", "Material_LastProcessedResource_2"));
        filterCollection.push(makeAlwaysTrue("Area", "Material_LastProcessedResource_Area_3"));
        filterCollection.push(makeAlwaysTrue("Resource", "Material_LastProcessedResource_2"));
        filterCollection.push(makeAlwaysTrue("Area", "Material_LastProcessedResource_Area_3"));
        filterCollection.push(makeAlwaysTrue("Facility", "Material_LastProcessedResource_Area_Facility_4"));
        filterCollection.push(makeAlwaysTrue("Resource", "Material_LastProcessedResource_2"));
        filterCollection.push(makeAlwaysTrue("Area", "Material_LastProcessedResource_Area_3"));
        filterCollection.push(makeAlwaysTrue("Facility", "Material_LastProcessedResource_Area_Facility_4"));
        filterCollection.push(makeAlwaysTrue("Site", "Material_LastProcessedResource_Area_Facility_Site_5"));

        const fieldCollection: Cmf.Foundation.BusinessObjects.QueryObject.FieldCollection =
            new Cmf.Foundation.BusinessObjects.QueryObject.FieldCollection();

        const makeField = (alias: string, objectName: string, objectAlias: string, name: string, position: number): Cmf.Foundation.BusinessObjects.QueryObject.Field => {
            const f = new Cmf.Foundation.BusinessObjects.QueryObject.Field();
            f.Alias = alias;
            f.ObjectName = objectName;
            f.ObjectAlias = objectAlias;
            f.IsUserAttribute = false;
            f.Name = name;
            f.Position = position;
            f.Sort = Cmf.Foundation.Common.FieldSort.NoSort;
            return f;
        };

        fieldCollection.push(makeField("Material", "Material", "Material_1", "Name", 1));
        fieldCollection.push(makeField("Resource", "Resource", "Material_LastProcessedResource_2", "Name", 3));
        fieldCollection.push(makeField("Area", "Area", "Material_LastProcessedResource_Area_3", "Name", 4));
        fieldCollection.push(makeField("Facility", "Facility", "Material_LastProcessedResource_Area_Facility_4", "Name", 5));
        fieldCollection.push(makeField("Site", "Site", "Material_LastProcessedResource_Area_Facility_Site_5", "Name", 6));
        fieldCollection.push(makeField("Enterprise", "Enterprise", "Material_LastProcessedResource_Area_Facility_Site_Enterprise_6", "Name", 7));

        const relationCollection: Cmf.Foundation.BusinessObjects.QueryObject.RelationCollection =
            new Cmf.Foundation.BusinessObjects.QueryObject.RelationCollection();

        const makeRelation = (
            sourceEntity: string, sourceAlias: string, sourceJoin: Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType,
            sourceProp: string, targetEntity: string, targetAlias: string,
            targetJoin: Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType, targetProp: string
        ): Cmf.Foundation.BusinessObjects.QueryObject.Relation => {
            const r = new Cmf.Foundation.BusinessObjects.QueryObject.Relation();
            r.Alias = "";
            r.IsRelation = false;
            r.Name = "";
            r.SourceEntity = sourceEntity;
            r.SourceEntityAlias = sourceAlias;
            r.SourceJoinType = sourceJoin;
            r.SourceProperty = sourceProp;
            r.TargetEntity = targetEntity;
            r.TargetEntityAlias = targetAlias;
            r.TargetJoinType = targetJoin;
            r.TargetProperty = targetProp;
            return r;
        };

        const JT = Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType;

        relationCollection.push(makeRelation("Material", "Material_1", JT.InnerJoin, "LastProcessedResourceId", "Resource", "Material_LastProcessedResource_2", JT.InnerJoin, "Id"));
        relationCollection.push(makeRelation("Resource", "Material_LastProcessedResource_2", JT.InnerJoin, "AreaId", "Area", "Material_LastProcessedResource_Area_3", JT.InnerJoin, "Id"));
        relationCollection.push(makeRelation("Area", "Material_LastProcessedResource_Area_3", JT.InnerJoin, "FacilityId", "Facility", "Material_LastProcessedResource_Area_Facility_4", JT.InnerJoin, "Id"));
        relationCollection.push(makeRelation("Facility", "Material_LastProcessedResource_Area_Facility_4", JT.InnerJoin, "SiteId", "Site", "Material_LastProcessedResource_Area_Facility_Site_5", JT.InnerJoin, "Id"));
        relationCollection.push(makeRelation("Site", "Material_LastProcessedResource_Area_Facility_Site_5", JT.InnerJoin, "EnterpriseId", "Enterprise", "Material_LastProcessedResource_Area_Facility_Site_Enterprise_6", JT.InnerJoin, "Id"));

        const query = new Cmf.Foundation.BusinessObjects.QueryObject.QueryObject();
        query.Description = "";
        query.EntityTypeName = "Material";
        query.Name = "GetIsa95FromMaterial";
        query.Query = new Cmf.Foundation.BusinessObjects.QueryObject.Query();
        query.Query.Distinct = false;
        query.Query.Filters = filterCollection;
        query.Query.Fields = fieldCollection;
        query.Query.Relations = relationCollection;
        query.Query.Top = 1;
        return query;
    }

    /**
     * Create a query to resolve the ISA-95 hierarchy starting from a Resource name.
     */
    static getIsa95QueryFromResource(resourceName: string): QueryObject.QueryObject {
        const filterCollection: Cmf.Foundation.BusinessObjects.QueryObject.FilterCollection =
            new Cmf.Foundation.BusinessObjects.QueryObject.FilterCollection();

        const filter_0 = new Cmf.Foundation.BusinessObjects.QueryObject.Filter();
        filter_0.Name = "Name";
        filter_0.ObjectName = "Resource";
        filter_0.ObjectAlias = "Resource_1";
        filter_0.Operator = Cmf.Foundation.Common.FieldOperator.IsEqualTo;
        filter_0.Value = resourceName;
        filter_0.LogicalOperator = Cmf.Foundation.Common.LogicalOperator.Nothing;
        filter_0.FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal;

        const makeAlwaysTrue = (objectName: string, objectAlias: string): Cmf.Foundation.BusinessObjects.QueryObject.Filter => {
            const f = new Cmf.Foundation.BusinessObjects.QueryObject.Filter();
            f.ObjectName = objectName;
            f.ObjectAlias = objectAlias;
            f.Value = null;
            f.LogicalOperator = Cmf.Foundation.Common.LogicalOperator.AND;
            f.FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.AlwaysTrue;
            return f;
        };

        filterCollection.push(filter_0);
        filterCollection.push(makeAlwaysTrue("Area", "Resource_Area_2"));
        filterCollection.push(makeAlwaysTrue("Area", "Resource_Area_2"));
        filterCollection.push(makeAlwaysTrue("Area", "Resource_Area_2"));
        filterCollection.push(makeAlwaysTrue("Facility", "Resource_Area_Facility_3"));
        filterCollection.push(makeAlwaysTrue("Area", "Resource_Area_2"));
        filterCollection.push(makeAlwaysTrue("Facility", "Resource_Area_Facility_3"));
        filterCollection.push(makeAlwaysTrue("Area", "Resource_Area_2"));
        filterCollection.push(makeAlwaysTrue("Facility", "Resource_Area_Facility_3"));
        filterCollection.push(makeAlwaysTrue("Site", "Resource_Area_Facility_Site_4"));
        filterCollection.push(makeAlwaysTrue("Area", "Resource_Area_2"));
        filterCollection.push(makeAlwaysTrue("Facility", "Resource_Area_Facility_3"));
        filterCollection.push(makeAlwaysTrue("Site", "Resource_Area_Facility_Site_4"));

        const fieldCollection: Cmf.Foundation.BusinessObjects.QueryObject.FieldCollection =
            new Cmf.Foundation.BusinessObjects.QueryObject.FieldCollection();

        const makeField = (alias: string, objectName: string, objectAlias: string, position: number): Cmf.Foundation.BusinessObjects.QueryObject.Field => {
            const f = new Cmf.Foundation.BusinessObjects.QueryObject.Field();
            f.Alias = alias;
            f.ObjectName = objectName;
            f.ObjectAlias = objectAlias;
            f.IsUserAttribute = false;
            f.Name = "Name";
            f.Position = position;
            f.Sort = Cmf.Foundation.Common.FieldSort.NoSort;
            return f;
        };

        fieldCollection.push(makeField("Resource", "Resource", "Resource_1", 1));
        fieldCollection.push(makeField("Area", "Area", "Resource_Area_2", 2));
        fieldCollection.push(makeField("Facility", "Facility", "Resource_Area_Facility_3", 3));
        fieldCollection.push(makeField("Site", "Site", "Resource_Area_Facility_Site_4", 4));
        fieldCollection.push(makeField("Enterprise", "Enterprise", "Resource_Area_Facility_Site_Enterprise_5", 5));

        const relationCollection: Cmf.Foundation.BusinessObjects.QueryObject.RelationCollection =
            new Cmf.Foundation.BusinessObjects.QueryObject.RelationCollection();

        const makeRelation = (
            sourceEntity: string, sourceAlias: string, sourceJoin: Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType,
            sourceProp: string, targetEntity: string, targetAlias: string,
            targetJoin: Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType, targetProp: string
        ): Cmf.Foundation.BusinessObjects.QueryObject.Relation => {
            const r = new Cmf.Foundation.BusinessObjects.QueryObject.Relation();
            r.Alias = "";
            r.IsRelation = false;
            r.Name = "";
            r.SourceEntity = sourceEntity;
            r.SourceEntityAlias = sourceAlias;
            r.SourceJoinType = sourceJoin;
            r.SourceProperty = sourceProp;
            r.TargetEntity = targetEntity;
            r.TargetEntityAlias = targetAlias;
            r.TargetJoinType = targetJoin;
            r.TargetProperty = targetProp;
            return r;
        };

        const JT = Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType;

        relationCollection.push(makeRelation("Resource", "Resource_1", JT.InnerJoin, "AreaId", "Area", "Resource_Area_2", JT.InnerJoin, "Id"));
        relationCollection.push(makeRelation("Area", "Resource_Area_2", JT.LeftJoin, "FacilityId", "Facility", "Resource_Area_Facility_3", JT.InnerJoin, "Id"));
        relationCollection.push(makeRelation("Facility", "Resource_Area_Facility_3", JT.LeftJoin, "SiteId", "Site", "Resource_Area_Facility_Site_4", JT.InnerJoin, "Id"));
        relationCollection.push(makeRelation("Site", "Resource_Area_Facility_Site_4", JT.LeftJoin, "EnterpriseId", "Enterprise", "Resource_Area_Facility_Site_Enterprise_5", JT.InnerJoin, "Id"));

        const query = new Cmf.Foundation.BusinessObjects.QueryObject.QueryObject();
        query.Description = "";
        query.EntityTypeName = "Resource";
        query.Name = "GetIsa95FromResource";
        query.Query = new Cmf.Foundation.BusinessObjects.QueryObject.Query();
        query.Query.Distinct = false;
        query.Query.Filters = filterCollection;
        query.Query.Fields = fieldCollection;
        query.Query.Relations = relationCollection;
        query.Query.Top = 1;
        return query;
    }

    /**
     * Create a query to resolve the ISA-95 hierarchy starting from an Area name.
     */
    static getIsa95QueryFromArea(areaName: string): QueryObject.QueryObject {
        const filterCollection: Cmf.Foundation.BusinessObjects.QueryObject.FilterCollection =
            new Cmf.Foundation.BusinessObjects.QueryObject.FilterCollection();

        const filter_0 = new Cmf.Foundation.BusinessObjects.QueryObject.Filter();
        filter_0.Name = "Name";
        filter_0.ObjectName = "Area";
        filter_0.ObjectAlias = "Area_1";
        filter_0.Operator = Cmf.Foundation.Common.FieldOperator.IsEqualTo;
        filter_0.Value = areaName;
        filter_0.LogicalOperator = Cmf.Foundation.Common.LogicalOperator.Nothing;
        filter_0.FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal;

        const makeAlwaysTrue = (objectName: string, objectAlias: string): Cmf.Foundation.BusinessObjects.QueryObject.Filter => {
            const f = new Cmf.Foundation.BusinessObjects.QueryObject.Filter();
            f.ObjectName = objectName;
            f.ObjectAlias = objectAlias;
            f.Value = null;
            f.LogicalOperator = Cmf.Foundation.Common.LogicalOperator.AND;
            f.FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.AlwaysTrue;
            return f;
        };

        filterCollection.push(filter_0);
        filterCollection.push(makeAlwaysTrue("Facility", "Area_Facility_2"));
        filterCollection.push(makeAlwaysTrue("Facility", "Area_Facility_2"));
        filterCollection.push(makeAlwaysTrue("Facility", "Area_Facility_2"));
        filterCollection.push(makeAlwaysTrue("Site", "Area_Facility_Site_3"));
        filterCollection.push(makeAlwaysTrue("Facility", "Area_Facility_2"));
        filterCollection.push(makeAlwaysTrue("Site", "Area_Facility_Site_3"));

        const fieldCollection: Cmf.Foundation.BusinessObjects.QueryObject.FieldCollection =
            new Cmf.Foundation.BusinessObjects.QueryObject.FieldCollection();

        const makeField = (alias: string, objectName: string, objectAlias: string, position: number): Cmf.Foundation.BusinessObjects.QueryObject.Field => {
            const f = new Cmf.Foundation.BusinessObjects.QueryObject.Field();
            f.Alias = alias;
            f.ObjectName = objectName;
            f.ObjectAlias = objectAlias;
            f.IsUserAttribute = false;
            f.Name = "Name";
            f.Position = position;
            f.Sort = Cmf.Foundation.Common.FieldSort.NoSort;
            return f;
        };

        fieldCollection.push(makeField("Area", "Area", "Area_1", 0));
        fieldCollection.push(makeField("Facility", "Facility", "Area_Facility_2", 5));
        fieldCollection.push(makeField("Site", "Site", "Area_Facility_Site_3", 6));
        fieldCollection.push(makeField("Enterprise", "Enterprise", "Area_Facility_Site_Enterprise_4", 7));

        const relationCollection: Cmf.Foundation.BusinessObjects.QueryObject.RelationCollection =
            new Cmf.Foundation.BusinessObjects.QueryObject.RelationCollection();

        const JT = Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType;

        const makeRelation = (
            sourceEntity: string, sourceAlias: string, sourceJoin: Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType,
            sourceProp: string, targetEntity: string, targetAlias: string,
            targetJoin: Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType, targetProp: string
        ): Cmf.Foundation.BusinessObjects.QueryObject.Relation => {
            const r = new Cmf.Foundation.BusinessObjects.QueryObject.Relation();
            r.Alias = "";
            r.IsRelation = false;
            r.Name = "";
            r.SourceEntity = sourceEntity;
            r.SourceEntityAlias = sourceAlias;
            r.SourceJoinType = sourceJoin;
            r.SourceProperty = sourceProp;
            r.TargetEntity = targetEntity;
            r.TargetEntityAlias = targetAlias;
            r.TargetJoinType = targetJoin;
            r.TargetProperty = targetProp;
            return r;
        };

        relationCollection.push(makeRelation("Area", "Area_1", JT.LeftJoin, "FacilityId", "Facility", "Area_Facility_2", JT.InnerJoin, "Id"));
        relationCollection.push(makeRelation("Facility", "Area_Facility_2", JT.LeftJoin, "SiteId", "Site", "Area_Facility_Site_3", JT.InnerJoin, "Id"));
        relationCollection.push(makeRelation("Site", "Area_Facility_Site_3", JT.InnerJoin, "EnterpriseId", "Enterprise", "Area_Facility_Site_Enterprise_4", JT.InnerJoin, "Id"));

        const query = new Cmf.Foundation.BusinessObjects.QueryObject.QueryObject();
        query.Description = "";
        query.EntityTypeName = "Area";
        query.Name = "GetIsa95FromArea";
        query.Query = new Cmf.Foundation.BusinessObjects.QueryObject.Query();
        query.Query.Distinct = false;
        query.Query.Filters = filterCollection;
        query.Query.Fields = fieldCollection;
        query.Query.Relations = relationCollection;
        query.Query.Top = 1;
        return query;
    }

    /**
     * Create a query to resolve the ISA-95 hierarchy starting from a Facility name.
     */
    static getIsa95QueryFromFacility(facilityName: string): QueryObject.QueryObject {
        const filterCollection: Cmf.Foundation.BusinessObjects.QueryObject.FilterCollection =
            new Cmf.Foundation.BusinessObjects.QueryObject.FilterCollection();

        const filter_0 = new Cmf.Foundation.BusinessObjects.QueryObject.Filter();
        filter_0.Name = "Name";
        filter_0.ObjectName = "Facility";
        filter_0.ObjectAlias = "Facility_1";
        filter_0.Operator = Cmf.Foundation.Common.FieldOperator.IsEqualTo;
        filter_0.Value = facilityName;
        filter_0.LogicalOperator = Cmf.Foundation.Common.LogicalOperator.Nothing;
        filter_0.FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal;

        const filter_1 = new Cmf.Foundation.BusinessObjects.QueryObject.Filter();
        filter_1.ObjectName = "Site";
        filter_1.ObjectAlias = "Facility_Site_2";
        filter_1.Value = null;
        filter_1.LogicalOperator = Cmf.Foundation.Common.LogicalOperator.AND;
        filter_1.FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.AlwaysTrue;

        filterCollection.push(filter_0);
        filterCollection.push(filter_1);

        const fieldCollection: Cmf.Foundation.BusinessObjects.QueryObject.FieldCollection =
            new Cmf.Foundation.BusinessObjects.QueryObject.FieldCollection();

        const makeField = (alias: string, objectName: string, objectAlias: string, position: number): Cmf.Foundation.BusinessObjects.QueryObject.Field => {
            const f = new Cmf.Foundation.BusinessObjects.QueryObject.Field();
            f.Alias = alias;
            f.ObjectName = objectName;
            f.ObjectAlias = objectAlias;
            f.IsUserAttribute = false;
            f.Name = "Name";
            f.Position = position;
            f.Sort = Cmf.Foundation.Common.FieldSort.NoSort;
            return f;
        };

        fieldCollection.push(makeField("Facility", "Facility", "Facility_1", 0));
        fieldCollection.push(makeField("Site", "Site", "Facility_Site_2", 1));
        fieldCollection.push(makeField("Enterprise", "Enterprise", "Facility_Site_Enterprise_3", 2));

        const relationCollection: Cmf.Foundation.BusinessObjects.QueryObject.RelationCollection =
            new Cmf.Foundation.BusinessObjects.QueryObject.RelationCollection();

        const JT = Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType;

        const r0 = new Cmf.Foundation.BusinessObjects.QueryObject.Relation();
        r0.Alias = "";
        r0.IsRelation = false;
        r0.Name = "";
        r0.SourceEntity = "Facility";
        r0.SourceEntityAlias = "Facility_1";
        r0.SourceJoinType = JT.InnerJoin;
        r0.SourceProperty = "SiteId";
        r0.TargetEntity = "Site";
        r0.TargetEntityAlias = "Facility_Site_2";
        r0.TargetJoinType = JT.InnerJoin;
        r0.TargetProperty = "Id";

        const r1 = new Cmf.Foundation.BusinessObjects.QueryObject.Relation();
        r1.Alias = "";
        r1.IsRelation = false;
        r1.Name = "";
        r1.SourceEntity = "Site";
        r1.SourceEntityAlias = "Facility_Site_2";
        r1.SourceJoinType = JT.InnerJoin;
        r1.SourceProperty = "EnterpriseId";
        r1.TargetEntity = "Enterprise";
        r1.TargetEntityAlias = "Facility_Site_Enterprise_3";
        r1.TargetJoinType = JT.InnerJoin;
        r1.TargetProperty = "Id";

        relationCollection.push(r0);
        relationCollection.push(r1);

        const query = new Cmf.Foundation.BusinessObjects.QueryObject.QueryObject();
        query.Description = "";
        query.EntityTypeName = "Facility";
        query.Name = "GetIsa95FromFacility";
        query.Query = new Cmf.Foundation.BusinessObjects.QueryObject.Query();
        query.Query.Distinct = false;
        query.Query.Filters = filterCollection;
        query.Query.Fields = fieldCollection;
        query.Query.Relations = relationCollection;
        query.Query.Top = 1;
        return query;
    }

    /**
     * Create a query to resolve the ISA-95 hierarchy starting from a Site name.
     */
    static getIsa95QueryFromSite(siteName: string): QueryObject.QueryObject {
        const filterCollection: Cmf.Foundation.BusinessObjects.QueryObject.FilterCollection =
            new Cmf.Foundation.BusinessObjects.QueryObject.FilterCollection();

        const filter_0 = new Cmf.Foundation.BusinessObjects.QueryObject.Filter();
        filter_0.Name = "Name";
        filter_0.ObjectName = "Site";
        filter_0.ObjectAlias = "Site_1";
        filter_0.Operator = Cmf.Foundation.Common.FieldOperator.IsEqualTo;
        filter_0.Value = siteName;
        filter_0.LogicalOperator = Cmf.Foundation.Common.LogicalOperator.Nothing;
        filter_0.FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal;

        filterCollection.push(filter_0);

        const fieldCollection: Cmf.Foundation.BusinessObjects.QueryObject.FieldCollection =
            new Cmf.Foundation.BusinessObjects.QueryObject.FieldCollection();

        const f0 = new Cmf.Foundation.BusinessObjects.QueryObject.Field();
        f0.Alias = "Site";
        f0.ObjectName = "Site";
        f0.ObjectAlias = "Site_1";
        f0.IsUserAttribute = false;
        f0.Name = "Name";
        f0.Position = 0;
        f0.Sort = Cmf.Foundation.Common.FieldSort.NoSort;

        const f1 = new Cmf.Foundation.BusinessObjects.QueryObject.Field();
        f1.Alias = "Enterprise";
        f1.ObjectName = "Enterprise";
        f1.ObjectAlias = "Site_Enterprise_2";
        f1.IsUserAttribute = false;
        f1.Name = "Name";
        f1.Position = 1;
        f1.Sort = Cmf.Foundation.Common.FieldSort.NoSort;

        fieldCollection.push(f0);
        fieldCollection.push(f1);

        const relationCollection: Cmf.Foundation.BusinessObjects.QueryObject.RelationCollection =
            new Cmf.Foundation.BusinessObjects.QueryObject.RelationCollection();

        const JT = Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType;
        const r0 = new Cmf.Foundation.BusinessObjects.QueryObject.Relation();
        r0.Alias = "";
        r0.IsRelation = false;
        r0.Name = "";
        r0.SourceEntity = "Site";
        r0.SourceEntityAlias = "Site_1";
        r0.SourceJoinType = JT.InnerJoin;
        r0.SourceProperty = "EnterpriseId";
        r0.TargetEntity = "Enterprise";
        r0.TargetEntityAlias = "Site_Enterprise_2";
        r0.TargetJoinType = JT.InnerJoin;
        r0.TargetProperty = "Id";

        relationCollection.push(r0);

        const query = new Cmf.Foundation.BusinessObjects.QueryObject.QueryObject();
        query.Description = "";
        query.EntityTypeName = "Site";
        query.Name = "GetIsa95FromSite";
        query.Query = new Cmf.Foundation.BusinessObjects.QueryObject.Query();
        query.Query.Distinct = false;
        query.Query.Filters = filterCollection;
        query.Query.Fields = fieldCollection;
        query.Query.Relations = relationCollection;
        query.Query.Top = 1;
        return query;
    }
}

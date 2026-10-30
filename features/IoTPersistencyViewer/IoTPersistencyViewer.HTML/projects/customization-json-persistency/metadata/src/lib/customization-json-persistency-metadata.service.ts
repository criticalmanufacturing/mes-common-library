import {
    Injectable
} from '@angular/core';

import {
    RouteConfig,
    PackageMetadata,
    Action,
    MenuGroup,
    MenuItem,
    ActionButton,
    ActionButtonGroup,
    EntityTypeMetadata,
    PackageInfo,
    ViewType
} from 'cmf-core';


@Injectable()
export class CustomizationJsonPersistencyMetadataService extends PackageMetadata {

    /**
     * Package Info
     */
    public override get packageInfo(): PackageInfo {
        return {
            name: 'customization-json-persistency',
            loader: () => import(
                /* webpackExports: ["CustomizationJsonPersistencyComponent"] */
                'customization-json-persistency'),
            converters: [],
            widgets: [],
            dataSources: [],
            components: ["CustomizationJsonPersistencyComponent"]
        };
    }

    /**
     * Action Button Groups
     */
    public override get actionButtonGroups(): ActionButtonGroup[] {
        return [];
    }

    /**
     * Action Buttons
     */
    public override get actionButtons(): ActionButton[] {
        return [
            {
                id: 'PersistencyKey.Refresh',
                title: $localize`:@@customization-json-persistency/main#actions.REFRESH:Refresh`,
                alternative: $localize`:@@customization-json-persistency/main#actions.REFRESH:Refresh`,
                iconClass: 'icon-core-st-lg-reload',
                actionId: 'Action.Refresh'
            }
        ];
    }

    /**
     * Actions
     */
    public override get actions(): Action[] {
        return [];
    }

    /**
     * Menu Groups
     */
    public override get menuGroups(): MenuGroup[] {
        return [];
    }

    /**
     * Menu Items
     */
    public override get menuItems(): MenuItem[] {
        return [];
    }

    /**
     * Entity Types
     */
    public override get entityTypes(): EntityTypeMetadata[] {
        return [{
            name: 'AutomationControllerInstance',
            views: [{
                id: 'Custom.AutomationControllerInstancePersistencyViewer',
                name: 'Persistency Viewer',
                path: 'persistency-viewer',
                loadComponent: () => import(
                    /* webpackExports: "CustomizationJsonPersistencyComponent" */
                    'customization-json-persistency')
                    .then(m => m.CustomizationJsonPersistencyComponent),
                canExecute: () => { return Promise.resolve(true); },
                type: ViewType.Simple
            }]
        }];
    }

    /**
     * Routes
     */
    public override get routes(): RouteConfig[] {
        return [];
    }
}

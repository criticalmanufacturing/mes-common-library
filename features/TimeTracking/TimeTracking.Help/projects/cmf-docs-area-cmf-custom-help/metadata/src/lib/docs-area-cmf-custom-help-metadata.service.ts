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
    PackageInfo
} from 'cmf-core';


@Injectable()
export class DocsAreaCmfCustomHelpMetadataService extends PackageMetadata {

    /**
     * Package Info
     */
    public override get packageInfo(): PackageInfo {
        return {
            name: 'cmf-docs-area-cmf-custom-help',
            loader: () => import(
                /* webpackExports: [] */
                'cmf-docs-area-cmf-custom-help'),
            converters: [],
            widgets: [],
            dataSources: [],
            components: []
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
        return [];
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
        return [
            {
                position: 2000,
                id: 'Shell.cmf-custom-help',
                iconClass: 'icon-docs-st-lg-userguide',
                route: 'cmf-custom-help',
                itemsGenerator: class MenuGen {
                    public items (): Promise<MenuItem[]> {
                      return fetch('./assets/cmf-docs-area-cmf-custom-help/__generatedMenuItems.json').then((response) => {
                        return response.json();
                      });
                    }
                }.prototype,
                title: 'Community'
              }
        ];
    }

    /**
     * Menu Items
     */
    public override get menuItems(): MenuItem[] {
        return [
            {
                id: 'cmf-custom-help.index',
                menuGroupId: 'Shell.cmf-custom-help',
                title: 'Index',
                actionId: ''
              },
              {
                id: 'cmf-custom-help.techspec',
                menuGroupId: 'Shell.cmf-custom-help',
                title: 'Technical Specification',
                actionId: ''
              },
              {
                id: 'cmf-custom-help.userguide',
                menuGroupId: 'Shell.cmf-custom-help',
                title: 'User Guide',
                actionId: ''
              },
              {
                id: 'cmf-custom-help.releasenotes',
                menuGroupId: 'Shell.cmf-custom-help',
                title: 'Release Notes',
                actionId: ''
              },
              {
                id: 'cmf-custom-help.faq',
                menuGroupId: 'Shell.cmf-custom-help',
                title: 'FAQ',
                actionId: ''
              }
        ];
    }

    /**
     * Entity Types
     */
    public override get entityTypes(): EntityTypeMetadata[] {
        return [];
    }

    /**
     * Routes
     */
    public override get routes(): RouteConfig[] {
        return [];
    }
}

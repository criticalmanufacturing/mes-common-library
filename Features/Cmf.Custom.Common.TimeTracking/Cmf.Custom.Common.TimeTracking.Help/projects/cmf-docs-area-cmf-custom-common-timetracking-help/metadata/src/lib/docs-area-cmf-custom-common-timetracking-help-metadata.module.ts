import { EnvironmentProviders, NgModule } from '@angular/core';
import { provideMetadata } from 'cmf-core';

import { DocsAreaCmfCustomCommonTimetrackingHelpMetadataService } from './docs-area-cmf-custom-common-timetracking-help-metadata.service';

@NgModule({
    providers: [provideDocsAreaCmfCustomCommonTimetrackingHelp()]
})
export class DocsAreaCmfCustomCommonTimetrackingHelpMetadataModule { }

/** Provides Docs Area Cmf Custom Common Timetracking Help functionality */
export function provideDocsAreaCmfCustomCommonTimetrackingHelp(): EnvironmentProviders {
    return provideMetadata(DocsAreaCmfCustomCommonTimetrackingHelpMetadataService);
}

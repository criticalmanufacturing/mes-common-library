import { EnvironmentProviders, NgModule } from '@angular/core';
import { provideMetadata } from 'cmf-core';

import { DocsAreaCmfCustomHelpMetadataService } from './docs-area-cmf-custom-help-metadata.service';

@NgModule({
    providers: [provideDocsAreaCmfCustomHelp()]
})
export class DocsAreaCmfCustomHelpMetadataModule { }

/** Provides Docs Area Cmf Custom Help functionality */
export function provideDocsAreaCmfCustomHelp(): EnvironmentProviders {
    return provideMetadata(DocsAreaCmfCustomHelpMetadataService);
}

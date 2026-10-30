import { EnvironmentProviders, NgModule } from '@angular/core';
import { provideMetadata } from 'cmf-core';

import { CustomizationJsonPersistencyMetadataService } from './customization-json-persistency-metadata.service';

@NgModule({
    providers: [provideCustomizationJsonPersistency()]
})
export class CustomizationJsonPersistencyMetadataModule { }

/** Provides Customization Json Persistency functionality */
export function provideCustomizationJsonPersistency(): EnvironmentProviders {
    return provideMetadata(CustomizationJsonPersistencyMetadataService);
}

import { ApplicationConfig, isDevMode } from '@angular/core';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { provideServiceWorker } from '@angular/service-worker';
import { provideMesUI } from 'cmf-mes-ui';
import { provideMetadataRouter } from 'cmf-core';
import { provideCustomizationJsonPersistency } from 'customization-json-persistency/metadata';

export const appConfig: ApplicationConfig = {
    providers: [provideRouter(routes), provideServiceWorker('ngsw-loader-worker.js', {
        enabled: !isDevMode(),
        registrationStrategy: 'registerWhenStable:30000'
    }), provideMesUI(), provideCustomizationJsonPersistency(), provideMetadataRouter()]
};

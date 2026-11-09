import { ApplicationConfig, isDevMode } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideServiceWorker } from '@angular/service-worker';
import { provideMetadataRouter } from 'cmf-core';
import { provideCoreControls } from 'cmf-core-controls/metadata';
import { provideDocsControls } from 'cmf-docs-controls/metadata';
import { provideDocsShell } from 'cmf-docs-shell/metadata';
import { provideDocsAreaDesignsystem } from 'cmf-docs-area-designsystem/metadata';
import { provideDocsAreaCmfCustomHelp } from 'cmf-docs-area-cmf-custom-help/metadata';
import './app.workers';

export const appConfig: ApplicationConfig = {
  providers: [
    provideHttpClient(),
    provideCoreControls(),
    provideDocsShell(),
    provideDocsControls(),
    provideDocsAreaDesignsystem(),
    provideDocsAreaCmfCustomHelp(),
    provideMetadataRouter(),
    provideServiceWorker('ngsw-worker.js', {
      enabled: !isDevMode(),
      registrationStrategy: 'registerWhenStable:30000'
    })
  ]
};
